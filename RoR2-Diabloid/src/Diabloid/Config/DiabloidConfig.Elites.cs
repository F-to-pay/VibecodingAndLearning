using BepInEx.Configuration;
using Diabloid.Elites;
using Diabloid.Loot;

namespace Diabloid.Config
{
    /// <summary>Общие настройки любой нашей элитки: тир спавна и бусты хп/урона.</summary>
    internal sealed class EliteSettings
    {
        public ConfigEntry<EliteTierChoice> Tier;
        public ConfigEntry<float> HealthMultiplier;
        public ConfigEntry<float> DamageMultiplier;
    }

    internal static partial class DiabloidConfig
    {
        public const string SectionTitan = "EliteTitan";
        public const string SectionBloodsucker = "EliteBloodsucker";
        public const string SectionClockmaker = "EliteClockmaker";
        // Новая секция (до 0.5.0 была EliteGlass): у силы другие дефолты, старые цифры стекла не должны подхватиться.
        public const string SectionPower = "ElitePower";
        public const string SectionArtifacts = "Artifacts";

        public static EliteSettings Titan;
        public static ConfigEntry<int> TitanCopyCount;
        public static ConfigEntry<float> TitanSizeMultiplier;
        public static ConfigEntry<bool> TitanCopiesGiveRewards;

        public static EliteSettings Bloodsucker;
        public static ConfigEntry<float> BloodsuckerRadius;
        public static ConfigEntry<float> BloodsuckerDrainPercent;
        public static ConfigEntry<float> BloodsuckerLifestealPercent;
        public static ConfigEntry<float> BloodsuckerTickInterval;
        public static ConfigEntry<bool> BloodsuckerDrainsMonsters;
        public static ConfigEntry<float> BloodsuckerUnhealablePercent;

        public static EliteSettings Clockmaker;
        public static ConfigEntry<float> ClockmakerRadius;
        public static ConfigEntry<float> ClockmakerMoveSlowPercent;
        public static ConfigEntry<float> ClockmakerAttackSlowPercent;
        public static ConfigEntry<bool> ClockmakerShowAura;

        public static EliteSettings Power;

        public static ConfigEntry<bool> ElitismTier2AfterLoop;

        public static ConfigEntry<float> GearArtifactDifficultyScale;
        public static ConfigEntry<bool> GearArtifactRemoveItemStations;
        public static ConfigEntry<float> GearArtifactWhiteUncommonChance;

        public static EliteSettings Get(EliteKind kind)
        {
            switch (kind)
            {
                case EliteKind.Titan: return Titan;
                case EliteKind.Bloodsucker: return Bloodsucker;
                case EliteKind.Clockmaker: return Clockmaker;
                default: return Power;
            }
        }

