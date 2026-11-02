using System;

namespace Destroyer.Art
{
    /// <summary>
    /// Холст для иконок без Unity. Квадрат Size×Size, координаты в долях 0..1 (0,0 — левый верхний угол),
    /// поэтому одна и та же иконка рисуется на любом размере. Всё сглаживается по SDF фигур.
    ///
    /// Порядок как у художника: сначала то, что сзади. В конце обычно Outline() — тёмная обводка,
    /// как у ванильных иконок, чтобы предмет читался на любом фоне.
    /// </summary>
    public sealed class IconCanvas
    {
        public readonly int Size;

        // Цвет с умноженной альфой (premultiplied): так смешивание правильное и без тёмных ореолов.
        private readonly float[] r, g, b, a;

        public IconCanvas(int size)
        {
            if (size < 8) throw new ArgumentException("IconCanvas is too small.");
            Size = size;
            int n = size * size;
            r = new float[n]; g = new float[n]; b = new float[n]; a = new float[n];
        }

        /// <summary>Один пиксель холста в долях (для толщин "в один пиксель").</summary>
        public float Pixel => 1f / Size;

        public void Fill(Sdf shape, Rgba color) => Fill(shape, Paints.Solid(color));

        /// <summary>Залить фигуру. opacity — общая прозрачность слоя.</summary>
        public void Fill(Sdf shape, Paint paint, float opacity = 1f)
        {
            float px = Size;
            for (int j = 0; j < Size; j++)
            {
                float y = (j + 0.5f) / Size;
                for (int i = 0; i < Size; i++)
                {
                    float x = (i + 0.5f) / Size;
                    // Расстояние в пикселях → покрытие пикселя: полпикселя внутрь/наружу = плавный край.
                    float cover = 0.5f - shape(x, y) * px;
                    if (cover <= 0f) continue;
                    if (cover > 1f) cover = 1f;
                    Blend(j * Size + i, paint(x, y), cover * opacity);
                }
            }
        }

        /// <summary>Обвести контур фигуры линией width (в долях холста).</summary>
        public void Stroke(Sdf shape, float width, Rgba color) => Fill(Shapes.Outline(shape, width), color);

        /// <summary>
        /// Обводка ПОД всем, что уже нарисовано: силуэт раздувается на width и красится color.
        /// Ванильные иконки RoR2 почти все с тёмной обводкой — без неё предмет теряется на фоне тира.
        /// </summary>
        public void Outline(float width, Rgba color) => Halo(width, color, soft: false);

        /// <summary>Мягкое свечение под всем нарисованным (тает к краю радиуса).</summary>
        public void Glow(float radius, Rgba color) => Halo(radius, color, soft: true);

        /// <summary>Залить весь холст (для фона в предпросмотре; в иконках предметов фон должен быть прозрачным).</summary>
        public void Background(Rgba color) => Behind(_ => 1f, color);

        /// <summary>Очистить.</summary>
        public void Clear()
        {
            Array.Clear(r, 0, r.Length); Array.Clear(g, 0, g.Length);
            Array.Clear(b, 0, b.Length); Array.Clear(a, 0, a.Length);
        }

        /// <summary>Сколько холста закрашено (сумма альфы / площадь) — для самотеста.</summary>
        public float Coverage()
        {
            double sum = 0;
            foreach (var v in a) sum += v;
            return (float)(sum / a.Length);
        }

        /// <summary>Пиксель (обычная альфа) — для самотеста и предпросмотра.</summary>
        public Rgba Get(int i, int j)
        {
            int k = j * Size + i;
            float al = a[k];
            return al <= 0f ? Rgba.Clear : new Rgba(r[k] / al, g[k] / al, b[k] / al, al);
        }

        /// <summary>Записать пиксель напрямую (обычная альфа), без смешивания.</summary>
        public void Set(int i, int j, Rgba c)
        {
            int k = j * Size + i;
            r[k] = c.R * c.A; g[k] = c.G * c.A; b[k] = c.B * c.A; a[k] = c.A;
        }

