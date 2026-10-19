using System;
using System.Globalization;

namespace Diabloid.Loot
{
    // ВАЖНО: папка Loot не знает ничего про Unity и RoR2.
    // Это чистый C#, поэтому её же компилирует tools/LootSim — можно гонять генератор без игры.

    /// <summary>Какие характеристики вообще могут выпасть на шмотке.</summary>
    /// <remarks>
    /// Значения enum уходят по сети байтом, поэтому НОВЫЕ статы добавляй только в конец,
    /// иначе у хоста и клиента с разными версиями всё поедет.
    /// </remarks>
    public enum StatType : byte
    {
        Damage,             // +% урона
        AttackSpeed,        // +% скорости атаки
        CritChance,         // +% шанса крита (абсолютные проценты)
        CritDamage,         // +% урона крита
        MaxHealth,          // +N здоровья
        MaxHealthPercent,   // +% здоровья
        Regen,              // +N ОЗ/сек
        Armor,              // +N брони
        Shield,             // +N щита
        MoveSpeed,          // +% скорости передвижения (всегда, и шагом, и бегом)
        CooldownReduction,  // -% перезарядки
        JumpPower,          // +% силы прыжка
        HealOnHit,          // +N ОЗ за попадание (умножается на proc coefficient)
        RegenPercent,       // +% регенерации. Сам не выпадает — только проклятие пустотных шмоток (в минус)
        Lifesteal,          // +% вампиризма: лечит долю нанесённого урона (умножается на proc coefficient)
        ExtraJumps,         // +1 прыжок. Всегда ровно 1 на аффикс, больше — только повторами
        SprintSpeed,        // +% скорости бега (спринта), отдельно от общей скорости
        DamageBlock,        // -N урона с каждого полученного удара (как Repulsion Armor Plate), минимум 1 урона проходит
        BaseDamage,         // +N к базовому урону (тот, что растёт с уровнем персонажа и умножается процентами)
    }

    /// <summary>Род существительного — нужен, чтобы русские прилагательные согласовывались.</summary>
    public enum Gender : byte { Masculine, Feminine, Neuter, Plural }

    /// <summary>Всё, что нужно знать о стате: баланс + как его назвать и напечатать на обоих языках.</summary>
    public sealed class StatInfo
    {
        public readonly StatType Type;

        /// <summary>Значение аффикса на 1 уровне, обычное качество, бросок 100%.</summary>
        public readonly float BaseValue;

        /// <summary>Сколько долей от BaseValue добавляется за каждый уровень предмета.</summary>
        public readonly float LevelScaling;

        /// <summary>До скольки знаков округлять выпавшее значение.</summary>
        public readonly int Decimals;

        /// <summary>
        /// Значение всегда ровно BaseValue: качество, бросок, уровень и множители не влияют (прыжки: всегда +1).
        /// </summary>
        public readonly bool FixedValue;

        /// <summary>
        /// База для проклятий (лунных и пустотных). Обычно равна BaseValue, но у здоровья и регена
        /// бонусы перекручены (÷3 и ×2), а проклятия должны остаться на старой базе.
        /// </summary>
        public readonly float CurseBaseValue;

        /// <summary>Шаблон для вывода, {0} — число.</summary>
        private readonly Words format;

        /// <summary>Английский префикс ("Savage Axe"). В английском прилагательные не склоняются.</summary>
        private readonly string prefixEn;

        /// <summary>Русский префикс в 4 формах: м, ж, ср, мн. ("свирепый топор", "свирепая булава").</summary>
        private readonly string[] prefixRu;

        /// <summary>Суффикс: "Axe OF FURY" / "топор ЯРОСТИ" (родительный падеж).</summary>
        private readonly Words suffix;

        public StatInfo(StatType type, float baseValue, float levelScaling, int decimals,
                        Words format, string prefixEn, string[] prefixRu, Words suffix, float curseBaseValue = -1f,
                        bool fixedValue = false)
        {
            if (prefixRu == null || prefixRu.Length != 4)
                throw new ArgumentException("Need exactly 4 Russian adjective forms", nameof(prefixRu));

            Type = type;
            BaseValue = baseValue;
            CurseBaseValue = curseBaseValue > 0f ? curseBaseValue : baseValue;
            FixedValue = fixedValue;
            LevelScaling = levelScaling;
            Decimals = decimals;
            this.format = format;
            this.prefixEn = prefixEn;
            this.prefixRu = prefixRu;
            this.suffix = suffix;
        }

        public string Prefix(Gender gender, Lang lang) => lang == Lang.Russian ? prefixRu[(int)gender] : prefixEn;

        public string Suffix(Lang lang) => suffix.Get(lang);

