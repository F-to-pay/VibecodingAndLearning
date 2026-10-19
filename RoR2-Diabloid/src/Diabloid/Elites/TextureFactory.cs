using UnityEngine;

namespace Diabloid.Elites
{
    /// <summary>
    /// Текстуры, нарисованные кодом, — чтобы не тащить в мод картинки и AssetBundle.
    /// </summary>
    internal static class TextureFactory
    {
        /// <summary>
        /// "Рампа" элитки — полоска-градиент, по которой шейдер игры перекрашивает модель
        /// (так Пылающие становятся красными, а Ледяные — белыми). Тёмный → цвет → светлый.
        /// </summary>
        public static Texture2D CreateEliteRamp(Color32 color)
        {
            const int width = 256, height = 8;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "texRampDiabloid",
            };

            Color baseColor = color;
            Color dark = baseColor * 0.15f;
            dark.a = 1f;
            Color light = Color.Lerp(baseColor, Color.white, 0.6f);

            for (int x = 0; x < width; x++)
            {
                float t = x / (width - 1f);
                Color c = t < 0.5f
                    ? Color.Lerp(dark, baseColor, t * 2f)
                    : Color.Lerp(baseColor, light, (t - 0.5f) * 2f);
                c.a = 1f;
                for (int y = 0; y < height; y++)
                    tex.SetPixel(x, y, c);
            }

            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Иконка артефакта элитаризма: корона на тёмном круге. Включённый — золотой, выключенный — серый.
        /// Игра требует обе иконки, иначе экран выбора артефактов падает.
        /// </summary>
        public static Sprite CreateArtifactIcon(bool selected)
        {
            // Контур короны (в координатах 0..1, y вверх): основание + три зубца.
            Vector2[] crown =
            {
                new Vector2(0.24f, 0.28f), new Vector2(0.76f, 0.28f), new Vector2(0.80f, 0.70f),
                new Vector2(0.64f, 0.50f), new Vector2(0.50f, 0.78f), new Vector2(0.36f, 0.50f),
                new Vector2(0.20f, 0.70f),
            };

            return DrawIcon(selected ? "texDiabloidElitismOn" : "texDiabloidElitismOff", crown,
                background: selected ? new Color(0.20f, 0.08f, 0.10f, 1f) : new Color(0.12f, 0.12f, 0.12f, 1f),
                rim: selected ? new Color(0.85f, 0.25f, 0.25f, 1f) : new Color(0.35f, 0.35f, 0.35f, 1f),
                foreground: selected ? new Color(1f, 0.78f, 0.20f, 1f) : new Color(0.55f, 0.55f, 0.55f, 1f));
        }

        /// <summary>Иконка артефакта снаряжения: рыцарский щит. Включённый — стальной на синем, выключенный — серый.</summary>
        public static Sprite CreateGearArtifactIcon(bool selected)
        {
            // Классический "геральдический" щит: прямой верх, скруглённые бока, острый низ.
            Vector2[] shield =
            {
                new Vector2(0.26f, 0.76f), new Vector2(0.74f, 0.76f), new Vector2(0.74f, 0.52f),
                new Vector2(0.70f, 0.40f), new Vector2(0.62f, 0.30f), new Vector2(0.50f, 0.20f),
                new Vector2(0.38f, 0.30f), new Vector2(0.30f, 0.40f), new Vector2(0.26f, 0.52f),
            };

            return DrawIcon(selected ? "texDiabloidGearOn" : "texDiabloidGearOff", shield,
                background: selected ? new Color(0.07f, 0.10f, 0.22f, 1f) : new Color(0.12f, 0.12f, 0.12f, 1f),
                rim: selected ? new Color(0.30f, 0.55f, 1f, 1f) : new Color(0.35f, 0.35f, 0.35f, 1f),
                foreground: selected ? new Color(0.80f, 0.86f, 0.95f, 1f) : new Color(0.55f, 0.55f, 0.55f, 1f));
        }

        /// <summary>Круглая иконка 128×128: ободок, фон и залитый многоугольник по центру.</summary>
        private static Sprite DrawIcon(string name, Vector2[] shape, Color background, Color rim, Color foreground)
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = name,
            };

            Color clear = new Color(0f, 0f, 0f, 0f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                    float distance = Vector2.Distance(p, new Vector2(0.5f, 0.5f));

                    Color c;
                    if (distance > 0.48f) c = clear;
                    else if (distance > 0.43f) c = rim;
                    else c = background;

                    if (distance <= 0.43f && InsidePolygon(p, shape)) c = foreground;
                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        /// <summary>Классический тест "луч пересекает рёбра нечётное число раз".</summary>
        private static bool InsidePolygon(Vector2 p, Vector2[] polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                var a = polygon[i];
                var b = polygon[j];
                if ((a.y > p.y) != (b.y > p.y) &&
                    p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                {
                    inside = !inside;
                }
            }
            return inside;
        }
    }
}
