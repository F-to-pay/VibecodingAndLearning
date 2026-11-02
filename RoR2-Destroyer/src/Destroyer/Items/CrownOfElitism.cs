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
    /// Корона Элитаризма (красный). Обычная ходьба на 50% медленнее (на бег не влияет). 5 с без бега и без нанесённого
    /// урона — вокруг тебя аура 50 (+20) м: враги в ней −15% (−2%) скорости атаки, −15% (−2%) урона и −30 (−5) брони.
    /// 0.12.2: радиус +20 м за копию (было +5); смерть врага во включённой ауре (кто бы ни убил) лечит носителя на 0 (+5%) макс. здоровья.
    /// Бег или прямая атака (урон основного, вторичного или особого навыка) гасят ауру и запускают отсчёт заново.
    /// Не гасят: рывки/мобильность (Utility), урон предметов, DoT, снаряжение, полученный урон (0.12.1; в 0.12.0 гасил любой урон).
    ///
    /// Ходьба: RecalculateStatsAPI, когда !isSprinting — moveSpeedReductionMultAdd += p/(1−p) (игра делит скорость на
    /// 1 + сумма замедлений, IL RecalculateStats 1.5; p = 0.5 → +1 → ÷2). isSprinting синкается на сервер и сам зовёт
    /// RecalculateStats при смене (IL set_isSprinting), так что бег сразу быстрый.
    /// Отсчёт — сервер: таймер на носителя, сброс при isSprinting и в onServerDamageDealt, где атакующий — носитель
    /// и урон — прямая атака (IsDirectAttack). Готово — бафф CROWN_REIGN на себе (видно) и
    /// дебафф CROWN_OPPRESSION врагам через EnemyAura (стаки = копии носителя, сильнейший побеждает).
    /// </summary>
    internal sealed class CrownOfElitism : ItemBase
    {
        public override string Id => "CROWN_OF_ELITISM";
        public override ItemTier Tier => ItemTier.Tier3;
        public override string ModelFrom => "BarrierOnOverHeal"; // Aegis — золотое и пафосное
        public override ItemTag[] Tags => new[] { ItemTag.Utility };

        public override Words Name => new Words("Crown of Elitism", "Корона Элитаризма");

        public override Words Pickup => new Words(
            "Walk slower. Hold your fire long enough and nearby enemies become weaker.",
            "Ходьба медленнее. Не атакуй какое-то время — и враги вокруг ослабнут.");

        public override Words Description => new Words(
            $"{Tx.Health("Walking speed is reduced by " + Tx.Pct(walkPenalty.Value))} (sprinting is not affected). " +
            $"After {Tx.Utility(Tx.Seconds(calmTime.Value).En)} without sprinting or attacking, enemies within {Tx.Utility(Tx.Meters(radius.Value).En)} {Tx.Stack("+" + Tx.Meters(radiusPerStack.Value).En + " per stack")} " +
            $"have {Tx.Damage("-" + Tx.Pct(attackSlow.Value) + " attack speed")} {Tx.Stack("-" + Tx.Pct(attackSlowPerStack.Value) + " per stack")}, " +
            $"{Tx.Damage("-" + Tx.Pct(weaken.Value) + " damage")} {Tx.Stack("-" + Tx.Pct(weakenPerStack.Value) + " per stack")} " +
            $"and {Tx.Damage("-" + Tx.Num(armor.Value) + " armor")} {Tx.Stack("-" + Tx.Num(armorPerStack.Value) + " per stack")}. An enemy dying inside the active aura heals you for {Tx.Healing(Tx.Pct(killHeal.Value) + " max health")} {Tx.Stack("+" + Tx.Pct(killHealPerStack.Value) + " per stack")}. " +
            $"Sprinting or hitting with your primary, secondary or special skill ends it; mobility skills, items and taking damage do not.",
            $"{Tx.Health("Ходьба медленнее на " + Tx.Pct(walkPenalty.Value))} (бег не трогает). " +
            $"После {Tx.Utility(Tx.Seconds(calmTime.Value).Ru)} без бега и без атак враги в {Tx.Utility(Tx.Meters(radius.Value).Ru)} {Tx.Stack("+" + Tx.Meters(radiusPerStack.Value).Ru + " за копию")} " +
            $"получают {Tx.Damage("−" + Tx.Pct(attackSlow.Value) + " к скорости атаки")} {Tx.Stack("−" + Tx.Pct(attackSlowPerStack.Value) + " за копию")}, " +
            $"{Tx.Damage("−" + Tx.Pct(weaken.Value) + " к урону")} {Tx.Stack("−" + Tx.Pct(weakenPerStack.Value) + " за копию")} " +
            $"и {Tx.Damage("−" + Tx.Num(armor.Value) + " брони")} {Tx.Stack("−" + Tx.Num(armorPerStack.Value) + " за копию")}. Смерть врага во включённой ауре лечит тебя на {Tx.Healing(Tx.Pct(killHeal.Value) + " макс. здоровья")} {Tx.Stack("+" + Tx.Pct(killHealPerStack.Value) + " за копию")}. " +
            $"Бег или удар основным, вторичным или особым навыком гасят ауру; рывки, предметы и полученный урон — нет.");

        public override Words Lore => new Words(
            "True power does not run. It waits for you to come to it.",
            "Настоящая власть не бегает. Она ждёт, пока ты сам придёшь.");

        private ConfigEntry<float> walkPenalty, calmTime, radius, radiusPerStack, attackSlow, attackSlowPerStack, weaken, weakenPerStack, armor, armorPerStack, killHeal, killHealPerStack;

        protected override void BindConfig(ItemSettings s)
        {
            walkPenalty = s.Float("WalkPenalty", 0.5f, 0f, 0.95f, new Words("Walking slow", "Замедление ходьбы"),
                new Words("Walking (not sprinting) speed reduction (0.5 = -50%).", "Насколько медленнее ходьба без бега (0.5 = −50%)."), "{0:P0}");
            calmTime = s.Float("CalmTime", 5f, 0f, 60f, new Words("Calm time", "Время спокойствия"),
                new Words("Seconds without sprinting or dealing damage before the aura appears.", "Сколько секунд без бега и без урона до появления ауры."), "{0:0.#} s");
            radius = s.Float("Radius", 50f, 1f, 300f, new Words("Aura radius", "Радиус ауры"),
                new Words("Aura radius with the first copy, meters.", "Радиус ауры с первой копией, м."), "{0:0} m");
            radiusPerStack = s.Float("RadiusPerStack", 20f, 0f, 300f, new Words("Radius per stack", "Радиус за копию"),
                new Words("Extra radius per additional copy, meters.", "Прибавка радиуса за каждую копию сверх первой, м."), "{0:0} m");
            attackSlow = s.Float("AttackSpeedReduction", 0.15f, 0f, 0.95f, new Words("Enemy attack speed loss", "Снижение скорости атаки врагов"),
                new Words("Enemy attack speed reduction with the first copy (0.15 = -15%).", "Снижение скорости атаки врагов с первой копией (0.15 = −15%)."), "{0:P0}");
            attackSlowPerStack = s.Float("AttackSpeedReductionPerStack", 0.02f, 0f, 0.95f, new Words("…per stack", "…за копию"),
                new Words("Extra reduction per additional copy (0.02 = -2%). Capped at 95% total.", "Прибавка за каждую копию сверх первой (0.02 = −2%). Не больше 95% всего."), "{0:P0}");
            weaken = s.Float("DamageReduction", 0.15f, 0f, 0.95f, new Words("Enemy damage loss", "Снижение урона врагов"),
                new Words("Enemy damage reduction with the first copy (0.15 = -15%).", "Снижение урона врагов с первой копией (0.15 = −15%)."), "{0:P0}");
            weakenPerStack = s.Float("DamageReductionPerStack", 0.02f, 0f, 0.95f, new Words("…per stack", "…за копию"),
                new Words("Extra reduction per additional copy (0.02 = -2%). Capped at 95% total.", "Прибавка за каждую копию сверх первой (0.02 = −2%). Не больше 95% всего."), "{0:P0}");
            armor = s.Float("ArmorReduction", 30f, 0f, 500f, new Words("Enemy armor loss", "Снижение брони врагов"),
                new Words("Armor removed from enemies with the first copy.", "Сколько брони снимается с врагов с первой копией."), "{0:0}");
            killHeal = s.Float("KillHeal", 0f, 0f, 1f, new Words("Heal per kill", "Лечение за убийство"),
                new Words("Max health healed when an enemy dies inside your active aura, first copy (0 = none).",
                          "Доля макс. здоровья за смерть врага внутри включённой ауры, с первой копией (0 = нет)."), "{0:P0}");
            killHealPerStack = s.Float("KillHealPerStack", 0.05f, 0f, 1f, new Words("Heal per kill per stack", "Лечение за убийство за копию"),
                new Words("Extra heal per additional copy (0.05 = +5% max health).", "Прибавка за каждую копию сверх первой (0.05 = +5% макс. здоровья)."), "{0:P0}");
            armorPerStack = s.Float("ArmorReductionPerStack", 5f, 0f, 500f, new Words("…per stack", "…за копию"),
                new Words("Extra armor removed per additional copy.", "Прибавка за каждую копию сверх первой."), "{0:0}");
        }

        private BuffDef reign, oppression;
        private EnemyAura aura;
        private readonly Dictionary<CharacterBody, float> calm = new Dictionary<CharacterBody, float>();
        private readonly List<EnemyAura.Source> sources = new List<EnemyAura.Source>();
        private readonly List<CharacterBody> stale = new List<CharacterBody>();
        private const float TickInterval = 0.25f;
        private float tickTimer;

        protected override void Hook()
        {
            reign = BuffFactory.Create("CROWN_REIGN", BuffFactory.Hex("#ffd84d"), isDebuff: false, canStack: false);
            oppression = BuffFactory.Create("CROWN_OPPRESSION", BuffFactory.Hex("#b08cff"), isDebuff: true, canStack: true);
            aura = new EnemyAura(oppression);

            RecalculateStatsAPI.GetStatCoefficients += (body, args) =>
            {
                // Носитель: медленная ходьба.
                if (!body.isSprinting && Count(body) > 0)
                {
                    float p = Mathf.Clamp(walkPenalty.Value, 0f, 0.95f);
                    args.moveSpeedReductionMultAdd += p / (1f - p);
                }

                // Враг в ауре.
                int n = oppression ? body.GetBuffCount(oppression) : 0;
                if (n <= 0) return;
                args.attackSpeedTotalMult *= 1f - Mathf.Min(0.95f, Stacking.Linear(n, attackSlow.Value, attackSlowPerStack.Value));
                args.damageTotalMult *= 1f - Mathf.Min(0.95f, Stacking.Linear(n, weaken.Value, weakenPerStack.Value));
                args.armorAdd -= Stacking.Linear(n, armor.Value, armorPerStack.Value);
            };

            GlobalEventManager.onServerDamageDealt += report =>
            {
                var attacker = report != null ? report.attackerBody : null;
                if (!attacker || report.damageDealt <= 0f || report.victimBody == attacker || !calm.ContainsKey(attacker)) return;
                if (!IsDirectAttack(report.damageInfo)) return;
                calm[attacker] = 0f;
            };

            RoR2Application.onFixedUpdate += Tick;
            GlobalEventManager.onCharacterDeathGlobal += OnDeath;
            Run.onRunStartGlobal += run => { calm.Clear(); aura.Clear(); };
        }

        /// <summary>
        /// "Прямая атака" (0.12.1): урон с меткой навыка Primary/Secondary/Special (DamageTypeCombo.damageSource, флаги
        /// с 1.3 — ими же игра различает урон навыков). Utility — это рывки/мобильность, не считаем. Не считаются и
        /// предметы (у них NoneSpecified), DoT (флаг DOT и dotIndex), снаряжение, окружение (Hazard).
        /// </summary>
        private static bool IsDirectAttack(DamageInfo info)
        {
            if (info == null || info.dotIndex != DotController.DotIndex.None) return false;
            var source = info.damageType.damageSource;
            const DamageSource attacks = DamageSource.Primary | DamageSource.Secondary | DamageSource.Special;
            return (source & attacks) != 0 && (source & DamageSource.DOT) == 0;
        }

        private float RadiusFor(int n) => Stacking.Linear(n, radius.Value, radiusPerStack.Value);

        /// <summary>Сервер: враг умер внутри включённой ауры (кто бы ни убил) — носитель лечится на 0 (+5%) макс. здоровья.</summary>
        private void OnDeath(DamageReport report)
        {
            if (!NetworkServer.active || report == null || reign == null) return;
            var victim = report.victimBody;
            if (!victim) return;
            Vector3 at = victim.corePosition;
            foreach (var holder in calm.Keys)
            {
                if (!holder || !holder.HasBuff(reign) || !holder.healthComponent || !holder.healthComponent.alive) continue;
                if (!Geometry.AreEnemies(holder, victim)) continue;
                int n = Count(holder);
                float heal = Stacking.Linear(n, killHeal.Value, killHealPerStack.Value);
                if (n <= 0 || heal <= 0f) continue;
                if ((at - holder.corePosition).magnitude - victim.bestFitRadius > RadiusFor(n)) continue;
                holder.healthComponent.HealFraction(heal, default(ProcChainMask));
            }
        }

        private void Tick()
        {
            if (!NetworkServer.active || !Run.instance) return;
            tickTimer -= Time.fixedDeltaTime;
            if (tickTimer > 0f) return;
            tickTimer = TickInterval;

            stale.Clear();
            foreach (var key in calm.Keys)
                if (!key || !key.healthComponent || !key.healthComponent.alive || Count(key) <= 0) stale.Add(key);
            foreach (var key in stale)
            {
                if (key && reign && key.HasBuff(reign)) key.RemoveBuff(reign);
                calm.Remove(key);
            }

            sources.Clear();
            foreach (var body in CharacterBody.readOnlyInstancesList)
            {
                if (!body || !body.healthComponent || !body.healthComponent.alive) continue;
                int n = Count(body);
                if (n <= 0) continue;

                calm.TryGetValue(body, out float t);
                t = body.isSprinting ? 0f : t + TickInterval;
                calm[body] = t;

                bool ready = t >= calmTime.Value;
                bool has = body.HasBuff(reign);
                if (ready && !has) body.AddBuff(reign);
                else if (!ready && has) body.RemoveBuff(reign);

                if (ready) sources.Add(new EnemyAura.Source { Holder = body, Radius = RadiusFor(n), Stacks = n });
            }
            aura.Apply(sources);
        }
    }
}