        /// <summary>"+12.5% damage" / "+12.5% к урону". Отрицательное (проклятие) — "-12.5% damage".</summary>
        public string Describe(float value, Lang lang)
        {
            // InvariantCulture — чтобы на русской винде не было "12,5" в одних местах и "12.5" в других
            string number = Math.Round(Math.Abs(value), Decimals).ToString(Decimals == 0 ? "0" : "0.#", CultureInfo.InvariantCulture);
            string text = string.Format(format.Get(lang), number);

            // Шаблоны начинаются со знака ("+{0}% к урону", "-{0}% к перезарядке").
            // У проклятий знак переворачиваем: "-18% к урону", "+8% к перезарядке" (то есть дольше).
            if (value < 0f && text.Length > 0 && (text[0] == '+' || text[0] == '-'))
                text = (text[0] == '+' ? '-' : '+') + text.Substring(1);
            return text;
        }

        public string Describe(float value) => Describe(value, Loc.Current);
    }

    /// <summary>Справочник всех статов. Индекс в массиве == (int)StatType.</summary>
    public static class Stats
    {
        private static readonly StatInfo[] all =
        {
            //           стат                        база  +ур./ур.  знаки
            new StatInfo(StatType.Damage,            6f,   0.03f,    1,
                new Words("+{0}% damage", "+{0}% к урону"),
                "Savage", new[] { "свирепый", "свирепая", "свирепое", "свирепые" },
                new Words("of Fury", "ярости")),

            new StatInfo(StatType.AttackSpeed,       6f,   0.03f,    1,
                new Words("+{0}% attack speed", "+{0}% к скорости атаки"),
                "Swift", new[] { "быстрый", "быстрая", "быстрое", "быстрые" },
                new Words("of the Whirlwind", "вихря")),

            new StatInfo(StatType.CritChance,        3f,   0.02f,    1,
                new Words("+{0}% crit chance", "+{0}% к шансу крита"),
                "Keen", new[] { "точный", "точная", "точное", "точные" },
                new Words("of the Hawk", "ястреба")),

            new StatInfo(StatType.CritDamage,        10f,  0.03f,    0,
                new Words("+{0}% crit damage", "+{0}% к урону крита"),
                "Cruel", new[] { "жестокий", "жестокая", "жестокое", "жестокие" },
                new Words("of the Executioner", "палача")),

            // Здоровье со шмоток урезано втрое (было 25 и 5%). Реген: 0.8 → ×2 (0.5.0) → ÷3 (0.8.4) = 1.6/3.
            // Проклятия считаются от старых баз — см. CurseBaseValue.
            new StatInfo(StatType.MaxHealth,         25f / 3f, 0.30f, 0,
                new Words("+{0} max health", "+{0} к здоровью"),
                "Sturdy", new[] { "крепкий", "крепкая", "крепкое", "крепкие" },
                new Words("of the Bear", "медведя")),

            new StatInfo(StatType.MaxHealthPercent,  5f / 3f, 0.02f,  1,
                new Words("+{0}% max health", "+{0}% к здоровью"),
                "Mighty", new[] { "могучий", "могучая", "могучее", "могучие" },
                new Words("of the Titan", "титана"), curseBaseValue: 5f),

            new StatInfo(StatType.Regen,             1.6f / 3f, 0.20f, 1,
                new Words("+{0} HP/s regen", "+{0} ОЗ/с к регенерации"),
                "Vital", new[] { "живучий", "живучая", "живучее", "живучие" },
                new Words("of the Troll", "тролля"), curseBaseValue: 0.8f),

            new StatInfo(StatType.Armor,             5f,   0.05f,    1,
                new Words("+{0} armor", "+{0} к броне"),
                "Reinforced", new[] { "прочный", "прочная", "прочное", "прочные" },
                new Words("of the Turtle", "черепахи")),

            new StatInfo(StatType.Shield,            20f,  0.30f,    0,
                new Words("+{0} shield", "+{0} к щиту"),
                "Shimmering", new[] { "мерцающий", "мерцающая", "мерцающее", "мерцающие" },
                new Words("of the Mirage", "миража")),

            new StatInfo(StatType.MoveSpeed,         5f,   0.01f,    1,
                new Words("+{0}% movement speed", "+{0}% к скорости передвижения"),
                "Fleet", new[] { "лёгкий", "лёгкая", "лёгкое", "лёгкие" },
                new Words("of the Wind", "ветра")),

            new StatInfo(StatType.CooldownReduction, 4f,   0.00f,    1,
                new Words("-{0}% cooldowns", "-{0}% к перезарядке"),
                "Wise", new[] { "мудрый", "мудрая", "мудрое", "мудрые" },
                new Words("of the Sage", "мудреца")),

            new StatInfo(StatType.JumpPower,         8f,   0.01f,    0,
                new Words("+{0}% jump power", "+{0}% к силе прыжка"),
                "Springy", new[] { "прыгучий", "прыгучая", "прыгучее", "прыгучие" },
                new Words("of the Grasshopper", "кузнечика")),

            new StatInfo(StatType.HealOnHit,         2f,   0.30f,    1,
                new Words("+{0} HP on hit", "+{0} ОЗ за попадание"),
                "Vampiric", new[] { "вампирский", "вампирская", "вампирское", "вампирские" },
                new Words("of the Leech", "пиявки")),

            // Только проклятие пустотных шмоток: "-22% к регенерации". От уровня не растёт.
            // Префикс/суффикс на всякий случай есть, но в имя проклятия не попадают.
            new StatInfo(StatType.RegenPercent,      10f,  0.00f,    0,
                new Words("+{0}% regen", "+{0}% к регенерации"),
                "Vital", new[] { "живучий", "живучая", "живучее", "живучие" },
                new Words("of the Troll", "тролля")),

            // --- 0.6.0 ---

            // Доля нанесённого урона в лечение, × proc coefficient (как и лечение за попадание). От уровня не растёт.
            new StatInfo(StatType.Lifesteal,         1f,   0.00f,    1,
                new Words("+{0}% lifesteal", "+{0}% вампиризма"),
                "Bloodthirsty", new[] { "кровожадный", "кровожадная", "кровожадное", "кровожадные" },
                new Words("of the Vampire", "вампира")),

            // Ровно +1 прыжок, что бы ни выпало. "Прыжки дважды" (повтор) — +2.
            new StatInfo(StatType.ExtraJumps,        1f,   0.00f,    0,
                new Words("+{0} extra jump", "+{0} к числу прыжков"),
                "Soaring", new[] { "парящий", "парящая", "парящее", "парящие" },
                new Words("of the Sky", "неба"), fixedValue: true),

            // Прибавка к множителю спринта (у выживших он 1.45): +10% = бег на 10% базовой скорости быстрее.
            new StatInfo(StatType.SprintSpeed,       5f,   0.01f,    1,
                new Words("+{0}% sprint speed", "+{0}% к скорости бега"),
                "Rushing", new[] { "стремительный", "стремительная", "стремительное", "стремительные" },
                new Words("of the Cheetah", "гепарда")),

            // Слабый: ~1 на 1-м уровне, растёт как урон монстров (+20% за уровень), иначе к 3-му этапу ничего не значит.
            new StatInfo(StatType.DamageBlock,       1f,   0.20f,    1,
                new Words("-{0} damage taken per hit", "-{0} к получаемому урону за удар"),
                "Padded", new[] { "стёганый", "стёганая", "стёганое", "стёганые" },
                new Words("of the Wall", "стены")),

            // Плоский базовый урон. У Коммандо база 12 (+2.4 за уровень) — растёт так же, +20% за уровень,
            // так что обычный аффикс держит ~+4..8% урона Коммандо на любом уровне.
            new StatInfo(StatType.BaseDamage,        1.0f, 0.20f,    1,
                new Words("+{0} base damage", "+{0} к базовому урону"),
                "Heavy", new[] { "тяжёлый", "тяжёлая", "тяжёлое", "тяжёлые" },
                new Words("of the Ogre", "огра")),
        };

