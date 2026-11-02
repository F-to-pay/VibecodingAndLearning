using System.Globalization;

namespace Destroyer.Core
{
    /// <summary>
    /// Разметка текстов предметов, как у ванильных описаний. Игра красит по тегу &lt;style=...&gt;.
    ///
    /// Пример описания:
    ///   $"Наносит {Tx.Damage("+" + Tx.Pct(0.15f) + " урона")} {Tx.Stack("+" + Tx.Pct(0.1f) + " за копию")}."
    /// превращается в "Наносит +15% урона (+10% за копию)." с оранжевым уроном и серой скобкой.
    /// </summary>
    public static class Tx
    {
        private static string Style(string style, string text) => "<style=" + style + ">" + text + "</style>";

        /// <summary>Оранжевый — урон.</summary>
        public static string Damage(string text) => Style("cIsDamage", text);

        /// <summary>Зелёный — лечение.</summary>
        public static string Healing(string text) => Style("cIsHealing", text);

        /// <summary>Голубой — полезное (скорость, перезарядка, щит...).</summary>
        public static string Utility(string text) => Style("cIsUtility", text);

        /// <summary>Красный — здоровье (обычно отрицательные эффекты на игрока).</summary>
        public static string Health(string text) => Style("cIsHealth", text);

        /// <summary>Фиолетовый — Пустота.</summary>
        public static string Void(string text) => Style("cIsVoid", text);

        /// <summary>Синий — лунное.</summary>
        public static string Lunar(string text) => Style("cIsLunar", text);

        /// <summary>Красный "смертельный" — мгновенное убийство и т.п.</summary>
        public static string Death(string text) => Style("cDeath", text);

        /// <summary>Жёлтый — святилища, деньги.</summary>
        public static string Shrine(string text) => Style("cShrine", text);

        /// <summary>Серый мелкий — второстепенное.</summary>
        public static string Sub(string text) => Style("cSub", text);

        /// <summary>Ключевое слово (как "Кровотечение" в описаниях).</summary>
        public static string Keyword(string text) => Style("cKeywordName", text);

        /// <summary>Серая скобка про стаки: Tx.Stack("+10% per stack") → "(+10% per stack)".</summary>
        public static string Stack(string text) => Style("cStack", "(" + text + ")");

        /// <summary>Доля → проценты без лишних нулей: 0.15 → "15%", 0.125 → "12.5%".</summary>
        public static string Pct(float fraction) => Num(fraction * 100f) + "%";

        /// <summary>Число без лишних нулей, всегда с точкой: 2.50 → "2.5".</summary>
        public static string Num(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        /// <summary>Секунды: 3 → "3s" / "3 с".</summary>
        public static Words Seconds(float value) => new Words(Num(value) + "s", Num(value) + " с");

        /// <summary>Метры: 12 → "12m" / "12 м".</summary>
        public static Words Meters(float value) => new Words(Num(value) + "m", Num(value) + " м");
    }
}
