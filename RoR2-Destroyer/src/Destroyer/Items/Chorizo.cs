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
    /// Чоризо (зелёный). Шанс при попадании повесить на врага "Вкусный" (10 с). Подошёл к вкусному врагу вплотную
    /// (3 м до его края) — укус: 600% (+80% за копию) базового урона с proc 1.2 и лечение 10% макс. здоровья.
    /// Метка одноразовая. Кусает любой враг вкусного с чоризо, урон — по его копиям.
    /// </summary>
    internal sealed class Chorizo : ItemBase
    {
        public override string Id => "CHORIZO";
        public override ItemTier Tier => ItemTier.Tier2;
        public override string ModelFrom => "FlatHealth"; // Бизоний стейк
        public override ItemTag[] Tags => new[] { ItemTag.Damage, ItemTag.Healing };

        public override Words Name => new Words("Chorizo", "Чоризо");

        public override Words Pickup => new Words(
            "Chance on hit to make enemies Tasty. Get close to a Tasty enemy to take a bite.",
            "Шанс при попадании сделать врага вкусным. Подойди к вкусному врагу вплотную — и откуси.");

        public override Words Description => new Words(
            $"{Tx.Damage(Tx.Pct(chance.Value))} {Tx.Stack("+" + Tx.Pct(chancePerStack.Value) + " per stack")} chance on hit to make an enemy " +
            $"{Tx.Keyword("Tasty")} for {Tx.Utility(Tx.Seconds(duration.Value).En)}. Come within {Tx.Utility(Tx.Meters(range.Value).En)} of a Tasty enemy " +
            $"to take a bite: deal {Tx.Damage(Tx.Pct(biteDamage.Value) + " base damage")} {Tx.Stack("+" + Tx.Pct(biteDamagePerStack.Value) + " per stack")} " +
            $"and {Tx.Healing("heal " + Tx.Pct(biteHeal.Value) + " of your maximum health")}. One bite per Tasty.",
            $"{Tx.Damage(Tx.Pct(chance.Value))} {Tx.Stack("+" + Tx.Pct(chancePerStack.Value) + " за копию")} шанс при попадании сделать врага " +
            $"{Tx.Keyword("вкусным")} на {Tx.Utility(Tx.Seconds(duration.Value).Ru)}. Подойди к вкусному врагу на {Tx.Utility(Tx.Meters(range.Value).Ru)} " +
            $"и откуси: {Tx.Damage(Tx.Pct(biteDamage.Value) + " базового урона")} {Tx.Stack("+" + Tx.Pct(biteDamagePerStack.Value) + " за копию")} " +
            $"и {Tx.Healing("лечение на " + Tx.Pct(biteHeal.Value) + " макс. здоровья")}. Один укус на одну метку.");

        public override Words Lore => new Words(
            "Smells like victory. Tastes like the enemy.",
            "Пахнет победой. На вкус — как враг.");

        private ConfigEntry<float> chance, chancePerStack, duration, range, biteDamage, biteDamagePerStack, biteHeal, biteProc;

        protected override void BindConfig(ItemSettings s)
        {
            chance = s.Float("TastyChance", 0.04f, 0f, 1f, new Words("Tasty chance", "Шанс «вкусного»"),
                new Words("Chance on hit with the first copy (0.04 = 4%), scaled by the attack's proc coefficient.",
                          "Шанс при попадании с первой копией (0.04 = 4%), умножается на proc coefficient атаки."), "{0:P1}");
            chancePerStack = s.Float("TastyChancePerStack", 0.01f, 0f, 1f, new Words("Tasty chance per stack", "Шанс за копию"),
                new Words("Extra chance per additional copy.", "Прибавка шанса за каждую копию сверх первой."), "{0:P1}");
            duration = s.Float("TastyDuration", 10f, 1f, 60f, new Words("Tasty duration", "Длительность «вкусного»"),
                new Words("How long an enemy stays Tasty, seconds.", "Сколько секунд враг остаётся вкусным."), "{0:0.#} s");
            range = s.Float("BiteRange", 3f, 0.5f, 15f, new Words("Bite range", "Дальность укуса"),
                new Words("Distance from you to the enemy's surface to bite, meters.", "Расстояние от тебя до края врага, при котором случается укус, м."), "{0:0.#} m");
            biteDamage = s.Float("BiteDamage", 6f, 0f, 50f, new Words("Bite damage", "Урон укуса"),
                new Words("Bite damage with the first copy, in base damage (6 = 600%).", "Урон укуса с первой копией в базовом уроне (6 = 600%)."), "{0:P0}");
            biteDamagePerStack = s.Float("BiteDamagePerStack", 0.8f, 0f, 20f, new Words("Bite damage per stack", "Урон укуса за копию"),
                new Words("Extra bite damage per additional copy (0.8 = +80%).", "Прибавка урона укуса за каждую копию сверх первой (0.8 = +80%)."), "{0:P0}");
            biteHeal = s.Float("BiteHeal", 0.10f, 0f, 1f, new Words("Bite heal", "Лечение укусом"),
                new Words("Healing per bite, fraction of maximum health.", "Лечение за укус, доля макс. здоровья."), "{0:P0}");
            biteProc = s.Float("BiteProcCoefficient", 1.2f, 0f, 3f, new Words("Bite proc coefficient", "Proc coefficient укуса"),
                new Words("How strongly the bite triggers on-hit items.", "Насколько укус запускает предметы «при попадании»."), "{0:0.##}");
        }

        private BuffDef tasty;

        protected override void Hook()
        {
            tasty = BuffFactory.Create("TASTY", BuffFactory.Hex("#ff8a3d"), isDebuff: true, canStack: false);

            HitEvents.OnHit += OnHit;
            RoR2Application.onFixedUpdate += BiteTick;
        }

        private void OnHit(DamageInfo info, CharacterBody attacker, CharacterBody victim)
        {
            int n = Count(attacker);
            if (n <= 0 || !victim.healthComponent || !victim.healthComponent.alive) return;
            if (!Roll(Stacking.Linear(n, chance.Value, chancePerStack.Value), info.procCoefficient, attacker.master)) return;
            victim.AddTimedBuff(tasty, duration.Value);
        }

        // ---------- Укус: сервер, 10 раз в секунду ----------

        private const float TickInterval = 0.1f;
        private float tickTimer;
        private readonly List<CharacterBody> eaters = new List<CharacterBody>();
        private readonly List<CharacterBody> meals = new List<CharacterBody>();

        private void BiteTick()
        {
            if (!NetworkServer.active || !Run.instance || tasty == null) return;
            tickTimer -= Time.fixedDeltaTime;
            if (tickTimer > 0f) return;
            tickTimer = TickInterval;

            eaters.Clear();
            meals.Clear();
            foreach (var body in CharacterBody.readOnlyInstancesList)
            {
                if (!body || !body.healthComponent || !body.healthComponent.alive) continue;
                if (body.HasBuff(tasty)) meals.Add(body);
                if (Count(body) > 0) eaters.Add(body);
            }
            if (meals.Count == 0 || eaters.Count == 0) return;

            foreach (var meal in meals)
            {
                CharacterBody best = null;
                float bestDistance = float.MaxValue;
                Vector3 bestPoint = meal.corePosition;
                foreach (var eater in eaters)
                {
                    if (eater == meal || !eater.teamComponent || !meal.teamComponent) continue;
                    if (eater.teamComponent.teamIndex == meal.teamComponent.teamIndex) continue; // своих не едим
                    var point = Geometry.ClosestSurfacePoint(meal, eater.corePosition);
                    float distance = Vector3.Distance(point, eater.corePosition);
                    if (distance <= range.Value && distance < bestDistance)
                    {
                        best = eater;
                        bestDistance = distance;
                        bestPoint = point;
                    }
                }
                if (best) Bite(best, meal, bestPoint);
            }
        }

        private void Bite(CharacterBody eater, CharacterBody meal, Vector3 point)
        {
            meal.ClearTimedBuffs(tasty); // метка одноразовая — снимаем ДО урона, чтобы второй укус не случился

            int n = Count(eater);
            var damage = new DamageInfo
            {
                attacker = eater.gameObject,
                inflictor = eater.gameObject,
                damage = eater.damage * Stacking.Linear(n, biteDamage.Value, biteDamagePerStack.Value),
                crit = eater.RollCrit(),
                position = point,
                procCoefficient = biteProc.Value,
                damageColorIndex = DamageColorIndex.Item,
                damageType = DamageType.Generic,
            };
            meal.healthComponent.TakeDamage(damage);
            if (biteProc.Value > 0f && GlobalEventManager.instance)
            {
                GlobalEventManager.instance.OnHitEnemy(damage, meal.gameObject);
                GlobalEventManager.instance.OnHitAll(damage, meal.gameObject);
            }

            if (eater.healthComponent) eater.healthComponent.HealFraction(biteHeal.Value, default(ProcChainMask));
            GameAssets.SpawnEffect(GameAssets.BloodBurst, point, 1f);
        }
    }
}
