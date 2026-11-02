using BepInEx.Configuration;
using Destroyer.Content;
using Destroyer.Core;
using R2API;
using RoR2;

namespace Destroyer.Items
{
    /// <summary>
    /// Зелёные краски (зелёный, 0.16.0). Здоровье +1% (+1%) (healthMultAdd). Каждое попадание (HitEvents: proc &gt; 0,
    /// не DoT) добивает отдельным уроном: макс. здоровье × 1% (+1%) × proc атаки. Отдельный DamageInfo: proc 0
    /// (ничего не прокает), крит — как у самой атаки, цвет предметов. OnHitEnemy не зовётся — цепочек нет.
    /// </summary>
    internal sealed class GreenPaint : ItemBase
    {
        public override string Id => "GREEN_PAINT";
        public override ItemTier Tier => ItemTier.Tier2;
        public override string ModelFrom => "ScrapGreen";
        public override ItemTag[] Tags => new[] { ItemTag.Damage, ItemTag.Healing };

        public override Words Name => new Words("Green Paints", "Зелёные краски");

        public override Words Pickup => new Words(
            "Slightly increase health. Your hits deal extra damage equal to the bonus.",
            "Немного больше здоровья. Удары добавляют урон, равный этой прибавке.");

        public override Words Description => new Words(
            $"Increase {Tx.Healing("maximum health")} by {Tx.Healing(Tx.Pct(bonus.Value))} {Tx.Stack("+" + Tx.Pct(bonus.Value) + " per stack")}. " +
            $"Your hits deal {Tx.Damage("extra damage")} equal to {Tx.Damage(Tx.Pct(bonus.Value) + " of your maximum health")} {Tx.Stack("+" + Tx.Pct(bonus.Value) + " per stack")}.",
            $"{Tx.Healing("Макс. здоровье")} выше на {Tx.Healing(Tx.Pct(bonus.Value))} {Tx.Stack("+" + Tx.Pct(bonus.Value) + " за копию")}. " +
            $"Каждый удар наносит {Tx.Damage("доп. урон")}: {Tx.Damage(Tx.Pct(bonus.Value) + " макс. здоровья")} {Tx.Stack("+" + Tx.Pct(bonus.Value) + " за копию")}.");

        public override Words Lore => new Words("Two coats. Heavy ones.", "Два слоя. Тяжёлых.");

        private ConfigEntry<float> bonus;

        protected override void BindConfig(ItemSettings s)
        {
            bonus = s.Float("HealthBonus", 0.01f, 0f, 1f, new Words("Health bonus per stack", "Здоровье за копию"),
                new Words("Max health bonus per copy, also dealt as extra damage on hit (0.01 = 1%).",
                          "Бонус макс. здоровья за копию — столько же (доля макс. здоровья) наносится доп. уроном при ударе (0.01 = 1%)."), "{0:P0}");
        }

        protected override void Hook()
        {
            RecalculateStatsAPI.GetStatCoefficients += (body, args) =>
            {
                int n = Count(body);
                if (n > 0) args.healthMultAdd += bonus.Value * n;
            };
            HitEvents.OnHit += OnHit;
        }

        private void OnHit(DamageInfo info, CharacterBody attacker, CharacterBody victim)
        {
            if (info.dotIndex != DotController.DotIndex.None) return;
            int n = Count(attacker);
            if (n <= 0) return;
            var health = victim.healthComponent;
            if (!health || !health.alive) return;
            float damage = attacker.maxHealth * bonus.Value * n * info.procCoefficient;
            if (damage <= 0f) return;

            health.TakeDamage(new DamageInfo
            {
                attacker = attacker.gameObject,
                inflictor = attacker.gameObject,
                damage = damage,
                crit = info.crit,
                position = info.position,
                procCoefficient = 0f,
                procChainMask = info.procChainMask,
                damageColorIndex = DamageColorIndex.Item,
                damageType = DamageType.Generic,
            });
        }
    }
}
