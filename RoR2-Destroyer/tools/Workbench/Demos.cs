using System;
using Destroyer.Art;

namespace Workbench
{
    /// <summary>Витрина холста (_demo.png). Каждая плитка — один приём, код можно копировать в свои иконки.</summary>
    internal static class Demos
    {
        public static readonly Action<IconCanvas>[] All =
        {
            // 1. Круг + Lit (объём) + обводка.
            c => { c.Fill(Shapes.Circle(0.5f, 0.5f, 0.32f), Paints.Lit(IconBook.Fire)); c.Outline(IconBook.OutlineWidth, IconBook.Ink); },

            // 2. Скруглённый прямоугольник, вертикальный градиент.
            c => { c.Fill(Shapes.Box(0.5f, 0.5f, 0.3f, 0.22f, 0.06f), Paints.Vertical(IconBook.Steel.Tint(0.3f), IconBook.DarkSteel)); c.Outline(IconBook.OutlineWidth, IconBook.Ink); },

            // 3. Звезда с радиальной заливкой и свечением.
            c => { c.Fill(Shapes.Star(0.5f, 0.5f, 0.36f, 0.15f, 5), Paints.Radial(0.5f, 0.5f, 0.36f, IconBook.Ember.Tint(0.5f), IconBook.Fire)); c.Glow(0.08f, IconBook.Fire.WithAlpha(0.8f)); },

            // 4. Кольцо + капсулы (прицел).
            c =>
            {
                var ring = Shapes.Ring(0.5f, 0.5f, 0.28f, 0.06f);
                var cross = Shapes.Union(
                    Shapes.Capsule(0.5f, 0.12f, 0.5f, 0.32f, 0.025f), Shapes.Capsule(0.5f, 0.68f, 0.5f, 0.88f, 0.025f),
                    Shapes.Capsule(0.12f, 0.5f, 0.32f, 0.5f, 0.025f), Shapes.Capsule(0.68f, 0.5f, 0.88f, 0.5f, 0.025f));
                c.Fill(Shapes.Union(ring, cross), IconBook.Blood);
                c.Outline(IconBook.OutlineWidth, IconBook.Ink);
            },

            // 5. Многоугольник (клинок), повёрнутый на 45°.
            c =>
            {
                var blade = Shapes.Polygon(0.5f, 0.08f, 0.58f, 0.2f, 0.56f, 0.66f, 0.44f, 0.66f, 0.42f, 0.2f);
                var guard = Shapes.Box(0.5f, 0.7f, 0.16f, 0.03f, 0.015f);
                var grip = Shapes.Capsule(0.5f, 0.74f, 0.5f, 0.9f, 0.03f);
                c.Fill(Shapes.Rotate(grip, 0.5f, 0.5f, 45f), Rgba.Hex("#6b4a2b"));
                c.Fill(Shapes.Rotate(blade, 0.5f, 0.5f, 45f), Paints.Lit(IconBook.Steel, 0.6f));
                c.Fill(Shapes.Rotate(guard, 0.5f, 0.5f, 45f), Paints.Lit(IconBook.Ember.Shade(0.8f)));
                c.Outline(IconBook.OutlineWidth, IconBook.Ink);
            },

            // 6. Вычитание: полумесяц.
            c => { c.Fill(Shapes.Subtract(Shapes.Circle(0.5f, 0.5f, 0.32f), Shapes.Circle(0.64f, 0.42f, 0.27f)), Paints.Lit(IconBook.LunarBlue)); c.Outline(IconBook.OutlineWidth, IconBook.Ink); },

            // 7. Мягкое объединение (капли Пустоты).
            c =>
            {
                var blob = Shapes.SmoothUnion(Shapes.SmoothUnion(Shapes.Circle(0.4f, 0.55f, 0.18f), Shapes.Circle(0.62f, 0.45f, 0.14f), 0.12f),
                                              Shapes.Circle(0.55f, 0.72f, 0.1f), 0.1f);
                c.Fill(blob, Paints.Radial(0.45f, 0.45f, 0.4f, IconBook.VoidPurple.Tint(0.4f), IconBook.VoidPurple.Shade(0.5f)));
                c.Outline(IconBook.OutlineWidth, IconBook.Ink);
            },

            // 8. Симметрия: рисуешь правую половину — левая зеркалится (щит).
            //    Половинка должна заходить за ось (здесь до x = 0.4), иначе по оси останется шов.
            c =>
            {
                var half = Shapes.Polygon(0.4f, 0.14f, 0.5f, 0.14f, 0.8f, 0.22f, 0.78f, 0.52f, 0.5f, 0.88f, 0.4f, 0.88f);
                var shield = Shapes.SymmetricX(half);
                c.Fill(shield, Paints.Lit(IconBook.Steel));
                c.Fill(Shapes.Grow(shield, -0.07f), Paints.Stripes(IconBook.Blood, IconBook.Blood.Shade(0.7f), 0.08f));
                c.Outline(IconBook.OutlineWidth, IconBook.Ink);
            },

            // 9. Сектор (кулдаун-«пицца»).
            c => { c.Fill(Shapes.Circle(0.5f, 0.5f, 0.32f), IconBook.DarkSteel); c.Fill(Shapes.Pie(0.5f, 0.5f, 0.32f, 0f, 250f), Paints.Lit(IconBook.Ember)); c.Outline(IconBook.OutlineWidth, IconBook.Ink); },

            // 10. Правильный шестиугольник с дыркой (гайка).
            c => { c.Fill(Shapes.Subtract(Shapes.RegularPolygon(0.5f, 0.5f, 0.33f, 6, 30f), Shapes.Circle(0.5f, 0.5f, 0.13f)), Paints.Lit(IconBook.Steel, 0.7f)); c.Outline(IconBook.OutlineWidth, IconBook.Ink); },

            // 11. Заглушка мода.
            IconBook.Placeholder,
        };
    }
}
