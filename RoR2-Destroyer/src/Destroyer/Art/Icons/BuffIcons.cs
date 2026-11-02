namespace Destroyer.Art
{
    /// <summary>Иконки баффов: белые силуэты, игра красит их в цвет баффа.</summary>
    public static partial class IconBook
    {
        /// <summary>"Вкусный": вилка и нож.</summary>
        public static void BuffTasty(IconCanvas c)
        {
            var fork = Shapes.Union(
                Shapes.Capsule(0.35f, 0.46f, 0.35f, 0.86f, 0.042f),
                Shapes.Box(0.35f, 0.38f, 0.095f, 0.06f, 0.035f),
                Shapes.Capsule(0.275f, 0.13f, 0.275f, 0.36f, 0.022f),
                Shapes.Capsule(0.35f, 0.13f, 0.35f, 0.36f, 0.022f),
                Shapes.Capsule(0.425f, 0.13f, 0.425f, 0.36f, 0.022f));
            var knife = Shapes.Union(
                Shapes.Capsule(0.66f, 0.56f, 0.66f, 0.86f, 0.046f),
                Shapes.Grow(Shapes.Polygon(0.62f, 0.56f, 0.62f, 0.17f, 0.67f, 0.12f, 0.71f, 0.22f, 0.70f, 0.56f), 0.01f));
            c.Fill(Shapes.Union(fork, knife), Rgba.White);
        }

        /// <summary>Броня шампиньона: силуэт гриба.</summary>
        public static void BuffChampignonArmor(IconCanvas c)
        {
            var cap = Shapes.Intersect(Shapes.Ellipse(0.5f, 0.52f, 0.38f, 0.34f), Shapes.Box(0.5f, 0.26f, 0.6f, 0.26f));
            var stem = Shapes.Grow(Shapes.Polygon(0.41f, 0.5f, 0.59f, 0.5f, 0.62f, 0.86f, 0.38f, 0.86f), 0.02f);
            c.Fill(Shapes.Subtract(Shapes.Union(cap, stem), Shapes.Box(0.5f, 0.535f, 0.4f, 0.018f)), Rgba.White);
        }
    }
}
