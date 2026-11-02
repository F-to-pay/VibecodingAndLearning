using System;
using System.Collections.Generic;
using Destroyer.Core;
using R2API;
using RoR2;

namespace Destroyer.Content
{
    /// <summary>
    /// Мостик между языком игры и нашим Loc + регистрация текстов.
    /// Правило: игра на русском — мод на русском, на любом другом языке — английский.
    /// </summary>
    internal static class GameLanguage
    {
        /// <summary>Так русский называется внутри RoR2 (имя папки в StreamingAssets/Language).</summary>
        public const string RussianLanguageName = "RU";

        /// <summary>Язык игры прямо сейчас. Пока язык не загрузился (самое начало запуска) — английский.</summary>
        public static Lang Current =>
            string.Equals(Language.currentLanguageName, RussianLanguageName, StringComparison.OrdinalIgnoreCase)
                ? Lang.Russian
                : Lang.English;

        private static readonly Dictionary<string, (LanguageAPI.LanguageOverlay en, LanguageAPI.LanguageOverlay ru)> overlays =
            new Dictionary<string, (LanguageAPI.LanguageOverlay, LanguageAPI.LanguageOverlay)>();

        /// <summary>
        /// Задать текст токена на двух языках. Можно звать повторно — текст заменится прямо в игре
        /// (так описание предмета следует за настройками из Risk of Options).
        ///
        /// Почему overlay, а не LanguageAPI.Add: Add молча ИГНОРИРУЕТ ключ, который уже есть (проверено по IL
        /// R2API.Language 1.1.0), то есть второй раз текст не поменять. Overlay снимается и ставится заново.
        /// Порядок поиска в R2API: overlay[язык игры] → overlay[generic] → Add[язык] → Add[generic],
        /// поэтому английский кладём в generic, русский — в "RU", и оба всегда через overlay.
        /// </summary>
        public static void SetText(string token, Words text)
        {
            if (overlays.TryGetValue(token, out var old))
            {
                old.en.Remove();
                old.ru.Remove();
            }

            var en = LanguageAPI.AddOverlay(token, text.En ?? "");
            var ru = LanguageAPI.AddOverlay(token, text.Ru ?? text.En ?? "", RussianLanguageName);
            overlays[token] = (en, ru);
        }
    }
}
