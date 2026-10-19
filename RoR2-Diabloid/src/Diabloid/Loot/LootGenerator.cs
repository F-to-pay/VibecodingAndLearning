using System;
using System.Collections.Generic;

namespace Diabloid.Loot
{
    /// <summary>Крутилки баланса для генератора. В игре заполняются из конфига, в симуляторе — по умолчанию.</summary>
    public sealed class LootSettings
    {
        /// <summary>Общий множитель всех статов.</summary>
        public float StatMultiplier = 1f;

        /// <summary>Множитель к росту статов от уровня (0 — уровень вообще не влияет).</summary>
        public float LevelScalingMultiplier = 1f;

        /// <summary>Во сколько раз implicit-стат жирнее обычного аффикса.</summary>
        public float ImplicitMultiplier = 1.5f;

        /// <summary>
        /// Шанс (0..1), что очередной аффикс повторит стат, который на шмотке уже есть (включая главный).
        /// 0 — все статы на шмотке разные, 1 — вся шмотка в один стат.
        /// </summary>
        public float DuplicateStatChance = 0.25f;

        /// <summary>
        /// Веса качеств: Common, Uncommon, Rare, Epic, Legendary. Особые (Lunar, Void) рандом не выбирает,
        /// вырезанный Epic тоже (его вес игнорируется, даже если в массиве не ноль).
        /// </summary>
        public float[] QualityWeights = Qualities.DefaultWeights();
    }

    /// <summary>Процедурная генерация. Никакой магии: взвешенный рандом + перемешанный пул статов.</summary>
    public static class LootGenerator
    {
        public static GearSlot RollSlot(Random rng) => (GearSlot)rng.Next(Slots.Count);

        /// <summary>
        /// Выбор качества по весам.
        /// luck сдвигает шансы вверх: вес i-го качества умножается на (1 + luck)^i.
        /// При luck = 1 легендарка становится в 16 раз вероятнее, эпик — в 8 и т.д.
        /// minQuality отрезает всё, что ниже (боссы не роняют мусор).
        /// </summary>
        public static GearQuality RollQuality(Random rng, float[] weights, GearQuality minQuality = GearQuality.Common, float luck = 0f)
        {
            // Только "обычные" качества: лунное и пустотное выдаются напрямую, а не рандомом, эпик вырезан.
            int count = Qualities.RandomlyRolledCount;
            var adjusted = new double[count];
            double total = 0;

            for (int i = 0; i < count; i++)
            {
                if (i < (int)minQuality || !Qualities.Get((GearQuality)i).RandomlyRolled) continue;
                double w = i < weights.Length ? Math.Max(0f, weights[i]) : 0f;
                w *= Math.Pow(1.0 + Math.Max(0f, luck), i);
                adjusted[i] = w;
                total += w;
            }

            if (total <= 0) return LowestRolledFrom(minQuality);

            double roll = rng.NextDouble() * total;
            int last = 0;
            for (int i = 0; i < count; i++)
            {
                if (adjusted[i] <= 0) continue;
                last = i;
                roll -= adjusted[i];
                if (roll < 0) return (GearQuality)i;
            }
            return (GearQuality)last;
        }

        /// <summary>Ближайшее качество не ниже заданного, которое вообще бывает (minQuality = Epic → Legendary).</summary>
        private static GearQuality LowestRolledFrom(GearQuality minQuality)
        {
            for (int i = (int)minQuality; i < Qualities.RandomlyRolledCount; i++)
                if (Qualities.Get((GearQuality)i).RandomlyRolled) return (GearQuality)i;
            return minQuality;
        }

        /// <summary>
        /// Собрать шмотку целиком: implicit + N случайных аффиксов из пула слота.
        /// С шансом DuplicateStatChance аффикс повторяет стат, который на шмотке уже есть ("урон трижды").
        /// Лунные — ещё и проклятия (отрицательные аффиксы из Stats.CursePool, не пересекаются с остальными статами),
        /// пустотные — обязательное проклятие регенерации.
        /// Пустотные — цифры для 1-го уровня без округления (шаблон), настоящие даёт GearItem.Current().
        /// </summary>
        public static GearItem Generate(GearSlot slot, GearQuality quality, int level, Random rng, LootSettings settings)
        {
            var slotInfo = Slots.Get(slot);
            var qualityInfo = Qualities.Get(quality);
            level = Math.Max(1, level);

            bool template = qualityInfo.ScalesWithMonsterLevel;
            int valueLevel = template ? 1 : level;

            var implicitAffix = new Affix(
                slotInfo.Implicit,
                Roll(slotInfo.Implicit, qualityInfo.StatMultiplier, qualityInfo.RollMin, valueLevel, rng, settings,
                     settings.ImplicitMultiplier, round: !template));

            // Свежие статы берём из перемешанного пула по порядку — так "новый" стат никогда не совпадёт с уже выпавшим.
            var pool = (StatType[])slotInfo.Pool.Clone();
            Shuffle(pool, rng);
            int nextFresh = 0;

            int affixCount = qualityInfo.AffixCount;
            var affixes = new List<Affix>(affixCount + qualityInfo.CurseCount);
            for (int i = 0; i < affixCount; i++)
            {
                var stat = PickStat(implicitAffix, affixes, pool, ref nextFresh, rng, settings.DuplicateStatChance);
                affixes.Add(new Affix(stat, Roll(stat, qualityInfo.StatMultiplier, qualityInfo.RollMin, valueLevel, rng, settings,
                                                 1f, round: !template)));
            }

            if (qualityInfo.CurseCount > 0)
                AddCurses(affixes, implicitAffix.Stat, qualityInfo, valueLevel, rng, settings, !template);

            byte baseIndex = (byte)rng.Next(slotInfo.Bases.Length);
            var perks = RollPerks(qualityInfo, rng);

            return new GearItem(slot, quality, valueLevel, baseIndex, implicitAffix, affixes, perks);
        }

