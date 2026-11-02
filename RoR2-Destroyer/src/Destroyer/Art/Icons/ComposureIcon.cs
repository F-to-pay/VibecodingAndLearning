namespace Destroyer.Art
{
    public static partial class IconBook
    {
        /// <summary>Ледяной кристалл-щит.</summary>
        private static Sdf IceShardShape() => Shapes.Union(
            Shapes.Polygon(0.50f, 0.08f, 0.74f, 0.36f, 0.66f, 0.86f, 0.34f, 0.86f, 0.26f, 0.36f),
            Shapes.Polygon(0.22f, 0.30f, 0.32f, 0.20f, 0.30f, 0.56f),
            Shapes.Polygon(0.78f, 0.30f, 0.68f, 0.20f, 0.70f, 0.56f));

        /// <summary>Хладнокровие: голубой кристалл с гранями и снежинкой.</summary>
        public static void Composure(IconCanvas c)
        {
            var ice = Rgba.Hex("#9fe4ff");
            var deep = Rgba.Hex("#2a6fb8");
            c.Fill(IceShardShape(), Paints.Linear(0.3f, 0.1f, 0.7f, 0.9f, ice.Tint(0.4f), deep));
            // Грани.
            c.Fill(Shapes.Union(
                Shapes.Capsule(0.50f, 0.10f, 0.50f, 0.84f, 0.008f),
                Shapes.Capsule(0.27f, 0.37f, 0.73f, 0.37f, 0.008f)), Rgba.White.WithAlpha(0.45f));
            // Косые грани и иней.
            c.Fill(Shapes.Union(
                Shapes.Capsule(0.27f, 0.37f, 0.40f, 0.86f, 0.007f),
                Shapes.Capsule(0.73f, 0.37f, 0.60f, 0.86f, 0.007f)), deep.Shade(0.7f).WithAlpha(0.6f));
            c.Fill(Snowflake(0.62f, 0.62f, 0.07f, 0.009f), Rgba.White.WithAlpha(0.8f));
            c.Fill(Shapes.Ellipse(0.40f, 0.24f, 0.03f, 0.07f), Rgba.White.WithAlpha(0.55f));
            c.Outline(OutlineWidth, Ink);
        }

        public static void BuffComposureActive(IconCanvas c) => c.Fill(IceShardShape(), Rgba.White);
    }
}
