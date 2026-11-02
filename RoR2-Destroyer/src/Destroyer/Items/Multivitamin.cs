using BepInEx.Configuration;
using Destroyer.Content;
using Destroyer.Core;
using R2API;
using RoR2;

namespace Destroyer.Items
{
    /// <summary>
    /// Мультивитамин (белый). Все характеристики +2% (+2%) (0.15.1, было +1% (+1%)) — честным множителем (TotalMult), как у первоцвета:
    /// здоровье, щит, реген, бег, прыжок, урон, скорость атаки, шанс и урон крита, броня; перезарядки быстрее во столько же раз.
    /// </summary>
    internal sealed class Multivitamin : ItemBase
    {
        public override string Id => "MULTIVITAMIN";
        public override ItemTier Tier => ItemTier.Tier1;
        public override string ModelFrom => "Mushroom"; // Bustling Fungus — "что-то полезное"
        public override ItemTag[] Tags => new[] { ItemTag.Damage, ItemTag.Utility, ItemTag.Healing };

        public override Words Name => new Words("Multivitamin", "Мультивитамин");

        public override Words Pickup => new Words("Slightly increase all stats.", "Слегка повышает все характеристики.");

        public override Words Description => new Words(
            $"Increase {Tx.Utility("all stats")} by {Tx.Utility(Tx.Pct(bonus.Value))} {Tx.Stack("+" + Tx.Pct(bonus.Value) + " per stack")}.",
            $"Все характеристики выше на {Tx.Utility(Tx.Pct(bonus.Value))} {Tx.Stack("+" + Tx.Pct(bonus.Value) + " за копию")}.");

        public override Words Lore => new Words("Take one daily. Or forty.", "Принимать по одной в день. Или по сорок.");

        private ConfigEntry<float> bonus;

        protected override void BindConfig(ItemSettings s)
        {
            bonus = s.Float("StatBonus", 0.02f, 0f, 1f, new Words("Stat bonus per stack", "Бонус за копию"),
                new Words("All-stat bonus per copy (0.02 = +2%).", "Бонус ко всем характеристикам за копию (0.02 = +2%)."), "{0:P0}");
        }

        protected override void Hook()
        {
            RecalculateStatsAPI.GetStatCoefficients += (body, args) =>
            {
                int n = Count(body);
                if (n <= 0) return;
                float k = 1f + Stacking.Linear(n, bonus.Value);
                args.healthTotalMult *= k;
                args.shieldTotalMult *= k;
                args.regenTotalMult *= k;
                args.moveSpeedTotalMult *= k;
                args.jumpPowerTotalMult *= k;
                args.damageTotalMult *= k;
                args.attackSpeedTotalMult *= k;
                args.critTotalMult *= k;
                args.critDamageTotalMult *= k;
                args.armorTotalMult *= k;
            };
            SkillCooldowns.Register(body => { int n = Count(body); return n > 0 ? 1f / (1f + Stacking.Linear(n, bonus.Value)) : 1f; }); // ÷k, своим хуком (0.14.4)
        }
    }
}
