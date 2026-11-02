using BepInEx.Configuration;
using Destroyer.Content;
using Destroyer.Core;
using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace Destroyer.Items
{
    /// <summary>
    /// Стул (белый). Стоишь на месте — +15% (+15%) шанса крита и +15% (+15%) урона крита.
    ///
    /// "Стоишь" — ванильный GetNotMoving (как у Bustling Fungus и шампиньона): считается только на сервере, поэтому
    /// сервер вешает бафф "сидит", а статы из баффа считает RecalculateStats на каждой машине.
    /// </summary>
    internal sealed class Chair : ItemBase
    {
        public override string Id => "CHAIR";
        public override ItemTier Tier => ItemTier.Tier1;
        public override string ModelFrom => "Plant"; // Interstellar Desk Plant — офис же
        public override ItemTag[] Tags => new[] { ItemTag.Damage };

        public override Words Name => new Words("Chair", "Стул");

        public override Words Pickup => new Words(
            "Increase critical chance and critical damage while standing still.",
            "Пока стоишь на месте — больше шанс и урон крита.");

        public override Words Description => new Words(
            $"While standing still, gain {Tx.Damage("+" + Tx.Pct(crit.Value) + " critical chance")} {Tx.Stack("+" + Tx.Pct(crit.Value) + " per stack")} " +
            $"and {Tx.Damage("+" + Tx.Pct(critDamage.Value) + " critical damage")} {Tx.Stack("+" + Tx.Pct(critDamage.Value) + " per stack")}.",
            $"Пока стоишь на месте: {Tx.Damage("+" + Tx.Pct(crit.Value) + " к шансу крита")} {Tx.Stack("+" + Tx.Pct(crit.Value) + " за копию")} " +
            $"и {Tx.Damage("+" + Tx.Pct(critDamage.Value) + " к урону крита")} {Tx.Stack("+" + Tx.Pct(critDamage.Value) + " за копию")}.");

        public override Words Lore => new Words(
            "Ergonomic. Mostly.",
            "Эргономичный. В основном.");

        private ConfigEntry<float> crit, critDamage;

        protected override void BindConfig(ItemSettings s)
        {
            crit = s.Float("CritChance", 0.15f, 0f, 1f, new Words("Crit chance per stack", "Шанс крита за копию"),
                new Words("Critical chance per copy while standing still (0.15 = +15%).", "Шанс крита за копию, пока стоишь (0.15 = +15%)."), "{0:P0}");
            critDamage = s.Float("CritDamage", 0.15f, 0f, 5f, new Words("Crit damage per stack", "Урон крита за копию"),
                new Words("Critical damage per copy while standing still (0.15 = +15%).", "Урон крита за копию, пока стоишь (0.15 = +15%)."), "{0:P0}");
        }

        private BuffDef seated;
        internal static Chair Instance;

        protected override void Hook()
        {
            Instance = this;
            seated = BuffFactory.Create("CHAIR_SEATED", BuffFactory.Hex("#c9a06a"), isDebuff: false, canStack: false);
            UseBehavior<ChairBehavior>(); // сервер

            RecalculateStatsAPI.GetStatCoefficients += (body, args) =>
            {
                if (!body.HasBuff(seated)) return;
                int n = Count(body);
                if (n <= 0) return;
                args.critAdd += 100f * Stacking.Linear(n, crit.Value); // critAdd — в процентах (1 = 1%)
                args.critDamageMultAdd += Stacking.Linear(n, critDamage.Value);
            };
        }

        internal BuffDef Seated => seated;

        /// <summary>Сервер: включает/выключает бафф по GetNotMoving.</summary>
        private sealed class ChairBehavior : CharacterBody.ItemBehavior
        {
            private void FixedUpdate()
            {
                if (!NetworkServer.active || !body) return;
                var buff = Instance?.Seated;
                if (!buff) return;
                bool still = body.GetNotMoving();
                bool has = body.HasBuff(buff);
                if (still && !has) body.AddBuff(buff);
                else if (!still && has) body.RemoveBuff(buff);
            }

            private void OnDisable()
            {
                var buff = Instance?.Seated;
                if (NetworkServer.active && buff && body && body.HasBuff(buff)) body.RemoveBuff(buff);
            }
        }
    }
}
