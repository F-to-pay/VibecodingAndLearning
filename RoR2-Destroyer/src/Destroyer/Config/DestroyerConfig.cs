using System.Collections.Generic;
using BepInEx.Configuration;
using Destroyer.Core;
using UnityEngine;

namespace Destroyer.Config
{
    /// <summary>
    /// Всё, что нужно меню Risk of Options, чтобы нарисовать настройку: запись конфига,
    /// названия и описания на двух языках, диапазон слайдера, имя категории.
    /// </summary>
    internal sealed class OptionInfo
    {
        public ConfigEntryBase Entry;
        public Words Name;
        public Words Description;
        public Words Category;
        public float Min;
        public float Max;
        public string Format;
        public bool RestartRequired;
    }

    /// <summary>
    /// Общие настройки мода. BepInEx сам создаст BepInEx/config/FToPay.Destroyer.cfg после первого запуска.
    /// Настройки предметов живут в своих секциях "Item: ID" (см. Content/ItemSettings.cs), но регистрируются тут же,
    /// через Bind — чтобы все попали в меню Risk of Options.
    ///
    /// В .cfg описания на английском (так принято), в меню Risk of Options — на языке игры.
    /// </summary>
    internal static class DestroyerConfig
    {
        public const string SectionDebug = "Debug";
        public const string SectionVisual = "Visual";

        public static ConfigEntry<bool> DebugMenuEnabled;
        public static ConfigEntry<KeyboardShortcut> DebugKey;
        public static ConfigEntry<bool> FlatPickupModels;

        private static readonly List<OptionInfo> options = new List<OptionInfo>();

        /// <summary>Все настройки в порядке объявления — для меню Risk of Options.</summary>
        public static IReadOnlyList<OptionInfo> Options => options;

        public static void Bind(ConfigFile cfg)
        {
            var debug = new Words("Debug", "Отладка");
            var visual = new Words("Visual", "Внешний вид");

            FlatPickupModels = Bind(cfg, SectionVisual, "FlatPickupModels", true, visual,
                new Words("2D item models", "2D-модельки предметов"),
                new Words("Items lying on the ground show their icon as a flat card facing the camera. Off — borrowed vanilla 3D models. Needs a restart.",
                          "Предметы на земле — плоская иконка лицом к камере. Выключено — одолженные ванильные 3D-модельки. Нужен перезапуск."),
                restartRequired: true);

            DebugMenuEnabled = Bind(cfg, SectionDebug, "DebugMenu", false, debug,
                new Words("Debug menu", "Отладочное меню"),
                new Words("Allow the debug menu: give, take and drop any item of this mod (host only).",
                          "Разрешить отладочное меню: выдать, забрать или бросить любой предмет мода (только хост)."));

            DebugKey = Bind(cfg, SectionDebug, "DebugKey", new KeyboardShortcut(KeyCode.F3), debug,
                new Words("Debug menu key", "Клавиша отладочного меню"),
                new Words("Key that toggles the debug menu (works only if the debug menu is allowed).",
                          "Клавиша, открывающая отладочное меню (работает, только если меню разрешено)."));
        }

        /// <summary>Обёртка над cfg.Bind: заодно запоминает всё, что нужно для игрового меню.</summary>
        public static ConfigEntry<T> Bind<T>(ConfigFile cfg, string section, string key, T defaultValue,
                                             Words category, Words name, Words description,
                                             float min = 0f, float max = 100f, string format = "{0:0.##}",
                                             bool restartRequired = false)
        {
            var entry = cfg.Bind(section, key, defaultValue, description.En);
            options.Add(new OptionInfo
            {
                Entry = entry,
                Name = name,
                Description = description,
                Category = category,
                Min = min,
                Max = max,
                Format = format,
                RestartRequired = restartRequired,
            });
            return entry;
        }
    }
}
