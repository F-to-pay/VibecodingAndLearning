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
    /// Красные краски (красный, 0.16.0). Шанс крита +10% (от копий НЕ растёт). Каждое критическое попадание (HitEvents,
    /// флаг crit, не DoT) вешает стак "Разложения": он помнит урон этого крита (damageInfo.damage × critMultiplier
    /// атакующего) и навсегда — до смерти цели — снимает 4% (+4%) от него в секунду. Стаки без предела и без таймера.
    ///
    /// Сервер: компонент DecayTracker на жертве копит сумму "урон в секунду" по каждому атакующему и раз в 0.5 с бьёт
    /// DamageInfo (DoT, proc 0, без крита, цвет Poison) — разложение не прокает предметы и не порождает новое разложение.
    /// Атакующий умер/пропал — тик идёт без автора (убийство не засчитается). Видимость — бафф RED_PAINT_DECAY,
    /// число стаков = число критов (кап отображения игры не важен, урон считается отдельно).
    /// </summary>
    internal sealed class RedPaint : ItemBase
    {
        public override string Id => "RED_PAINT";
        public override ItemTier Tier => ItemTier.Tier3;
        public override string ModelFrom => "ScrapRed";
        public override ItemTag[] Tags => new[] { ItemTag.Damage };

        public override Words Name => new Words("Red Paints", "Красные краски");

        public override Words Pickup => new Words(
            "Increase critical chance. Critical hits cause endless, stacking decay.",
            "Шанс крита выше. Криты вызывают бесконечное стакающееся разложение.");

        public override Words Description => new Words(
            $"Gain {Tx.Damage("+" + Tx.Num(crit.Value) + "% critical chance")}. Critical hits inflict {Tx.Damage("decay")} that deals " +
            $"{Tx.Damage(Tx.Pct(decay.Value) + " of the critical hit's damage per second")} {Tx.Stack("+" + Tx.Pct(decay.Value) + " per stack")} {Tx.Damage("until the target dies")}. Stacks infinitely.",
            $"{Tx.Damage("+" + Tx.Num(crit.Value) + "% к шансу крита")}. Криты вызывают {Tx.Damage("разложение")}: " +
            $"{Tx.Damage(Tx.Pct(decay.Value) + " урона крита в секунду")} {Tx.Stack("+" + Tx.Pct(decay.Value) + " за копию")} {Tx.Damage("до смерти цели")}. Стакается бесконечно.");

        public override Words Lore => new Words("It never dries. It just keeps eating.", "Она не сохнет. Она просто ест дальше.");

        private ConfigEntry<float> crit, decay, interval;

        protected override void BindConfig(ItemSettings s)
        {
            crit = s.Float("CritChance", 10f, 0f, 100f, new Words("Critical chance", "Шанс крита"),
                new Words("Flat critical chance, does not grow with stacks (10 = +10%).", "Плоский шанс крита, от копий не растёт (10 = +10%)."), "{0:0.#}%");
            decay = s.Float("DecayPerSecond", 0.04f, 0f, 5f, new Words("Decay per second per stack", "Разложение в секунду за копию"),
                new Words("Share of the critical hit's damage dealt per second per decay stack, per copy (0.04 = 4%).",
                          "Доля урона крита в секунду, которую снимает каждый стак разложения, за копию (0.04 = 4%)."), "{0:P0}");
            interval = s.Float("TickInterval", 0.5f, 0.1f, 5f, new Words("Tick interval", "Интервал тика"),
                new Words("Seconds between decay ticks.", "Секунд между тиками разложения."), "{0:0.##} s");
        }

        internal static RedPaint Instance;
        internal BuffDef Decay;

        protected override void Hook()
        {
            Instance = this;
            Decay = BuffFactory.Create("RED_PAINT_DECAY", BuffFactory.Hex("#b5302a"), isDebuff: true, canStack: true);
            RecalculateStatsAPI.GetStatCoefficients += (body, args) =>
            {
                if (Count(body) > 0) args.critAdd += crit.Value;
            };
            HitEvents.OnHit += OnHit;
        }

        private void OnHit(DamageInfo info, CharacterBody attacker, CharacterBody victim)
        {
            if (!info.crit || info.dotIndex != DotController.DotIndex.None) return;
            int n = Count(attacker);
            if (n <= 0) return;
            var health = victim.healthComponent;
            if (!health || !health.alive) return;
            float dps = info.damage * attacker.critMultiplier * decay.Value * n;
            if (dps <= 0f) return;

            var tracker = victim.GetComponent<DecayTracker>() ?? victim.gameObject.AddComponent<DecayTracker>();
            tracker.Add(attacker.gameObject, dps);
            if (Decay) victim.AddBuff(Decay);
        }

        internal float Interval => Mathf.Max(0.1f, interval.Value);

        /// <summary>Сервер: копит урон в секунду по атакующим и тикает до смерти цели.</summary>
        private sealed class DecayTracker : MonoBehaviour
        {
            private readonly Dictionary<GameObject, float> perAttacker = new Dictionary<GameObject, float>();
            private readonly List<KeyValuePair<GameObject, float>> snapshot = new List<KeyValuePair<GameObject, float>>();
            private float orphaned; // урон в секунду от атакующих, которых уже нет
            private float timer;
            private HealthComponent health;
            private CharacterBody body;

            private void Awake()
            {
                health = GetComponent<HealthComponent>();
                body = GetComponent<CharacterBody>();
            }

            public void Add(GameObject attacker, float dps)
            {
                perAttacker.TryGetValue(attacker, out float have);
                perAttacker[attacker] = have + dps;
            }

            private void FixedUpdate()
            {
                if (!NetworkServer.active || !health || !health.alive || Instance == null) return;
                timer += Time.fixedDeltaTime;
                float step = Instance.Interval;
                if (timer < step) return;
                timer -= step;

                snapshot.Clear();
                snapshot.AddRange(perAttacker);
                foreach (var pair in snapshot)
                {
                    if (!pair.Key) { orphaned += pair.Value; perAttacker.Remove(pair.Key); continue; }
                    Tick(pair.Key, pair.Value * step);
                    if (!health || !health.alive) return;
                }
                if (orphaned > 0f) Tick(null, orphaned * step);
            }

            private void Tick(GameObject attacker, float damage)
            {
                if (damage <= 0f) return;
                health.TakeDamage(new DamageInfo
                {
                    attacker = attacker,
                    inflictor = attacker,
                    damage = damage,
                    crit = false,
                    position = body ? body.corePosition : transform.position,
                    procCoefficient = 0f,
                    damageColorIndex = DamageColorIndex.Poison,
                    damageType = new DamageTypeCombo { damageType = DamageType.DoT, damageSource = DamageSource.DOT },
                });
            }
        }
    }
}
