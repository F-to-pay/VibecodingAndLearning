using System;

namespace Destroyer.Art
{
    /// <summary>
    /// Фигура = функция "расстояние до края" (SDF): меньше нуля — внутри, больше — снаружи.
    /// Координаты холста 0..1, (0,0) — левый ВЕРХНИЙ угол, y вниз (как в картинках).
    /// Из расстояния холст сам считает сглаживание краёв, поэтому фигуры без "лесенки" на любом размере.
    /// </summary>
    public delegate float Sdf(float x, float y);

    /// <summary>Готовые фигуры и операции над ними.</summary>
    public static class Shapes
    {
        // ---------- Примитивы ----------

        public static Sdf Circle(float cx, float cy, float r) =>
            (x, y) => Len(x - cx, y - cy) - r;

        /// <summary>Эллипс (приближённо, но для иконок неотличимо).</summary>
        public static Sdf Ellipse(float cx, float cy, float rx, float ry) =>
            (x, y) =>
            {
                float dx = (x - cx) / rx, dy = (y - cy) / ry;
                float k = Len(dx, dy);
                return (k - 1f) * Math.Min(rx, ry);
            };

        /// <summary>Прямоугольник по центру и половинам сторон, с радиусом скругления углов.</summary>
        public static Sdf Box(float cx, float cy, float halfW, float halfH, float round = 0f) =>
            (x, y) =>
            {
                float qx = Math.Abs(x - cx) - halfW + round;
                float qy = Math.Abs(y - cy) - halfH + round;
                return Len(Math.Max(qx, 0f), Math.Max(qy, 0f)) + Math.Min(Math.Max(qx, qy), 0f) - round;
            };

        /// <summary>Отрезок с толщиной (палка с круглыми концами). Для линий, рукоятей, лучей.</summary>
        public static Sdf Capsule(float ax, float ay, float bx, float by, float r) =>
            (x, y) => SegmentDistance(x, y, ax, ay, bx, by) - r;

        /// <summary>Кольцо: окружность радиуса r толщиной thickness.</summary>
        public static Sdf Ring(float cx, float cy, float r, float thickness) =>
            (x, y) => Math.Abs(Len(x - cx, y - cy) - r) - thickness * 0.5f;

        /// <summary>
        /// Многоугольник по точкам (x0, y0, x1, y1, ...). Любой, в том числе невыпуклый; порядок обхода не важен.
        /// </summary>
        public static Sdf Polygon(params float[] xy)
        {
            if (xy.Length < 6 || xy.Length % 2 != 0) throw new ArgumentException("Polygon needs at least 3 points (x, y pairs).");
            int n = xy.Length / 2;
            return (x, y) =>
            {
                float d = float.MaxValue;
                bool inside = false;
                for (int i = 0, j = n - 1; i < n; j = i++)
                {
                    float xi = xy[i * 2], yi = xy[i * 2 + 1], xj = xy[j * 2], yj = xy[j * 2 + 1];
                    d = Math.Min(d, SegmentDistance(x, y, xj, yj, xi, yi));
                    if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) inside = !inside;
                }
                return inside ? -d : d;
            };
        }

        /// <summary>Правильный многоугольник: n углов, rotation в градусах (0 — вершина смотрит вверх).</summary>
        public static Sdf RegularPolygon(float cx, float cy, float r, int n, float rotation = 0f) =>
            Polygon(StarPoints(cx, cy, r, r, n, rotation, false));

        /// <summary>Звезда: n лучей, внешний и внутренний радиус.</summary>
        public static Sdf Star(float cx, float cy, float rOuter, float rInner, int n, float rotation = 0f) =>
            Polygon(StarPoints(cx, cy, rOuter, rInner, n, rotation, true));

        /// <summary>
        /// Дуга окружности с толщиной и круглыми концами ("колбаса", подкова, радуга).
        /// Углы в градусах: 0 — вверх, по часовой; from &lt; to.
        /// </summary>
        public static Sdf Arc(float cx, float cy, float r, float from, float to, float thickness)
        {
            double a0 = from * Math.PI / 180.0, a1 = to * Math.PI / 180.0;
            float x0 = cx + r * (float)Math.Sin(a0), y0 = cy - r * (float)Math.Cos(a0);
            float x1 = cx + r * (float)Math.Sin(a1), y1 = cy - r * (float)Math.Cos(a1);
            float half = thickness * 0.5f;
            return (x, y) =>
            {
                double a = Math.Atan2(x - cx, -(y - cy)) * 180.0 / Math.PI;
                // Угол точки в том же "обороте", что и дуга.
                while (a < from - 180.0) a += 360.0;
                while (a > from + 180.0) a -= 360.0;
                float d = a >= from && a <= to
                    ? Math.Abs(Len(x - cx, y - cy) - r)
                    : Math.Min(Len(x - x0, y - y0), Len(x - x1, y - y1));
                return d - half;
            };
        }

        /// <summary>Сектор круга ("кусок пиццы") от угла from до угла to (градусы, 0 — вверх, по часовой).</summary>
        public static Sdf Pie(float cx, float cy, float r, float from, float to) =>
            Intersect(Circle(cx, cy, r), AngleWedge(cx, cy, from, to));

        // ---------- Операции ----------

