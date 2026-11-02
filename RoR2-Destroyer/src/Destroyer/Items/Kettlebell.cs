using BepInEx.Configuration;
using Destroyer.Content;
using Destroyer.Core;
using RoR2;
using UnityEngine;

namespace Destroyer.Items
{
    /// <summary>
    /// Гиря (белый). Каждое попадание отталкивает цель от тебя. Сила × proc coefficient атаки и растёт с копиями.
    ///
    /// Толчок — HealthComponent.TakeDamageForce (сервер): это ИМПУЛЬС, поэтому лёгкие враги летят далеко, тяжёлые и боссы —
    /// чуть-чуть, по физике. Тела с флагом Unmovable не трогаем. Немного вверх — иначе стоящего на земле прибьёт трением.
    /// </summary>
    internal sealed class Kettlebell : ItemBase
    {
        public override string Id => "KETTLEBELL";
        public override ItemTier Tier => ItemTier.Tier1;
        public override string ModelFrom => "StunChanceOnHit"; // Stun Grenade — круглое и тяжёлое
        public override ItemTag[] Tags => new[] { ItemTag.Utility };

        public override Words Name => new Words("Kettlebell", "Гиря");

        public override Words Pickup => new Words(
            "Your hits knock enemies back.",
            "Удары отбрасывают врагов.");

        public override Words Description => new Words(
            $"Hits {Tx.Utility("knock enemies back")}. Knockback {Tx.Stack("+100% per stack")} scales with the attack's proc coefficient; " +
            $"heavy enemies and bosses barely budge.",
            $"Удары {Tx.Utility("отбрасывают врагов")}. Сила толчка {Tx.Stack("+100% за копию")} зависит от proc coefficient атаки; " +
            $"тяжёлых врагов и боссов почти не сдвигает.");

        public override Words Lore => new Words(
            "16 kilograms of pure argument.",
            "16 килограммов чистого аргумента.");

        private ConfigEntry<float> force, upward;

        protected override void BindConfig(ItemSettings s)
        {
            force = s.Float("Force", 250f, 0f, 20000f, new Words("Force per stack", "Сила за копию"),
                new Words("Knockback impulse per copy at proc coefficient 1. A Lemurian (~100 mass) gets ~2.5 m/s per 250.",
                          "Импульс толчка за копию при proc coefficient 1. Лемуриец (масса ~100) получает ~2.5 м/с на каждые 250."), "{0:0}");
            upward = s.Float("Upward", 0.3f, 0f, 1f, new Words("Upward share", "Доля вверх"),
                new Words("How much the push goes upward (lets grounded enemies slide).", "Насколько толчок направлен вверх (чтобы стоящих не держало трение)."), "{0:0.##}");
        }

        protected override void Hook() => HitEvents.OnHit += OnHit;

        private void OnHit(DamageInfo info, CharacterBody attacker, CharacterBody victim)
        {
            int n = Count(attacker);
            if (n <= 0 || force.Value <= 0f) return;
            var health = victim.healthComponent;
            if (!health || !health.alive) return;
            if ((victim.bodyFlags & CharacterBody.BodyFlags.Unmovable) != 0) return;

            Vector3 away = victim.corePosition - attacker.corePosition;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f) away = attacker.inputBank ? attacker.inputBank.aimDirection : Vector3.forward;
            away.y = 0f;
            away.Normalize();
            Vector3 direction = (away + Vector3.up * upward.Value).normalized;

            float impulse = force.Value * n * info.procCoefficient;
            health.TakeDamageForce(direction * impulse, alwaysApply: true, disableAirControlUntilCollision: true);
        }
    }
}
