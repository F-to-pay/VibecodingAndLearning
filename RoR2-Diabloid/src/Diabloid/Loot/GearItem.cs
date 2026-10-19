using System;
using System.Collections.Generic;

namespace Diabloid.Loot
{
    /// <summary>Одна строчка характеристики на шмотке: "какой стат" + "сколько".</summary>
    public readonly struct Affix
    {
        public readonly StatType Stat;
        public readonly float Value;

        public Affix(StatType stat, float value)
        {
            Stat = stat;
            Value = value;
        }

        /// <summary>Насколько аффикс жирный относительно базового значения стата. Нужен для имени и оценки.</summary>
        /// <remarks>Проклятия меряются от своей базы (CurseBaseValue), иначе урезанное здоровье раздуло бы их втрое.</remarks>
        public float Strength => Value / (Value < 0f ? Stats.Get(Stat).CurseBaseValue : Stats.Get(Stat).BaseValue);

        /// <summary>Проклятие лунной или пустотной шмотки: стат со знаком минус.</summary>
        public bool IsCurse => Value < 0f;

        public string Describe(Lang lang) => Stats.Get(Stat).Describe(Value, lang);

        public string Describe() => Describe(Loc.Current);
    }

    /// <summary>
    /// Конкретная выпавшая шмотка со всеми цифрами.
    /// После создания не меняется — так её безопасно гонять по сети и кешировать имя.
    /// </summary>
    /// <remarks>
    /// Язык в шмотке НЕ хранится: по сети ходят только цифры, а имя каждая машина
    /// собирает сама на своём языке. Поэтому у хоста-русского и клиента-англичанина
    /// одна и та же шмотка называется "Свирепый топор ярости" и "Savage Axe of Fury".
    /// </remarks>
    public sealed class GearItem
    {
        /// <summary>
        /// Текущий уровень монстров — для пустотных шмоток, чей уровень растёт вместе с забегом.
        /// В игре это Run.ambientLevelFloor, в симуляторе — что угодно (null = 1-й уровень).
        /// </summary>
        public static Func<int> MonsterLevelProvider;

        /// <summary>Текущий множитель роста от уровня (из конфига). null = 1.</summary>
        public static Func<float> LevelScalingProvider;

        public readonly GearSlot Slot;
        public readonly GearQuality Quality;
        public readonly int Level;

        /// <summary>Индекс в Slots.Get(Slot).Bases — косметика, "топор" или "булава".</summary>
        public readonly byte BaseIndex;

        /// <summary>Главный стат слота (у оружия — урон, у ботинок — скорость и т.д.).</summary>
        public readonly Affix Implicit;

        /// <summary>Дополнительные случайные аффиксы. Их число зависит от качества.</summary>
        public readonly IReadOnlyList<Affix> Affixes;

        /// <summary>Перки (особые свойства): с редкого качества. От уровня не зависят, кроме берсерка.</summary>
        public readonly IReadOnlyList<Perk> Perks;

        // Имя на каждом языке считается один раз и запоминается. Индекс — (int)Lang.
        private readonly string[] cachedNames = new string[Enum.GetValues(typeof(Lang)).Length];

        // --- Пустотные шмотки ---
        // Сама шмотка (то, что лежит на земле, надето и ходит по сети) — это "шаблон": цифры
        // посчитаны для 1-го уровня и НЕ округлены. Настоящие цифры для текущего уровня монстров
        // собирает Current() и запоминает, пока уровень не сменится.
        private readonly GearItem template; // != null — значит, это готовая копия шаблона под конкретный уровень
        private GearItem resolved;
        private int resolvedLevel = -1;
        private float resolvedScaling = float.NaN;

        public GearItem(GearSlot slot, GearQuality quality, int level, byte baseIndex,
                        Affix implicitAffix, IList<Affix> affixes, IList<Perk> perks = null)
            : this(slot, quality, level, baseIndex, implicitAffix, affixes, perks, null)
        {
        }

        private GearItem(GearSlot slot, GearQuality quality, int level, byte baseIndex,
                         Affix implicitAffix, IList<Affix> affixes, IList<Perk> perks, GearItem template)
        {
            Slot = slot;
            Quality = quality;
            Level = level;
            BaseIndex = baseIndex;
            Implicit = implicitAffix;
            Affixes = new List<Affix>(affixes ?? Array.Empty<Affix>()).AsReadOnly();
            Perks = new List<Perk>(perks ?? Array.Empty<Perk>()).AsReadOnly();
            this.template = template;
        }

        /// <summary>Уровень этой шмотки растёт вместе с уровнем монстров (пустотное качество).</summary>
        public bool ScalesWithMonsterLevel => QualityInfo.ScalesWithMonsterLevel;

