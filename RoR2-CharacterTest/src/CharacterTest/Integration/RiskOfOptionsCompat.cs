using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using CharacterTest.Config;
using CharacterTest.Content;
using CharacterTest.Core;
using RiskOfOptions;
using RiskOfOptions.OptionConfigs;
using RiskOfOptions.Options;

namespace CharacterTest.Integration
{
    /// <summary>
    /// Настройки в игровом меню (Settings → Mod Options) через Risk of Options. Зависимость МЯГКАЯ:
    /// ни один тип из RiskOfOptions.dll не всплывает вне этого класса, Init помечен NoInlining
    /// (схема из Diabloid/Destroyer, проверена в игре).
    /// </summary>
    internal static class RiskOfOptionsCompat
    {
        public const string PluginGuid = "com.rune580.riskofoptions";

        public static bool IsInstalled => Chainloader.PluginInfos.ContainsKey(PluginGuid);

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Init()
        {
            const string guid = CharacterTestPlugin.PluginGUID;
            const string name = CharacterTestPlugin.PluginName;

            GameLanguage.SetText("CHARTEST_OPTIONS_DESCRIPTION", new Words(
                "Custom survivors. Numbers of skills can be tuned here; most changes apply immediately.",
                "Свои выжившие. Здесь крутятся цифры навыков; большинство правок действует сразу."));
            ModSettingsManager.SetModDescriptionToken("CHARTEST_OPTIONS_DESCRIPTION", guid, name);

            int count = 0;
            foreach (var info in CharacterTestConfig.Options)
            {
                BaseOption option = CreateOption(info);
                if (option == null)
                {
                    CharacterTestPlugin.Log.LogWarning($"Risk of Options: no widget for {info.Entry.SettingType.Name} ({info.Entry.Definition.Key})");
                    continue;
                }

                string prefix = Token($"CHARTEST_OPTION_{info.Entry.Definition.Section}_{info.Entry.Definition.Key}");
                GameLanguage.SetText(prefix + "_NAME", info.Name);
                GameLanguage.SetText(prefix + "_DESC", info.Description);
                ModSettingsManager.AddOption(option, guid, name, prefix + "_NAME", prefix + "_DESC");
                count++;
            }

            CharacterTestPlugin.Log.LogInfo($"Risk of Options: registered {count} settings.");
        }

        private static string Token(string raw)
        {
            var chars = raw.ToUpperInvariant().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (!char.IsLetterOrDigit(chars[i])) chars[i] = '_';
            return new string(chars);
        }

        private static BaseOption CreateOption(OptionInfo info)
        {
            switch (info.Entry)
            {
                case ConfigEntry<float> f:
                    return new SliderOption(f, new SliderConfig
                    {
                        min = info.Min,
                        max = info.Max,
                        formatString = info.Format,
                        restartRequired = info.RestartRequired,
                    });
                case ConfigEntry<int> i:
                    return new IntSliderOption(i, new IntSliderConfig
                    {
                        min = (int)info.Min,
                        max = (int)info.Max,
                        formatString = info.Format,
                        restartRequired = info.RestartRequired,
                    });
                case ConfigEntry<bool> b:
                    return new CheckBoxOption(b, new CheckBoxConfig { restartRequired = info.RestartRequired });
                default:
                    return null;
            }
        }
    }
}
