using System;

namespace Destroyer.Core
{
    /// <summary>Языки мода. Русский — если игра на русском, иначе английский.</summary>
    public enum Lang : byte { English, Russian }

    /// <summary>
    /// Текущий язык. Папка Core не знает про RoR2, поэтому язык ей "подсказывают" снаружи:
    /// плагин ставит Provider, который смотрит на язык игры, а верстак — на аргумент командной строки.
    /// </summary>
    public static class Loc
    {
        public static Func<Lang> Provider = () => Lang.English;

        public static Lang Current => Provider();

        public static bool IsRussian => Current == Lang.Russian;

        /// <summary>Быстрый выбор строки: Loc.T("empty", "пусто").</summary>
        public static string T(string en, string ru) => IsRussian ? ru : en;
    }

    /// <summary>Пара строк "английский + русский".</summary>
    public readonly struct Words
    {
        public readonly string En;
        public readonly string Ru;

        public Words(string en, string ru)
        {
            En = en;
            Ru = ru;
        }

        /// <summary>Одна строка на оба языка (числа, имена собственные).</summary>
        public Words(string both) : this(both, both) { }

        public bool IsEmpty => string.IsNullOrEmpty(En) && string.IsNullOrEmpty(Ru);

        public string Get(Lang lang) => lang == Lang.Russian ? Ru : En;

        public override string ToString() => Get(Loc.Current);
    }
}
