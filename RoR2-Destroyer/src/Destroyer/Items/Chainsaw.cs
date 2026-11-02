using System.Collections.Generic;
using BepInEx.Configuration;
using Destroyer.Content;
using Destroyer.Core;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace Destroyer.Items
{
    /// <summary>
    /// Бензопила (красный, 0.11.1). Враг впритык (1.5 м между телами) — пила заводится на 3 с: каждые 0.3 с бьёт
    /// БЛИЖАЙШЕГО врага в контакте на 100% (+50%) базового урона, proc 0.3. Потом перезарядка 10 с (0.11.2, было 20).
    /// Скорость атаки разгоняет пилу вдвое сильнее, урон тика делится на неё:
    ///   a = attackSpeed / baseAttackSpeed, частота = 1 + 2·(a − 1), урон тика = урон / a. Потолка частоты нет:
    ///   если тик короче физического кадра, за кадр бьёт несколько раз (предохранитель — 50 за кадр).
    ///
    /// Состояние — ванильные таймер-баффы (видно игроку и синкается само): CHAINSAW_ACTIVE на 3 с и
    /// CHAINSAW_COOLDOWN (isCooldown) на 3 + 10 с с момента запуска, то есть 10 с после конца работы.
    /// Цель — каждый тик заново: ближайший враг в контакте (Geometry.DistanceToSurface от центра минус свой радиус).
    /// Нет никого в контакте — тик пропадает, время работы идёт. Урон — как укус чоризо: TakeDamage + OnHitEnemy/OnHitAll.
    /// (0.11.0: 50% (+30%) раз в 0.2 с, proc 0.4, все в контакте, без перезарядки, потолок 20 тиков/с.)
    /// </summary>
    internal sealed class Chainsaw : ItemBase
    {
        public override string Id => "CHAINSAW";
        public override ItemTier Tier => ItemTier.Tier3;
        public override string ModelFrom => "Thorns"; // Razorwire — зубья
        public override ItemTag[] Tags => new[] { ItemTag.Damage };

        public override Words Name => new Words("Chainsaw", "Бензопила");

        public override Words Pickup => new Words(
            "Get up close to rev up a chainsaw. Attack speed revs it faster.",
            "Подойди вплотную — заведётся бензопила. Скорость атаки её разгоняет.");

        public override Words Description => new Words(
            $"Getting within {Tx.Damage(Tx.Meters(range.Value).En)} of an enemy revs up a chainsaw for {Tx.Damage(Tx.Seconds(duration.Value).En)}: " +
            $"every {Tx.Damage(Tx.Seconds(interval.Value).En)} it hits the nearest enemy for {Tx.Damage(Tx.Pct(damage.Value) + " base damage")} {Tx.Stack("+" + Tx.Pct(damagePerStack.Value) + " per stack")} " +
            $"(proc coefficient {Tx.Num(proc.Value)}). Recharges in {Tx.Utility(Tx.Seconds(cooldown.Value).En)}. " +
            $"{Tx.Utility("Attack speed")} increases the rate {Tx.Utility("twice as much")}, but each hit's damage is divided by your attack speed.",
            $"Подойдя к врагу на {Tx.Damage(Tx.Meters(range.Value).Ru)}, заводишь бензопилу на {Tx.Damage(Tx.Seconds(duration.Value).Ru)}: " +
            $"каждые {Tx.Damage(Tx.Seconds(interval.Value).Ru)} она бьёт ближайшего врага на {Tx.Damage(Tx.Pct(damage.Value) + " базового урона")} {Tx.Stack("+" + Tx.Pct(damagePerStack.Value) + " за копию")} " +
            $"(proc coefficient {Tx.Num(proc.Value)}). Перезарядка {Tx.Utility(Tx.Seconds(cooldown.Value).Ru)}. " +
            $"{Tx.Utility("Скорость атаки")} разгоняет пилу {Tx.Utility("вдвое сильнее")}, но урон каждого удара делится на скорость атаки.");

        public override Words Lore => new Words(
            "It does not need a reason. It needs fuel.",
            "Ей не нужен повод. Ей нужен бензин.");

        private ConfigEntry<float> range, damage, damagePerStack, interval, proc, rateScale, duration, cooldown;

        protected override void BindConfig(ItemSettings s)
        {
            range = s.Float("Range", 1.5f, 0.1f, 10f, new Words("Contact range", "Дистанция контакта"),
                new Words("Gap between you and the enemy's hitbox that counts as touching, meters.",
                          "Зазор между тобой и хитбоксом врага, который считается «впритык», м."), "{0:0.#} m");
            damage = s.Float("Damage", 1f, 0f, 20f, new Words("Damage per tick", "Урон за тик"),
                new Words("Damage per tick with the first copy, in base damage (1 = 100%).", "Урон за тик с первой копией, в базовом уроне (1 = 100%)."), "{0:P0}");
            damagePerStack = s.Float("DamagePerStack", 0.5f, 0f, 20f, new Words("Damage per stack", "Урон за копию"),
                new Words("Extra damage per additional copy (0.5 = +50%).", "Прибавка урона за каждую копию сверх первой (0.5 = +50%)."), "{0:P0}");
            interval = s.Float("Interval", 0.3f, 0.02f, 5f, new Words("Tick interval", "Интервал тиков"),
                new Words("Seconds between ticks without attack speed bonuses.", "Секунд между тиками без бонусов скорости атаки."), "{0:0.##} s");
            proc = s.Float("ProcCoefficient", 0.3f, 0f, 3f, new Words("Proc coefficient", "Proc coefficient"),
                new Words("Proc coefficient of each tick.", "Proc coefficient каждого тика."), "{0:0.##}");
            rateScale = s.Float("AttackSpeedRateScale", 2f, 0f, 10f, new Words("Attack speed → rate", "Скорость атаки → частота"),
                new Words("Tick rate = 1 + this × (attack speed multiplier − 1). 2 = attack speed bonuses count twice.",
                          "Частота = 1 + это × (множитель скорости атаки − 1). 2 = бонусы скорости атаки считаются вдвое."), "{0:0.##}");
            duration = s.Float("Duration", 3f, 0.1f, 60f, new Words("Run time", "Время работы"),
                new Words("How long the chainsaw runs once revved up, seconds.", "Сколько секунд пила работает после запуска."), "{0:0.#} s");
            cooldown = s.Float("Cooldown", 10f, 0f, 600f, new Words("Cooldown", "Перезарядка"),
                new Words("Recharge after the chainsaw stops, seconds.", "Перезарядка после того, как пила заглохла, с."), "{0:0} s");
        }

        private BuffDef active, recharge;

        protected override void Hook()
        {
            active = BuffFactory.Create("CHAINSAW_ACTIVE", BuffFactory.Hex("#ff8a1f"), isDebuff: false, canStack: false);
            recharge = BuffFactory.Create("CHAINSAW_COOLDOWN", BuffFactory.Hex("#8c96a0"), isDebuff: false, canStack: false);
            recharge.isCooldown = true;
            RoR2Application.onFixedUpdate += Tick;
            Run.onRunStartGlobal += run => timers.Clear();
        }

        private readonly Dictionary<CharacterBody, float> timers = new Dictionary<CharacterBody, float>();
        private readonly List<CharacterBody> owners = new List<CharacterBody>();
        private readonly List<CharacterBody> stale = new List<CharacterBody>();
        private const int MaxTicksPerFrame = 50;

        private static float AttackSpeedMult(CharacterBody body) =>
            body.baseAttackSpeed > 0f ? Mathf.Max(0.01f, body.attackSpeed / body.baseAttackSpeed) : 1f;

        private void Tick()
        {
            if (!NetworkServer.active || !Run.instance || active == null) return;
            float dt = Time.fixedDeltaTime;

            stale.Clear();
            foreach (var key in timers.Keys) if (!key || !key.HasBuff(active)) stale.Add(key);
            foreach (var key in stale) timers.Remove(key);

            owners.Clear();
            foreach (var body in CharacterBody.readOnlyInstancesList)
                if (body && body.healthComponent && body.healthComponent.alive && Count(body) > 0) owners.Add(body);

            foreach (var owner in owners)
            {
                if (!owner.HasBuff(active))
                {
                    if (owner.HasBuff(recharge) || !Nearest(owner)) continue;
                    // Запуск: работает duration, перезарядка — cooldown после конца работы.
                    owner.AddTimedBuff(active, duration.Value);
                    if (cooldown.Value > 0f) owner.AddTimedBuff(recharge, duration.Value + cooldown.Value);
                    timers[owner] = 0f; // первый удар сразу
                }

                timers.TryGetValue(owner, out float timer);
                timer -= dt;
                float a = AttackSpeedMult(owner);
                // Ускорение считается вдвое, замедление — один раз (0.14.3): при a < 1 формула 1 + 2(a − 1) уходила в ноль
                // (Ненависть: a = 0.5 → пила раз в 30 с).
                float rate = a >= 1f ? 1f + rateScale.Value * (a - 1f) : a;
                float step = interval.Value / Mathf.Max(0.01f, rate);
                int ticks = 0;
                while (timer <= 0f && ticks < MaxTicksPerFrame)
                {
                    timer += step;
                    ticks++;
                    var target = Nearest(owner);
                    if (!target) continue; // никого рядом — удар в воздух
                    float hit = owner.damage * Stacking.Linear(Count(owner), damage.Value, damagePerStack.Value) / a;
                    Saw(owner, target, hit, owner.RollCrit(), ticks == 1);
                    if (!owner || !owner.healthComponent || !owner.healthComponent.alive) break;
                }
                if (timer <= 0f) timer = step; // предохранитель сработал — не копим долг
                timers[owner] = timer;
            }
        }

        /// <summary>Ближайший живой враг в контакте или null.</summary>
        private CharacterBody Nearest(CharacterBody owner)
        {
            Vector3 from = owner.corePosition;
            float reach = range.Value + owner.radius;
            CharacterBody best = null;
            float bestDistance = float.MaxValue;
            foreach (var enemy in CharacterBody.readOnlyInstancesList)
            {
                if (!Geometry.AreEnemies(owner, enemy) || !enemy.healthComponent || !enemy.healthComponent.alive) continue;
                if (Vector3.Distance(from, enemy.corePosition) - enemy.bestFitRadius > reach + 5f) continue; // грубый отсев
                float d = Geometry.DistanceToSurface(enemy, from);
                if (d > reach || d >= bestDistance) continue;
                best = enemy;
                bestDistance = d;
            }
            return best;
        }

        private void Saw(CharacterBody owner, CharacterBody enemy, float amount, bool crit, bool effect)
        {
            if (!enemy || !enemy.healthComponent || !enemy.healthComponent.alive) return;
            Vector3 point = Geometry.ClosestSurfacePoint(enemy, owner.corePosition);
            var info = new DamageInfo
            {
                attacker = owner.gameObject,
                inflictor = owner.gameObject,
                damage = amount,
                crit = crit,
                position = point,
                procCoefficient = proc.Value,
                damageColorIndex = DamageColorIndex.Item,
                damageType = DamageType.Generic,
            };
            enemy.healthComponent.TakeDamage(info);
            if (proc.Value > 0f && GlobalEventManager.instance)
            {
                GlobalEventManager.instance.OnHitEnemy(info, enemy.gameObject);
                GlobalEventManager.instance.OnHitAll(info, enemy.gameObject);
            }
            if (effect) GameAssets.SpawnEffect(GameAssets.Hitspark, point, 1f);
        }
    }
}
