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
    /// Книга высасывания жизни (красный, переделана в 0.14.1 — теперь именно реген, а не урон). У врагов в 35 (+15) м
    /// регенерация ниже на 10 (+10) ОЗ/с (с уровнем врага, как ванильный реген: ×(1 + 0.2·(уровень − 1))), а твоя
    /// регенерация выше на пятую часть всего, что у них отнято.
    ///
    /// Враги: дебафф LIFE_DRAIN через EnemyAura, стаки = копии сильнейшего носителя рядом → RecalculateStatsAPI:
    /// baseRegenAdd −= d, levelRegenAdd −= 0.2·d (R2API масштабирует уровнем сам). У большинства монстров реген 0, так
    /// что он уходит в минус — и ВАНИЛЬ сама снимает здоровье (HealthComponent.ServerFixedUpdate: отрицательный
    /// аккумулятор → health −=, на нуле Suicide). Это не урон: без цифр, без проков, броня не мешает.
    /// Suicide зовётся без убийцы — ни денег, ни опыта. Поэтому On-хук HealthComponent.Suicide: если убийцы нет, а на
    /// теле наш дебафф — убийца = носитель, который пил его последним (IL Suicide: killerOverride → DamageInfo.attacker).
    ///
    /// Носитель: сервер раз в 0.5 с считает, сколько регена отнято у врагов в его радиусе (по их уровню), ÷5, и пишет
    /// в стаки своего баффа LIFE_DRAIN_GAIN в единицах 0.1 ОЗ/с → RecalculateStatsAPI: baseRegenAdd += стаки/10.
    /// </summary>
    internal sealed class LifeDrainBook : ItemBase
    {
        public override string Id => "LIFE_DRAIN_BOOK";
        public override ItemTier Tier => ItemTier.Tier3;
        public override string ModelFrom => "BonusGoldPackOnKill"; // Ghor's Tome — книга
        public override ItemTag[] Tags => new[] { ItemTag.Damage, ItemTag.Healing };

        public override Words Name => new Words("Book of Life Drain", "Книга высасывания жизни");

        public override Words Pickup => new Words(
            "Nearby enemies lose health regeneration. You gain a part of it.",
            "Враги рядом теряют регенерацию. Часть достаётся тебе.");

        public override Words Description => new Words(
            $"Enemies within {Tx.Damage(Tx.Meters(radius.Value).En)} {Tx.Stack("+" + Tx.Meters(radiusPerStack.Value).En + " per stack")} " +
            $"have {Tx.Damage("-" + Tx.Num(drain.Value) + " health regeneration")} {Tx.Stack("-" + Tx.Num(drainPerStack.Value) + " per stack")} (scales with their level; can go negative). " +
            $"You {Tx.Healing("regenerate " + Tx.Pct(1f / Mathf.Max(1f, share.Value)) + " of all regeneration taken")}.",
            $"У врагов в {Tx.Damage(Tx.Meters(radius.Value).Ru)} {Tx.Stack("+" + Tx.Meters(radiusPerStack.Value).Ru + " за копию")} " +
            $"{Tx.Damage("регенерация ниже на " + Tx.Num(drain.Value) + " ОЗ/с")} {Tx.Stack("−" + Tx.Num(drainPerStack.Value) + " за копию")} (растёт с их уровнем; может уйти в минус). " +
            $"Твоя {Tx.Healing("регенерация выше на " + Tx.Pct(1f / Mathf.Max(1f, share.Value)) + " от всего отнятого")}.");

        public override Words Lore => new Words(
            "Every page is a little thinner than the last. So is everyone nearby.",
            "Каждая страница чуть тоньше предыдущей. Как и все вокруг.");

        private ConfigEntry<float> radius, radiusPerStack, drain, drainPerStack, share;

        protected override void BindConfig(ItemSettings s)
        {
            radius = s.Float("Radius", 35f, 1f, 300f, new Words("Radius", "Радиус"),
                new Words("Radius with the first copy, meters.", "Радиус с первой копией, м."), "{0:0} m");
            radiusPerStack = s.Float("RadiusPerStack", 15f, 0f, 300f, new Words("Radius per stack", "Радиус за копию"),
                new Words("Extra radius per additional copy, meters.", "Прибавка радиуса за каждую копию сверх первой, м."), "{0:0} m");
            drain = s.Float("Drain", 10f, 0f, 1000f, new Words("Regen reduction", "Снижение регена"),
                new Words("Enemy regeneration reduction with the first copy, hp/s at level 1 (+20% per enemy level).",
                          "Снижение регенерации врагов с первой копией, ОЗ/с на 1-м уровне (+20% за уровень врага)."), "{0:0.#}");
            drainPerStack = s.Float("DrainPerStack", 10f, 0f, 1000f, new Words("Regen reduction per stack", "Снижение за копию"),
                new Words("Extra reduction per additional copy.", "Прибавка за каждую копию сверх первой."), "{0:0.#}");
            share = s.Float("HealDivisor", 5f, 1f, 1000f, new Words("Heal divisor", "Делитель лечения"),
                new Words("Your regeneration grows by (taken regeneration) / this. 5 = one fifth.",
                          "Твоя регенерация растёт на (отнятое) / это. 5 = пятая часть."), "{0:0.#}");
        }

        private BuffDef drained, gain;
        private EnemyAura aura;
        private readonly List<EnemyAura.Source> sources = new List<EnemyAura.Source>();
        private readonly List<CharacterBody> holders = new List<CharacterBody>();
        private readonly Dictionary<CharacterBody, CharacterBody> lastDrainer = new Dictionary<CharacterBody, CharacterBody>();
        private readonly List<CharacterBody> stale = new List<CharacterBody>();
        private const float TickInterval = 0.5f;
        private float tickTimer;

        private float RadiusFor(int n) => Stacking.Linear(n, radius.Value, radiusPerStack.Value);
        private float DrainFor(int n) => Stacking.Linear(n, drain.Value, drainPerStack.Value);
        private static float LevelScale(CharacterBody body) => 1f + 0.2f * Mathf.Max(0f, body.level - 1f);

        protected override void Hook()
        {
            drained = BuffFactory.Create("LIFE_DRAIN", BuffFactory.Hex("#9b1d3a"), isDebuff: true, canStack: true);
            gain = BuffFactory.Create("LIFE_DRAIN_GAIN", BuffFactory.Hex("#d94a6a"), isDebuff: false, canStack: true);
            gain.isHidden = true; // число в 0.1 ОЗ/с — на иконке только запутает
            aura = new EnemyAura(drained);

            RecalculateStatsAPI.GetStatCoefficients += (body, args) =>
            {
                int n = drained ? body.GetBuffCount(drained) : 0;
                if (n > 0)
                {
                    float d = DrainFor(n);
                    args.baseRegenAdd -= d;
                    args.levelRegenAdd -= 0.2f * d;
                }
                int g = gain ? body.GetBuffCount(gain) : 0;
                if (g > 0) args.baseRegenAdd += g / 10f;
            };

            On.RoR2.HealthComponent.Suicide += OnSuicide;
            RoR2Application.onFixedUpdate += Tick;
            Run.onRunStartGlobal += run => { aura.Clear(); lastDrainer.Clear(); };
        }

        private void OnSuicide(On.RoR2.HealthComponent.orig_Suicide orig, HealthComponent self, GameObject killerOverride, GameObject inflictorOverride, DamageTypeCombo damageType)
        {
            // Смерть от нашего отрицательного регена: засчитать носителю, иначе ни денег, ни опыта.
            if (NetworkServer.active && !killerOverride && self && self.body && drained && self.body.HasBuff(drained)
                && lastDrainer.TryGetValue(self.body, out var holder) && holder)
                killerOverride = holder.gameObject;
            orig(self, killerOverride, inflictorOverride, damageType);
        }

        private void Tick()
        {
            if (!NetworkServer.active || !Run.instance) return;
            tickTimer -= Time.fixedDeltaTime;
            if (tickTimer > 0f) return;
            tickTimer = TickInterval;

            holders.Clear();
            sources.Clear();
            foreach (var body in CharacterBody.readOnlyInstancesList)
            {
                if (!body || !body.healthComponent || !body.healthComponent.alive) continue;
                int n = Count(body);
                if (n > 0)
                {
                    holders.Add(body);
                    sources.Add(new EnemyAura.Source { Holder = body, Radius = RadiusFor(n), Stacks = n });
                }
                else if (gain && body.GetBuffCount(gain) > 0) body.SetBuffCount(gain.buffIndex, 0); // выбросил книгу
            }
            aura.Apply(sources);

            stale.Clear();
            foreach (var key in lastDrainer.Keys) if (!key) stale.Add(key);
            foreach (var key in stale) lastDrainer.Remove(key);

            foreach (var holder in holders)
            {
                int n = Count(holder);
                float r = RadiusFor(n);
                Vector3 center = holder.corePosition;
                float taken = 0f;
                foreach (var enemy in CharacterBody.readOnlyInstancesList)
                {
                    if (!Geometry.AreEnemies(holder, enemy) || !enemy.healthComponent || !enemy.healthComponent.alive) continue;
                    if ((enemy.corePosition - center).magnitude - enemy.bestFitRadius > r) continue;
                    // Отнято у врага — по силе дебаффа на нём (сильнейшая книга рядом), а не по своей.
                    int onEnemy = enemy.GetBuffCount(drained);
                    if (onEnemy <= 0) continue;
                    taken += DrainFor(onEnemy) * LevelScale(enemy);
                    lastDrainer[enemy] = holder;
                }
                int wanted = Mathf.Min(Mathf.RoundToInt(taken / Mathf.Max(1f, share.Value) * 10f), 100000);
                if (holder.GetBuffCount(gain) != wanted) holder.SetBuffCount(gain.buffIndex, wanted);
            }
        }
    }
}
