using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Diabloid.Loot;

namespace Diabloid.LootSim
{
    /// <summary>
    /// dotnet run --project tools/LootSim                 — 12 случайных шмоток 1-го уровня
    /// dotnet run --project tools/LootSim -- 20 15 42     — 20 шмоток, уровень 15, сид 42
    /// dotnet run --project tools/LootSim -- --test       — самопроверка генератора (код выхода 0 = всё ок)
    /// Флаг --en в любом месте — вывод на английском (по умолчанию русский).
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            // --en можно поставить где угодно, остальные аргументы — как раньше.
            var argList = new List<string>(args);
            bool english = argList.Remove("--en");
            args = argList.ToArray();
            Loc.Provider = () => english ? Lang.English : Lang.Russian;

            Stats.Validate();
            Qualities.Validate();
            Slots.Validate();

            if (args.Length > 0 && args[0] == "--rates")
            {
                PrintStatRates();
                return 0;
            }

            if (args.Length > 0 && args[0] == "--test")
                return SelfTest();

            int count = args.Length > 0 && int.TryParse(args[0], out var c) ? c : 12;
            int level = args.Length > 1 && int.TryParse(args[1], out var l) ? l : 1;
            int seed = args.Length > 2 && int.TryParse(args[2], out var s) ? s : Environment.TickCount;

            var rng = new Random(seed);
            var settings = new LootSettings();
            var gear = new GearSet();

            // Пустотные шмотки живут на "текущем уровне монстров" — в симуляторе это просто уровень из аргументов.
            GearItem.MonsterLevelProvider = () => level;

            Console.WriteLine(Loc.T($"Seed {seed}, item level {level}\n", $"Сид {seed}, уровень предметов {level}\n"));

            for (int i = 0; i < count; i++)
            {
                var slot = LootGenerator.RollSlot(rng);
                var quality = LootGenerator.RollQuality(rng, settings.QualityWeights);
                var item = LootGenerator.Generate(slot, quality, level, rng, settings);
                PrintItem(item);

                // Надеваем, только если сильнее надетого — просто чтобы в конце было что показать.
                var current = gear.Get(slot);
                if (current == null || item.Score > current.Score) gear.Equip(item);
            }

            Console.WriteLine(Loc.T("=== Special qualities (not from random drops) ===", "=== Особые качества (не из обычного дропа) ==="));
            PrintItem(LootGenerator.Generate(LootGenerator.RollSlot(rng), GearQuality.Lunar, level, rng, settings));
            PrintItem(LootGenerator.Generate(LootGenerator.RollSlot(rng), GearQuality.Void, level, rng, settings));

            Console.WriteLine(Loc.T("=== Best equipped ===", "=== Лучшее надетое ==="));
            for (int i = 0; i < Slots.Count; i++)
            {
                var item = gear.Get((GearSlot)i);
                Console.Write($"{Slots.Get((GearSlot)i).DisplayName,-9}: ");
                if (item == null) { Console.WriteLine(Loc.T("empty", "пусто")); continue; }
                WriteColored(item.Name, item.Quality);
                Console.WriteLine($"  ({Loc.T("power", "сила")} {item.Score:0.0})");
            }

            var totals = new float[Stats.Count];
            gear.AccumulateTotals(totals);
            Console.WriteLine(Loc.T("\nTotal:", "\nИтого:"));
            for (int i = 0; i < totals.Length; i++)
                if (totals[i] > 0) Console.WriteLine("   " + Stats.Get((StatType)i).Describe(totals[i]));

