using System;

namespace Destroyer.Core
{
    /// <summary>
    /// Как эффект растёт с числом копий предмета. Всё чистая математика — проверяется в верстаке.
    /// n — число копий (0 и меньше = эффекта нет, всегда 0).
    /// </summary>
    public static class Stacking
    {
        /// <summary>
        /// Линейно, как Кирка или Солдатский шприц: first за первую копию, perStack за каждую следующую.
        /// Linear(3, 0.15, 0.10) = 0.35.
        /// </summary>
        public static float Linear(int n, float first, float perStack)
            => n <= 0 ? 0f : first + perStack * (n - 1);

        /// <summary>Линейно с одинаковым шагом: Linear(n, x) = x × n.</summary>
        public static float Linear(int n, float perStack) => Linear(n, perStack, perStack);

        /// <summary>
        /// Гиперболически, как Tougher Times: 1 − 1 / (1 + k·n). Никогда не достигает 1.
        /// k = 0.15: 1 копия 13%, 5 копий 43%, 20 копий 75%.
        /// </summary>
        public static float Hyperbolic(int n, float k)
            => n <= 0 ? 0f : 1f - 1f / (1f + k * n);

        /// <summary>
        /// Экспоненциально: каждая копия срезает долю p от того, что осталось. 1 − (1 − p)^n.
        /// p = 0.1: 1 копия 10%, 5 копий 41%, 20 копий 88%.
        /// </summary>
        public static float Exponential(int n, float p)
            => n <= 0 ? 0f : 1f - (float)Math.Pow(1f - p, n);

        /// <summary>
        /// Множитель с геометрическим ростом: (1 + r)^n. Для "×1.1 за копию".
        /// В отличие от остальных, при n = 0 даёт 1, а не 0.
        /// </summary>
        public static float Geometric(int n, float r)
            => n <= 0 ? 1f : (float)Math.Pow(1f + r, n);

        /// <summary>Линейно, но не больше потолка.</summary>
        public static float LinearCapped(int n, float first, float perStack, float cap)
            => Math.Min(cap, Linear(n, first, perStack));

        /// <summary>
        /// Шанс в процентах, как его понимает Util.CheckRoll: 0..100. Удобно, когда в конфиге доли (0.1 = 10%).
        /// </summary>
        public static float ToPercent(float fraction) => fraction * 100f;
    }
}
