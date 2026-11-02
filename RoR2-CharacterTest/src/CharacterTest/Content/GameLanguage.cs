using System;
using System.Collections.Generic;
using CharacterTest.Core;
using R2API;
using RoR2;

namespace CharacterTest.Content
{
    /// <summary>
    /// Мостик между языком игры и нашим Loc + регистрация текстов (как в Destroyer).
    /// Правило: игра на русском — мод на русском, на любом другом языке — английский.
    /// </summary>
    internal static class GameLanguage
    {
        /// <summary>Так русский называется внутри RoR2 (имя папки в StreamingAssets/Language).</summary>
        public const string RussianLanguageName = "RU";

        public static Lang Current =>
            string.Equals(Language.currentLanguageName, RussianLanguageName, StringComparison.OrdinalIgnoreCase)
                ? Lang.Russian
                : Lang.English;

        private static readonly Dictionary<string, (LanguageAPI.LanguageOverlay en, LanguageAPI.LanguageOverlay ru)> overlays =
            new Dictionary<string, (LanguageAPI.LanguageOverlay, LanguageAPI.LanguageOverlay)>();

        /// <summary>
        /// Текст токена на двух языках. Можно звать повторно — текст заменится прямо в игре.
        /// Overlay, а не LanguageAPI.Add: Add молча игнорирует уже существующий ключ (см. RAZBOR Destroyer).
        /// Английский — в generic, русский — в "RU".
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
