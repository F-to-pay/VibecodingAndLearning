using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using Diabloid.Config;
using Diabloid.Content;
using Diabloid.Loot;
using RiskOfOptions;
using RiskOfOptions.OptionConfigs;
using RiskOfOptions.Options;

namespace Diabloid.Integration
{
    /// <summary>
    /// Настройки мода в игровом меню (Settings → Mod Options) через мод Risk of Options.
    ///
    /// Зависимость МЯГКАЯ: нет Risk of Options — мод работает, просто без меню (конфиг правится в r2modman).
    /// Фокус в том, чтобы ни один тип из RiskOfOptions.dll не всплыл в коде, который выполняется без него:
    /// поэтому всё обращение к нему спрятано в этот класс, а метод Init помечен NoInlining —
    /// JIT подгружает RiskOfOptions.dll только когда метод реально вызывают.
    /// </summary>
    internal static class RiskOfOptionsCompat
    {
        public const string PluginGuid = "com.rune580.riskofoptions";

        public static bool IsInstalled => Chainloader.PluginInfos.ContainsKey(PluginGuid);

        // Названия категорий (= секций конфига) на двух языках.
        private static readonly Dictionary<string, Words> categoryNames = new Dictionary<string, Words>
        {
            { DiabloidConfig.SectionDrops,   new Words("Drops", "Дроп") },
            { DiabloidConfig.SectionBalance, new Words("Balance", "Баланс") },
            { DiabloidConfig.SectionVisuals, new Words("Visuals", "Внешний вид") },
            { DiabloidConfig.SectionUI,      new Words("Interface", "Интерфейс") },
            { DiabloidConfig.SectionTitan,       new Words("Elite: Titan", "Элитка: Титан") },
            { DiabloidConfig.SectionBloodsucker, new Words("Elite: Bloodsucker", "Элитка: Кровопийца") },
            { DiabloidConfig.SectionClockmaker,  new Words("Elite: Clockmaker", "Элитка: Часовщик") },
            { DiabloidConfig.SectionPower,       new Words("Elite: Power", "Элитка: Сила") },
            { DiabloidConfig.SectionArtifacts,   new Words("Artifacts", "Артефакты") },
        };

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Init()
        {
            const string guid = DiabloidPlugin.PluginGUID;
            const string name = DiabloidPlugin.PluginName;

            // Описание мода в меню.
            GameLanguage.AddToken("DIABLOID_OPTIONS_DESCRIPTION", new Words(
                "Diablo-like loot: enemies drop gear for 6 slots with procedurally rolled stats and 7 qualities (incl. Lunar and Void). " +
                "Press E on gear to equip it, F2 opens the equipment panel. Also: 4 new elites, Artifacts of Elitism and Gear.",
                "Диаблоидный лут: с врагов падают шмотки для 6 слотов с процедурными статами и 7 качествами (включая лунное и пустотное). " +
                "E на шмотке — надеть, F2 — панель экипировки. А ещё 4 новые элитки и артефакты элитаризма и снаряжения."));
            ModSettingsManager.SetModDescriptionToken("DIABLOID_OPTIONS_DESCRIPTION", guid, name);

            var namedCategories = new HashSet<string>();

            foreach (var info in DiabloidConfig.Options)
            {
                var option = CreateOption(info);
                if (option == null)
                {
                    DiabloidPlugin.Log.LogWarning($"Risk of Options: no widget for setting type {info.Entry.SettingType.Name} ({info.Entry.Definition.Key})");
                    continue;
                }

                // Свои токены вместо автоматических — так название и описание переводятся вместе с игрой.
                string prefix = $"DIABLOID_OPTION_{info.Entry.Definition.Section}_{info.Entry.Definition.Key}".ToUpperInvariant();
                GameLanguage.AddToken(prefix + "_NAME", info.Name);
                GameLanguage.AddToken(prefix + "_DESC", info.Description);
                ModSettingsManager.AddOption(option, guid, name, prefix + "_NAME", prefix + "_DESC");

                // Категорию можно переименовать, только когда в ней уже есть хотя бы одна настройка.
                string section = info.Entry.Definition.Section;
                if (namedCategories.Add(section) && categoryNames.TryGetValue(section, out var categoryName))
                {
                    string categoryToken = $"DIABLOID_OPTION_CATEGORY_{section}".ToUpperInvariant();
                    GameLanguage.AddToken(categoryToken, categoryName);
                    ModSettingsManager.SetCategoryNameToken(guid, option, categoryToken);
                }
            }

            DiabloidPlugin.Log.LogInfo($"Risk of Options: registered {DiabloidConfig.Options.Count} settings.");
        }

        /// <summary>Подбираем виджет по типу настройки.</summary>
        private static BaseOption CreateOption(OptionInfo info)
        {
            switch (info.Entry)
            {
                case ConfigEntry<float> floatEntry:
                    return new SliderOption(floatEntry, new SliderConfig
                    {
                        min = info.Min,
                        max = info.Max,
                        FormatString = info.Format,
                        restartRequired = info.RestartRequired,
                    });

                case ConfigEntry<int> intEntry:
                    return new IntSliderOption(intEntry, new IntSliderConfig
                    {
                        min = (int)info.Min,
                        max = (int)info.Max,
                        formatString = "{0}",
                        restartRequired = info.RestartRequired,
                    });

                case ConfigEntry<bool> boolEntry:
                    return new CheckBoxOption(boolEntry, new CheckBoxConfig { restartRequired = info.RestartRequired });

                case ConfigEntry<KeyboardShortcut> keyEntry:
                    return new KeyBindOption(keyEntry, new KeyBindConfig { restartRequired = info.RestartRequired });

                case ConfigEntry<string> stringEntry:
                    return new StringInputFieldOption(stringEntry, new InputFieldConfig
                    {
                        restartRequired = info.RestartRequired,
                        submitOn = InputFieldConfig.SubmitEnum.OnExitOrSubmit,
                    });

                default:
                    // Перечисления (тир спавна и т.п.) — выпадающий список.
                    if (info.Entry.SettingType.IsEnum)
                        return new ChoiceOption(info.Entry, new ChoiceConfig { restartRequired = info.RestartRequired });
                    return null;
            }
        }
    }
}
