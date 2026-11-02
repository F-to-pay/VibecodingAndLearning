using BepInEx.Configuration;
using Destroyer.Content;
using Destroyer.Core;
using RoR2;
using UnityEngine;

namespace Destroyer.Items
{
    /// <summary>
    /// Вафельница (белый). КАЖДОЕ попадание (не шанс: proc coefficient и удача не важны) вешает на цель стак подпала.
    /// 6 стаков — цель загорается: 100% (+50%) базового урона огнём за 2 с (0.7.1, было 300% (+150%)), стаки сбрасываются.
    ///
    /// Попадание — HitEvents (proc &gt; 0); тики DoT туда не попадают, плюс явный отсев info.dotIndex — иначе горение
    /// кормило бы само себя. Стаки — свой дебафф без таймера (висит, пока не наберётся 6). Поджигает тот, чей удар был
    /// шестым, с его копиями.
    ///
    /// Горение — ванильный Burn через InflictDotInfo.totalDamage (IL DotController.AddDot 1.5: при totalDamage
    /// длительность = totalDamage × interval / урон за тик). damageMultiplier подобран так, чтобы выходило ровно 2 с.
    /// Перед поджогом — StrengthenBurnUtils.CheckDotForUpgrade, как у ванильных поджогов: Ignition Tank усиливает и это.
    /// </summary>
    internal sealed class WaffleIron : ItemBase
    {
        public override string Id => "WAFFLE_IRON";
        public override ItemTier Tier => ItemTier.Tier1;
        public override string ModelFrom => "StrengthenBurn"; // Ignition Tank
        public override ItemTag[] Tags => new[] { ItemTag.Damage };

        public override Words Name => new Words("Waffle Iron", "Вафельница");

        public override Words Pickup => new Words(
            "Every hit scorches. Enough scorches set the enemy on fire.",
            "Каждый удар подпаливает. Хватит подпалин — враг загорается.");

        public override Words Description => new Words(
            $"{Tx.Damage("Every hit")} scorches the enemy. At {Tx.Damage(stacksToIgnite.Value + " scorches")} it {Tx.Damage("ignites")} " +
            $"for {Tx.Damage(Tx.Pct(burnDamage.Value) + " base damage")} {Tx.Stack("+" + Tx.Pct(burnDamagePerStack.Value) + " per stack")} " +
            $"over {Tx.Damage(Tx.Seconds(burnDuration.Value).En)}.",
            $"{Tx.Damage("Каждый удар")} подпаливает врага. На {Tx.Damage(stacksToIgnite.Value + " подпалинах")} враг {Tx.Damage("загорается")}: " +
            $"{Tx.Damage(Tx.Pct(burnDamage.Value) + " базового урона")} {Tx.Stack("+" + Tx.Pct(burnDamagePerStack.Value) + " за копию")} " +
            $"огнём за {Tx.Damage(Tx.Seconds(burnDuration.Value).Ru)}.");

        public override Words Lore => new Words(
            "Grid pattern guaranteed. On anything.",
            "Клеточка гарантирована. На чём угодно.");

        private ConfigEntry<float> burnDamage, burnDamagePerStack, burnDuration;
        private ConfigEntry<int> stacksToIgnite;

        protected override void BindConfig(ItemSettings s)
        {
            stacksToIgnite = s.Int("StacksToIgnite", 6, 1, 100, new Words("Hits to ignite", "Ударов до поджога"),
                new Words("Scorch stacks needed to ignite the enemy.", "Сколько подпалин нужно, чтобы враг загорелся."));
            burnDamage = s.Float("BurnDamage", 1f, 0f, 100f, new Words("Burn damage", "Урон горения"),
                new Words("Total burn damage with the first copy, in base damage (1 = 100%).",
                          "Весь урон горения с первой копией, в базовом уроне (1 = 100%)."), "{0:P0}");
            burnDamagePerStack = s.Float("BurnDamagePerStack", 0.5f, 0f, 100f, new Words("Burn damage per stack", "Урон за копию"),
                new Words("Extra burn damage per additional copy (0.5 = +50%).", "Прибавка урона за каждую копию сверх первой (0.5 = +50%)."), "{0:P0}");
            burnDuration = s.Float("BurnDuration", 2f, 0.2f, 30f, new Words("Burn duration", "Длительность горения"),
                new Words("How long the burn lasts, seconds (the damage is spread over it).", "Сколько длится горение, с (урон размазан по нему)."), "{0:0.#} s");
        }

        private BuffDef scorch;
        private bool warnedNoBurn;

        protected override void Hook()
        {
            scorch = BuffFactory.Create("SCORCH", BuffFactory.Hex("#ff9a3d"), isDebuff: true, canStack: true);
            HitEvents.OnHit += OnHit;
        }

        private void OnHit(DamageInfo info, CharacterBody attacker, CharacterBody victim)
        {
            if (info.dotIndex != DotController.DotIndex.None) return; // тики DoT не подпаливают
            int n = Count(attacker);
            if (n <= 0 || scorch == null) return;
            var health = victim.healthComponent;
            if (!health || !health.alive) return;

            victim.AddBuff(scorch);
            if (victim.GetBuffCount(scorch) < stacksToIgnite.Value) return;
            victim.SetBuffCount(scorch.buffIndex, 0);
            Ignite(attacker, victim, info, n);
        }

        private void Ignite(CharacterBody attacker, CharacterBody victim, DamageInfo info, int n)
        {
            var def = DotController.GetDotDef(DotController.DotIndex.Burn);
            if (def == null || def.damageCoefficient <= 0f || def.interval <= 0f)
            {
                if (!warnedNoBurn) DestroyerPlugin.Log.LogWarning("Waffle iron: vanilla Burn DoT not found, nothing to ignite.");
                warnedNoBurn = true;
                return;
            }

            float coefficient = Stacking.Linear(n, burnDamage.Value, burnDamagePerStack.Value);
            float duration = burnDuration.Value;
            if (coefficient <= 0f || attacker.damage <= 0f) return;

            var dot = new InflictDotInfo
            {
                victimObject = victim.gameObject,
                attackerObject = attacker.gameObject,
                dotIndex = DotController.DotIndex.Burn,
                duration = duration,
                // Урон за тик = def.damageCoefficient × урон атакующего × damageMultiplier; тиков = duration / interval.
                damageMultiplier = coefficient * def.interval / (duration * def.damageCoefficient),
                totalDamage = attacker.damage * coefficient,
                hitHurtBox = victim.mainHurtBox,
                procChainMask = info.procChainMask,
            };
            if (attacker.inventory) StrengthenBurnUtils.CheckDotForUpgrade(attacker.inventory, ref dot);
            DotController.InflictDot(ref dot);
        }
    }
}
