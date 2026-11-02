using BepInEx.Configuration;
using Destroyer.Content;
using Destroyer.Core;
using RoR2;

namespace Destroyer.Items
{
    /// <summary>
    /// Баллончик с краской (белый). 5% при попадании (от копий НЕ растёт) ослепить врага на 2 (+0.5) с.
    /// Слепота — ванильный дебафф Blinded (DLC1): CharacterBody.RecalculateStats режет visionDistance до 15 м
    /// (+0.75 радиуса тела), а по visionDistance ИИ ищет цели и проверяет видимость (BaseAI.HasLOS, BullseyeSearch).
    /// Вдобавок сразу сбрасываем ИИ текущую цель: издалека тебя заново не найдёт, вплотную — найдёт.
    /// </summary>
    internal sealed class PaintCan : ItemBase
    {
        public override string Id => "PAINT_CAN";
        public override ItemTier Tier => ItemTier.Tier1;
        public override string ModelFrom => "IgniteOnKill"; // канистра Бензина
        public override ItemTag[] Tags => new[] { ItemTag.Utility };

        /// <summary>Ванильный кап зрения ослеплённого (CharacterBody.RecalculateStats, IL 1.5) — только для текста.</summary>
        private const float VanillaBlindVision = 15f;

        public override Words Name => new Words("Spray Paint Can", "Баллончик с краской");

        public override Words Pickup => new Words(
            "Chance on hit to blind enemies.",
            "Шанс при попадании ослепить врага.");

        public override Words Description => new Words(
            $"{Tx.Utility(Tx.Pct(chance.Value))} chance on hit to " +
            $"{Tx.Utility("blind")} an enemy for {Tx.Utility(Tx.Seconds(duration.Value).En)} {Tx.Stack("+" + Tx.Seconds(durationPerStack.Value).En + " per stack")}. " +
            $"Blinded enemies lose their target and only see {Tx.Utility(Tx.Meters(VanillaBlindVision).En)} around them.",
            $"{Tx.Utility(Tx.Pct(chance.Value))} шанс при попадании " +
            $"{Tx.Utility("ослепить")} врага на {Tx.Utility(Tx.Seconds(duration.Value).Ru)} {Tx.Stack("+" + Tx.Seconds(durationPerStack.Value).Ru + " за копию")}. " +
            $"Ослеплённый враг теряет цель и видит только на {Tx.Utility(Tx.Meters(VanillaBlindVision).Ru)} вокруг.");

        public override Words Lore => new Words(
            "Pink. It had to be pink.",
            "Розовая. Конечно же, розовая.");

        private ConfigEntry<float> chance, duration, durationPerStack;

        protected override void BindConfig(ItemSettings s)
        {
            chance = s.Float("BlindChance", 0.05f, 0f, 1f, new Words("Blind chance", "Шанс ослепить"),
                new Words("Chance on hit (0.05 = 5%), scaled by the attack's proc coefficient. Does not grow with stacks.",
                          "Шанс при попадании (0.05 = 5%), умножается на proc coefficient атаки. От копий не растёт."), "{0:P0}");
            duration = s.Float("BlindDuration", 2f, 0.1f, 30f, new Words("Blind duration", "Длительность слепоты"),
                new Words("Blind duration with the first copy, seconds.", "Длительность слепоты с первой копией, с."), "{0:0.#} s");
            durationPerStack = s.Float("BlindDurationPerStack", 0.5f, 0f, 30f, new Words("Blind duration per stack", "Длительность за копию"),
                new Words("Extra blind duration per additional copy, seconds.", "Прибавка длительности за каждую копию сверх первой, с."), "{0:0.#} s");
        }

        private bool warnedNoBuff;

        protected override void Hook() => HitEvents.OnHit += OnHit;

        private void OnHit(DamageInfo info, CharacterBody attacker, CharacterBody victim)
        {
            int n = Count(attacker);
            if (n <= 0 || !victim.healthComponent || !victim.healthComponent.alive) return;
            if (!Roll(chance.Value, info.procCoefficient, attacker.master)) return;

            var blinded = DLC1Content.Buffs.Blinded;
            if (!blinded)
            {
                if (!warnedNoBuff) DestroyerPlugin.Log.LogWarning("Paint can: vanilla Blinded buff (DLC1) not found, nothing to apply.");
                warnedNoBuff = true;
                return;
            }

            victim.AddTimedBuff(blinded, Stacking.Linear(n, duration.Value, durationPerStack.Value));
            victim.RecalculateStats(); // зрение режется сразу, а не на следующем пересчёте — иначе ИИ успеет перецелиться

            var master = victim.master;
            var brains = master ? master.AiComponents : null;
            if (brains == null) return;
            foreach (var ai in brains)
            {
                if (!ai) continue;
                ai.currentEnemy.Reset();
                ai.enemyAttention = 0f;
            }
        }
    }
}