        public static int Count => all.Length;

        public static StatInfo Get(StatType type) => all[(int)type];

        /// <summary>
        /// Из чего выбираются проклятия лунных шмоток: меньше здоровья, брони, скорости, регенерации,
        /// дольше перезарядка, слабее прыжок. Значение проклятия — тот же стат, только со знаком минус.
        /// </summary>
        public static readonly StatType[] CursePool =
        {
            StatType.MaxHealthPercent,
            StatType.Armor,
            StatType.MoveSpeed,
            StatType.Regen,
            StatType.CooldownReduction,
            // Урон убран из проклятий в 0.8.4: "-30% урона" слишком некомфортно. Вместо него — сила прыжка:
            // пул должен быть не меньше 6, иначе шмотка, у которой 4 стата из пула уже заняты бонусами,
            // получит меньше 2 проклятий (у лунной: главный + 3 аффикса = 4).
            StatType.JumpPower,
        };

        /// <summary>Самопроверка: порядок в массиве совпадает с enum. Дёргается из плагина и симулятора.</summary>
        public static void Validate()
        {
            if (all.Length != Enum.GetValues(typeof(StatType)).Length)
                throw new InvalidOperationException("Stats: entry count does not match enum StatType");
            for (int i = 0; i < all.Length; i++)
                if ((int)all[i].Type != i)
                    throw new InvalidOperationException($"Stats: entry {i} is {all[i].Type}, order is broken");
        }
    }
}
