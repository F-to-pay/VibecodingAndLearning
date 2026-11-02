using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using Destroyer.Config;
using Destroyer.Content;
using Destroyer.Core;
using RiskOfOptions;
using RiskOfOptions.OptionConfigs;
using RiskOfOptions.Options;

namespace Destroyer.Integration
{
    /// <summary>
    /// Настройки мода в игровом меню (Settings → Mod Options) через мод Risk of Options.
    ///
    /// Зависимость МЯГКАЯ: нет Risk of Options — мод работает, просто без меню (конфиг правится в r2modman).
    /// Ни один тип из RiskOfOptions.dll не должен всплыть в коде, который выполняется без него:
    /// всё спрятано в этот класс, а Init помечен NoInlining — JIT грузит RiskOfOptions.dll, только когда его зовут.
    /// (Схема из Diabloid, проверена в игре.)
    /// </summary>
    internal static class RiskOfOptionsCompat
    {
        public const string PluginGuid = "com.rune580.riskofoptions";

        public static bool IsInstalled => Chainloader.PluginInfos.ContainsKey(PluginGuid);

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Init()
        {
            const string guid = DestroyerPlugin.PluginGUID;
            const string name = DestroyerPlugin.PluginName;

            GameLanguage.SetText("DESTROYER_OPTIONS_DESCRIPTION", new Words(
                "New items for Risk of Rain 2. Every item has its own section: numbers, and a switch to turn it off.",
                "Новые предметы для Risk of Rain 2. У каждого предмета своя секция: цифры и выключатель."));
            ModSettingsManager.SetModDescriptionToken("DESTROYER_OPTIONS_DESCRIPTION", guid, name);

            var namedCategories = new HashSet<string>();

            foreach (var info in DestroyerConfig.Options)
            {
                var option = CreateOption(info);
                if (option == null)
                {
                    DestroyerPlugin.Log.LogWarning($"Risk of Options: no widget for setting type {info.Entry.SettingType.Name} ({info.Entry.Definition.Key})");
                    continue;
                }

                // Свои токены вместо автоматических — так название и описание переводятся вместе с игрой.
                string prefix = Token($"DESTROYER_OPTION_{info.Entry.Definition.Section}_{info.Entry.Definition.Key}");
                GameLanguage.SetText(prefix + "_NAME", info.Name);
                GameLanguage.SetText(prefix + "_DESC", info.Description);
                ModSettingsManager.AddOption(option, guid, name, prefix + "_NAME", prefix + "_DESC");

                // Категорию можно переименовать, только когда в ней уже есть хотя бы одна настройка.
                string section = info.Entry.Definition.Section;
                if (namedCategories.Add(section) && !info.Category.IsEmpty)
                {
                    string categoryToken = Token("DESTROYER_OPTION_CATEGORY_" + section);
                    GameLanguage.SetText(categoryToken, info.Category);
                    ModSettingsManager.SetCategoryNameToken(guid, option, categoryToken);
                }
            }

            DestroyerPlugin.Log.LogInfo($"Risk of Options: registered {DestroyerConfig.Options.Count} settings.");
        }

        /// <summary>Токен из имени секции: "Item: BLAST_CORE" → "..._ITEM__BLAST_CORE" (только буквы, цифры, _).</summary>
        private static string Token(string raw)
        {
            var chars = raw.ToUpperInvariant().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (!char.IsLetterOrDigit(chars[i])) chars[i] = '_';
            return new string(chars);
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
                    if (info.Entry.SettingType.IsEnum)
                        return new ChoiceOption(info.Entry, new ChoiceConfig { restartRequired = info.RestartRequired });
                    return null;
            }
        }
    }
}
