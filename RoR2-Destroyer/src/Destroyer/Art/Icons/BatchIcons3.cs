namespace Destroyer.Art
{
    /// <summary>Преобразователь боли, звуковая пушка, вафельница и их баффы.</summary>
    public static partial class IconBook
    {
        /// <summary>Сердце в кольце из двух стрелок-дуг (урон по кругу превращается в лечение).</summary>
        private static Sdf Heart(float cx, float cy, float s) =>
            Shapes.SmoothUnion(
                Shapes.SmoothUnion(Shapes.Circle(cx - 0.5f * s, cy - 0.15f * s, 0.55f * s), Shapes.Circle(cx + 0.5f * s, cy - 0.15f * s, 0.55f * s), 0.05f * s),
                Shapes.Polygon(cx - 1.02f * s, cy + 0.05f * s, cx + 1.02f * s, cy + 0.05f * s, cx, cy + 1.15f * s), 0.12f * s);

        public static void PainConverter(IconCanvas c)
        {
            var red = Rgba.Hex("#e0303a");
            var green = Rgba.Hex("#6fe06b");
            var metal = Rgba.Hex("#5a5f6a");

            // Корпус — тёмная шайба.
            c.Fill(Shapes.Circle(0.5f, 0.52f, 0.36f), Paints.Lit(metal, 0.5f));
            c.Fill(Shapes.Ring(0.5f, 0.52f, 0.36f, 0.035f), DarkSteel);

            // Две стрелки по кругу: красная (боль) сверху, зелёная (лечение) снизу.
            c.Fill(Shapes.Arc(0.5f, 0.52f, 0.28f, -60f, 70f, 0.06f), red);
            c.Fill(Shapes.Polygon(0.80f, 0.55f, 0.70f, 0.66f, 0.66f, 0.50f), red);
            c.Fill(Shapes.Arc(0.5f, 0.52f, 0.28f, 120f, 250f, 0.06f), green);
            c.Fill(Shapes.Polygon(0.20f, 0.49f, 0.30f, 0.38f, 0.34f, 0.54f), green);

            // Сердце в центре: верх красный, низ зелёный.
            c.Fill(Heart(0.5f, 0.50f, 0.12f), Paints.Vertical(red, green, 0.42f, 0.66f));
            c.Fill(Shapes.Ellipse(0.45f, 0.47f, 0.025f, 0.018f), Rgba.White.WithAlpha(0.6f));

            c.Outline(OutlineWidth, Ink);
        }

        public static void SonicCannon(IconCanvas c)
        {
            var deep = Rgba.Hex("#3b1966");
            var light = Rgba.Hex("#c08aff");

            // Раструб-динамик, смотрит вправо.
            var horn = Shapes.Grow(Shapes.Polygon(0.16f, 0.44f, 0.36f, 0.44f, 0.58f, 0.24f, 0.58f, 0.80f, 0.36f, 0.60f, 0.16f, 0.60f), 0.02f);
            c.Fill(horn, Paints.Linear(0.16f, 0.3f, 0.6f, 0.7f, light, deep));
            c.Fill(Shapes.Ellipse(0.58f, 0.52f, 0.05f, 0.28f), Paints.Radial(0.58f, 0.52f, 0.28f, Rgba.Hex("#1a0b2e"), deep));
            c.Fill(Shapes.Box(0.20f, 0.52f, 0.05f, 0.11f, 0.02f), DarkSteel);

            // Волны.
            c.Fill(Shapes.Arc(0.58f, 0.52f, 0.13f, 50f, 130f, 0.04f), VoidPurple);
            c.Fill(Shapes.Arc(0.58f, 0.52f, 0.22f, 45f, 135f, 0.045f), VoidPurple.Tint(0.2f));
            c.Fill(Shapes.Arc(0.58f, 0.52f, 0.31f, 42f, 138f, 0.05f), VoidPurple.Tint(0.4f));

            c.Outline(OutlineWidth, Ink);
        }

        public static void WaffleIron(IconCanvas c)
        {
            var dough = Rgba.Hex("#e8b45a");
            var crust = Rgba.Hex("#a8692a");

            // Пламя сзади: капля остриём вверх, внутри светлее.
            Sdf Flame(float cx, float cy, float r) => Shapes.SmoothUnion(
                Shapes.Circle(cx, cy, r),
                Shapes.Polygon(cx - r * 0.95f, cy - r * 0.2f, cx + r * 0.15f, cy - r * 2.6f, cx + r * 0.95f, cy - r * 0.2f), r * 0.4f);
            c.Fill(Flame(0.74f, 0.34f, 0.15f), Paints.Vertical(Fire.Shade(1.1f), Fire, 0.0f, 0.5f));
            c.Fill(Flame(0.75f, 0.38f, 0.08f), Ember);

            // Вафля — скруглённый квадрат, слегка повёрнут.
            var waffle = Shapes.Rotate(Shapes.Box(0.46f, 0.58f, 0.27f, 0.27f, 0.05f), 0.46f, 0.58f, -12f);
            c.Fill(waffle, Paints.Lit(dough, 0.45f));
            // Решётка: ячейки темнее.
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                {
                    float x = 0.46f - 0.18f + i * 0.12f, y = 0.58f - 0.18f + j * 0.12f;
                    var cell = Shapes.Rotate(Shapes.Box(x, y, 0.038f, 0.038f, 0.01f), 0.46f, 0.58f, -12f);
                    c.Fill(cell, crust);
                }
            c.Outline(OutlineWidth, Ink);
        }

        /// <summary>Возвраты боли: сердце со стрелкой по кругу.</summary>
        public static void BuffPainRestore(IconCanvas c)
        {
            c.Fill(Shapes.Union(Heart(0.5f, 0.46f, 0.18f),
                Shapes.Arc(0.5f, 0.5f, 0.38f, 200f, 340f, 0.07f),
                Shapes.Polygon(0.16f, 0.30f, 0.30f, 0.30f, 0.17f, 0.44f)), Rgba.White);
        }

        /// <summary>Броня боли: щит с трещиной.</summary>
        public static void BuffPainArmor(IconCanvas c)
        {
            var shield = Shapes.SmoothUnion(
                Shapes.Box(0.5f, 0.36f, 0.30f, 0.20f, 0.05f),
                Shapes.Polygon(0.20f, 0.42f, 0.80f, 0.42f, 0.5f, 0.88f), 0.06f);
            var crack = Shapes.Union(
                Shapes.Capsule(0.52f, 0.14f, 0.44f, 0.38f, 0.03f),
                Shapes.Capsule(0.44f, 0.38f, 0.56f, 0.52f, 0.03f),
                Shapes.Capsule(0.56f, 0.52f, 0.48f, 0.70f, 0.03f));
            c.Fill(Shapes.Subtract(shield, crack), Rgba.White);
        }

        /// <summary>Подпал: решётка 3×3 (клеточка вафли).</summary>
        public static void BuffScorch(IconCanvas c)
        {
            var plate = Shapes.Box(0.5f, 0.5f, 0.34f, 0.34f, 0.06f);
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    plate = Shapes.Subtract(plate, Shapes.Box(0.5f + (i - 1) * 0.22f, 0.5f + (j - 1) * 0.22f, 0.07f, 0.07f, 0.015f));
            c.Fill(plate, Rgba.White);
        }
    }
}
