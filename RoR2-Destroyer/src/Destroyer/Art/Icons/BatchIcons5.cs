namespace Destroyer.Art
{
    /// <summary>Мультивитамин, Ненависть, Пешка, Книга высасывания жизни, Холодные объятья и их баффы.</summary>
    public static partial class IconBook
    {
        public static void Multivitamin(IconCanvas c)
        {
            // Капсула пополам: оранжевая и белая половины, наискосок.
            var capsule = Shapes.Rotate(Shapes.Capsule(0.32f, 0.5f, 0.68f, 0.5f, 0.17f), 0.5f, 0.5f, -35f);
            var half = Shapes.Rotate(Shapes.Box(0.30f, 0.5f, 0.2f, 0.3f), 0.5f, 0.5f, -35f);
            c.Fill(capsule, Paints.Lit(Rgba.Hex("#f2f2ec"), 0.4f));
            c.Fill(Shapes.Intersect(capsule, half), Paints.Lit(Rgba.Hex("#ff8a1f"), 0.5f));
            c.Fill(Shapes.Union(Shapes.Circle(0.72f, 0.24f, 0.03f), Shapes.Circle(0.80f, 0.34f, 0.02f), Shapes.Circle(0.22f, 0.76f, 0.025f)), Ember);
            c.Fill(Shapes.Rotate(Shapes.Capsule(0.36f, 0.43f, 0.58f, 0.43f, 0.025f), 0.5f, 0.5f, -35f), Rgba.White.WithAlpha(0.5f));
            c.Outline(OutlineWidth, Ink);
        }

        public static void Hatred(IconCanvas c)
        {
            // Лунное сердце с трещиной и злыми бровями-шипами.
            var heart = Heart(0.5f, 0.42f, 0.30f);
            c.Fill(heart, Paints.Radial(0.45f, 0.38f, 0.4f, Rgba.Hex("#8fc4ff"), Rgba.Hex("#1f3a8a")));
            c.Fill(Shapes.Union(
                Shapes.Capsule(0.50f, 0.30f, 0.45f, 0.45f, 0.018f), Shapes.Capsule(0.45f, 0.45f, 0.55f, 0.56f, 0.018f),
                Shapes.Capsule(0.55f, 0.56f, 0.49f, 0.70f, 0.018f)), Rgba.Hex("#0c1636"));
            c.Fill(Shapes.Union(Shapes.Polygon(0.22f, 0.20f, 0.40f, 0.34f, 0.36f, 0.38f), Shapes.Polygon(0.78f, 0.20f, 0.60f, 0.34f, 0.64f, 0.38f)), Blood);
            c.Outline(OutlineWidth, Ink);
        }

        private static Sdf PawnShape() => Shapes.Union(
            Shapes.Circle(0.5f, 0.26f, 0.11f),
            Shapes.Box(0.5f, 0.40f, 0.12f, 0.03f, 0.02f),
            Shapes.Polygon(0.42f, 0.42f, 0.58f, 0.42f, 0.66f, 0.74f, 0.34f, 0.74f),
            Shapes.Box(0.5f, 0.80f, 0.24f, 0.07f, 0.03f));

        public static void Pawn(IconCanvas c)
        {
            c.Fill(PawnShape(), Paints.Lit(Rgba.Hex("#e8e0c8"), 0.55f));
            c.Fill(Shapes.Box(0.5f, 0.80f, 0.24f, 0.015f), Rgba.Hex("#a89a78"));
            c.Outline(OutlineWidth, Ink);
        }

        public static void LifeDrainBook(IconCanvas c)
        {
            var cover = Rgba.Hex("#4a0f1e");
            // Книга чуть повёрнута: обложка, корешок, страницы.
            var book = Shapes.Rotate(Shapes.Box(0.48f, 0.54f, 0.26f, 0.32f, 0.03f), 0.5f, 0.5f, -8f);
            var pages = Shapes.Rotate(Shapes.Box(0.52f, 0.56f, 0.22f, 0.29f, 0.01f), 0.5f, 0.5f, -8f);
            c.Fill(Shapes.Move(pages, 0.03f, 0.01f), Rgba.Hex("#e8dcc0"));
            c.Fill(book, Paints.Lit(cover, 0.5f));
            c.Fill(Shapes.Rotate(Shapes.Box(0.25f, 0.54f, 0.03f, 0.32f), 0.5f, 0.5f, -8f), cover.Shade(0.6f));
            // Капля крови на обложке.
            var drop = Shapes.SmoothUnion(Shapes.Circle(0.5f, 0.58f, 0.09f), Shapes.Polygon(0.43f, 0.55f, 0.57f, 0.55f, 0.5f, 0.38f), 0.03f);
            c.Fill(drop, Paints.Radial(0.47f, 0.55f, 0.12f, Rgba.Hex("#ff5a6a"), Blood));
            c.Outline(OutlineWidth, Ink);
        }

        private static Sdf Snowflake(float cx, float cy, float r, float t)
        {
            Sdf flake = null;
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * (float)System.Math.PI / 180f;
                float sx = (float)System.Math.Sin(a), cy0 = (float)System.Math.Cos(a);
                var arm = Shapes.Capsule(cx, cy, cx + r * sx, cy - r * cy0, t);
                // Веточки.
                float bx = cx + 0.6f * r * sx, by = cy - 0.6f * r * cy0;
                float a1 = a + 0.8f, a2 = a - 0.8f;
                var twigs = Shapes.Union(
                    Shapes.Capsule(bx, by, bx + 0.3f * r * (float)System.Math.Sin(a1), by - 0.3f * r * (float)System.Math.Cos(a1), t * 0.8f),
                    Shapes.Capsule(bx, by, bx + 0.3f * r * (float)System.Math.Sin(a2), by - 0.3f * r * (float)System.Math.Cos(a2), t * 0.8f));
                var both = Shapes.Union(arm, twigs);
                flake = flake == null ? both : Shapes.Union(flake, both);
            }
            return flake;
        }

        public static void ColdEmbrace(IconCanvas c)
        {
            // Лунная сфера, внутри снежинка, вокруг "объятье" — дуги.
            c.Fill(Shapes.Circle(0.5f, 0.5f, 0.34f), Paints.Radial(0.42f, 0.40f, 0.42f, Rgba.Hex("#e6f6ff"), Rgba.Hex("#2d6fb3")));
            c.Fill(Snowflake(0.5f, 0.5f, 0.24f, 0.022f), Rgba.White);
            c.Fill(Shapes.Arc(0.5f, 0.5f, 0.42f, 200f, 320f, 0.05f), LunarBlue.Shade(0.8f));
            c.Fill(Shapes.Arc(0.5f, 0.5f, 0.42f, 20f, 140f, 0.05f), LunarBlue.Shade(0.8f));
            c.Outline(OutlineWidth, Ink);
        }

        public static void BuffPawnGuard(IconCanvas c) => c.Fill(PawnShape(), Rgba.White);

        public static void BuffLifeDrain(IconCanvas c) =>
            c.Fill(Shapes.SmoothUnion(Shapes.Circle(0.5f, 0.60f, 0.22f), Shapes.Polygon(0.32f, 0.54f, 0.68f, 0.54f, 0.5f, 0.12f), 0.06f), Rgba.White);

        public static void BuffColdEmbraceStacks(IconCanvas c) => c.Fill(Snowflake(0.5f, 0.5f, 0.40f, 0.05f), Rgba.White);
    }
}
