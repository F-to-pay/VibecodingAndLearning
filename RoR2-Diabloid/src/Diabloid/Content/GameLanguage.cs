using System;
using Diabloid.Loot;
using R2API;
using RoR2;

namespace Diabloid.Content
{
    /// <summary>
    /// Мостик между языком игры и нашим Loc.
    /// Правило простое: игра на русском — мод на русском, на любом другом языке — английский.
    /// </summary>
    internal static class GameLanguage
    {
        /// <summary>Так русский называется внутри RoR2 (это имя папки в StreamingAssets/Language).</summary>
        public const string RussianLanguageName = "RU";

        /// <summary>Язык игры прямо сейчас. Пока язык не загрузился (самое начало запуска) — английский.</summary>
        public static Lang Current =>
            string.Equals(Language.currentLanguageName, RussianLanguageName, StringComparison.OrdinalIgnoreCase)
                ? Lang.Russian
                : Lang.English;

        /// <summary>
        /// Регистрирует токен игры на двух языках. Английский — "общий" (для всех языков),
        /// русский — поверх него. Игра сама подставит нужный при смене языка в настройках.
        /// </summary>
        public static void AddToken(string token, Words text)
        {
            LanguageAPI.Add(token, text.En);
            LanguageAPI.Add(token, text.Ru, RussianLanguageName);
        }
    }
}
