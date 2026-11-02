namespace Destroyer.Art
{
    public static partial class IconBook
    {
        private static Sdf BucketBody() => Shapes.Polygon(0.24f, 0.42f, 0.76f, 0.42f, 0.70f, 0.86f, 0.30f, 0.86f);
        private static Sdf BucketHandle() => Shapes.Arc(0.50f, 0.42f, 0.25f, -88f, 88f, 0.03f);

        /// <summary>Ведро краски: металлическая банка, сверху краска с подтёком по боку.</summary>
        private static void PaintBucket(IconCanvas c, Rgba paint, bool empty)
        {
            var metal = Rgba.Hex("#a9b0b8");
            c.Fill(BucketHandle(), metal.Shade(0.6f));
            c.Fill(BucketBody(), Paints.Linear(0.24f, 0f, 0.76f, 0f, metal.Tint(0.35f), metal.Shade(0.65f)));
            c.Fill(Shapes.Box(0.50f, 0.56f, 0.25f, 0.018f), metal.Shade(0.55f).WithAlpha(0.7f)); // обруч
            c.Fill(Shapes.Box(0.50f, 0.74f, 0.22f, 0.018f), metal.Shade(0.55f).WithAlpha(0.7f));
            c.Fill(Shapes.Ellipse(0.50f, 0.42f, 0.26f, 0.065f), metal.Shade(0.45f));                 // край банки
            if (empty)
            {
                c.Fill(Shapes.Ellipse(0.50f, 0.43f, 0.22f, 0.045f), metal.Shade(0.25f));
                c.Fill(Shapes.Ellipse(0.42f, 0.44f, 0.06f, 0.015f), Rgba.White.WithAlpha(0.35f)); // остатки на дне
            }
            else
            {
                c.Fill(Shapes.Ellipse(0.50f, 0.425f, 0.225f, 0.05f), Paints.Lit(paint, 0.5f));
                // Подтёк по боку и капля.
                var drip = Shapes.Union(
                    Shapes.Capsule(0.34f, 0.44f, 0.335f, 0.64f, 0.032f),
                    Shapes.Capsule(0.62f, 0.44f, 0.625f, 0.54f, 0.026f),
                    Shapes.Circle(0.335f, 0.66f, 0.04f));
                c.Fill(Shapes.Grow(drip, 0.012f), paint.Shade(0.45f)); // тёмный кант — белая краска видна на металле
                c.Fill(drip, Paints.Lit(paint, 0.45f));
                c.Fill(Shapes.Ellipse(0.43f, 0.415f, 0.07f, 0.014f), Rgba.White.WithAlpha(0.5f));
            }
            c.Outline(OutlineWidth, Ink);
        }

        public static void WhitePaint(IconCanvas c) => PaintBucket(c, Rgba.Hex("#f4f4f0"), false);
        public static void WhitePaintEmpty(IconCanvas c) => PaintBucket(c, Rgba.Hex("#f4f4f0"), true);
        public static void GreenPaint(IconCanvas c) => PaintBucket(c, Rgba.Hex("#57c84a"), false);
        public static void RedPaint(IconCanvas c) => PaintBucket(c, Rgba.Hex("#d6352a"), false);

        /// <summary>Разложение: капля.</summary>
        public static void BuffRedPaintDecay(IconCanvas c) => c.Fill(Shapes.SmoothUnion(
            Shapes.Circle(0.50f, 0.62f, 0.22f),
            Shapes.Polygon(0.50f, 0.12f, 0.68f, 0.52f, 0.32f, 0.52f), 0.06f), Rgba.White);
    }
}