        /// <summary>
        /// RGBA по байту на канал, обычная альфа. bottomUp = true — первая строка нижняя (так хочет Unity Texture2D),
        /// false — верхняя (так пишется PNG).
        /// </summary>
        public byte[] ToRgba32(bool bottomUp)
        {
            var bytes = new byte[Size * Size * 4];
            for (int j = 0; j < Size; j++)
            {
                int row = bottomUp ? Size - 1 - j : j;
                for (int i = 0; i < Size; i++)
                {
                    var c = Get(i, j);
                    int o = (row * Size + i) * 4;
                    bytes[o] = ToByte(c.R);
                    bytes[o + 1] = ToByte(c.G);
                    bytes[o + 2] = ToByte(c.B);
                    bytes[o + 3] = ToByte(c.A);
                }
            }
            return bytes;
        }

        /// <summary>Нарисовать другой холст поверх этого в точке (x, y) пикселей — для листа предпросмотра.</summary>
        public void Paste(IconCanvas src, int x, int y)
        {
            for (int j = 0; j < src.Size; j++)
            {
                int ty = y + j;
                if (ty < 0 || ty >= Size) continue;
                for (int i = 0; i < src.Size; i++)
                {
                    int tx = x + i;
                    if (tx < 0 || tx >= Size) continue;
                    int s = j * src.Size + i, d = ty * Size + tx;
                    float k = 1f - src.a[s];
                    r[d] = src.r[s] + r[d] * k;
                    g[d] = src.g[s] + g[d] * k;
                    b[d] = src.b[s] + b[d] * k;
                    a[d] = src.a[s] + a[d] * k;
                }
            }
        }

        // ---------- Внутреннее ----------

        private void Blend(int k, Rgba c, float cover)
        {
            float sa = c.A * cover;
            if (sa <= 0f) return;
            float inv = 1f - sa;
            r[k] = c.R * sa + r[k] * inv;
            g[k] = c.G * sa + g[k] * inv;
            b[k] = c.B * sa + b[k] * inv;
            a[k] = sa + a[k] * inv;
        }

        /// <summary>Подложить цвет ПОД существующее (операция "behind"): alphaOf(k) — альфа подложки в пикселе k.</summary>
        private void Behind(Func<int, float> alphaOf, Rgba color)
        {
            for (int k = 0; k < a.Length; k++)
            {
                float sa = alphaOf(k) * color.A;
                if (sa <= 0f) continue;
                float inv = 1f - a[k];
                r[k] += color.R * sa * inv;
                g[k] += color.G * sa * inv;
                b[k] += color.B * sa * inv;
                a[k] += sa * inv;
            }
        }

        /// <summary>Раздутый силуэт (максимум альфы в круге радиуса width) — подкладывается под рисунок.</summary>
        private void Halo(float width, Rgba color, bool soft)
        {
            float radius = width * Size;
            if (radius <= 0f) return;
            int reach = (int)Math.Ceiling(radius + 0.5f);

            // Заранее веса ядра: жёсткое — круг со сглаженным краем, мягкое — спадает квадратично.
            int side = reach * 2 + 1;
            var kernel = new float[side * side];
            for (int dy = -reach; dy <= reach; dy++)
                for (int dx = -reach; dx <= reach; dx++)
                {
                    float dist = Shapes.Len(dx, dy);
                    float w = soft
                        ? (dist >= radius ? 0f : (1f - dist / radius) * (1f - dist / radius))
                        : Math.Max(0f, Math.Min(1f, radius + 0.5f - dist));
                    kernel[(dy + reach) * side + dx + reach] = w;
                }

            var halo = new float[a.Length];
            for (int j = 0; j < Size; j++)
            {
                for (int i = 0; i < Size; i++)
                {
                    int k = j * Size + i;
                    if (a[k] >= 0.999f) continue; // под непрозрачным обводку всё равно не видно
                    float best = 0f;
                    for (int dy = -reach; dy <= reach && best < 1f; dy++)
                    {
                        int y = j + dy;
                        if (y < 0 || y >= Size) continue;
                        int krow = (dy + reach) * side + reach;
                        int row = y * Size;
                        for (int dx = -reach; dx <= reach; dx++)
                        {
                            int x = i + dx;
                            if (x < 0 || x >= Size) continue;
                            float w = kernel[krow + dx];
                            if (w <= best) continue;
                            float v = a[row + x] * w;
                            if (v > best) best = v;
                        }
                    }
                    halo[k] = best;
                }
            }
            Behind(k => halo[k], color);
        }

        private static byte ToByte(float v) => (byte)Math.Round(Rgba.Clamp01(v) * 255f);
    }
}
