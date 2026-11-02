namespace Destroyer.Art
{
    public static partial class IconBook
    {
        /// <summary>"Я не прощу тебя": тряпичная кукла вуду со швами, глаз-крестик и лунные иголки.</summary>
        public static void Doll(IconCanvas c)
        {
            var burlap = Rgba.Hex("#c4a272");
            var thread = Rgba.Hex("#5e4529");
            var steel = Rgba.Hex("#d6dde4");

            // Иголки — сначала стержни (часть уходит "в куклу", поэтому рисуем до тела).
            (float hx, float hy, float tx, float ty)[] pins =
            {
                (0.80f, 0.14f, 0.58f, 0.34f),
                (0.15f, 0.50f, 0.40f, 0.60f),
                (0.83f, 0.70f, 0.58f, 0.63f),
            };
            foreach (var p in pins)
                c.Fill(Shapes.Capsule(p.hx, p.hy, p.tx, p.ty, 0.013f), Paints.Linear(p.hx, p.hy, p.tx, p.ty, steel, steel.Shade(0.6f)));

            // Тело одним силуэтом: голова, туловище, руки, ноги, слитые мягко.
            var head = Shapes.Circle(0.5f, 0.29f, 0.165f);
            var torso = Shapes.Box(0.5f, 0.60f, 0.135f, 0.19f, 0.07f);
            var arms = Shapes.Union(
                Shapes.Capsule(0.40f, 0.50f, 0.22f, 0.43f, 0.055f),
                Shapes.Capsule(0.60f, 0.50f, 0.78f, 0.43f, 0.055f));
            var legs = Shapes.Union(
                Shapes.Capsule(0.45f, 0.72f, 0.39f, 0.88f, 0.06f),
                Shapes.Capsule(0.55f, 0.72f, 0.61f, 0.88f, 0.06f));
            var doll = Shapes.SmoothUnion(Shapes.SmoothUnion(Shapes.SmoothUnion(head, torso, 0.06f), arms, 0.05f), legs, 0.05f);
            c.Fill(doll, Paints.Lit(burlap, 0.55f, 0.35f, 0.2f));

            // Шов по туловищу.
            for (int i = 0; i < 5; i++)
            {
                float y = 0.46f + i * 0.065f;
                c.Fill(Shapes.Capsule(0.5f, y, 0.5f, y + 0.035f, 0.009f), thread);
                c.Fill(Shapes.Capsule(0.485f, y + 0.017f, 0.515f, y + 0.017f, 0.007f), thread);
            }

            // Глаза: крестик и пуговица. Рот — зашит.
            c.Fill(Shapes.Union(
                Shapes.Capsule(0.405f, 0.235f, 0.465f, 0.295f, 0.013f),
                Shapes.Capsule(0.465f, 0.235f, 0.405f, 0.295f, 0.013f)), thread);
            c.Fill(Shapes.Circle(0.575f, 0.265f, 0.036f), Rgba.Hex("#1b1d24"));
            c.Fill(Shapes.Circle(0.565f, 0.255f, 0.01f), Rgba.White.WithAlpha(0.6f));
            c.Fill(Shapes.Capsule(0.43f, 0.365f, 0.57f, 0.365f, 0.008f), thread);
            for (int i = 0; i < 4; i++)
            {
                float x = 0.45f + i * 0.033f;
                c.Fill(Shapes.Capsule(x, 0.345f, x, 0.385f, 0.007f), thread);
            }

            // Головки иголок — лунные.
            foreach (var p in pins)
                c.Fill(Shapes.Circle(p.hx, p.hy, 0.042f), Paints.Radial(p.hx - 0.012f, p.hy - 0.012f, 0.06f, LunarBlue.Tint(0.5f), LunarBlue.Shade(0.7f)));

            c.Outline(OutlineWidth, Ink);
        }
    }
}
