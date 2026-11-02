using System.Collections.Generic;
using BepInEx.Configuration;
using Diabloid.Loot;
using UnityEngine;

namespace Diabloid.Config
{
    /// <summary>
    /// Всё, что нужно меню Risk of Options, чтобы нарисовать настройку: сама запись конфига,
    /// названия и описания на двух языках, диапазон слайдера.
    /// </summary>
    internal sealed class OptionInfo
    {
        public ConfigEntryBase Entry;
        public Words Name;
        public Words Description;
        public float Min;
        public float Max;
        public string Format;
        public bool RestartRequired;
    }

    /// <summary>
    /// Все настройки мода. BepInEx сам создаст файл BepInEx/config/FToPay.Diabloid.cfg после первого запуска.
    /// В самом .cfg описания на английском (так принято), а в игровом меню Risk of Options —
    /// на языке игры (см. Integration/RiskOfOptionsCompat.cs).
    /// Значения читаются в момент использования, поэтому почти всё меняется прямо в забеге.
    /// </summary>
    internal static partial class DiabloidConfig
    {
        // Секции .cfg = категории в меню. Имена секций не переводим: это ключи в файле.
        public const string SectionDrops = "Drops";
        public const string SectionBalance = "Balance";
        public const string SectionVisuals = "Visuals";
        public const string SectionUI = "UI";
        public const string SectionDebug = "Debug";

        // --- Дроп ---
        public static ConfigEntry<float> NormalDropChance;
        public static ConfigEntry<float> EliteDropChance;
        public static ConfigEntry<float> ChampionDropChance;
        public static ConfigEntry<float> EliteLuck;
        public static ConfigEntry<float> ChampionLuck;
        public static ConfigEntry<float>[] QualityWeights;
        public static ConfigEntry<float> LunarMonsterLunarChance;
        public static ConfigEntry<float> VoidMonsterVoidChance;
        public static ConfigEntry<float> BookDropChance;

        // --- Баланс статов ---
        public static ConfigEntry<float> StatMultiplier;
        public static ConfigEntry<float> LevelScalingMultiplier;
        public static ConfigEntry<float> ImplicitMultiplier;
        public static ConfigEntry<float> DuplicateStatChance;
        public static ConfigEntry<float> CooldownReductionCap;
        public static ConfigEntry<float> RadianceEnemyStats; // 0.19.0
        public static ConfigEntry<float> RadianceSpawnRate;
        public static ConfigEntry<float> RecycleXp;            // 0.20.0
        public static ConfigEntry<float> RecycleForgottenShare;
        public static ConfigEntry<KeyboardShortcut> BurnKey;

        // --- Внешний вид ---
        public static ConfigEntry<string>[] SlotModels;

        /// <summary>Запасные модельки (базовая игра), если предмета из настройки нет — например, без DLC.</summary>
        public static readonly string[] FallbackModels = { "Crowbar", "GhostOnKill", "ArmorPlate", "FireRing", "SprintOutOfCombat", "Hoof" };

        // --- Интерфейс ---
        public static ConfigEntry<KeyboardShortcut> PanelKey;
        public static ConfigEntry<bool> AnnounceInChat;
        public static ConfigEntry<float> PanelScale;
        public static ConfigEntry<bool> ShowPerkAura;

        // --- Отладка ---
        public static ConfigEntry<bool> DebugMenuEnabled;
        public static ConfigEntry<KeyboardShortcut> DebugKey;

        private static readonly List<OptionInfo> options = new List<OptionInfo>();

        /// <summary>Все настройки в порядке объявления — для меню Risk of Options.</summary>
        public static IReadOnlyList<OptionInfo> Options => options;

