namespace Destroyer.Art
{
    public static partial class IconBook
    {
        /// <summary>Бензопила: оранжевый корпус с ручкой, шина с цепью-зубьями, смотрит вправо-вверх.</summary>
        public static void Chainsaw(IconCanvas c)
        {
            var orange = Rgba.Hex("#ff8a1f");
            var steel = Rgba.Hex("#b9c1c9");

            // Шина с зубьями (повёрнута на −25°).
            const float ang = -25f;
            var bar = Shapes.Box(0.62f, 0.50f, 0.27f, 0.075f, 0.075f);
            Sdf teeth = null;
            for (int i = 0; i < 9; i++)
            {
                float x = 0.40f + i * 0.058f;
                var top = Shapes.Polygon(x - 0.022f, 0.425f, x + 0.022f, 0.425f, x + 0.03f, 0.385f);
                var bottom = Shapes.Polygon(x - 0.022f, 0.575f, x + 0.022f, 0.575f, x - 0.03f, 0.615f);
                var pair = Shapes.Union(top, bottom);
                teeth = teeth == null ? pair : Shapes.Union(teeth, pair);
            }
            var chain = Shapes.Rotate(Shapes.Union(bar, teeth), 0.5f, 0.5f, ang);
            c.Fill(chain, Paints.Lit(steel, 0.5f));
            c.Fill(Shapes.Rotate(Shapes.Box(0.64f, 0.50f, 0.2f, 0.018f, 0.018f), 0.5f, 0.5f, ang), DarkSteel);

            // Корпус и ручка.
            var body = Shapes.Box(0.30f, 0.62f, 0.17f, 0.13f, 0.04f);
            var handle = Shapes.Outline(Shapes.Box(0.27f, 0.43f, 0.11f, 0.07f, 0.05f), 0.04f);
            c.Fill(Shapes.Intersect(handle, Shapes.Box(0.27f, 0.42f, 0.2f, 0.1f)), DarkSteel);
            c.Fill(body, Paints.Lit(orange, 0.55f));
            c.Fill(Shapes.Box(0.24f, 0.66f, 0.06f, 0.035f, 0.01f), Rgba.Hex("#2a2d33"));

            // Выхлоп-дымок.
            c.Fill(Shapes.Union(Shapes.Circle(0.10f, 0.78f, 0.035f), Shapes.Circle(0.06f, 0.70f, 0.025f)), Rgba.Hex("#9aa0a8").WithAlpha(0.8f));

            c.Outline(OutlineWidth, Ink);
        }

        /// <summary>Силуэт пилы для баффов: шина с зубьями и корпус.</summary>
        private static Sdf ChainsawShape()
        {
            var bar = Shapes.Box(0.62f, 0.50f, 0.27f, 0.08f, 0.08f);
            Sdf teeth = null;
            for (int i = 0; i < 7; i++)
            {
                float x = 0.42f + i * 0.07f;
                var pair = Shapes.Union(
                    Shapes.Polygon(x - 0.028f, 0.44f, x + 0.028f, 0.44f, x + 0.035f, 0.37f),
                    Shapes.Polygon(x - 0.028f, 0.56f, x + 0.028f, 0.56f, x - 0.035f, 0.63f));
                teeth = teeth == null ? pair : Shapes.Union(teeth, pair);
            }
            var saw = Shapes.Union(bar, teeth, Shapes.Box(0.26f, 0.56f, 0.14f, 0.15f, 0.04f));
            return Shapes.Rotate(saw, 0.5f, 0.5f, -25f);
        }

        public static void BuffChainsawActive(IconCanvas c) => c.Fill(ChainsawShape(), Rgba.White);

        public static void BuffChainsawCooldown(IconCanvas c) =>
            c.Fill(Shapes.Subtract(ChainsawShape(), Shapes.Rotate(Shapes.Box(0.62f, 0.50f, 0.2f, 0.025f), 0.5f, 0.5f, -25f)), Rgba.White);
    }
}
