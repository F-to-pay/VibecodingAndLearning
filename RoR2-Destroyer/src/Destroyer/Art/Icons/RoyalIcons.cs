namespace Destroyer.Art
{
    /// <summary>Голова льва, Корона Элитаризма и их баффы.</summary>
    public static partial class IconBook
    {
        /// <summary>Морда льва: грива-звезда, морда, уши, глаза, нос.</summary>
        public static void LionHead(IconCanvas c)
        {
            var mane = Rgba.Hex("#b5651d");
            var fur = Rgba.Hex("#f0b44a");
            var dark = Rgba.Hex("#3a2410");

            c.Fill(Shapes.Fuzz(Shapes.Grow(Shapes.Star(0.5f, 0.52f, 0.42f, 0.32f, 14, 0f), 0.02f), 0.01f, 30f), Paints.Radial(0.5f, 0.5f, 0.45f, mane.Tint(0.2f), mane.Shade(0.7f)));
            c.Fill(Shapes.Union(Shapes.Circle(0.34f, 0.30f, 0.06f), Shapes.Circle(0.66f, 0.30f, 0.06f)), fur.Shade(0.85f));
            var face = Shapes.SmoothUnion(Shapes.Circle(0.5f, 0.50f, 0.22f), Shapes.Ellipse(0.5f, 0.64f, 0.14f, 0.11f), 0.05f);
            c.Fill(face, Paints.Lit(fur, 0.45f));
            c.Fill(Shapes.Ellipse(0.5f, 0.67f, 0.10f, 0.07f), fur.Tint(0.4f));
            c.Fill(Shapes.Union(Shapes.Ellipse(0.42f, 0.47f, 0.03f, 0.02f), Shapes.Ellipse(0.58f, 0.47f, 0.03f, 0.02f)), dark);
            c.Fill(Shapes.Polygon(0.45f, 0.58f, 0.55f, 0.58f, 0.5f, 0.64f), dark);
            c.Fill(Shapes.Union(Shapes.Capsule(0.5f, 0.64f, 0.46f, 0.70f, 0.008f), Shapes.Capsule(0.5f, 0.64f, 0.54f, 0.70f, 0.008f)), dark);
            c.Outline(OutlineWidth, Ink);
        }

        private static Sdf CrownShape(float cx, float cy, float w)
        {
            var band = Shapes.Box(cx, cy + 0.10f * w, 0.30f * w, 0.07f * w, 0.02f * w);
            var spikes = Shapes.Polygon(
                cx - 0.30f * w, cy + 0.05f * w, cx - 0.30f * w, cy - 0.22f * w, cx - 0.15f * w, cy - 0.05f * w,
                cx, cy - 0.30f * w, cx + 0.15f * w, cy - 0.05f * w, cx + 0.30f * w, cy - 0.22f * w, cx + 0.30f * w, cy + 0.05f * w);
            var balls = Shapes.Union(Shapes.Circle(cx - 0.30f * w, cy - 0.24f * w, 0.04f * w), Shapes.Circle(cx, cy - 0.32f * w, 0.045f * w), Shapes.Circle(cx + 0.30f * w, cy - 0.24f * w, 0.04f * w));
            return Shapes.Union(band, spikes, balls);
        }

        /// <summary>Золотая корона с камнями.</summary>
        public static void CrownOfElitism(IconCanvas c)
        {
            var gold = Rgba.Hex("#f2c230");
            c.Fill(CrownShape(0.5f, 0.55f, 1.3f), Paints.Lit(gold, 0.55f));
            c.Fill(Shapes.Box(0.5f, 0.68f, 0.39f, 0.03f), gold.Shade(0.7f));
            c.Fill(Shapes.Circle(0.5f, 0.67f, 0.045f), Paints.Radial(0.49f, 0.66f, 0.045f, Rgba.Hex("#ff8a9a"), Blood));
            c.Fill(Shapes.Union(Shapes.Circle(0.32f, 0.67f, 0.03f), Shapes.Circle(0.68f, 0.67f, 0.03f)), Paints.Radial(0.5f, 0.66f, 0.3f, Rgba.Hex("#9ad0ff"), LunarBlue));
            c.Fill(Shapes.Ellipse(0.36f, 0.45f, 0.04f, 0.08f), Rgba.White.WithAlpha(0.35f));
            c.Outline(OutlineWidth, Ink);
        }

        /// <summary>Страх льва: силуэт оскаленной морды (грива-звезда без деталей).</summary>
        public static void BuffLionFear(IconCanvas c) =>
            c.Fill(Shapes.Subtract(Shapes.Grow(Shapes.Star(0.5f, 0.5f, 0.42f, 0.32f, 12, 0f), 0.02f),
                Shapes.Union(Shapes.Ellipse(0.40f, 0.46f, 0.05f, 0.035f), Shapes.Ellipse(0.60f, 0.46f, 0.05f, 0.035f), Shapes.Polygon(0.42f, 0.62f, 0.58f, 0.62f, 0.5f, 0.70f))), Rgba.White);

        /// <summary>Власть: силуэт короны.</summary>
        public static void BuffCrownReign(IconCanvas c) => c.Fill(CrownShape(0.5f, 0.56f, 1.4f), Rgba.White);

        /// <summary>Гнёт: корона над стрелкой вниз.</summary>
        public static void BuffCrownOppression(IconCanvas c) =>
            c.Fill(Shapes.Union(CrownShape(0.5f, 0.38f, 0.9f),
                Shapes.Grow(Shapes.Polygon(0.32f, 0.62f, 0.68f, 0.62f, 0.5f, 0.86f), 0.01f)), Rgba.White);
    }
}