        private static void BindElites(ConfigFile cfg)
        {
            // --- Титан ---
            Titan = BindEliteCommon(cfg, SectionTitan, new Words("Titan", "Титан"), EliteTierChoice.Tier1Late, 6f, 1f);

            TitanCopyCount = Bind(cfg, SectionTitan, "CopyCount", 4,
                new Words("Copies", "Копии"),
                new Words("How many copies a Titan brings along. Copies are Titans too, but don't summon more.",
                          "Сколько копий приводит с собой титан. Копии — тоже титаны, но сами больше не призывают."),
                0f, 10f);

            TitanSizeMultiplier = Bind(cfg, SectionTitan, "SizeMultiplier", 2f,
                new Words("Size", "Размер"),
                new Words("How much bigger Titans are (hitboxes grow too).",
                          "Во сколько раз титаны больше (хитбоксы тоже растут)."),
                1f, 4f, "x{0:0.##}");

            // Ключ новый (был CopiesGiveRewards = true): дефолт сменился, старое значение не должно подхватиться.
            TitanCopiesGiveRewards = Bind(cfg, SectionTitan, "CopiesGiveGoldAndXp", false,
                new Words("Copies give gold and XP", "Копии дают золото и опыт"),
                new Words("Copies give the same gold and experience as the original Titan. Gear never drops from copies.",
                          "Копии дают столько же золота и опыта, сколько оригинал. Шмотки с копий не падают никогда."));

            // --- Кровопийца ---
            Bloodsucker = BindEliteCommon(cfg, SectionBloodsucker, new Words("Bloodsucker", "Кровопийца"), EliteTierChoice.Tier1, 3f, 1.5f);

            BloodsuckerRadius = Bind(cfg, SectionBloodsucker, "Radius", 30f,
                new Words("Drain radius", "Радиус высасывания"),
                new Words("Radius (in meters) in which a wounded Bloodsucker drains health.",
                          "Радиус (в метрах), в котором раненый кровопийца сосёт здоровье."),
                5f, 100f, "{0:0}m");

            BloodsuckerDrainPercent = Bind(cfg, SectionBloodsucker, "DrainPercentOfDamage", 100f,
                new Words("Drain per second", "Высасывание в секунду"),
                new Words("Damage per second to EACH target, in % of the Bloodsucker's base damage.",
                          "Урон в секунду по КАЖДОЙ цели, в % от базового урона кровопийцы."),
                0f, 500f, "{0:0}%");

            BloodsuckerLifestealPercent = Bind(cfg, SectionBloodsucker, "LifestealPercent", 100f,
                new Words("Lifesteal", "Вампиризм"),
                new Words("Which part of the drained health the Bloodsucker gets back, in %.",
                          "Какую часть высосанного здоровья кровопийца получает себе, в %."),
                0f, 300f, "{0:0}%");

            BloodsuckerTickInterval = Bind(cfg, SectionBloodsucker, "TickInterval", 0.5f,
                new Words("Drain tick interval", "Интервал высасывания"),
                new Words("How often the drain ticks, in seconds. Damage per second doesn't change.",
                          "Как часто тикает высасывание, в секундах. Урон в секунду от этого не меняется."),
                0.1f, 2f, "{0:0.0}s");

            BloodsuckerDrainsMonsters = Bind(cfg, SectionBloodsucker, "DrainsOtherMonsters", true,
                new Words("Drains other monsters too", "Сосёт и других монстров"),
                new Words("If on, drains everyone around (except other Bloodsuckers), not only players and their allies.",
                          "Если включено — сосёт всех вокруг (кроме других кровопийц), а не только игроков и их союзников."));

            BloodsuckerUnhealablePercent = Bind(cfg, SectionBloodsucker, "UnhealablePercent", 10f,
                new Words("Unhealable damage", "Неисцеляемый урон"),
                new Words("Which part of the damage a Bloodsucker TAKES can never be healed back (like Mithrix), in %. Lowers its max health, so lifesteal can't undo your damage.",
                          "Какая часть урона, который ПОЛУЧАЕТ кровопийца, не лечится никогда (как у Митрикса), в %. Срезает ему максимум здоровья, чтобы вампиризм не отменял твой урон."),
                0f, 100f, "{0:0}%");

            // --- Часовщик ---
            Clockmaker = BindEliteCommon(cfg, SectionClockmaker, new Words("Clockmaker", "Часовщик"), EliteTierChoice.Tier1, 3f, 1.5f);

            ClockmakerRadius = Bind(cfg, SectionClockmaker, "Radius", 30f,
                new Words("Slow radius", "Радиус замедления"),
                new Words("Radius (in meters) in which the Clockmaker slows its enemies.",
                          "Радиус (в метрах), в котором часовщик замедляет своих врагов."),
                5f, 100f, "{0:0}m");

            // Ключи новые (были MoveSlowPercent/AttackSlowPercent по 50): дефолты сменились на 70/30.
            ClockmakerMoveSlowPercent = Bind(cfg, SectionClockmaker, "MoveSlow", 70f,
                new Words("Movement slow", "Замедление бега"),
                new Words("How much slower enemies move inside the aura, in %.",
                          "Насколько медленнее бегают враги внутри ауры, в %."),
                0f, 90f, "{0:0}%");

            ClockmakerAttackSlowPercent = Bind(cfg, SectionClockmaker, "AttackSlow", 30f,
                new Words("Attack slow", "Замедление атаки"),
                new Words("How much slower enemies attack inside the aura, in %.",
                          "Насколько медленнее атакуют враги внутри ауры, в %."),
                0f, 90f, "{0:0}%");

            ClockmakerShowAura = Bind(cfg, SectionClockmaker, "ShowAura", true,
                new Words("Show aura", "Показывать ауру"),
                new Words("Draw the aura sphere around Clockmakers (only on your screen).",
                          "Рисовать сферу ауры вокруг часовщиков (только у тебя на экране)."));

            // --- Сила (до 0.5.0 — стекло) ---
            // Здоровье меньше 1 по-прежнему разрешено: 0.1 вернёт старое стекло.
            Power = BindEliteCommon(cfg, SectionPower, new Words("Power", "Сила"), EliteTierChoice.Tier1, 1f, 5f, healthMin: 0.01f);

            // --- Артефакт ---
            ElitismTier2AfterLoop = Bind(cfg, SectionArtifacts, "ElitismTier2AfterLoop", true,
                new Words("Elitism: Tier 2 after loop", "Элитаризм: 2-й тир после лупа"),
                new Words("Artifact of Elitism: after looping, bosses can also become Tier 2 elites (Malachite, Celestine...).",
                          "Артефакт элитаризма: после лупа боссы могут становиться и элитками 2-го тира (Малахитовые, Небесные...)."));

            GearArtifactDifficultyScale = Bind(cfg, SectionArtifacts, "GearDifficultyScale", 0.5f,
                new Words("Gear: difficulty growth", "Снаряжение: рост сложности"),
                new Words("Artifact of Gear: how fast difficulty grows compared to normal (monster level, spawns, prices, gold). 0.5 = twice as slow.",
                          "Артефакт снаряжения: насколько быстро растёт сложность относительно обычного (уровень монстров, спавн, цены, золото). 0.5 — вдвое медленнее."),
                0.1f, 1f, "x{0:0.##}");

            GearArtifactRemoveItemStations = Bind(cfg, SectionArtifacts, "GearRemoveItemStations", true,
                new Words("Gear: remove printers and scrappers", "Снаряжение: убрать принтеры и утилизаторы"),
                new Words("Artifact of Gear: printers, scrappers and cauldrons don't appear (there are no items to feed them anyway). Applies from the next stage.",
                          "Артефакт снаряжения: принтеры, утилизаторы и котлы не появляются (кормить их всё равно нечем). Действует со следующего этапа."));

            GearArtifactWhiteUncommonChance = Bind(cfg, SectionArtifacts, "GearWhiteUncommonChance", 30f,
                new Words("Gear: white → Uncommon chance", "Снаряжение: белый → необычное"),
                new Words("Artifact of Gear: a source of white items (small chest...) gives Common gear, with this chance (in %) — Uncommon.",
                          "Артефакт снаряжения: источник белых предметов (малый сундук...) даёт обычную шмотку, с этим шансом (в %) — необычную."),
                0f, 100f, "{0:0}%");
        }