        public static void Bind(ConfigFile cfg)
        {
            options.Clear();

            NormalDropChance = Bind(cfg, SectionDrops, "NormalDropChance", 4f,
                new Words("Normal enemy drop chance", "Шанс дропа с обычных врагов"),
                new Words("Chance (in %) that a regular enemy drops a piece of gear.",
                          "Шанс (в %), что обычный враг уронит шмотку."),
                0f, 100f, "{0:0.#}%");

            EliteDropChance = Bind(cfg, SectionDrops, "EliteDropChance", 20f,
                new Words("Elite drop chance", "Шанс дропа с элиток"),
                new Words("Chance (in %) that an elite enemy drops a piece of gear.",
                          "Шанс (в %), что элитный враг уронит шмотку."),
                0f, 100f, "{0:0.#}%");

            ChampionDropChance = Bind(cfg, SectionDrops, "ChampionDropChance", 100f,
                new Words("Boss drop chance", "Шанс дропа с боссов"),
                new Words("Chance (in %) that a boss or champion drops a piece of gear. Their minimum quality is Rare.",
                          "Шанс (в %), что босс или чемпион уронит шмотку. Минимальное качество у них — редкое."),
                0f, 100f, "{0:0.#}%");

            EliteLuck = Bind(cfg, SectionDrops, "EliteLuck", 1f,
                new Words("Elite luck", "Удача элиток"),
                new Words("Shifts quality up for elite drops: the weight of quality #i is multiplied by (1 + luck)^i. 0 = same as regular enemies.",
                          "Сдвигает качество вверх для дропа с элиток: вес i-го качества умножается на (1 + удача)^i. 0 — как у обычных врагов."),
                0f, 5f, "{0:0.##}");

            ChampionLuck = Bind(cfg, SectionDrops, "ChampionLuck", 1f,
                new Words("Boss luck", "Удача боссов"),
                new Words("Same as elite luck, but for bosses.",
                          "То же самое, но для боссов."),
                0f, 5f, "{0:0.##}");

            // Веса только для "обычных" качеств: лунное и пустотное рандом не выбирает.
            // Вырезанный эпик тоже без веса (null): старая строка WeightEpic в .cfg просто игнорируется.
            var defaults = Qualities.DefaultWeights();
            QualityWeights = new ConfigEntry<float>[defaults.Length];
            for (int i = 0; i < defaults.Length; i++)
            {
                var quality = (GearQuality)i;
                var info = Qualities.Get(quality);
                if (!info.RandomlyRolled) continue;
                QualityWeights[i] = Bind(cfg, SectionDrops, $"Weight{quality}", defaults[i],
                    new Words($"{info.DisplayName(Lang.English)} weight", $"Вес: {info.DisplayName(Lang.Russian).ToLowerInvariant()}"),
                    new Words($"Relative weight of the {info.DisplayName(Lang.English)} quality. Only proportions matter, the sum doesn't have to be 100.",
                              $"Относительный вес качества «{info.DisplayName(Lang.Russian).ToLowerInvariant()}». Важны только пропорции, сумма не обязана быть 100."),
                    0f, 100f, "{0:0.#}");
            }

            LunarMonsterLunarChance = Bind(cfg, SectionDrops, "LunarMonsterLunarChance", 25f,
                new Words("Lunar gear from lunar monsters", "Лунный шмот с лунных мобов"),
                new Words("When a lunar monster (lunar team or Perfected elite) drops gear, chance (in %) that it is Lunar: 3 huge bonuses and 2 harsh curses.",
                          "Когда лунный моб (лунная команда или Совершенная элитка) роняет шмотку — шанс (в %), что она лунная: 3 жирных бонуса и 2 злых проклятия."),
                0f, 100f, "{0:0.#}%");

            VoidMonsterVoidChance = Bind(cfg, SectionDrops, "VoidMonsterVoidChance", 25f,
                new Words("Void gear from void monsters", "Пустотный шмот с пустотных мобов"),
                new Words("When a void monster drops gear, chance (in %) that it is Void: strong, its level always equals the current monster level, but it always cuts regen.",
                          "Когда пустотный моб роняет шмотку — шанс (в %), что она пустотная: сильная, уровень всегда равен текущему уровню монстров, но всегда режет регенерацию."),
                0f, 100f, "{0:0.#}%");

            BookDropChance = Bind(cfg, SectionDrops, "BookDropChance", 5f,
                new Words("Books from monsters", "Книги с мобов"),
                new Words("When a monster drops gear, chance (in %) that it drops a book (active equipment) instead.",
                          "Когда моб роняет шмотку — шанс (в %), что вместо неё упадёт книга (активное снаряжение)."),
                0f, 100f, "{0:0.#}%");

            StatMultiplier = Bind(cfg, SectionBalance, "StatMultiplier", 1f,
                new Words("Stat multiplier", "Множитель статов"),
                new Words("Global multiplier for all stats on newly rolled gear.",
                          "Общий множитель всех статов на новых шмотках."),
                0.1f, 10f, "x{0:0.##}");

            LevelScalingMultiplier = Bind(cfg, SectionBalance, "LevelScalingMultiplier", 1f,
                new Words("Level scaling", "Рост от уровня"),
                new Words("How strongly stats grow with item level. 0 = item level doesn't matter.",
                          "Насколько сильно статы растут с уровнем предмета. 0 — уровень не влияет."),
                0f, 5f, "x{0:0.##}");

            ImplicitMultiplier = Bind(cfg, SectionBalance, "ImplicitMultiplier", 1.5f,
                new Words("Main stat multiplier", "Множитель главного стата"),
                new Words("How much stronger the slot's main stat is compared to a regular affix.",
                          "Во сколько раз главный стат слота жирнее обычного аффикса."),
                0.5f, 5f, "x{0:0.##}");

            DuplicateStatChance = Bind(cfg, SectionBalance, "DuplicateStatChance", 25f,
                new Words("Duplicate stat chance", "Шанс повтора стата"),
                new Words("Chance (in %) that each extra affix repeats a stat the item already has, the main stat included (\"damage three times\"). " +
                          "0 = all stats on an item are different. Curses never repeat.",
                          "Шанс (в %), что каждый дополнительный аффикс повторит стат, который на шмотке уже есть, включая главный («урон трижды»). " +
                          "0 — все статы на шмотке разные. Проклятия не повторяются никогда."),
                0f, 100f, "{0:0}%");

            CooldownReductionCap = Bind(cfg, SectionBalance, "CooldownReductionCap", 50f,
                new Words("Cooldown reduction cap", "Потолок сокращения перезарядки"),
                new Words("Maximum total cooldown reduction from gear, in %.",
                          "Максимальное суммарное сокращение перезарядки со шмоток, в %."),
                0f, 90f, "{0:0}%");

            // 0.19.0 — сияние (Gameplay/Radiance.cs).
            // 0.20.1: 2 → 4% (решение пользователя). Новый ключ — BepInEx не обновляет сохранённое значение при смене дефолта.
            RadianceEnemyStats = Bind(cfg, SectionBalance, "RadianceEnemyStatsV2", 4f,
                new Words("Radiance: enemy stats", "Сияние: статы врагов"),
                new Words("Every piece of gear gives Radiance by quality (common 1, uncommon 2, rare 3, legendary 5, void 4, lunar 0; book 1, scroll 2). " +
                          "Each level of Radiance (averaged over players) gives enemies this many % health, damage, armor and regen. 0 = off.",
                          "Каждая шмотка даёт сияние по качеству (обычная 1, необычная 2, редкая 3, легендарка 5, пустотная 4, лунная 0; книга 1, свиток 2). " +
                          "Каждый уровень сияния (в среднем по игрокам) даёт врагам столько % здоровья, урона, брони и регена. 0 — выключено."),
                0f, 10f, "{0:0.#}%");

            RadianceSpawnRate = Bind(cfg, SectionBalance, "RadianceSpawnRate", 3f,
                new Words("Radiance: spawn rate", "Сияние: скорость спавна"),
                new Words("Each level of Radiance (averaged over players) speeds up enemy spawning by this many % (director credits). 0 = off.",
                          "Каждый уровень сияния (в среднем по игрокам) ускоряет спавн врагов на столько % (кредиты директора). 0 — выключено."),
                0f, 10f, "{0:0.#}%");

            // 0.20.0 — переработка шмота в опыт (Gameplay/GearRecycling.cs).
            RecycleXp = Bind(cfg, SectionBalance, "RecycleXp", 1f,
                new Words("Recycling: XP multiplier", "Переработка: множитель опыта"),
                new Words("Burning gear gives XP like a regular monster of the gear's level, times quality " +
                          "(common 1, uncommon 2, rare 4, legendary 10, lunar/void 6; book 3, scroll 4), times this multiplier.",
                          "Сожжённая шмотка даёт опыт как обычный моб её уровня, умноженный на качество " +
                          "(обычная 1, необычная 2, редкая 4, легендарка 10, лунная/пустотная 6; книга 3, свиток 4), и на этот множитель."),
                0f, 10f, "x{0:0.##}");

            RecycleForgottenShare = Bind(cfg, SectionBalance, "RecycleForgottenShare", 50f,
                new Words("Recycling: forgotten gear", "Переработка: забытый шмот"),
                new Words("Gear and books left on the ground when you leave a stage are recycled for this share (in %) of the burn XP, given at the start of the next stage.",
                          "Шмот и книги, оставленные на земле при уходе с этапа, перерабатываются за эту долю (в %) опыта от сжигания — приходит в начале следующего этапа."),
                0f, 100f, "{0:0}%");

            // Модельки берём у ванильных предметов по их внутреннему имени (ItemCatalog).
            // Применяются один раз при загрузке игры — отсюда restartRequired.
            // 0.16.0: оружие — Tri-Tip Dagger (BleedOnHit), ботинки — Hiker's Boots (CritAtLowerElevation, Alloyed Collective).
            string[] defaultModels = { "BleedOnHit", "GhostOnKill", "ArmorPlate", "FireRing", "SprintOutOfCombat", "CritAtLowerElevation" };
            SlotModels = new ConfigEntry<string>[Slots.Count];
            for (int i = 0; i < Slots.Count; i++)
            {
                var slot = (GearSlot)i;
                var name = Slots.Get(slot).DisplayName;
                SlotModels[i] = Bind(cfg, SectionVisuals, $"{slot}Model", defaultModels[i],
                    new Words($"{name.En} model", $"Модель: {name.Ru.ToLowerInvariant()}"),
                    new Words($"Internal name of the vanilla item whose model is used for {name.En}. Unknown name = '?' model.",
                              $"Внутреннее имя ванильного предмета, чья моделька используется для слота «{name.Ru}». Неизвестное имя — модель '?'."),
                    restartRequired: true);
            }

            PanelKey = Bind(cfg, SectionUI, "PanelKey", new KeyboardShortcut(KeyCode.F2),
                new Words("Equipment panel key", "Клавиша панели экипировки"),
                new Words("Key that toggles the equipment panel.",
                          "Клавиша, открывающая панель экипировки."));

            BurnKey = Bind(cfg, SectionUI, "BurnKey", new KeyboardShortcut(KeyCode.X),
                new Words("Burn gear key", "Клавиша сжигания шмота"),
                new Words("Look at gear or a book on the ground (the one highlighted for pick up) and press this key to burn it for XP.",
                          "Смотришь на шмотку или книгу на земле (ту, что подсвечена для подбора) и жмёшь эту клавишу — она сгорает в опыт."));

            PanelScale = Bind(cfg, SectionUI, "PanelScale", 1f,
                new Words("Equipment panel scale", "Масштаб панели экипировки"),
                new Words("Text size of the equipment panel. It also follows the screen resolution (1 = 14 px at 1080p).",
                          "Размер текста панели экипировки. Сам подстраивается под разрешение экрана (1 = 14 px при 1080p)."),
                0.5f, 2f, "x{0:0.##}");

            AnnounceInChat = Bind(cfg, SectionUI, "AnnounceInChat", true,
                new Words("Announce in chat", "Сообщать в чат"),
                new Words("Write to chat who equipped what.",
                          "Писать в чат, кто что надел."));

            ShowPerkAura = Bind(cfg, SectionUI, "ShowPerkAura", true,
                new Words("Show damage aura", "Показывать ауру урона"),
                new Words("Draw a sphere around players with the damage aura perk (only on your screen).",
                          "Рисовать сферу вокруг игроков с перком «аура урона» (только у тебя на экране)."));

            // Элитки и артефакт — в соседнем файле DiabloidConfig.Elites.cs.
            BindElites(cfg);

            DebugMenuEnabled = Bind(cfg, SectionDebug, "DebugMenu", false,
                new Words("Debug menu", "Отладочное меню"),
                new Words("Allow the debug menu: spawn gear of any slot and quality and any book (host only).",
                          "Разрешить отладочное меню: создать шмотку любого слота и качества и любую книгу (только хост)."));

            DebugKey = Bind(cfg, SectionDebug, "DebugKey", new KeyboardShortcut(KeyCode.F3),
                new Words("Debug menu key", "Клавиша отладочного меню"),
                new Words("Key that toggles the debug menu (works only if the debug menu is allowed).",
                          "Клавиша, открывающая отладочное меню (работает, только если меню разрешено)."));
        }

