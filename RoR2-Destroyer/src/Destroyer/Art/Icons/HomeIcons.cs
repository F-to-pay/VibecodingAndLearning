namespace Destroyer.Art
{
    /// <summary>Иконки 0.5.0: лисий хвост, шарики, стул, батарейки, гиря, пылесос + их баффы.</summary>
    public static partial class IconBook
    {
        // ---------- Общие силуэты (их же используют баффы) ----------

        private static Sdf FoxTailShape()
        {
            // Цепочка кругов по изгибу, слитая мягко, и "мех" по краю.
            (float x, float y, float r)[] spine =
            {
                (0.20f, 0.90f, 0.035f), (0.22f, 0.80f, 0.07f), (0.27f, 0.66f, 0.12f), (0.36f, 0.52f, 0.16f),
                (0.49f, 0.41f, 0.185f), (0.63f, 0.35f, 0.17f), (0.75f, 0.36f, 0.13f), (0.84f, 0.42f, 0.08f), (0.89f, 0.49f, 0.04f),
            };
            Sdf tail = Shapes.Circle(spine[0].x, spine[0].y, spine[0].r);
            for (int i = 1; i < spine.Length; i++)
                tail = Shapes.SmoothUnion(tail, Shapes.Circle(spine[i].x, spine[i].y, spine[i].r), 0.08f);
            return Shapes.Fuzz(tail, 0.009f, 85f);
        }

        private static Sdf BalloonShape(float cx, float cy, float rx, float ry) =>
            Shapes.Union(Shapes.Ellipse(cx, cy, rx, ry), Shapes.Polygon(cx - 0.025f, cy + ry + 0.035f, cx + 0.025f, cy + ry + 0.035f, cx, cy + ry - 0.02f));

        private static Sdf ChairShape()
        {
            var back = Shapes.Union(
                Shapes.Box(0.36f, 0.33f, 0.035f, 0.22f, 0.015f),
                Shapes.Box(0.64f, 0.33f, 0.035f, 0.22f, 0.015f),
                Shapes.Box(0.50f, 0.18f, 0.17f, 0.05f, 0.02f),
                Shapes.Box(0.50f, 0.32f, 0.15f, 0.025f, 0.01f));
            var seat = Shapes.Box(0.50f, 0.58f, 0.21f, 0.045f, 0.02f);
            var legs = Shapes.Union(
                Shapes.Box(0.33f, 0.74f, 0.03f, 0.13f, 0.012f),
                Shapes.Box(0.67f, 0.74f, 0.03f, 0.13f, 0.012f),
                Shapes.Box(0.50f, 0.78f, 0.15f, 0.018f, 0.008f));
            return Shapes.Union(back, seat, legs);
        }

        // ---------- Предметы ----------

        /// <summary>Лисий хвост: пушистый рыжий, белый кончик.</summary>
        public static void FoxTail(IconCanvas c)
        {
            var orange = Rgba.Hex("#f2802e");
            var tail = FoxTailShape();
            c.Fill(tail, Paints.Lit(orange, 0.55f, 0.3f, 0.3f));
            // Белый кончик.
            c.Fill(Shapes.Intersect(tail, Shapes.Fuzz(Shapes.Circle(0.95f, 0.50f, 0.21f), 0.01f, 90f)), Paints.Lit(Rgba.Hex("#fffdf8"), 0.15f));
            // Шерсть: несколько штрихов.
            c.Fill(Shapes.Intersect(Shapes.Union(
                Shapes.Capsule(0.28f, 0.62f, 0.33f, 0.55f, 0.011f),
                Shapes.Capsule(0.38f, 0.50f, 0.45f, 0.45f, 0.011f),
                Shapes.Capsule(0.50f, 0.44f, 0.58f, 0.41f, 0.011f),
                Shapes.Capsule(0.62f, 0.40f, 0.70f, 0.40f, 0.011f),
                Shapes.Capsule(0.43f, 0.55f, 0.52f, 0.50f, 0.011f)), Shapes.Grow(tail, -0.03f)), orange.Shade(0.7f));
            // Блик по спинке.
            c.Fill(Shapes.Intersect(Shapes.Arc(0.58f, 0.66f, 0.38f, -62f, -8f, 0.03f), tail), Rgba.White.WithAlpha(0.3f));
            c.Outline(OutlineWidth, Ink);
        }

        /// <summary>Шарики: три ярких воздушных шара на ниточках.</summary>
        public static void Balloons(IconCanvas c)
        {
            // Ниточки — сзади.
            c.Fill(Shapes.Union(
                Shapes.Capsule(0.31f, 0.56f, 0.48f, 0.90f, 0.008f),
                Shapes.Capsule(0.66f, 0.50f, 0.52f, 0.90f, 0.008f),
                Shapes.Capsule(0.50f, 0.70f, 0.50f, 0.90f, 0.008f)), Rgba.Hex("#e8e4dc"));

            (float x, float y, float rx, float ry, string color)[] balloons =
            {
                (0.31f, 0.33f, 0.15f, 0.19f, "#ff3d8b"),
                (0.67f, 0.28f, 0.15f, 0.19f, "#2ec9ff"),
                (0.50f, 0.48f, 0.15f, 0.19f, "#ffd23a"),
            };
            foreach (var b in balloons)
            {
                var color = Rgba.Hex(b.color);
                c.Fill(BalloonShape(b.x, b.y, b.rx, b.ry), Paints.Radial(b.x - 0.05f, b.y - 0.07f, 0.26f, color.Tint(0.45f), color.Shade(0.75f)));
                c.Fill(Shapes.Ellipse(b.x - 0.055f, b.y - 0.08f, 0.03f, 0.05f), Rgba.White.WithAlpha(0.55f));
            }
            c.Outline(OutlineWidth, Ink);
        }

        /// <summary>Стул: деревянный, со спинкой.</summary>
        public static void Chair(IconCanvas c)
        {
            var wood = Rgba.Hex("#b5793f");
            c.Fill(ChairShape(), Paints.Lit(wood, 0.5f, 0.3f, 0.2f));
            // Сиденье светлее — на него падает свет.
            c.Fill(Shapes.Box(0.50f, 0.565f, 0.20f, 0.02f, 0.01f), wood.Tint(0.3f));
            c.Outline(OutlineWidth, Ink);
        }

        /// <summary>Батарейки: две пальчиковые, одна поверх другой, и искра.</summary>
        public static void Batteries(IconCanvas c)
        {
            void Battery(float cx, float cy, float angle, Rgba body)
            {
                Sdf R(Sdf s) => Shapes.Rotate(s, cx, cy, angle);
                c.Fill(R(Shapes.Box(cx, cy - 0.24f, 0.035f, 0.025f, 0.01f)), Paints.Lit(Rgba.Hex("#d9dde2"), 0.6f));
                var shell = R(Shapes.Box(cx, cy, 0.085f, 0.22f, 0.03f));
                c.Fill(shell, Paints.Lit(body, 0.55f));
                c.Fill(Shapes.Intersect(R(Shapes.Box(cx, cy - 0.14f, 0.1f, 0.075f)), shell), Paints.Lit(Rgba.Hex("#c98a2e"), 0.5f));
                c.Fill(R(Shapes.Union(Shapes.Box(cx, cy - 0.14f, 0.032f, 0.009f), Shapes.Box(cx, cy - 0.14f, 0.009f, 0.032f))), Rgba.White.WithAlpha(0.9f));
                c.Fill(Shapes.Intersect(R(Shapes.Capsule(cx - 0.055f, cy - 0.05f, cx - 0.055f, cy + 0.18f, 0.012f)), shell), Rgba.White.WithAlpha(0.3f));
            }

            Battery(0.40f, 0.52f, -18f, Rgba.Hex("#2f3a44"));
            Battery(0.60f, 0.50f, 14f, Rgba.Hex("#3e8a4b"));
            // Молния.
            c.Fill(Shapes.Polygon(0.80f, 0.10f, 0.70f, 0.28f, 0.77f, 0.28f, 0.71f, 0.44f, 0.88f, 0.22f, 0.80f, 0.22f, 0.86f, 0.10f),
                   Paints.Vertical(Ember.Tint(0.4f), Fire, 0.1f, 0.44f));
            c.Outline(OutlineWidth, Ink);
        }

        /// <summary>Гиря: чугунный шар с ручкой.</summary>
        public static void Kettlebell(IconCanvas c)
        {
            var iron = Rgba.Hex("#434950");
            var handle = Shapes.Subtract(Shapes.Box(0.5f, 0.32f, 0.2f, 0.17f, 0.15f), Shapes.Box(0.5f, 0.34f, 0.115f, 0.1f, 0.09f));
            c.Fill(handle, Paints.Lit(iron, 0.6f));
            var bell = Shapes.Union(Shapes.Circle(0.5f, 0.62f, 0.27f), Shapes.Box(0.5f, 0.87f, 0.16f, 0.03f, 0.02f));
            c.Fill(bell, Paints.Lit(iron, 0.65f, 0.3f, 0.4f));
            c.Fill(Shapes.Ellipse(0.40f, 0.52f, 0.06f, 0.04f), Rgba.White.WithAlpha(0.35f));
            // Литая полоска "веса".
            c.Fill(Shapes.Intersect(Shapes.Box(0.5f, 0.64f, 0.09f, 0.05f, 0.02f), bell), Rgba.Hex("#e0c25a"));
            c.Outline(OutlineWidth, Ink);
        }

        /// <summary>Пылесос: красный корпус, шланг, щётка и засасываемая пыль.</summary>
        public static void Vacuum(IconCanvas c)
        {
            var red = Rgba.Hex("#d93a3a");
            var gray = Rgba.Hex("#8f98a2");
            // Пыль и монетки, которые летят к щётке.
            c.Fill(Shapes.Union(Shapes.Circle(0.86f, 0.66f, 0.018f), Shapes.Circle(0.92f, 0.74f, 0.012f), Shapes.Circle(0.80f, 0.58f, 0.012f)), Rgba.Hex("#c9c1b0"));
            c.Fill(Shapes.Circle(0.88f, 0.86f, 0.03f), Paints.Lit(Ember, 0.5f));
            // Шланг: дуга от корпуса к трубке.
            c.Fill(Shapes.Arc(0.46f, 0.40f, 0.20f, 10f, 110f, 0.045f), Paints.Lit(gray.Shade(0.8f), 0.4f));
            c.Fill(Shapes.Capsule(0.65f, 0.47f, 0.76f, 0.80f, 0.028f), Paints.Lit(Steel, 0.6f));
            c.Fill(Shapes.Box(0.76f, 0.83f, 0.10f, 0.03f, 0.015f), Paints.Lit(Rgba.Hex("#2b2f35"), 0.5f));
            // Корпус и колесо.
            var body = Shapes.Ellipse(0.34f, 0.62f, 0.22f, 0.17f);
            c.Fill(body, Paints.Lit(red, 0.55f, 0.25f, 0.45f));
            c.Fill(Shapes.Circle(0.30f, 0.79f, 0.07f), Paints.Lit(Rgba.Hex("#25282d"), 0.5f));
            c.Fill(Shapes.Circle(0.30f, 0.79f, 0.025f), gray);
            c.Fill(Shapes.Intersect(Shapes.Box(0.34f, 0.60f, 0.22f, 0.02f), body), red.Shade(0.65f));
            c.Fill(Shapes.Ellipse(0.26f, 0.53f, 0.06f, 0.03f), Rgba.White.WithAlpha(0.35f));
            c.Outline(OutlineWidth, Ink);
        }

        // ---------- Баффы ----------

        /// <summary>Перезарядка лисьего хвоста: силуэт хвоста.</summary>
        public static void BuffFoxTailCooldown(IconCanvas c) => c.Fill(FoxTailShape(), Rgba.White);

        /// <summary>"Сидит": силуэт стула.</summary>
        public static void BuffChairSeated(IconCanvas c) => c.Fill(ChairShape(), Rgba.White);

        /// <summary>Метка шарика (скрытая): силуэт шара.</summary>
        public static void BuffBalloon(IconCanvas c) => c.Fill(BalloonShape(0.5f, 0.42f, 0.24f, 0.3f), Rgba.White);
    }
}
