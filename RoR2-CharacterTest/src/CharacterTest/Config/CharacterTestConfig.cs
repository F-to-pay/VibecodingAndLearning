using System.Collections.Generic;
using BepInEx.Configuration;
using CharacterTest.Core;

namespace CharacterTest.Config
{
    /// <summary>Описание настройки для меню Risk of Options (названия на двух языках, пределы ползунка).</summary>
    public sealed class OptionInfo
    {
        public ConfigEntryBase Entry;
        public Words Name;
        public Words Description;
        public float Min;
        public float Max;
        public string Format = "{0:0.##}";
        public bool RestartRequired;
    }

    /// <summary>Общие настройки мода + реестр настроек для меню. У каждого выжившего ещё своя секция "Survivor: ID".</summary>
    public static class CharacterTestConfig
    {
        /// <summary>Подробный лог состояний навыков (вход/выход) — для отладки.</summary>
        public static ConfigEntry<bool> VerboseLog;

        /// <summary>Всё, что попадёт в меню Risk of Options (если он стоит).</summary>
        public static readonly List<OptionInfo> Options = new List<OptionInfo>();

        internal static void Bind(ConfigFile config)
        {
            VerboseLog = config.Bind("Debug", "VerboseLog", false,
                "Писать в лог каждый вход в состояние навыка. / Log every skill state entry.");
            Options.Add(new OptionInfo
            {
                Entry = VerboseLog,
                Name = new Words("Verbose log", "Подробный лог"),
                Description = new Words("Log every skill state entry.", "Писать в лог каждый вход в состояние навыка."),
            });
        }

        /// <summary>Число с ползунком в меню. Значение читай через .Value в момент использования — тогда правка в меню работает сразу.</summary>
        public static ConfigEntry<float> Float(ConfigFile config, string section, string key, float defaultValue,
            float min, float max, Words name, Words description, string format = "{0:0.##}", bool restartRequired = false)
        {
            var entry = config.Bind(section, key, defaultValue,
                new ConfigDescription($"{description.En} / {description.Ru}", new AcceptableValueRange<float>(min, max)));
            Options.Add(new OptionInfo
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

        /// <summary>Целое с ползунком в меню.</summary>
        public static ConfigEntry<int> Int(ConfigFile config, string section, string key, int defaultValue,
            int min, int max, Words name, Words description, bool restartRequired = false)
        {
            var entry = config.Bind(section, key, defaultValue,
                new ConfigDescription($"{description.En} / {description.Ru}", new AcceptableValueRange<int>(min, max)));
            Options.Add(new OptionInfo
            {
                Entry = entry,
                Name = name,
                Description = description,
                Min = min,
                Max = max,
                Format = "{0}",
                RestartRequired = restartRequired,
            });
            return entry;
        }
    }
}