        /// <summary>
        /// Шмотка с актуальными цифрами. Для обычных — она сама. Для пустотных — копия под текущий
        /// уровень монстров. Всё, что показывает или применяет статы, должно смотреть сюда.
        /// </summary>
        public GearItem Current()
        {
            if (template != null || !ScalesWithMonsterLevel) return this;

            int level = Math.Max(1, MonsterLevelProvider?.Invoke() ?? 1);
            float scaling = LevelScalingProvider?.Invoke() ?? 1f;
            if (resolved != null && resolvedLevel == level && resolvedScaling == scaling) return resolved;

            var affixes = new List<Affix>(Affixes.Count);
            foreach (var affix in Affixes) affixes.Add(Scale(affix, level, scaling));

            resolved = new GearItem(Slot, Quality, level, BaseIndex, Scale(Implicit, level, scaling), affixes, new List<Perk>(Perks), this);
            resolvedLevel = level;
            resolvedScaling = scaling;
            return resolved;
        }

        private static Affix Scale(Affix raw, int level, float scaling)
        {
            float value = raw.Value * LootGenerator.LevelScale(raw.Stat, level, scaling);
            return new Affix(raw.Stat, LootGenerator.RoundStat(raw.Stat, value));
        }

        public SlotInfo SlotInfo => Slots.Get(Slot);
        public QualityInfo QualityInfo => Qualities.Get(Quality);

        public BaseName Base
        {
            get
            {
                var bases = SlotInfo.Bases;
                return bases[BaseIndex % bases.Length];
            }
        }

        /// <summary>Implicit + все аффиксы одним списком.</summary>
        public IEnumerable<Affix> AllAffixes
        {
            get
            {
                yield return Implicit;
                foreach (var affix in Affixes) yield return affix;
            }
        }

        /// <summary>Грубая "сила предмета": сумма аффиксов в долях от базового значения.</summary>
        public float Score
        {
            get
            {
                float score = 0f;
                foreach (var affix in AllAffixes) score += affix.Strength;
                return score;
            }
        }

        /// <summary>Сгенерированное имя на нужном языке: "Savage Axe of Fury" / "Свирепый топор ярости".</summary>
        /// <remarks>Копия пустотной шмотки берёт имя у шаблона: оно не должно меняться с уровнем.</remarks>
        public string GetName(Lang lang) => template != null ? template.GetName(lang) : cachedNames[(int)lang] ??= BuildName(lang);

        /// <summary>Имя на текущем языке.</summary>
        public string Name => GetName(Loc.Current);

        /// <summary>Имя, покрашенное в цвет качества (rich text для чата и панели).</summary>
        public string ColoredName => $"<color=#{QualityInfo.ColorHex}>{Name}</color>";

        /// <summary>
        /// Обычные — просто "топор" / "Axe".
        /// Необычные — префикс от самого жирного аффикса: "свирепый топор" / "Savage Axe".
        /// Редкие и выше — ещё и суффикс от следующего по силе другого стата: "свирепый топор ястреба" / "Savage Axe of the Hawk".
        /// </summary>
        private string BuildName(Lang lang)
        {
            var baseName = Base;

            // Сортируем дополнительные аффиксы по силе, самый жирный — первый.
            var ranked = new List<Affix>(Affixes);
            ranked.Sort((a, b) => b.Strength.CompareTo(a.Strength));

            string name = baseName.Get(lang);

            if (Quality >= GearQuality.Uncommon && ranked.Count >= 1)
                name = Stats.Get(ranked[0].Stat).Prefix(baseName.RuGender, lang) + " " + name;

            // Суффикс — от самого жирного аффикса ДРУГОГО стата ("свирепый топор ястреба", а не "свирепый топор ярости").
            // Если вся шмотка в один стат — что ж, "свирепый топор ярости" тоже честно.
            if (Quality >= GearQuality.Rare && ranked.Count >= 2)
            {
                var suffixAffix = ranked[1];
                for (int i = 1; i < ranked.Count; i++)
                {
                    if (ranked[i].Stat == ranked[0].Stat || ranked[i].IsCurse) continue;
                    suffixAffix = ranked[i];
                    break;
                }
                name = name + " " + Stats.Get(suffixAffix.Stat).Suffix(lang);
            }

            // Русские слова в таблицах с маленькой буквы, английские — уже с большой.
            return Capitalize(name);
        }

        /// <summary>"Legendary Helmet" / "Легендарный шлем" — так шмотка называется на земле, до подбора.</summary>
        public static string GroundName(GearSlot slot, GearQuality quality, Lang lang)
        {
            var slotInfo = Slots.Get(slot);
            string adjective = Qualities.Get(quality).Adjective(slotInfo.DisplayGenderRu, lang);
            string noun = slotInfo.DisplayName.Get(lang);
            if (lang == Lang.Russian) noun = noun.ToLowerInvariant();
            return Capitalize(adjective + " " + noun);
        }

        private static string Capitalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }
    }
}
