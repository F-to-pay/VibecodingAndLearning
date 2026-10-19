using System;

namespace Diabloid.Loot
{
    /// <summary>Качество шмотки. Чем выше — тем больше аффиксов и жирнее цифры.</summary>
    /// <remarks>
    /// Значение уходит по сети байтом и зашито в ItemDef'ы, поэтому новые качества — только в конец.
    /// Lunar и Void — "особые": обычный рандом их не выбирает (вес 0), они приходят только
    /// из лунных/пустотных источников (см. артефакт снаряжения и дроп с лунных/пустотных мобов).
    /// Epic с 0.5.0 вырезан из игры (не выпадает вообще), но из enum его не убрать — сломается сетевой формат.
    /// </remarks>
    public enum GearQuality : byte { Common, Uncommon, Rare, Epic, Legendary, Lunar, Void }

    public sealed class QualityInfo
    {
        public readonly GearQuality Quality;

        /// <summary>Английское название качества ("Legendary").</summary>
        private readonly string nameEn;

        /// <summary>Русское прилагательное качества в 4 формах: м, ж, ср, мн.</summary>
        private readonly string[] formsRu;

        /// <summary>Цвет для чата/панели, hex без решётки.</summary>
        public readonly string ColorHex;

        /// <summary>Множитель ко всем статам шмотки.</summary>
        public readonly float StatMultiplier;

        /// <summary>
        /// Нижняя граница броска. Бросок — случайное число в [RollMin; 1].
        /// У легендарки разброс узкий: она почти всегда близка к максимуму.
        /// </summary>
        public readonly float RollMin;

        /// <summary>Сколько аффиксов СВЕРХ implicit-стата.</summary>
        public readonly int AffixCount;

        /// <summary>Вес по умолчанию при выборе качества (переопределяется конфигом).</summary>
        public readonly float DefaultWeight;

        /// <summary>
        /// Участвует ли качество в обычном рандоме (дроп с мобов, веса в конфиге).
        /// Особые качества (лунное, пустотное) выдаются только напрямую.
        /// </summary>
        public readonly bool RandomlyRolled;

        /// <summary>Сколько проклятий (отрицательных аффиксов) сверху. Лунные — 2 случайных, пустотные — 1 (реген).</summary>
        public readonly int CurseCount;

        /// <summary>Сколько перков (особых свойств). Редкое, лунное, пустотное — 1, легендарка — 2.</summary>
        public readonly int PerkCount;

        /// <summary>
        /// Проклятие, которое есть всегда (первым), остальные — случайные из Stats.CursePool.
        /// Пустотные: всегда минус % регенерации.
        /// </summary>
        public readonly StatType? ForcedCurse;

        /// <summary>Сила проклятий относительно базового значения стата (как StatMultiplier, только в минус).</summary>
        public readonly float CurseMultiplier;

        /// <summary>
        /// Уровень шмотки всегда равен текущему уровню монстров и растёт вместе с ним (пустотные).
        /// Цифры у таких шмоток хранятся "сырыми" для 1-го уровня, актуальные даёт GearItem.Current().
        /// </summary>
        public readonly bool ScalesWithMonsterLevel;

        public QualityInfo(GearQuality quality, string nameEn, string[] formsRu, string colorHex,
                           float statMultiplier, float rollMin, int affixCount, float defaultWeight,
                           bool randomlyRolled = true, int curseCount = 0, float curseMultiplier = 0f,
                           bool scalesWithMonsterLevel = false, StatType? forcedCurse = null, int perkCount = 0)
        {
            if (formsRu == null || formsRu.Length != 4)
                throw new ArgumentException("Need exactly 4 Russian adjective forms", nameof(formsRu));

            Quality = quality;
            this.nameEn = nameEn;
            this.formsRu = formsRu;
            ColorHex = colorHex;
            StatMultiplier = statMultiplier;
            RollMin = rollMin;
            AffixCount = affixCount;
            DefaultWeight = defaultWeight;
            RandomlyRolled = randomlyRolled;
            CurseCount = curseCount;
            CurseMultiplier = curseMultiplier;
            ScalesWithMonsterLevel = scalesWithMonsterLevel;
            ForcedCurse = forcedCurse;
            PerkCount = perkCount;
        }

        /// <summary>Прилагательное качества: "Legendary" или "легендарная" (по роду русского слова).</summary>
        public string Adjective(Gender ruGender, Lang lang) => lang == Lang.Russian ? formsRu[(int)ruGender] : nameEn;

        /// <summary>Просто название качества для интерфейса: "Legendary" / "Легендарное".</summary>
        public string DisplayName(Lang lang)
        {
            if (lang != Lang.Russian) return nameEn;
            string s = formsRu[(int)Gender.Neuter];
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }
    }

