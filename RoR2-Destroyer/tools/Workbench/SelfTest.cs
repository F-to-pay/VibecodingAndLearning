using System;
using Destroyer.Art;
using Destroyer.Core;

namespace Workbench
{
    /// <summary>Самотест чистых частей мода. Падает с кодом 1 и списком, если что-то разъехалось.</summary>
    internal static class SelfTest
    {
        private static int failures;

        public static int Run()
        {
            Stacks();
            Texts();
            Drawing();
            Icons();

            Console.WriteLine(failures == 0 ? "ALL OK" : $"FAILED: {failures}");
            return failures == 0 ? 0 : 1;
        }

        private static void Stacks()
        {
            Near("Linear 0", Stacking.Linear(0, 0.15f, 0.1f), 0f);
            Near("Linear 1", Stacking.Linear(1, 0.15f, 0.1f), 0.15f);
            Near("Linear 3", Stacking.Linear(3, 0.15f, 0.1f), 0.35f);
            Near("Linear same step", Stacking.Linear(4, 0.25f), 1f);
            Near("Hyperbolic 1", Stacking.Hyperbolic(1, 0.15f), 0.1304f, 1e-3f);
            Near("Hyperbolic 20", Stacking.Hyperbolic(20, 0.15f), 0.75f, 1e-3f);
            Check("Hyperbolic < 1", Stacking.Hyperbolic(100000, 1f) < 1f);
            Near("Exponential 5", Stacking.Exponential(5, 0.1f), 0.40951f, 1e-4f);
            Near("Geometric 0 = 1", Stacking.Geometric(0, 0.1f), 1f);
            Near("Geometric 2", Stacking.Geometric(2, 0.1f), 1.21f, 1e-4f);
            Near("Capped", Stacking.LinearCapped(10, 0.2f, 0.2f, 1f), 1f);
        }

        private static void Texts()
        {
            Equal("Pct 0.15", Tx.Pct(0.15f), "15%");
            Equal("Pct 0.125", Tx.Pct(0.125f), "12.5%");
            Equal("Num invariant", Tx.Num(2.5f), "2.5");
            Equal("Stack", Tx.Stack("+10%"), "<style=cStack>(+10%)</style>");
            Equal("Damage", Tx.Damage("x"), "<style=cIsDamage>x</style>");

            Loc.Provider = () => Lang.Russian;
            Equal("Loc RU", Loc.T("a", "б"), "б");
            Equal("Words RU", new Words("a", "б").ToString(), "б");
            Loc.Provider = () => Lang.English;
            Equal("Loc EN", Loc.T("a", "б"), "a");
        }

        private static void Drawing()
        {
            // Площадь круга r = 0.3 на холсте 1×1 — π·0.09 ≈ 0.2827.
            var c = new IconCanvas(256);
            c.Fill(Shapes.Circle(0.5f, 0.5f, 0.3f), Rgba.White);
            Near("Circle area", c.Coverage(), (float)(Math.PI * 0.09), 2e-3f);

            // Квадрат 0.4×0.4 = 0.16; многоугольник того же квадрата — то же самое.
            c.Clear();
            c.Fill(Shapes.Box(0.5f, 0.5f, 0.2f, 0.2f), Rgba.White);
            Near("Box area", c.Coverage(), 0.16f, 2e-3f);
            c.Clear();
            c.Fill(Shapes.Polygon(0.3f, 0.3f, 0.7f, 0.3f, 0.7f, 0.7f, 0.3f, 0.7f), Rgba.White);
            Near("Polygon area", c.Coverage(), 0.16f, 2e-3f);
            c.Clear();
            c.Fill(Shapes.Polygon(0.3f, 0.7f, 0.7f, 0.7f, 0.7f, 0.3f, 0.3f, 0.3f), Rgba.White);
            Near("Polygon reversed", c.Coverage(), 0.16f, 2e-3f);

            // Вычитание и полукруг.
            c.Clear();
            c.Fill(Shapes.Subtract(Shapes.Circle(0.5f, 0.5f, 0.3f), Shapes.Circle(0.5f, 0.5f, 0.15f)), Rgba.White);
            Near("Ring by subtract", c.Coverage(), (float)(Math.PI * (0.09 - 0.0225)), 2e-3f);
            c.Clear();
            c.Fill(Shapes.Pie(0.5f, 0.5f, 0.3f, 0f, 180f), Rgba.White);
            Near("Half pie", c.Coverage(), (float)(Math.PI * 0.09 / 2), 3e-3f);

            // Поворот не меняет площадь, сдвиг тоже.
            c.Clear();
            c.Fill(Shapes.Rotate(Shapes.Box(0.5f, 0.5f, 0.2f, 0.1f), 0.5f, 0.5f, 37f), Rgba.White);
            Near("Rotated box area", c.Coverage(), 0.08f, 2e-3f);

            // Поворот по часовой: отрезок, торчащий вверх, после +90° торчит вправо.
            c.Clear();
            c.Fill(Shapes.Rotate(Shapes.Capsule(0.5f, 0.5f, 0.5f, 0.1f, 0.03f), 0.5f, 0.5f, 90f), Rgba.White);
            Check("Rotate clockwise", c.Get(230, 128).A > 0.9f && c.Get(26, 128).A < 0.1f);

            // Звезда: вершина смотрит вверх.
            c.Clear();
            c.Fill(Shapes.Star(0.5f, 0.5f, 0.4f, 0.15f, 5), Rgba.White);
            Check("Star points up", c.Get(128, 30).A > 0.9f && c.Get(128, 225).A < 0.1f);

            // Обводка: добавляет кольцо вокруг и не трогает цвет внутри.
            c.Clear();
            c.Fill(Shapes.Circle(0.5f, 0.5f, 0.3f), Rgba.White);
            float before = c.Coverage();
            c.Outline(0.05f, Rgba.Black);
            Near("Outline area", c.Coverage(), (float)(Math.PI * 0.35 * 0.35), 4e-3f);
            Check("Outline behind", c.Get(128, 128).R > 0.99f && before < c.Coverage());

            // Сглаживание: на краю есть полупрозрачные пиксели, а не лесенка.
            c.Clear();
            c.Fill(Shapes.Circle(0.5f, 0.5f, 0.3f), Rgba.White);
            int partial = 0;
            for (int i = 0; i < 256; i++) { float al = c.Get(i, 128).A; if (al > 0.05f && al < 0.95f) partial++; }
            Check("Anti-aliased edge", partial >= 2);

            // Цвета.
            var hex = Rgba.Hex("#ff8000");
            Near("Hex R", hex.R, 1f); Near("Hex G", hex.G, 128f / 255f, 1e-3f); Near("Hex A", hex.A, 1f);
            Near("Hex alpha", Rgba.Hex("00000080").A, 128f / 255f, 1e-3f);

            // Байты для Unity идут снизу вверх: верхний левый пиксель — в последней строке.
            c.Clear();
            c.Fill(Shapes.Box(0.05f, 0.05f, 0.05f, 0.05f), Rgba.White);
            var up = c.ToRgba32(bottomUp: true);
            var down = c.ToRgba32(bottomUp: false);
            Check("Bottom-up rows", up[(255 * 256 + 2) * 4 + 3] == 255 && down[(2 * 256 + 2) * 4 + 3] == 255 && up[(2 * 256 + 2) * 4 + 3] == 0);
        }