        /// <summary>Перки: разные типы (два одинаковых на одной шмотке не бывает), у каждого свой бросок 0..1.</summary>
        private static List<Perk> RollPerks(QualityInfo quality, Random rng)
        {
            var perks = new List<Perk>(quality.PerkCount);
            if (quality.PerkCount <= 0) return perks;

            var types = (PerkType[])Enum.GetValues(typeof(PerkType));
            Shuffle(types, rng);
            for (int i = 0; i < quality.PerkCount && i < types.Length; i++)
                perks.Add(new Perk(types[i], (float)rng.NextDouble()));
            return perks;
        }

        /// <summary>
        /// Какой стат будет у очередного аффикса.
        /// С шансом duplicateChance — повтор: случайная СТРОКА из уже выпавших (главный стат тоже считается).
        /// Строки, а не статы: если урон уже дважды, третий урон вероятнее — так и рождается "урон трижды".
        /// Иначе — следующий свежий стат из пула. Свежие кончились — тоже повтор.
        /// </summary>
        private static StatType PickStat(Affix implicitAffix, List<Affix> affixes, StatType[] pool, ref int nextFresh,
                                         Random rng, float duplicateChance)
        {
            bool duplicate = nextFresh >= pool.Length || rng.NextDouble() < duplicateChance;
            if (!duplicate) return pool[nextFresh++];

            int line = rng.Next(affixes.Count + 1);
            return line == 0 ? implicitAffix.Stat : affixes[line - 1].Stat;
        }

        /// <summary>
        /// Проклятия: сначала обязательное (ForcedCurse, у пустотных — % регена), потом статы из CursePool,
        /// которых на шмотке ещё нет (иначе "+20% урона, -18% урона" — бред), со знаком минус.
        /// Сила считается от CurseBaseValue стата, а не от BaseValue.
        /// </summary>
        private static void AddCurses(List<Affix> affixes, StatType implicitStat, QualityInfo quality, int level,
                                      Random rng, LootSettings settings, bool round)
        {
            // Проклятия не повторяют ни бонусы, ни друг друга, даже когда повторы статов разрешены.
            var candidates = new List<StatType>(Stats.CursePool.Length);
            foreach (var stat in Stats.CursePool)
            {
                if (stat == implicitStat) continue;
                bool used = false;
                foreach (var affix in affixes)
                    if (affix.Stat == stat) { used = true; break; }
                if (!used) candidates.Add(stat);
            }

            var shuffled = candidates.ToArray();
            Shuffle(shuffled, rng);

            var curses = new List<StatType>(quality.CurseCount);
            if (quality.ForcedCurse.HasValue) curses.Add(quality.ForcedCurse.Value);
            foreach (var stat in shuffled)
            {
                if (curses.Count >= quality.CurseCount) break;
                if (!curses.Contains(stat)) curses.Add(stat);
            }

            foreach (var stat in curses)
            {
                var info = Stats.Get(stat);
                // Roll умножает на BaseValue — подменяем на базу проклятия доп.множителем.
                float curseBase = info.CurseBaseValue / info.BaseValue;
                float magnitude = Roll(stat, quality.CurseMultiplier, quality.RollMin, level, rng, settings, curseBase, round);
                affixes.Add(new Affix(stat, -magnitude));
            }
        }

        /// <summary>
        /// Значение одного аффикса:
        ///   база стата × множитель качества × рост от уровня × бросок [RollMin..1] × доп.множитель × общий множитель
        /// </summary>
        public static float RollValue(StatType stat, QualityInfo quality, int level, Random rng, LootSettings settings, float extraMultiplier)
        {
            return Roll(stat, quality.StatMultiplier, quality.RollMin, level, rng, settings, extraMultiplier, round: true);
        }

        private static float Roll(StatType stat, float qualityMultiplier, float rollMin, int level, Random rng,
                                  LootSettings settings, float extraMultiplier, bool round)
        {
            var info = Stats.Get(stat);
            float roll = rollMin + (float)rng.NextDouble() * (1f - rollMin);
            if (info.FixedValue) return info.BaseValue; // прыжки: всегда ровно +1 (бросок всё равно съеден — рандом не сдвигается)
            float value = info.BaseValue * qualityMultiplier * LevelScale(stat, level, settings.LevelScalingMultiplier) *
                          roll * extraMultiplier * settings.StatMultiplier;
            return round ? RoundStat(stat, value) : value;
        }

        /// <summary>Рост стата от уровня: 1 + рост_за_уровень × множитель × (уровень − 1).</summary>
        public static float LevelScale(StatType stat, int level, float levelScalingMultiplier)
        {
            return 1f + Stats.Get(stat).LevelScaling * levelScalingMultiplier * (Math.Max(1, level) - 1);
        }

        /// <summary>
        /// Округление до знаков стата. Ноль не даём ("+0% к урону" выглядит как баг):
        /// минимум один шаг, у проклятий — минус один шаг.
        /// </summary>
        public static float RoundStat(StatType stat, float value)
        {
            int decimals = Stats.Get(stat).Decimals;
            float rounded = (float)Math.Round(value, decimals);
            float minStep = (float)Math.Pow(10, -decimals);
            return value >= 0f ? Math.Max(rounded, minStep) : Math.Min(rounded, -minStep);
        }

        /// <summary>Тасование Фишера–Йетса.</summary>
        private static void Shuffle<T>(T[] array, Random rng)
        {
            for (int i = array.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (array[i], array[j]) = (array[j], array[i]);
            }
        }
    }
}
