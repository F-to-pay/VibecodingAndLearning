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
    /// Лисий хвост (белый). Подошёл к врагу вплотную (3 м до его поверхности) — невидимость на 3 с.
    /// Перезарядка 30 с, каждая копия сверх первой снижает её на 20% (×0.8, не до нуля).
    ///
    /// Невидимость — ванильный бафф Cloak (как у Old War Stealthkit): враги теряют цель. Перезарядка — свой бафф-кулдаун
    /// (как у ванильных колец), пока он висит, хвост не срабатывает. Проверка близости — сервер, 10 раз в секунду,
    /// расстояние — как у чоризо (Geometry.ClosestSurfacePoint по хитбоксам).
    /// </summary>
    internal sealed class FoxTail : ItemBase
    {
        public override string Id => "FOX_TAIL";
        public override ItemTier Tier => ItemTier.Tier1;
        public override string ModelFrom => "Phasing"; // Old War Stealthkit
        public override ItemTag[] Tags => new[] { ItemTag.Utility };

        public override Words Name => new Words("Fox Tail", "Лисий хвост");

        public override Words Pickup => new Words(
            "Turn invisible when you get up close to an enemy.",
            "Становишься невидимым, подойдя к врагу вплотную.");

        public override Words Description => new Words(
            $"Getting within {Tx.Utility(Tx.Meters(range.Value).En)} of an enemy makes you {Tx.Utility("invisible")} for {Tx.Utility(Tx.Seconds(duration.Value).En)}. " +
            $"Recharges in {Tx.Utility(Tx.Seconds(cooldown.Value).En)} {Tx.Stack("-" + Tx.Pct(cooldownCut.Value) + " per stack")}.",
            $"Подойдя к врагу на {Tx.Utility(Tx.Meters(range.Value).Ru)}, становишься {Tx.Utility("невидимым")} на {Tx.Utility(Tx.Seconds(duration.Value).Ru)}. " +
            $"Перезарядка {Tx.Utility(Tx.Seconds(cooldown.Value).Ru)} {Tx.Stack("-" + Tx.Pct(cooldownCut.Value) + " за копию")}.");

        public override Words Lore => new Words(
            "Nine of them were missing. This one was not.",
            "Девяти не хватало. Этот — на месте.");

        private ConfigEntry<float> range, duration, cooldown, cooldownCut;

        protected override void BindConfig(ItemSettings s)
        {
            range = s.Float("Range", 3f, 0.5f, 15f, new Words("Trigger range", "Дальность срабатывания"),
                new Words("Distance to the enemy's surface that counts as \"up close\", meters.", "Расстояние до поверхности врага, которое считается «вплотную», м."), "{0:0.#} m");
            duration = s.Float("Duration", 3f, 0.5f, 30f, new Words("Invisibility", "Невидимость"),
                new Words("Invisibility duration, seconds.", "Длительность невидимости, с."), "{0:0.#} s");
            cooldown = s.Float("Cooldown", 30f, 1f, 600f, new Words("Cooldown", "Перезарядка"),
                new Words("Cooldown with the first copy, seconds.", "Перезарядка с первой копией, с."), "{0:0} s");
            cooldownCut = s.Float("CooldownCutPerStack", 0.2f, 0f, 0.9f, new Words("Cooldown cut per stack", "Снижение перезарядки за копию"),
                new Words("Each extra copy multiplies the cooldown by (1 - this). 0.2: 30 → 24 → 19.2 s.",
                          "Каждая копия сверх первой умножает перезарядку на (1 - это). 0.2: 30 → 24 → 19.2 с."), "{0:P0}");
        }

        private BuffDef cooldownBuff;

        protected override void Hook()
        {
            cooldownBuff = BuffFactory.Create("FOX_TAIL_COOLDOWN", BuffFactory.Hex("#ff9a4d"), isDebuff: false, canStack: false);
            cooldownBuff.isCooldown = true;
            RoR2Application.onFixedUpdate += Tick;
        }

        private float CooldownFor(int n) => cooldown.Value * Mathf.Pow(1f - cooldownCut.Value, n - 1);

        private const float TickInterval = 0.1f;
        private float tickTimer;
        private readonly List<CharacterBody> owners = new List<CharacterBody>();

        private void Tick()
        {
            if (!NetworkServer.active || !Run.instance || cooldownBuff == null) return;
            tickTimer -= Time.fixedDeltaTime;
            if (tickTimer > 0f) return;
            tickTimer = TickInterval;

            owners.Clear();
            foreach (var body in CharacterBody.readOnlyInstancesList)
                if (body && body.healthComponent && body.healthComponent.alive && Count(body) > 0 && !body.HasBuff(cooldownBuff))
                    owners.Add(body);
            if (owners.Count == 0) return;

            var cloak = RoR2Content.Buffs.Cloak;
            if (!cloak) return;

            foreach (var owner in owners)
            {
                Vector3 from = owner.corePosition;
                foreach (var enemy in CharacterBody.readOnlyInstancesList)
                {
                    if (!Geometry.AreEnemies(owner, enemy) || !enemy.healthComponent || !enemy.healthComponent.alive) continue;
                    // Быстрый отсев по центру, потом точно — по хитбоксам.
                    float rough = Vector3.Distance(from, enemy.corePosition) - enemy.bestFitRadius;
                    if (rough > range.Value + 5f) continue;
                    if (Geometry.DistanceToSurface(enemy, from) > range.Value) continue;

                    owner.AddTimedBuff(cloak, duration.Value);
                    owner.AddTimedBuff(cooldownBuff, CooldownFor(Count(owner)));
                    break;
                }
            }
        }
    }
}