        private static void Icons()
        {
            Check("Placeholder for unknown id", IconBook.Get("NO_SUCH_ITEM") == (Action<IconCanvas>)IconBook.Placeholder);
            foreach (var pair in IconBook.All)
            {
                var canvas = IconBook.Render(pair.Value, 128);
                float cover = canvas.Coverage();
                Check($"Icon {pair.Key} draws something", cover > 0.03f);
                Check($"Icon {pair.Key} leaves transparent background", cover < 0.9f);
                Check($"Icon {pair.Key} id is upper-case", pair.Key == pair.Key.ToUpperInvariant());
            }
            foreach (var pair in IconBook.Buffs)
            {
                var canvas = IconBook.Render(pair.Value, 64);
                Check($"Buff {pair.Key} draws something", canvas.Coverage() > 0.05f);
                bool white = true;
                for (int j = 0; j < 64 && white; j++)
                    for (int i = 0; i < 64; i++)
                    {
                        var px = canvas.Get(i, j);
                        if (px.A > 0.01f && (px.R < 0.99f || px.G < 0.99f || px.B < 0.99f)) { white = false; break; }
                    }
                Check($"Buff {pair.Key} is pure white (game tints it)", white);
            }
            Check("Buff placeholder for unknown id", IconBook.GetBuff("NO_SUCH_BUFF") == (Action<IconCanvas>)IconBook.BuffPlaceholder);

            // Дуга: середина на окружности, концы скруглены, за концами пусто.
            var arc = new IconCanvas(256);
            arc.Fill(Shapes.Arc(0.5f, 0.5f, 0.3f, -45f, 45f, 0.06f), Rgba.White);
            Check("Arc top filled", arc.Get(128, 51).A > 0.9f);
            Check("Arc bottom empty", arc.Get(128, 204).A < 0.01f);
            Check("Arc wraps correctly (no fill at 180°)", arc.Get(128, 230).A < 0.01f);

            var placeholder = IconBook.Render(IconBook.Placeholder, 128);
            Check("Placeholder draws something", placeholder.Coverage() > 0.1f);
            Check("Placeholder corners transparent", placeholder.Get(0, 0).A == 0f && placeholder.Get(127, 127).A == 0f);
        }

        // ---------- Помощники ----------

        private static void Check(string name, bool ok)
        {
            Console.WriteLine($"{(ok ? "ok  " : "FAIL")} {name}");
            if (!ok) failures++;
        }

        private static void Near(string name, float actual, float expected, float eps = 1e-5f)
        {
            bool ok = Math.Abs(actual - expected) <= eps;
            Console.WriteLine($"{(ok ? "ok  " : "FAIL")} {name}: {actual:0.#####} (expected {expected:0.#####})");
            if (!ok) failures++;
        }

        private static void Equal(string name, string actual, string expected)
        {
            bool ok = actual == expected;
            Console.WriteLine($"{(ok ? "ok  " : "FAIL")} {name}: \"{actual}\"{(ok ? "" : $" (expected \"{expected}\")")}");
            if (!ok) failures++;
        }
    }
}