        public static Sdf Union(params Sdf[] shapes) =>
            (x, y) =>
            {
                float d = float.MaxValue;
                foreach (var s in shapes) d = Math.Min(d, s(x, y));
                return d;
            };

        public static Sdf Intersect(Sdf a, Sdf b) => (x, y) => Math.Max(a(x, y), b(x, y));

        /// <summary>a без b (вырезать дырку).</summary>
        public static Sdf Subtract(Sdf a, Sdf b) => (x, y) => Math.Max(a(x, y), -b(x, y));

        /// <summary>Раздуть (amount &gt; 0) или ужать (&lt; 0) фигуру. Заодно скругляет углы.</summary>
        public static Sdf Grow(Sdf s, float amount) => (x, y) => s(x, y) - amount;

        /// <summary>Только контур фигуры заданной толщины.</summary>
        public static Sdf Outline(Sdf s, float thickness) => (x, y) => Math.Abs(s(x, y)) - thickness * 0.5f;

        /// <summary>Мягкое объединение: фигуры "сливаются" как капли. k — ширина шва (0.02–0.08).</summary>
        public static Sdf SmoothUnion(Sdf a, Sdf b, float k) =>
            (x, y) =>
            {
                float da = a(x, y), db = b(x, y);
                float h = Math.Max(k - Math.Abs(da - db), 0f) / k;
                return Math.Min(da, db) - h * h * k * 0.25f;
            };

        /// <summary>
        /// "Мохнатый" край: граница колеблется на amount туда-сюда с частотой frequency (шерсть, облака, кусты).
        /// Это уже не точное расстояние, поэтому amount держи маленьким (0.005–0.02).
        /// </summary>
        public static Sdf Fuzz(Sdf s, float amount, float frequency) =>
            (x, y) => s(x, y) - amount * (float)(Math.Sin(x * frequency) * Math.Cos(y * frequency * 1.3) + 0.5 * Math.Sin((x + y) * frequency * 2.1));

        public static Sdf Move(Sdf s, float dx, float dy) => (x, y) => s(x - dx, y - dy);

        /// <summary>Повернуть вокруг точки (градусы, по часовой — потому что y вниз).</summary>
        public static Sdf Rotate(Sdf s, float cx, float cy, float degrees)
        {
            double a = -degrees * Math.PI / 180.0;
            float cos = (float)Math.Cos(a), sin = (float)Math.Sin(a);
            return (x, y) =>
            {
                float dx = x - cx, dy = y - cy;
                return s(cx + dx * cos - dy * sin, cy + dx * sin + dy * cos);
            };
        }

        /// <summary>Отразить по горизонтали относительно вертикали x = axis (по умолчанию центр холста).</summary>
        public static Sdf MirrorX(Sdf s, float axis = 0.5f) => (x, y) => s(2f * axis - x, y);

        /// <summary>Симметрия: левая половина — копия правой (рисуешь одну сторону).</summary>
        public static Sdf SymmetricX(Sdf s, float axis = 0.5f) => (x, y) => s(axis + Math.Abs(x - axis), y);

        // ---------- Внутреннее ----------

        internal static float Len(float x, float y) => (float)Math.Sqrt(x * x + y * y);

        private static float SegmentDistance(float px, float py, float ax, float ay, float bx, float by)
        {
            float pax = px - ax, pay = py - ay, bax = bx - ax, bay = by - ay;
            float lenSq = bax * bax + bay * bay;
            float h = lenSq > 0f ? Math.Max(0f, Math.Min(1f, (pax * bax + pay * bay) / lenSq)) : 0f;
            return Len(pax - bax * h, pay - bay * h);
        }

        private static float[] StarPoints(float cx, float cy, float rOuter, float rInner, int n, float rotation, bool star)
        {
            if (n < 3) throw new ArgumentException("Need at least 3 corners.");
            int count = star ? n * 2 : n;
            var pts = new float[count * 2];
            for (int i = 0; i < count; i++)
            {
                double angle = (rotation + 360.0 * i / count) * Math.PI / 180.0;
                float r = star && (i % 2 == 1) ? rInner : rOuter;
                pts[i * 2] = cx + r * (float)Math.Sin(angle);
                pts[i * 2 + 1] = cy - r * (float)Math.Cos(angle);
            }
            return pts;
        }

        /// <summary>Клин между двумя лучами из центра (для сектора). Не настоящий SDF, но для пересечения с кругом годится.</summary>
        private static Sdf AngleWedge(float cx, float cy, float from, float to)
        {
            float span = ((to - from) % 360f + 360f) % 360f;
            if (span == 0f) span = 360f;
            return (x, y) =>
            {
                double a = Math.Atan2(x - cx, -(y - cy)) * 180.0 / Math.PI; // 0 — вверх, по часовой
                double rel = ((a - from) % 360.0 + 360.0) % 360.0;
                float dist = Len(x - cx, y - cy);
                if (rel <= span)
                {
                    double toEdge = Math.Min(rel, span - rel) * Math.PI / 180.0;
                    return -(float)(Math.Sin(Math.Min(toEdge, Math.PI / 2)) * dist);
                }
                double outside = Math.Min(rel - span, 360.0 - rel) * Math.PI / 180.0;
                return (float)(Math.Sin(Math.Min(outside, Math.PI / 2)) * dist);
            };
        }
    }
}
