using System;

namespace Destroyer.Art
{
    /// <summary>Чем заливать фигуру: цвет в каждой точке холста (координаты 0..1, y вниз).</summary>
    public delegate Rgba Paint(float x, float y);

    public static class Paints
    {
        public static Paint Solid(Rgba color) => (x, y) => color;

        /// <summary>Линейный градиент от точки (x0,y0) цвета a к точке (x1,y1) цвета b.</summary>
        public static Paint Linear(float x0, float y0, float x1, float y1, Rgba a, Rgba b)
        {
            float dx = x1 - x0, dy = y1 - y0;
            float lenSq = dx * dx + dy * dy;
            return (x, y) =>
            {
                float t = lenSq > 0f ? ((x - x0) * dx + (y - y0) * dy) / lenSq : 0f;
                return Rgba.Lerp(a, b, t);
            };
        }

        /// <summary>Сверху вниз: top на y0, bottom на y1. Самое частое "объёмное" освещение.</summary>
        public static Paint Vertical(Rgba top, Rgba bottom, float y0 = 0f, float y1 = 1f) =>
            Linear(0.5f, y0, 0.5f, y1, top, bottom);

        /// <summary>Радиальный: inner в центре, outer на радиусе r и дальше.</summary>
        public static Paint Radial(float cx, float cy, float r, Rgba inner, Rgba outer) =>
            (x, y) => Rgba.Lerp(inner, outer, Shapes.Len(x - cx, y - cy) / r);

        /// <summary>
        /// "Металл/глянец" для иконок: основной цвет, светлее к источнику света (сверху-слева), темнее к низу-справа.
        /// strength 0..1 — насколько сильный перепад.
        /// </summary>
        public static Paint Lit(Rgba color, float strength = 0.45f, float lightX = 0.3f, float lightY = 0.2f)
        {
            var light = color.Tint(strength * 0.6f);
            var dark = color.Shade(1f - strength * 0.7f);
            return (x, y) =>
            {
                float t = ((x - lightX) + (y - lightY)) / 1.2f + 0.5f;
                return t < 0.5f ? Rgba.Lerp(light, color, t * 2f) : Rgba.Lerp(color, dark, (t - 0.5f) * 2f);
            };
        }

        /// <summary>Полосы под углом (штриховка, "предупреждающая лента").</summary>
        public static Paint Stripes(Rgba a, Rgba b, float width, float degrees = 45f)
        {
            double rad = degrees * Math.PI / 180.0;
            float ux = (float)Math.Cos(rad), uy = (float)Math.Sin(rad);
            return (x, y) =>
            {
                float t = (x * ux + y * uy) / width;
                return ((int)Math.Floor(t) & 1) == 0 ? a : b;
            };
        }
    }
}
