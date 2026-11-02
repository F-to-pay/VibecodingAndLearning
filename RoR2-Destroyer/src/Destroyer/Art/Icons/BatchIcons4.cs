namespace Destroyer.Art
{
    /// <summary>Артиллерийский запал, Deus Ex Machina, первоцвет.</summary>
    public static partial class IconBook
    {
        /// <summary>Связка из трёх ракет с горящим фитилём.</summary>
        public static void ArtilleryFuse(IconCanvas c)
        {
            var body = Rgba.Hex("#d23a2a");
            var band = Rgba.Hex("#f2d06b");

            Sdf Rocket(float x, float y, float tilt)
            {
                var tube = Shapes.Box(x, y, 0.055f, 0.19f, 0.025f);
                var nose = Shapes.Polygon(x - 0.055f, y - 0.18f, x + 0.055f, y - 0.18f, x, y - 0.30f);
                var fins = Shapes.Polygon(x - 0.085f, y + 0.21f, x + 0.085f, y + 0.21f, x + 0.055f, y + 0.11f, x - 0.055f, y + 0.11f);
                return Shapes.Rotate(Shapes.Union(Shapes.Grow(nose, 0.01f), tube, fins), x, y, tilt);
            }

            var left = Rocket(0.24f, 0.58f, -12f);
            var right = Rocket(0.76f, 0.58f, 12f);
            var mid = Rocket(0.50f, 0.48f, 0f);
            c.Fill(left, Paints.Lit(body.Shade(0.85f), 0.4f));
            c.Fill(right, Paints.Lit(body.Shade(0.85f), 0.4f));
            c.Fill(mid, Paints.Lit(body, 0.5f));

            // Обвязка.
            c.Fill(Shapes.Intersect(Shapes.Box(0.5f, 0.60f, 0.45f, 0.025f), Shapes.Union(left, right, mid)), band);

            // Фитиль и искра.
            c.Fill(Shapes.Capsule(0.50f, 0.69f, 0.56f, 0.86f, 0.014f), Rgba.Hex("#c9a46a"));
            c.Fill(Shapes.Star(0.58f, 0.88f, 0.07f, 0.025f, 8, 10f), Paints.Radial(0.58f, 0.88f, 0.07f, Rgba.White, Fire));

            c.Outline(OutlineWidth, Ink);
        }

        /// <summary>Шестерня с нимбом и силуэтом-двойником внутри.</summary>
        public static void DeusExMachina(IconCanvas c)
        {
            var gold = Rgba.Hex("#e8c25a");
            var dark = Rgba.Hex("#4a3a1a");

            // Шестерня: звезда со скруглением минус дырка.
            var gear = Shapes.Grow(Shapes.Star(0.5f, 0.56f, 0.34f, 0.27f, 10, 0f), 0.015f);
            var hole = Shapes.Circle(0.5f, 0.56f, 0.17f);
            c.Fill(Shapes.Subtract(gear, hole), Paints.Lit(gold, 0.5f));
            c.Fill(hole, Paints.Radial(0.5f, 0.56f, 0.17f, Rgba.Hex("#fff3c4"), gold.Shade(0.8f)));

            // Силуэт человечка в центре.
            var person = Shapes.Union(
                Shapes.Circle(0.5f, 0.49f, 0.04f),
                Shapes.Intersect(Shapes.Ellipse(0.5f, 0.66f, 0.085f, 0.11f), Shapes.Box(0.5f, 0.61f, 0.1f, 0.07f)));
            c.Fill(person, dark);

            // Нимб.
            c.Fill(Shapes.Ring(0.5f, 0.10f, 0.10f, 0.03f), Ember);

            c.Outline(OutlineWidth, Ink);
        }

        /// <summary>Первоцвет: пятилепестковый пустотный цветок с тёмной сердцевиной и шипами-искрами.</summary>
        public static void Primrose(IconCanvas c)
        {
            var petal = Rgba.Hex("#b46bff");
            var deep = Rgba.Hex("#3b1966");

            // Стебель и лист.
            c.Fill(Shapes.Capsule(0.5f, 0.62f, 0.44f, 0.92f, 0.022f), Rgba.Hex("#5a3d8a"));
            c.Fill(Shapes.Rotate(Shapes.Ellipse(0.36f, 0.82f, 0.09f, 0.035f), 0.36f, 0.82f, -30f), Rgba.Hex("#7a52b8"));

            // Пять лепестков-сердечек вокруг центра.
            Sdf flower = null;
            for (int i = 0; i < 5; i++)
            {
                float a = i * 72f * (float)System.Math.PI / 180f;
                float x = 0.5f + 0.17f * (float)System.Math.Sin(a), y = 0.42f - 0.17f * (float)System.Math.Cos(a);
                var leaf = Shapes.Circle(x, y, 0.135f);
                flower = flower == null ? leaf : Shapes.SmoothUnion(flower, leaf, 0.03f);
            }
            c.Fill(flower, Paints.Radial(0.5f, 0.42f, 0.33f, petal.Tint(0.35f), deep));

            // Прожилки к центру.
            for (int i = 0; i < 5; i++)
            {
                float a = i * 72f * (float)System.Math.PI / 180f;
                c.Fill(Shapes.Capsule(0.5f, 0.42f, 0.5f + 0.22f * (float)System.Math.Sin(a), 0.42f - 0.22f * (float)System.Math.Cos(a), 0.008f), deep.WithAlpha(0.6f));
            }

            // Сердцевина.
            c.Fill(Shapes.Circle(0.5f, 0.42f, 0.075f), Paints.Radial(0.48f, 0.40f, 0.075f, Rgba.Hex("#ffe27a"), Rgba.Hex("#c77a1a")));

            c.Fill(Shapes.Union(Shapes.Circle(0.14f, 0.18f, 0.02f), Shapes.Circle(0.86f, 0.16f, 0.025f), Shapes.Circle(0.86f, 0.72f, 0.018f)), VoidPurple);
            c.Outline(OutlineWidth, Ink);
        }
    }
}
