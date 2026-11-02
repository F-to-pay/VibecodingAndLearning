namespace Destroyer.Art
{
    /// <summary>Иконки предметов. Координаты 0..1, (0,0) — левый верхний угол. Проверка: build.cmd → logs\icons\_sheet.png.</summary>
    public static partial class IconBook
    {
        /// <summary>Чоризо: изогнутая колбаса с жиринками и завязками на концах.</summary>
        public static void Chorizo(IconCanvas c)
        {
            const float cx = 0.5f, cy = 0.84f, r = 0.36f, thick = 0.23f, end = 52f;
            var meat = Rgba.Hex("#a8281d");
            var fat = Rgba.Hex("#f3d2bb");
            var twine = Rgba.Hex("#d9c08d");

            // Точка на дуге колбасы: угол (0 — верх, по часовой) и расстояние от центра дуги.
            (float x, float y) P(float angle, float radius)
            {
                double a = angle * System.Math.PI / 180.0;
                return (cx + radius * (float)System.Math.Sin(a), cy - radius * (float)System.Math.Cos(a));
            }

            // Завязки торчат из концов по касательной.
            foreach (float side in new[] { -1f, 1f })
            {
                var tip = P(side * (end + 16f), r);
                c.Fill(Shapes.Capsule(tip.x, tip.y, tip.x + side * 0.035f, tip.y + 0.065f, 0.018f), twine);
            }

            var sausage = Shapes.Arc(cx, cy, r, -end, end, thick);
            c.Fill(sausage, Paints.Lit(meat, 0.55f, 0.35f, 0.3f));

            // Жиринки — только внутри колбасы.
            var specks = new System.Collections.Generic.List<Sdf>();
            float[,] spots = { { -44, 0.40f }, { -33, 0.33f }, { -20, 0.39f }, { -8, 0.32f }, { 4, 0.40f }, { 16, 0.34f }, { 29, 0.39f }, { 41, 0.33f }, { -2, 0.36f } };
            for (int i = 0; i < spots.GetLength(0); i++)
            {
                var p = P(spots[i, 0], spots[i, 1]);
                float size = i % 2 == 0 ? 0.024f : 0.018f;
                specks.Add(Shapes.Ellipse(p.x, p.y, size, size * 0.7f));
            }
            c.Fill(Shapes.Intersect(Shapes.Union(specks.ToArray()), Shapes.Grow(sausage, -0.02f)), fat.WithAlpha(0.9f));

            // Перетяжки оболочки у концов.
            foreach (float side in new[] { -1f, 1f })
            {
                var a = P(side * (end - 8f), r - 0.13f);
                var b = P(side * (end - 8f), r + 0.13f);
                c.Fill(Shapes.Intersect(Shapes.Capsule(a.x, a.y, b.x, b.y, 0.01f), sausage), meat.Shade(0.55f));
            }

            // Блик вдоль спины.
            c.Fill(Shapes.Arc(cx, cy, r + 0.06f, -36f, 28f, 0.028f), Rgba.White.WithAlpha(0.35f));

            c.Outline(OutlineWidth, Ink);
        }

        /// <summary>Баллончик с краской: серебристый корпус, розовая этикетка и облако краски.</summary>
        public static void PaintCan(IconCanvas c)
        {
            var pink = Rgba.Hex("#ff3d8b");
            var silver = Rgba.Hex("#b9c1c9");

            // Облако краски — сзади, чтобы обводка корпуса легла поверх.
            var cloud = Shapes.SmoothUnion(Shapes.SmoothUnion(
                    Shapes.Circle(0.70f, 0.21f, 0.065f), Shapes.Circle(0.80f, 0.15f, 0.08f), 0.05f),
                Shapes.Circle(0.84f, 0.29f, 0.06f), 0.05f);
            c.Fill(cloud, Paints.Radial(0.62f, 0.22f, 0.32f, pink.Tint(0.45f), pink));
            c.Fill(Shapes.Union(Shapes.Circle(0.92f, 0.08f, 0.018f), Shapes.Circle(0.93f, 0.40f, 0.02f), Shapes.Circle(0.66f, 0.34f, 0.015f)), pink);

            // Корпус и плечи.
            var body = Shapes.Box(0.40f, 0.61f, 0.17f, 0.27f, 0.035f);
            var shoulder = Shapes.Ellipse(0.40f, 0.35f, 0.17f, 0.08f);
            c.Fill(Shapes.Union(body, shoulder), Paints.Lit(silver, 0.6f, 0.28f, 0.3f));

            // Этикетка с потёком.
            var label = Shapes.Union(Shapes.Box(0.40f, 0.63f, 0.17f, 0.11f), Shapes.Capsule(0.31f, 0.70f, 0.31f, 0.80f, 0.022f));
            c.Fill(Shapes.Intersect(label, body), Paints.Vertical(pink.Tint(0.15f), pink.Shade(0.75f), 0.5f, 0.8f));
            c.Fill(Shapes.Intersect(Shapes.Box(0.40f, 0.54f, 0.17f, 0.012f), body), Rgba.White.WithAlpha(0.5f));

            // Колпачок и распылитель.
            c.Fill(Shapes.Box(0.40f, 0.255f, 0.075f, 0.05f, 0.02f), Paints.Lit(pink, 0.5f));
            c.Fill(Shapes.Box(0.50f, 0.235f, 0.04f, 0.018f, 0.008f), Rgba.Hex("#2a2d33"));

            // Блик на корпусе.
            c.Fill(Shapes.Capsule(0.29f, 0.42f, 0.29f, 0.82f, 0.018f), Rgba.White.WithAlpha(0.35f));

            c.Outline(OutlineWidth, Ink);
        }

        /// <summary>Нитроглицериновая вата: пушистый комок с маслянистым пятном и искрами.</summary>
        public static void NitroCotton(IconCanvas c)
        {
            var cotton = Rgba.Hex("#f3eedf");
            var oil = Rgba.Hex("#f2c230");

            var puff = Shapes.SmoothUnion(Shapes.SmoothUnion(Shapes.SmoothUnion(Shapes.SmoothUnion(Shapes.SmoothUnion(
                Shapes.Circle(0.50f, 0.62f, 0.19f),
                Shapes.Circle(0.32f, 0.64f, 0.14f), 0.06f),
                Shapes.Circle(0.68f, 0.63f, 0.14f), 0.06f),
                Shapes.Circle(0.41f, 0.46f, 0.14f), 0.06f),
                Shapes.Circle(0.60f, 0.47f, 0.13f), 0.06f),
                Shapes.Circle(0.50f, 0.78f, 0.12f), 0.06f);
            c.Fill(puff, Paints.Lit(cotton, 0.5f, 0.3f, 0.35f));

            // Пропитка нитроглицерином.
            var stain = Shapes.SmoothUnion(Shapes.Ellipse(0.52f, 0.66f, 0.13f, 0.08f), Shapes.Circle(0.40f, 0.72f, 0.06f), 0.05f);
            c.Fill(Shapes.Intersect(stain, Shapes.Grow(puff, -0.03f)), Paints.Radial(0.5f, 0.64f, 0.16f, oil.Tint(0.3f), oil.Shade(0.8f)), 0.75f);

            // Искры.
            c.Fill(Shapes.Star(0.73f, 0.28f, 0.11f, 0.04f, 6, 15f), Paints.Radial(0.73f, 0.28f, 0.11f, Rgba.White, Fire));
            c.Fill(Shapes.Star(0.86f, 0.42f, 0.06f, 0.022f, 5, 0f), Paints.Radial(0.86f, 0.42f, 0.06f, Ember, Fire));
            c.Fill(Shapes.Star(0.61f, 0.17f, 0.045f, 0.017f, 5, 20f), Paints.Radial(0.61f, 0.17f, 0.045f, Ember, Fire));

            c.Outline(OutlineWidth, Ink);
        }

        /// <summary>Смартфон с меткой на карте, слегка наклонён.</summary>
        public static void Smartphone(IconCanvas c)
        {
            const float tilt = 12f;
            Sdf R(Sdf s) => Shapes.Rotate(s, 0.5f, 0.5f, tilt);

            var body = R(Shapes.Box(0.5f, 0.5f, 0.21f, 0.37f, 0.06f));
            c.Fill(body, Paints.Lit(Rgba.Hex("#2b3038"), 0.5f));

            var screen = R(Shapes.Box(0.5f, 0.5f, 0.175f, 0.30f, 0.025f));
            c.Fill(screen, Paints.Linear(0.3f, 0.2f, 0.7f, 0.8f, Rgba.Hex("#4fb0ff"), Rgba.Hex("#1b3f86")));

            // Сетка улиц на экране.
            var streets = Shapes.Union(
                Shapes.Capsule(0.30f, 0.38f, 0.70f, 0.30f, 0.008f),
                Shapes.Capsule(0.30f, 0.66f, 0.70f, 0.58f, 0.008f),
                Shapes.Capsule(0.42f, 0.20f, 0.46f, 0.82f, 0.008f));
            c.Fill(Shapes.Intersect(R(streets), screen), Rgba.White.WithAlpha(0.3f));

            // Метка: капля острием вниз с дыркой.
            var pin = Shapes.SmoothUnion(Shapes.Circle(0.54f, 0.42f, 0.085f),
                                         Shapes.Polygon(0.475f, 0.45f, 0.605f, 0.45f, 0.54f, 0.60f), 0.02f);
            pin = Shapes.Subtract(pin, Shapes.Circle(0.54f, 0.42f, 0.032f));
            c.Fill(R(pin), Paints.Lit(Rgba.Hex("#ff4a3d"), 0.5f));

            // Динамик и блик.
            c.Fill(R(Shapes.Capsule(0.45f, 0.165f, 0.55f, 0.165f, 0.012f)), Rgba.Hex("#0e1013"));
            c.Fill(Shapes.Intersect(R(Shapes.Polygon(0.33f, 0.2f, 0.47f, 0.2f, 0.33f, 0.5f)), screen), Rgba.White.WithAlpha(0.18f));

            c.Outline(OutlineWidth, Ink);
        }

        /// <summary>Шампиньон: гладкая светлая шляпка, пластинки снизу, крепкая ножка.</summary>
        public static void Champignon(IconCanvas c)
        {
            var capColor = Rgba.Hex("#f1ebe2");
            var stemColor = Rgba.Hex("#e9dfcd");

            // Ножка: трапеция, слегка скруглённая.
            var stem = Shapes.Grow(Shapes.Polygon(0.42f, 0.52f, 0.58f, 0.52f, 0.61f, 0.84f, 0.39f, 0.84f), 0.025f);
            c.Fill(stem, Paints.Lit(stemColor, 0.5f, 0.35f, 0.5f));
            c.Fill(Shapes.Intersect(Shapes.Ellipse(0.5f, 0.6f, 0.15f, 0.03f), stem), stemColor.Shade(0.75f));

            // Пластинки под шляпкой.
            c.Fill(Shapes.Ellipse(0.5f, 0.5f, 0.33f, 0.07f), Paints.Vertical(Rgba.Hex("#b99f86"), Rgba.Hex("#8e745d"), 0.44f, 0.57f));

            // Шляпка: верхняя половина эллипса, низ чуть скруглён.
            var cap = Shapes.Intersect(Shapes.Ellipse(0.5f, 0.50f, 0.36f, 0.31f), Shapes.Box(0.5f, 0.25f, 0.6f, 0.25f));
            cap = Shapes.Grow(Shapes.Grow(cap, -0.02f), 0.02f);
            c.Fill(cap, Paints.Lit(capColor, 0.45f, 0.35f, 0.2f));

            // Светло-коричневые пятнышки и блик.
            c.Fill(Shapes.Intersect(Shapes.Union(
                Shapes.Ellipse(0.62f, 0.30f, 0.05f, 0.03f),
                Shapes.Ellipse(0.72f, 0.41f, 0.035f, 0.022f),
                Shapes.Ellipse(0.48f, 0.26f, 0.03f, 0.02f)), cap), Rgba.Hex("#cdb79c").WithAlpha(0.7f));
            c.Fill(Shapes.Arc(0.5f, 0.52f, 0.26f, -62f, -22f, 0.035f), Rgba.White.WithAlpha(0.6f));

            c.Outline(OutlineWidth, Ink);
        }
    }
}