        /// <summary>Обёртка над cfg.Bind: заодно запоминает всё, что нужно для игрового меню.</summary>
        private static ConfigEntry<T> Bind<T>(ConfigFile cfg, string section, string key, T defaultValue,
                                              Words name, Words description,
                                              float min = 0f, float max = 100f, string format = "{0:0.##}",
                                              bool restartRequired = false)
        {
            var entry = cfg.Bind(section, key, defaultValue, description.En);
            options.Add(new OptionInfo
            {
                Entry = entry,
                Name = name,
                Description = description,
                Min = min,
                Max = max,
                Format = format,
                RestartRequired = restartRequired,
            });
            return entry;
        }

        /// <summary>Снимок текущих настроек для генератора (он не знает про BepInEx).</summary>
        public static LootSettings ToLootSettings()
        {
            var weights = new float[QualityWeights.Length];
            for (int i = 0; i < weights.Length; i++) weights[i] = QualityWeights[i] != null ? QualityWeights[i].Value : 0f;

            return new LootSettings
            {
                StatMultiplier = StatMultiplier.Value,
                LevelScalingMultiplier = LevelScalingMultiplier.Value,
                ImplicitMultiplier = ImplicitMultiplier.Value,
                DuplicateStatChance = DuplicateStatChance.Value / 100f,
                QualityWeights = weights,
            };
        }
    }
}