        private static EliteSettings BindEliteCommon(ConfigFile cfg, string section, Words name,
                                                     EliteTierChoice defaultTier, float defaultHealth, float defaultDamage,
                                                     float healthMin = 1f)
        {
            return new EliteSettings
            {
                Tier = Bind(cfg, section, "Tier", defaultTier,
                    new Words($"{name.En}: spawn tier", $"{name.Ru}: тир спавна"),
                    new Words("When this elite can appear. Tier1 = from stage 1 (like Blazing), Tier1Late = from stage 3 (like Gilded), " +
                              "Tier2 = after looping (like Malachite), Disabled = never. Requires restart.",
                              "Когда может появляться эта элитка. Tier1 — с 1-го этапа (как Пылающие), Tier1Late — с 3-го (как Позолоченные), " +
                              "Tier2 — после лупа (как Малахитовые), Disabled — никогда. Нужен перезапуск."),
                    restartRequired: true),

                HealthMultiplier = Bind(cfg, section, "HealthMultiplier", defaultHealth,
                    new Words("Health multiplier", "Множитель здоровья"),
                    new Words("Health of this elite compared to the regular monster.",
                              "Здоровье этой элитки относительно обычного монстра."),
                    healthMin, 30f, "x{0:0.##}"),

                DamageMultiplier = Bind(cfg, section, "DamageMultiplier", defaultDamage,
                    new Words("Damage multiplier", "Множитель урона"),
                    new Words("Damage of this elite compared to the regular monster.",
                              "Урон этой элитки относительно обычного монстра."),
                    1f, 10f, "x{0:0.#}"),
            };
        }
    }
}