            Console.WriteLine();
            PrintDistribution(Loc.T("Regular enemy", "Обычный моб"), settings, GearQuality.Common, 0f);
            PrintDistribution(Loc.T("Elite (luck 1)", "Элитка (удача 1)"), settings, GearQuality.Common, 1f);
            PrintDistribution(Loc.T("Boss (min Rare, luck 1)", "Босс (мин. редкое, удача 1)"), settings, GearQuality.Rare, 1f);
            return 0;
        }

        private static void PrintItem(GearItem item)
        {
            item = item.Current(); // у пустотных — цифры под текущий уровень
            WriteColored(item.Name, item.Quality);
            Console.WriteLine($"  [{Slots.Get(item.Slot).DisplayName}, {item.QualityInfo.DisplayName(Loc.Current)}, {Loc.T("lvl", "ур.")} {item.Level}, {Loc.T("power", "сила")} {item.Score:0.0}]");
            Console.WriteLine("    " + item.Implicit.Describe() + Loc.T("   <- main stat", "   <- главный стат"));
            foreach (var affix in item.Affixes)
                Console.WriteLine("    " + affix.Describe() + (affix.IsCurse ? Loc.T("   <- curse", "   <- проклятие") : ""));
            foreach (var perk in item.Perks)
                Console.WriteLine("    * " + perk.Describe(item.Level));
            Console.WriteLine();
        }

        private static void WriteColored(string text, GearQuality quality)
        {
            var old = Console.ForegroundColor;
            Console.ForegroundColor = quality switch
            {
                GearQuality.Common => ConsoleColor.White,
                GearQuality.Uncommon => ConsoleColor.Green,
                GearQuality.Rare => ConsoleColor.Red,
                GearQuality.Epic => ConsoleColor.Magenta,
                GearQuality.Legendary => ConsoleColor.Yellow,
                GearQuality.Lunar => ConsoleColor.Blue,
                _ => ConsoleColor.DarkMagenta,
            };
            Console.Write(text);
            Console.ForegroundColor = old;
        }

        /// <summary>
        /// --rates: в скольки процентах дропа с обычного моба (слот случайный, качество по весам) есть каждый стат.
        /// Отвечает на вопросы вида "почему я ни разу не видел +1 прыжок".
        /// </summary>
        private static void PrintStatRates()
        {
            const int drops = 200_000;
            var rng = new Random(7);
            var settings = new LootSettings();
            var hits = new int[Stats.Count];
            for (int i = 0; i < drops; i++)
            {
                var item = LootGenerator.Generate(LootGenerator.RollSlot(rng), LootGenerator.RollQuality(rng, settings.QualityWeights), 10, rng, settings);
                var seen = new bool[Stats.Count];
                foreach (var affix in item.AllAffixes)
                    if (!affix.IsCurse) seen[(int)affix.Stat] = true;
                for (int s = 0; s < seen.Length; s++) if (seen[s]) hits[s]++;
            }
            for (int s = 0; s < hits.Length; s++)
                Console.WriteLine($"{(StatType)s,-18} {100.0 * hits[s] / drops,5:0.0}%");
        }

        private static void PrintDistribution(string title, LootSettings settings, GearQuality min, float luck)
        {
            const int rolls = 100_000;
            var rng = new Random(1);
            var hits = new int[Qualities.RandomlyRolledCount];
            for (int i = 0; i < rolls; i++)
                hits[(int)LootGenerator.RollQuality(rng, settings.QualityWeights, min, luck)]++;

            Console.Write($"{title,-30}");
            for (int q = 0; q < hits.Length; q++)
            {
                if (!Qualities.Get((GearQuality)q).RandomlyRolled) continue; // вырезанный эпик
                Console.Write($"{(GearQuality)q}: {100.0 * hits[q] / rolls,5:0.0}%   ");
            }
            Console.WriteLine();
        }

        // ------------------------------------------------------------------

        private static int SelfTest()
        {
            var failures = new List<string>();
            void Check(bool condition, string message)
            {
                if (!condition && failures.Count < 50) failures.Add(message);
            }

            var settings = new LootSettings();
            var rng = new Random(12345);
            int monsterLevel = 1;
            GearItem.MonsterLevelProvider = () => monsterLevel;

            // 1. Генерируем кучу шмоток всех слотов, качеств и уровней.
            for (int n = 0; n < 20_000; n++)
            {
                var slot = (GearSlot)(n % Slots.Count);
                var quality = (GearQuality)((n / Slots.Count) % Qualities.Count);
                int level = 1 + n % 60;
                monsterLevel = level;
                var raw = LootGenerator.Generate(slot, quality, level, rng, settings);
                var item = raw.Current();
                var slotInfo = Slots.Get(slot);
                var qualityInfo = Qualities.Get(quality);
                string tag = $"[{slot}/{quality}/ур.{level}]";
                var buffs = item.Affixes.Where(a => !a.IsCurse).ToList();
                var curses = item.Affixes.Where(a => a.IsCurse).ToList();

                Check(item.Slot == slot && item.Quality == quality, $"{tag} слот/качество не совпали");
                Check(item.Level == level, $"{tag} уровень {item.Level}");
                Check(item.Implicit.Stat == slotInfo.Implicit, $"{tag} implicit не тот");
                Check(buffs.Count == qualityInfo.AffixCount, $"{tag} аффиксов {buffs.Count}");
                Check(curses.Count == qualityInfo.CurseCount, $"{tag} проклятий {curses.Count}");
                Check(curses.Select(a => a.Stat).Distinct().Count() == curses.Count, $"{tag} повторяющиеся проклятия");
                Check(curses.All(c => c.Stat != item.Implicit.Stat && buffs.All(b => b.Stat != c.Stat)), $"{tag} проклятие совпало с бонусом");
                Check(buffs.All(a => a.Stat == slotInfo.Implicit || Array.IndexOf(slotInfo.Pool, a.Stat) >= 0), $"{tag} аффикс не из пула слота");
                Check(curses.All(a => Array.IndexOf(Stats.CursePool, a.Stat) >= 0 || a.Stat == qualityInfo.ForcedCurse), $"{tag} проклятие не из CursePool");
                if (qualityInfo.ForcedCurse.HasValue)
                    Check(curses.Any(a => a.Stat == qualityInfo.ForcedCurse.Value), $"{tag} нет обязательного проклятия {qualityInfo.ForcedCurse}");
                if (quality == GearQuality.Void)
                {
                    var regenCut = curses.First(a => a.Stat == StatType.RegenPercent).Value;
                    Check(regenCut <= -20f && regenCut >= -25f, $"{tag} пустотное режет реген на {regenCut}%, ждали -20..-25%");
                }
                Check(item.Implicit.Value > 0 && buffs.All(a => a.Value > 0), $"{tag} положительный стат <= 0");
                Check(buffs.Where(a => a.Stat == StatType.ExtraJumps).All(a => a.Value == 1f), $"{tag} прыжки не ровно +1");
                Check(item.Perks.Count == qualityInfo.PerkCount, $"{tag} перков {item.Perks.Count}, ждали {qualityInfo.PerkCount}");
                Check(item.Perks.Select(p => p.Type).Distinct().Count() == item.Perks.Count, $"{tag} два одинаковых перка");
                Check(item.Perks.All(p => p.Roll >= 0f && p.Roll <= 1f), $"{tag} бросок перка вне 0..1");
                Check(ReferenceEquals(raw, item) || raw.Perks.Select(p => p.Type).SequenceEqual(item.Perks.Select(p => p.Type)), $"{tag} Current() потерял перки");
                foreach (Lang lang in Enum.GetValues(typeof(Lang)))
                    foreach (var perk in item.Perks)
                    {
                        string text = perk.Describe(item.Level, lang);
                        Check(text.IndexOf('{') < 0 && text != perk.Type.ToString(), $"{tag} кривое описание перка [{lang}] '{text}'");
                    }
                Check(item.AllAffixes.All(a => !float.IsNaN(a.Value) && !float.IsInfinity(a.Value)), $"{tag} кривое значение");
                foreach (Lang lang in Enum.GetValues(typeof(Lang)))
                    foreach (var curse in curses)
                    {
                        string text = curse.Describe(lang);
                        bool flipped = curse.Stat == StatType.CooldownReduction ? text.StartsWith("+") : text.StartsWith("-");
                        Check(flipped && !text.Contains("--") && !text.Contains("+-"), $"{tag} кривое описание проклятия [{lang}] '{text}'");
                    }
                foreach (Lang lang in Enum.GetValues(typeof(Lang)))
                {
                    string name = item.GetName(lang);
                    Check(!string.IsNullOrWhiteSpace(name) && char.IsUpper(name[0]), $"{tag} кривое имя [{lang}] '{name}'");
                    Check(name.IndexOf('{') < 0 && name.IndexOf('}') < 0, $"{tag} фигурные скобки в имени [{lang}]");
                    Check(item.AllAffixes.All(a => a.Describe(lang).IndexOf('{') < 0), $"{tag} неподставленный шаблон в описании стата [{lang}]");
                    bool hasCyrillic = name.Any(ch => ch >= 'А' && ch <= 'я');
                    Check(lang == Lang.Russian ? hasCyrillic : !hasCyrillic, $"{tag} имя не на том языке [{lang}] '{name}'");
                }
            }

            // 2. Качество выше — статы в среднем выше (на одном уровне). Особые проверяем отдельно ниже.
            for (int s = 0; s < Slots.Count; s++)
            {
                double previousAvg = 0;
                for (int q = 0; q < Qualities.RandomlyRolledCount; q++)
                {
                    if (!Qualities.Get((GearQuality)q).RandomlyRolled) continue; // вырезанный эпик
                    double sum = 0;
                    for (int i = 0; i < 2000; i++)
                        sum += LootGenerator.Generate((GearSlot)s, (GearQuality)q, 10, rng, settings).Implicit.Strength;
                    double avg = sum / 2000;
                    Check(avg > previousAvg, $"[{(GearSlot)s}] implicit у {(GearQuality)q} не жирнее предыдущего качества");
                    previousAvg = avg;
                }
            }

            // 2б. Лунное жирнее легендарки, пустотное на любом уровне ≈ эпику того же уровня.
            {
                double Avg(GearQuality q, int level)
                {
                    monsterLevel = level;
                    double sum = 0;
                    for (int i = 0; i < 4000; i++)
                        sum += LootGenerator.Generate((GearSlot)(i % Slots.Count), q, level, rng, settings).Current().Implicit.Strength;
                    return sum / 4000;
                }

                Check(Avg(GearQuality.Lunar, 10) > Avg(GearQuality.Legendary, 10), "лунное не жирнее легендарки");
                foreach (int level in new[] { 1, 10, 40 })
                {
                    double voidAvg = Avg(GearQuality.Void, level), epicAvg = Avg(GearQuality.Epic, level);
                    Check(Math.Abs(voidAvg / epicAvg - 1.0) < 0.05, $"пустотное на ур.{level}: {voidAvg:0.00} против эпика {epicAvg:0.00}");
                }
            }

            // 2б'. Проклятия лунных — вдвое злее, чем в 0.4.0 (там x3 от старых баз), бонусы ХП ÷3, реген ×2.
            {
                var flat = new LootSettings { DuplicateStatChance = 0f, LevelScalingMultiplier = 0f };
                double curseSum = 0; int curseN = 0;
                for (int i = 0; i < 6000; i++)
                    foreach (var c in LootGenerator.Generate((GearSlot)(i % Slots.Count), GearQuality.Lunar, 1, rng, flat).Affixes.Where(a => a.IsCurse))
                    {
                        curseSum += -c.Value / (Stats.Get(c.Stat).CurseBaseValue * 3.0); // во сколько раз злее старого x3
                        curseN++;
                    }
                double ratio = curseSum / curseN / 0.95; // средний бросок лунных 0.95
                Check(Math.Abs(ratio - 2.0) < 0.1, $"лунные проклятия злее старых в {ratio:0.00} раза, ждали ~2");
                Check(Math.Abs(Stats.Get(StatType.MaxHealth).BaseValue * 3f - 25f) < 0.01f, "база плоского ХП не 25/3");
                Check(Math.Abs(Stats.Get(StatType.MaxHealthPercent).BaseValue * 3f - 5f) < 0.01f, "база %ХП не 5/3");
                Check(Math.Abs(Stats.Get(StatType.Regen).BaseValue * 3f - 1.6f) < 0.001f, "база регена не 1.6/3");
                Check(Array.IndexOf(Stats.CursePool, StatType.Damage) < 0, "урон снова в проклятиях");
            }

            // 2б''. Перки: крайние значения диапазонов и обратная/прямая связь.
            {
                var lo = new Perk(PerkType.DamageAura, 0f); var hi = new Perk(PerkType.DamageAura, 1f);
                Check(lo.AuraRadius == 10f && lo.AuraDamagePercent == 80f && hi.AuraRadius == 50f && hi.AuraDamagePercent == 20f, "аура: края диапазонов");
                var a0 = new Perk(PerkType.ArmorOnHurt, 0f); var a1 = new Perk(PerkType.ArmorOnHurt, 1f);
                Check(a0.ArmorThresholdPercent == 10f && a1.ArmorThresholdPercent == 50f, "броня: края диапазона");
                var s0 = new Perk(PerkType.PercentHpStrike, 0f); var s1 = new Perk(PerkType.PercentHpStrike, 1f);
                Check(s0.StrikeChance == 8f && s0.StrikeHpPercent == 5f && s1.StrikeChance == 40f && s1.StrikeHpPercent == 1f, "удар: края диапазонов");
                Check(Math.Abs(Perk.BerserkDamagePerStep(1) - 1f) < 0.001f && Perk.BerserkDamagePerStep(11) > 2.9f, "берсерк: 1 на 1-м уровне, растёт с уровнем");
                var common = LootGenerator.Generate(GearSlot.Weapon, GearQuality.Common, 5, rng, settings);
                var uncommon = LootGenerator.Generate(GearSlot.Weapon, GearQuality.Uncommon, 5, rng, settings);
                Check(common.Perks.Count == 0 && uncommon.Perks.Count == 0, "перки у обычных/необычных");
            }

            // 2в. Пустотная шмотка растёт вместе с уровнем монстров, имя при этом не меняется.
            {
                monsterLevel = 1;
                var voidItem = LootGenerator.Generate(GearSlot.Armor, GearQuality.Void, 1, rng, settings);
                var atLevel1 = voidItem.Current();
                string name1 = atLevel1.GetName(Lang.Russian);
                monsterLevel = 30;
                var atLevel30 = voidItem.Current();
                Check(atLevel30.Level == 30, "пустотная шмотка не взяла уровень монстров");
                Check(ReferenceEquals(atLevel30, voidItem.Current()), "Current() не кеширует копию на том же уровне");
                Check(atLevel30.Implicit.Value > atLevel1.Implicit.Value, "пустотная шмотка не выросла с уровнем");
                Check(atLevel30.GetName(Lang.Russian) == name1, "имя пустотной шмотки поменялось с уровнем");
                Check(ReferenceEquals(atLevel30.Current(), atLevel30), "копия пустотной шмотки снова масштабируется");

                var set = new GearSet();
                set.Equip(voidItem);
                Check(set.HasLevelScaling, "GearSet не видит пустотную шмотку");
                // Сумма всех строк брони, а не только главного стата: повтор мог скопировать броню ещё раз.
                float expectedArmor = atLevel30.AllAffixes.Where(x => x.Stat == StatType.Armor).Sum(x => x.Value);
                Check(Math.Abs(set.GetTotal(StatType.Armor) - expectedArmor) < 0.001f, "GearSet считает пустотную шмотку не по текущему уровню");
            }

            // 2г. Повторы статов: 0% — все разные, 100% — вся шмотка в главный стат, 25% — где-то посередине.
            {
                monsterLevel = 10;
                var noDup = new LootSettings { DuplicateStatChance = 0f };
                var allDup = new LootSettings { DuplicateStatChance = 1f };
                int itemsWithDup = 0, legendaries = 0;
                for (int i = 0; i < 6000; i++)
                {
                    var slot = (GearSlot)(i % Slots.Count);
                    var q = (GearQuality)(i / Slots.Count % Qualities.Count);

                    var a = LootGenerator.Generate(slot, q, 10, rng, noDup).Current();
                    Check(a.AllAffixes.Select(x => x.Stat).Distinct().Count() == a.Affixes.Count + 1, $"[{slot}/{q}] повторы при шансе 0%");

                    var b = LootGenerator.Generate(slot, q, 10, rng, allDup).Current();
                    Check(b.Affixes.Where(x => !x.IsCurse).All(x => x.Stat == b.Implicit.Stat), $"[{slot}/{q}] при шансе 100% не всё в главный стат");
                    Check(b.Affixes.Where(x => x.IsCurse).All(x => x.Stat != b.Implicit.Stat), $"[{slot}/{q}] проклятие совпало с главным статом");

                    if (q != GearQuality.Legendary) continue;
                    legendaries++;
                    var c = LootGenerator.Generate(slot, q, 10, rng, settings);
                    if (c.AllAffixes.Select(x => x.Stat).Distinct().Count() < c.Affixes.Count + 1) itemsWithDup++;
                }
                double share = (double)itemsWithDup / legendaries;
                Check(share > 0.5 && share < 0.95, $"легендарки с повторами при 25%: {share:P0} (ждали где-то 70-80%)");
            }

            // 3. Распределение качеств соответствует весам, особые не выпадают, а minQuality отрезает низ.
            {
                var hits = new int[Qualities.Count];
                const int rolls = 200_000;
                for (int i = 0; i < rolls; i++) hits[(int)LootGenerator.RollQuality(rng, settings.QualityWeights, GearQuality.Common, 3f)]++;
                for (int q = 0; q < hits.Length; q++)
                    if (!Qualities.Get((GearQuality)q).RandomlyRolled)
                        Check(hits[q] == 0, $"качество {(GearQuality)q} выпало из обычного рандома (особое или вырезанное)");

                // Даже если в весах эпику что-то осталось (старый конфиг) — он не выпадает.
                var withEpic = (float[])settings.QualityWeights.Clone();
                withEpic[(int)GearQuality.Epic] = 50f;
                for (int i = 0; i < 20_000; i++)
                    Check(LootGenerator.RollQuality(rng, withEpic, GearQuality.Common, 1f) != GearQuality.Epic, "эпик выпал при ненулевом весе");

                Array.Clear(hits, 0, hits.Length);
                for (int i = 0; i < rolls; i++) hits[(int)LootGenerator.RollQuality(rng, settings.QualityWeights)]++;
                float total = settings.QualityWeights.Sum();
                for (int q = 0; q < settings.QualityWeights.Length; q++)
                {
                    double expected = settings.QualityWeights[q] / total;
                    double actual = (double)hits[q] / rolls;
                    Check(Math.Abs(expected - actual) < 0.01, $"качество {(GearQuality)q}: ждали {expected:P1}, вышло {actual:P1}");
                }

                for (int i = 0; i < 10_000; i++)
                    Check(LootGenerator.RollQuality(rng, settings.QualityWeights, GearQuality.Rare, 1f) >= GearQuality.Rare, "minQuality не сработал");
            }

            // 4. Названия на земле на обоих языках.
            for (int s = 0; s < Slots.Count; s++)
                for (int q = 0; q < Qualities.Count; q++)
                    foreach (Lang lang in Enum.GetValues(typeof(Lang)))
                    {
                        string ground = GearItem.GroundName((GearSlot)s, (GearQuality)q, lang);
                        Check(!string.IsNullOrWhiteSpace(ground) && char.IsUpper(ground[0]), $"кривое название на земле [{lang}] '{ground}'");
                    }

            // 5. GearSet: один слот — одна шмотка, замена возвращает старую.
            {
                var set = new GearSet();
                var a = LootGenerator.Generate(GearSlot.Helmet, GearQuality.Common, 1, rng, settings);
                var b = LootGenerator.Generate(GearSlot.Helmet, GearQuality.Legendary, 1, rng, settings);
                Check(set.IsEmpty, "новый GearSet не пустой");
                Check(set.Equip(a) == null, "первая шмотка в слот вернула что-то");
                Check(set.Equip(b) == a, "замена не вернула старую шмотку");
                Check(set.Get(GearSlot.Helmet) == b, "в слоте не новая шмотка");
                var totals = new float[Stats.Count];
                set.AccumulateTotals(totals);
                Check(Math.Abs(totals[(int)StatType.MaxHealth] - set.GetTotal(StatType.MaxHealth)) < 0.001f, "AccumulateTotals != GetTotal");
            }

            if (failures.Count == 0)
            {
                Console.WriteLine("SELFTEST OK: генератор ведёт себя прилично.");
                return 0;
            }

            Console.WriteLine($"SELFTEST FAIL ({failures.Count}):");
            foreach (var f in failures) Console.WriteLine(" - " + f);
            return 1;
        }
    }
}
