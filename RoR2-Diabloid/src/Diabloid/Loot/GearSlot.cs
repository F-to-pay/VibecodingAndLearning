using System;

namespace Diabloid.Loot
{
    /// <summary>Слоты экипировки. В каждом слоте может быть ровно одна шмотка.</summary>
    /// <remarks>Как и StatType — уходит по сети байтом, новые слоты только в конец.</remarks>
    public enum GearSlot : byte { Weapon, Helmet, Armor, Gloves, Pants, Boots }

    /// <summary>Косметическое базовое название ("Axe" / "топор") + род русского слова.</summary>
    public readonly struct BaseName
    {
        public readonly string En;
        public readonly string Ru;
        public readonly Gender RuGender;

        public BaseName(string en, string ru, Gender ruGender)
        {
            En = en;
            Ru = ru;
            RuGender = ruGender;
        }

        public string Get(Lang lang) => lang == Lang.Russian ? Ru : En;
    }

    public sealed class SlotInfo
    {
        public readonly GearSlot Slot;

        /// <summary>Как слот называется в интерфейсе и на земле ("Helmet" / "Шлем").</summary>
        public readonly Words DisplayName;

        /// <summary>Род русского DisplayName — для "Легендарный шлем" / "Легендарная броня".</summary>
        public readonly Gender DisplayGenderRu;

        /// <summary>Главный стат слота. Есть на любой шмотке этого слота и роллится жирнее.</summary>
        public readonly StatType Implicit;

        /// <summary>Из чего роллятся дополнительные аффиксы. Implicit сюда не входит.</summary>
        public readonly StatType[] Pool;

        /// <summary>Варианты базовых названий — чистая косметика. Индекс одинаковый для обоих языков.</summary>
        public readonly BaseName[] Bases;

        public SlotInfo(GearSlot slot, Words displayName, Gender displayGenderRu,
                        StatType implicitStat, StatType[] pool, BaseName[] bases)
        {
            Slot = slot;
            DisplayName = displayName;
            DisplayGenderRu = displayGenderRu;
            Implicit = implicitStat;
            Pool = pool;
            Bases = bases;
        }
    }

    public static class Slots
    {
        private const Gender M = Gender.Masculine, F = Gender.Feminine, N = Gender.Neuter, P = Gender.Plural;

        private static readonly SlotInfo[] all =
        {
            new SlotInfo(GearSlot.Weapon, new Words("Weapon", "Оружие"), N, StatType.Damage,
                new[] { StatType.AttackSpeed, StatType.CritChance, StatType.CritDamage, StatType.HealOnHit, StatType.CooldownReduction, StatType.MaxHealthPercent,
                        StatType.Lifesteal, StatType.BaseDamage },
                new[]
                {
                    new BaseName("Blade", "клинок", M), new BaseName("Axe", "топор", M), new BaseName("Mace", "булава", F),
                    new BaseName("Spear", "копьё", N), new BaseName("Staff", "посох", M),
                }),

            new SlotInfo(GearSlot.Helmet, new Words("Helmet", "Шлем"), M, StatType.MaxHealth,
                new[] { StatType.MaxHealthPercent, StatType.Regen, StatType.Armor, StatType.CooldownReduction, StatType.CritChance, StatType.Shield,
                        StatType.DamageBlock },
                new[]
                {
                    new BaseName("Helm", "шлем", M), new BaseName("Sallet", "шапель", F),
                    new BaseName("Crown", "корона", F), new BaseName("Hood", "капюшон", M),
                }),

            new SlotInfo(GearSlot.Armor, new Words("Armor", "Броня"), F, StatType.Armor,
                new[] { StatType.MaxHealth, StatType.MaxHealthPercent, StatType.Shield, StatType.Regen, StatType.Damage, StatType.CooldownReduction,
                        StatType.DamageBlock },
                new[]
                {
                    new BaseName("Armor", "броня", F), new BaseName("Cuirass", "кираса", F),
                    new BaseName("Chainmail", "кольчуга", F), new BaseName("Plate", "доспех", M),
                }),

            new SlotInfo(GearSlot.Gloves, new Words("Gloves", "Рукавицы"), P, StatType.AttackSpeed,
                new[] { StatType.Damage, StatType.CritChance, StatType.CritDamage, StatType.HealOnHit, StatType.Armor, StatType.MaxHealth,
                        StatType.Lifesteal, StatType.BaseDamage },
                new[]
                {
                    new BaseName("Mitts", "рукавицы", P), new BaseName("Gloves", "перчатки", P),
                    new BaseName("Bracers", "наручи", P), new BaseName("Gauntlets", "латные рукавицы", P),
                }),

            new SlotInfo(GearSlot.Pants, new Words("Pants", "Штаны"), P, StatType.Regen,
                new[] { StatType.Armor, StatType.MoveSpeed, StatType.MaxHealth, StatType.MaxHealthPercent, StatType.JumpPower, StatType.Shield,
                        StatType.SprintSpeed, StatType.DamageBlock, StatType.ExtraJumps },
                new[]
                {
                    new BaseName("Trousers", "штаны", P), new BaseName("Greaves", "поножи", P),
                    new BaseName("Breeches", "шаровары", P), new BaseName("Tassets", "набедренник", M),
                }),

            new SlotInfo(GearSlot.Boots, new Words("Boots", "Ботинки"), P, StatType.MoveSpeed,
                new[] { StatType.JumpPower, StatType.Armor, StatType.Regen, StatType.MaxHealth, StatType.CooldownReduction, StatType.AttackSpeed,
                        StatType.ExtraJumps, StatType.SprintSpeed },
                new[]
                {
                    new BaseName("Boots", "ботинки", P), new BaseName("High Boots", "сапоги", P),
                    new BaseName("Shoes", "башмаки", P), new BaseName("Sabatons", "сабатоны", P),
                }),
        };

        public static int Count => all.Length;

        public static SlotInfo Get(GearSlot slot) => all[(int)slot];

        public static void Validate()
        {
            if (all.Length != Enum.GetValues(typeof(GearSlot)).Length)
                throw new InvalidOperationException("Slots: entry count does not match enum GearSlot");

            for (int i = 0; i < all.Length; i++)
            {
                var info = all[i];
                if ((int)info.Slot != i)
                    throw new InvalidOperationException($"Slots: entry {i} is {info.Slot}, order is broken");
                if (Array.IndexOf(info.Pool, info.Implicit) >= 0)
                    throw new InvalidOperationException($"Slots: {info.Slot} has its implicit stat duplicated in the pool");
                if (info.Pool.Length < Qualities.MaxAffixCount)
                    throw new InvalidOperationException($"Slots: {info.Slot} pool has {info.Pool.Length} stats, legendary needs {Qualities.MaxAffixCount}");
                if (info.Bases.Length == 0 || info.Bases.Length > 255)
                    throw new InvalidOperationException($"Slots: {info.Slot} has a bad number of base names");
            }
        }
    }
}
