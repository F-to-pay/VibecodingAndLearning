using BepInEx.Configuration;
using Destroyer.Content;
using Destroyer.Core;
using R2API;
using RoR2;
using UnityEngine;

namespace Destroyer.Items
{
    /// <summary>
    /// Ненависть (лунный, 0.14.3). Множитель k = 2 (+1 за копию): урон ×k, скорость атаки ÷k, перезарядки навыков ×k.
    /// Основная атака и навыки на перезарядке выходят в ноль по урону в секунду (сильнее, но реже); в плюсе только то,
    /// что от твоих атак не зависит, а считается от урона: предметы на убийства, по таймеру, укусы и т.п.
    /// Стаки линейные (2 → 3 → 4), не степенью.
    /// (0.14.2: ×2^n урона и ×0.5^n скорости атаки без штрафа перезарядок — навыки на перезарядке получали ×2^n;
    ///  0.13.0–0.14.1 было наоборот: скорость ×2, урон ×0.5.)
    /// </summary>
    internal sealed class Hatred : ItemBase
    {
        public override string Id => "HATRED";
        public override ItemTier Tier => ItemTier.Lunar;
        public override string ModelFrom => "LunarBadLuck"; // Purity — лунная "цена за всё"
        public override ItemTag[] Tags => new[] { ItemTag.Damage };

        public override Words Name => new Words("Hatred", "Ненависть");

        public override Words Pickup => new Words(
            "Double your damage... <style=cDeath>but attack and recharge skills twice as slowly.</style>",
            "Урон вдвое выше... <style=cDeath>но атаки и перезарядка вдвое медленнее.</style>");

        public override Words Description => new Words(
            $"{Tx.Damage("Damage ×" + Tx.Num(first.Value))} {Tx.Stack("+" + Tx.Num(perStack.Value) + " per stack")}. " +
            $"{Tx.Health("Attack speed is divided and skill cooldowns are multiplied by the same amount")}.",
            $"{Tx.Damage("Урон ×" + Tx.Num(first.Value))} {Tx.Stack("+" + Tx.Num(perStack.Value) + " за копию")}. " +
            $"{Tx.Health("Скорость атаки делится, а перезарядка навыков умножается на столько же")}.");

        public override Words Lore => new Words("Slower. Angrier. Heavier.", "Медленнее. Злее. Тяжелее.");

        private ConfigEntry<float> first, perStack;

        protected override void BindConfig(ItemSettings s)
        {
            first = s.Float("DamageMultiplier", 2f, 1f, 20f, new Words("Multiplier", "Множитель"),
                new Words("With the first copy: damage ×this, attack speed ÷this, cooldowns ×this.",
                          "С первой копией: урон ×это, скорость атаки ÷это, перезарядки ×это."), "×{0:0.##}");
            perStack = s.Float("MultiplierPerStack", 1f, 0f, 20f, new Words("Multiplier per stack", "Множитель за копию"),
                new Words("Added to the multiplier per additional copy. 1: ×2 → ×3 → ×4.",
                          "Прибавка к множителю за каждую копию сверх первой. 1: ×2 → ×3 → ×4."), "{0:0.##}");
        }

        private float MultFor(int n) => Mathf.Max(1f, Stacking.Linear(n, first.Value, perStack.Value));

        protected override void Hook()
        {
            RecalculateStatsAPI.GetStatCoefficients += (body, args) =>
            {
                int n = Count(body);
                if (n <= 0) return;
                float k = MultFor(n);
                args.damageTotalMult *= k;
                args.attackSpeedTotalMult /= k;
            };
            SkillCooldowns.Register(body => { int n = Count(body); return n > 0 ? MultFor(n) : 1f; }); // ×k, своим хуком (0.14.4)
        }
    }
}
