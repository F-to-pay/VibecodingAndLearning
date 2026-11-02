using System.Collections.Generic;
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
    /// Преобразователь боли (красный, переработан в 0.7.1, финальный патч 0.7.3). 50% каждого полученного удара (каждая копия сверх первой
    /// закрывает 10% остатка до 100%: 50 → 55 → 59.5 → 63.6 → … → стремится к 100%, но не достигает) возвращается лечением
    /// за 20 с, каждый удар — отдельный возврат. Плюс броня от потерянного запаса: чем меньше здоровья, тем больше брони,
    /// у самого нуля — 10 (+10) (0.7.1 было 70% / 100 (+50) — на выносливость не убить). Линейно: броня = макс × (1 − доля здоровья+щита).
    ///
    /// Урон — GlobalEventManager.onServerDamageDealt (report.damageDealt — сколько реально снято с барьера/щита/здоровья).
    /// Лечение возвратов — Heal(..., nonRegen: false), то есть как регенерация: без цифр над головой и мимо Corpsebloom.
    /// Броня — стаки видимого баффа PAIN_ARMOR (= текущая броня, целым), сервер обновляет их раз в 0.1 с; смена стаков
    /// сама помечает статы грязными и уходит клиентам, RecalculateStats на каждой машине читает их как armorAdd
    /// (как у шампиньона). Здоровье — combinedHealthFraction: со щитом, чтобы Transcendence не ломал предмет.
    /// Число активных возвратов — стаки баффа PAIN_RESTORE.
    /// </summary>
    internal sealed class PainConverter : ItemBase
    {
        public override string Id => "PAIN_CONVERTER";
        public override ItemTier Tier => ItemTier.Tier3;
        public override string ModelFrom => "NovaOnHeal"; // N'kuhana's Opinion
        public override ItemTag[] Tags => new[] { ItemTag.Healing, ItemTag.Utility };

        public override Words Name => new Words("Pain Converter", "Преобразователь боли");

        public override Words Pickup => new Words(
            "Damage you take is slowly healed back. The lower your health, the more armor you have.",
            "Полученный урон постепенно возвращается лечением. Чем меньше здоровья, тем больше брони.");

        public override Words Description => new Words(
            $"{Tx.Healing(Tx.Pct(fraction.Value))} {Tx.Stack("approaches 100% with stacks")} {Tx.Healing("of damage taken")} " +
            $"is healed back over {Tx.Healing(Tx.Seconds(duration.Value).En)}; every hit is restored separately. " +
            $"Gain {Tx.Utility("armor")} the lower your health is, up to {Tx.Utility(Tx.Num(armor.Value) + " armor")} {Tx.Stack("+" + Tx.Num(armorPerStack.Value) + " per stack")} near 0%.",
            $"{Tx.Healing(Tx.Pct(fraction.Value))} {Tx.Stack("копии приближают к 100%")} {Tx.Healing("полученного урона")} " +
            $"возвращается лечением за {Tx.Healing(Tx.Seconds(duration.Value).Ru)}; каждый удар возвращается отдельно. " +
            $"Чем меньше здоровья, тем больше {Tx.Utility("брони")}: у самого нуля — {Tx.Utility(Tx.Num(armor.Value))} {Tx.Stack("+" + Tx.Num(armorPerStack.Value) + " за копию")}.");

        public override Words Lore => new Words(
            "Input: suffering. Output: also suffering, but later and in reverse.",
            "На входе — боль. На выходе — тоже боль, только позже и задом наперёд.");

        private ConfigEntry<float> fraction, fractionPerStack, duration, armor, armorPerStack;

        protected override void BindConfig(ItemSettings s)
        {
            fraction = s.Float("Fraction", 0.5f, 0f, 1f, new Words("Damage restored", "Возврат урона"),
                new Words("Share of every hit you take that is healed back with the first copy (0.5 = 50%). Capped at 100%.",
                          "Доля каждого полученного удара, которая вернётся лечением, с первой копией (0.5 = 50%). Не больше 100%."), "{0:P0}");
            fractionPerStack = s.Float("FractionPerStack", 0.1f, 0f, 0.99f, new Words("Gap closed per stack", "Доля остатка за копию"),
                new Words("Each extra copy closes this share of what is left up to 100%; never reaches 100%. 0.1: 50% → 55% → 59.5% → 63.6%…",
                          "Каждая копия сверх первой закрывает эту долю от остатка до 100%; 100% не достигается никогда. 0.1: 50% → 55% → 59.5% → 63.6%…"), "{0:P0}");
            duration = s.Float("Duration", 20f, 0.5f, 120f, new Words("Restore time", "Время возврата"),
                new Words("Time to heal back one hit, seconds. Does not change with stacks.", "За сколько секунд возвращается один удар. От копий не зависит."), "{0:0.#} s");
            armor = s.Float("MaxArmor", 10f, 0f, 1000f, new Words("Armor near 0% health", "Броня у нуля здоровья"),
                new Words("Armor at almost no health with the first copy; scales linearly with missing health + shield.",
                          "Броня почти без здоровья с первой копией; растёт линейно с потерянным здоровьем + щитом."), "{0:0}");
            armorPerStack = s.Float("MaxArmorPerStack", 10f, 0f, 1000f, new Words("Armor per stack", "Броня за копию"),
                new Words("Extra maximum armor per additional copy.", "Прибавка максимальной брони за каждую копию сверх первой."), "{0:0}");
        }

        private BuffDef restoring, armored;
        internal static PainConverter Instance;

        protected override void Hook()
        {
            Instance = this;
            restoring = BuffFactory.Create("PAIN_RESTORE", BuffFactory.Hex("#ff5a4d"), isDebuff: false, canStack: true);
            armored = BuffFactory.Create("PAIN_ARMOR", BuffFactory.Hex("#c8ced6"), isDebuff: false, canStack: true);
            UseBehavior<PainBehavior>(); // сервер

            GlobalEventManager.onServerDamageDealt += OnDamage;

            RecalculateStatsAPI.GetStatCoefficients += (body, args) =>
            {
                if (armored == null) return;
                int stacks = body.GetBuffCount(armored);
                if (stacks > 0) args.armorAdd += stacks;
            };
        }

        // 1 − (1 − база)·(1 − шаг)^(n−1): каждая копия закрывает долю остатка до 100%, 100% — асимптота (0.7.3).
        // Потолок 0.999 — на случай, если float округлит к единице на сотне копий.
        private float FractionFor(int n) =>
            Mathf.Min(0.999f, 1f - (1f - Mathf.Clamp01(fraction.Value)) * Mathf.Pow(1f - Mathf.Clamp01(fractionPerStack.Value), Mathf.Max(0, n - 1)));
        private float MaxArmorFor(int n) => Stacking.Linear(n, armor.Value, armorPerStack.Value);

        private void OnDamage(DamageReport report)
        {
            if (report == null || report.damageDealt <= 0f) return;
            var body = report.victimBody;
            int n = Count(body);
            if (n <= 0) return;
            var behavior = body.GetComponent<PainBehavior>();
            if (!behavior || !behavior.enabled) return;
            behavior.Add(report.damageDealt * FractionFor(n), duration.Value);
        }

        /// <summary>Сервер: список возвратов, их лечение и броня.</summary>
        private sealed class PainBehavior : CharacterBody.ItemBehavior
        {
            private struct Restore { public float left, rate; }

            private readonly List<Restore> restores = new List<Restore>();
            private float pending, tickTimer;
            private int shownRestores, shownArmor;
            private const float TickInterval = 0.1f;

            public void Add(float amount, float time)
            {
                if (amount <= 0f || time <= 0f) return;
                restores.Add(new Restore { left = amount, rate = amount / time });
            }

            private void FixedUpdate()
            {
                if (!NetworkServer.active || !body || !body.healthComponent) return;
                var self = Instance;
                if (self == null) return;

                float dt = Time.fixedDeltaTime;
                for (int i = restores.Count - 1; i >= 0; i--)
                {
                    var r = restores[i];
                    float give = Mathf.Min(r.left, r.rate * dt);
                    pending += give;
                    r.left -= give;
                    if (r.left <= 0.0001f) restores.RemoveAt(i);
                    else restores[i] = r;
                }

                tickTimer -= dt;
                if (tickTimer > 0f) return;
                tickTimer = TickInterval;

                var health = body.healthComponent;
                if (pending > 0f)
                {
                    if (health.alive) health.Heal(pending, default, nonRegen: false);
                    pending = 0f;
                }

                if (self.restoring && shownRestores != restores.Count)
                {
                    shownRestores = restores.Count;
                    body.SetBuffCount(self.restoring.buffIndex, shownRestores);
                }

                int wantedArmor = 0;
                if (health.alive)
                {
                    float missing = 1f - Mathf.Clamp01(health.combinedHealthFraction);
                    wantedArmor = Mathf.RoundToInt(self.MaxArmorFor(System.Math.Max(1, self.Count(body))) * missing);
                }
                if (self.armored && shownArmor != wantedArmor)
                {
                    shownArmor = wantedArmor;
                    body.SetBuffCount(self.armored.buffIndex, wantedArmor);
                }
            }

            private void OnDisable()
            {
                restores.Clear();
                pending = 0f;
                var self = Instance;
                if (NetworkServer.active && body && self != null)
                {
                    if (self.restoring && shownRestores != 0) body.SetBuffCount(self.restoring.buffIndex, 0);
                    if (self.armored && shownArmor != 0) body.SetBuffCount(self.armored.buffIndex, 0);
                }
                shownRestores = shownArmor = 0;
            }
        }
    }
}
