using System;
using System.Globalization;

namespace Destroyer.Art
{
    /// <summary>
    /// Цвет 0..1 без Unity. Альфа обычная (не умноженная) — так проще думать; умножает IconCanvas внутри.
    /// </summary>
    public readonly struct Rgba
    {
        public readonly float R, G, B, A;

        public Rgba(float r, float g, float b, float a = 1f)
        {
            R = r; G = g; B = b; A = a;
        }

        public static readonly Rgba Clear = new Rgba(0f, 0f, 0f, 0f);
        public static readonly Rgba Black = new Rgba(0f, 0f, 0f);
        public static readonly Rgba White = new Rgba(1f, 1f, 1f);

        /// <summary>"#ff8800", "ff8800", "#ff8800cc" (последние две цифры — альфа).</summary>
        public static Rgba Hex(string hex)
        {
            string h = hex.StartsWith("#") ? hex.Substring(1) : hex;
            if (h.Length != 6 && h.Length != 8) throw new ArgumentException("Color must be RRGGBB or RRGGBBAA: " + hex);
            float P(int i) => int.Parse(h.Substring(i, 2), NumberStyles.HexNumber) / 255f;
            return new Rgba(P(0), P(2), P(4), h.Length == 8 ? P(6) : 1f);
        }

        public Rgba WithAlpha(float a) => new Rgba(R, G, B, a);

        /// <summary>Темнее/светлее: Shade(0.5) — вдвое темнее, Shade(1.3) — светлее (без пересвета за 1).</summary>
        public Rgba Shade(float k) => new Rgba(Clamp01(R * k), Clamp01(G * k), Clamp01(B * k), A);

        /// <summary>Смешать с белым: Tint(0.3) — на 30% ближе к белому.</summary>
        public Rgba Tint(float t) => Lerp(this, White.WithAlpha(A), t);

        public static Rgba Lerp(Rgba a, Rgba b, float t)
        {
            t = Clamp01(t);
            return new Rgba(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t, a.A + (b.A - a.A) * t);
        }

        internal static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        public override string ToString() =>
            $"#{(int)(R * 255):x2}{(int)(G * 255):x2}{(int)(B * 255):x2}{(int)(A * 255):x2}";
    }
}
