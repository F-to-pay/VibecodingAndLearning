namespace Destroyer.Art
{
    /// <summary>Дымовая завеса: пустотное облако дыма с закрытым глазом; бафф "прозрел" — открытый глаз.</summary>
    public static partial class IconBook
    {
        public static void SmokeScreen(IconCanvas c)
        {
            var deep = Rgba.Hex("#4a1f7a");
            var light = Rgba.Hex("#d9b8ff");

            // Клубы дыма: несколько кругов, слитых мягко, с лохматым краем.
            var cloud = Shapes.SmoothUnion(Shapes.SmoothUnion(Shapes.SmoothUnion(Shapes.SmoothUnion(Shapes.SmoothUnion(
                Shapes.Circle(0.50f, 0.56f, 0.24f),
                Shapes.Circle(0.28f, 0.62f, 0.16f), 0.07f),
                Shapes.Circle(0.72f, 0.62f, 0.16f), 0.07f),
                Shapes.Circle(0.36f, 0.36f, 0.15f), 0.07f),
                Shapes.Circle(0.62f, 0.32f, 0.17f), 0.07f),
                Shapes.Box(0.50f, 0.74f, 0.30f, 0.08f, 0.08f), 0.06f);
            cloud = Shapes.Fuzz(cloud, 0.008f, 40f);
            c.Fill(cloud, Paints.Radial(0.42f, 0.34f, 0.55f, light, deep));

            // Внутренние клубы — светлее, чтобы облако не было плоским.
            c.Fill(Shapes.Intersect(Shapes.Ring(0.36f, 0.36f, 0.12f, 0.025f), Shapes.Box(0.30f, 0.30f, 0.1f, 0.1f)), light.WithAlpha(0.55f));
            c.Fill(Shapes.Intersect(Shapes.Ring(0.62f, 0.32f, 0.14f, 0.025f), Shapes.Box(0.56f, 0.24f, 0.12f, 0.1f)), light.WithAlpha(0.55f));

            // Закрытый глаз: дуга века вниз и три ресницы.
            var ink = Rgba.Hex("#1a0b2e");
            c.Fill(Shapes.Arc(0.50f, 0.44f, 0.17f, 120f, 240f, 0.035f), ink);
            c.Fill(Shapes.Union(
                Shapes.Capsule(0.50f, 0.61f, 0.50f, 0.69f, 0.014f),
                Shapes.Capsule(0.40f, 0.585f, 0.36f, 0.655f, 0.014f),
                Shapes.Capsule(0.60f, 0.585f, 0.64f, 0.655f, 0.014f)), ink);

            // Искры пустоты.
            c.Fill(Shapes.Union(Shapes.Circle(0.16f, 0.30f, 0.02f), Shapes.Circle(0.86f, 0.22f, 0.025f), Shapes.Circle(0.88f, 0.84f, 0.018f)), VoidPurple);

            c.Outline(OutlineWidth, Ink);
        }

        /// <summary>"Прозрел": открытый глаз (миндаль со зрачком).</summary>
        public static void BuffSmokeClear(IconCanvas c)
        {
            var almond = Shapes.Intersect(Shapes.Circle(0.5f, 0.78f, 0.42f), Shapes.Circle(0.5f, 0.22f, 0.42f));
            var eye = Shapes.Subtract(almond, Shapes.Subtract(Shapes.Grow(almond, -0.045f), Shapes.Circle(0.5f, 0.5f, 0.12f)));
            c.Fill(eye, Rgba.White);
        }
    }
}