    public static class Qualities
    {
        private static readonly QualityInfo[] all =
        {
            //               качество                 англ.        русские формы (м, ж, ср, мн)                                            цвет      xСтаты  мин.бросок  аффиксы  вес
            new QualityInfo(GearQuality.Common,     "Common",    new[] { "обычный", "обычная", "обычное", "обычные" },                 "FFFFFF", 1.0f,   0.5f,       1,       60f),
            new QualityInfo(GearQuality.Uncommon,   "Uncommon",  new[] { "необычный", "необычная", "необычное", "необычные" },         "77FF17", 1.3f,   0.6f,       2,       25f),
            new QualityInfo(GearQuality.Rare,       "Rare",      new[] { "редкий", "редкая", "редкое", "редкие" },                     "E7543A", 1.7f,   0.7f,       3,       10f,
                            perkCount: 1),
            // Эпик вырезан (0.5.0): не выпадает ни из рандома, ни из артефакта. Строка осталась ради порядка enum.
            new QualityInfo(GearQuality.Epic,       "Epic",      new[] { "эпический", "эпическая", "эпическое", "эпические" },         "C679FF", 2.2f,   0.8f,       4,       0f,
                            randomlyRolled: false),
            new QualityInfo(GearQuality.Legendary,  "Legendary", new[] { "легендарный", "легендарная", "легендарное", "легендарные" }, "FFB300", 3.0f,   0.9f,       5,       1f,
                            perkCount: 2),

            // Лунное: 3 очень жирных аффикса (x4) + 2 проклятия (x6 в минус). Сделка с луной, как у ванильных лунных предметов.
            new QualityInfo(GearQuality.Lunar,      "Lunar",     new[] { "лунный", "лунная", "лунное", "лунные" },                     "307FFF", 4.0f,   0.9f,       3,       0f,
                            randomlyRolled: false, curseCount: 2, curseMultiplier: 6.0f, perkCount: 1),

            // Пустотное: сила x2.2, уровень всегда = текущему уровню монстров, чем дольше забег — тем жирнее.
            // Плата: всегда режет регенерацию (-20..25% за шмотку, от уровня не растёт).
            new QualityInfo(GearQuality.Void,       "Void",      new[] { "пустотный", "пустотная", "пустотное", "пустотные" },         "ED7FCD", 2.2f,   0.8f,       4,       0f,
                            randomlyRolled: false, curseCount: 1, curseMultiplier: 2.5f,
                            scalesWithMonsterLevel: true, forcedCurse: StatType.RegenPercent, perkCount: 1),
        };

        public static int Count => all.Length;

        /// <summary>
        /// Размер таблицы весов: от Common до последнего качества, которое участвует в рандоме (Legendary).
        /// Внутри могут быть дыры — качества с RandomlyRolled = false (вырезанный Epic), у них вес всегда 0.
        /// Особые (лунное, пустотное) стоят после — в таблицу не входят.
        /// </summary>
        public static int RandomlyRolledCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < all.Length; i++)
                    if (all[i].RandomlyRolled) n = i + 1;
                return n;
            }
        }

        public static QualityInfo Get(GearQuality quality) => all[(int)quality];

        public static int MaxAffixCount
        {
            get
            {
                int max = 0;
                foreach (var q in all) max = Math.Max(max, q.AffixCount);
                return max;
            }
        }

        /// <summary>Веса по умолчанию только для обычного рандома (без особых качеств).</summary>
        public static float[] DefaultWeights()
        {
            var weights = new float[RandomlyRolledCount];
            for (int i = 0; i < weights.Length; i++) weights[i] = all[i].RandomlyRolled ? all[i].DefaultWeight : 0f;
            return weights;
        }

        public static void Validate()
        {
            if (all.Length != Enum.GetValues(typeof(GearQuality)).Length)
                throw new InvalidOperationException("Qualities: entry count does not match enum GearQuality");
            for (int i = 0; i < all.Length; i++)
                if ((int)all[i].Quality != i)
                    throw new InvalidOperationException($"Qualities: entry {i} is {all[i].Quality}, order is broken");
            if (all[(int)GearQuality.Lunar].RandomlyRolled || all[(int)GearQuality.Void].RandomlyRolled)
                throw new InvalidOperationException("Qualities: Lunar and Void must not be randomly rolled");
            foreach (var q in all)
                if (q.PerkCount > Perks.Count)
                    throw new InvalidOperationException($"Qualities: {q.Quality} wants {q.PerkCount} perks, only {Perks.Count} exist");
            foreach (var q in all)
                if (q.ForcedCurse.HasValue && q.CurseCount < 1)
                    throw new InvalidOperationException($"Qualities: {q.Quality} has a forced curse but CurseCount < 1");
        }
    }
}
