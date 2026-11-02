using System.Collections.Generic;
using BepInEx.Configuration;
using Destroyer.Content;
using Destroyer.Core;
using RoR2;
using UnityEngine;

namespace Destroyer.Items
{
    /// <summary>
    /// Нитроглицериновая вата (зелёный). 40% при попадании — взрыв вокруг цели: самой цели не вредит, всех врагов
    /// в радиусе 10 (+4) м бьёт на 30% ИТОГОВОГО урона попадания ("TOTAL damage": damageInfo.damage, крит — тот же).
    ///
    /// Как Brilliant Behemoth: шанс × proc coefficient, эффект взрыва тот же. Взрыв по умолчанию proc 0 (как у
    /// Бегемота) — ничего не прокает и не может породить цепочку взрывов. Если поднять proc в настройках, повторный
    /// взрыв из взрыва всё равно невозможен (флаг на время взрыва).
    /// </summary>
    internal sealed class NitroCotton : ItemBase
    {
        public override string Id => "NITRO_COTTON";
        public override ItemTier Tier => ItemTier.Tier2;
        public override string ModelFrom => "StickyBomb";
        public override ItemTag[] Tags => new[] { ItemTag.Damage };

        public override Words Name => new Words("Nitroglycerin Cotton", "Нитроглицериновая вата");

        public override Words Pickup => new Words(
            "Chance on hit to explode around the target, sparing the target itself.",
            "Шанс при попадании взорвать всё вокруг цели, кроме самой цели.");

        public override Words Description => new Words(
            $"{Tx.Damage(Tx.Pct(chance.Value))} chance on hit to set off an explosion that {Tx.Utility("spares the target")}, but hits every other enemy " +
            $"within {Tx.Damage(Tx.Meters(radius.Value).En)} {Tx.Stack("+" + Tx.Meters(radiusPerStack.Value).En + " per stack")} " +
            $"for {Tx.Damage(Tx.Pct(damageFraction.Value) + " TOTAL damage")}.",
            $"{Tx.Damage(Tx.Pct(chance.Value))} шанс при попадании вызвать взрыв, который {Tx.Utility("не задевает цель")}, но бьёт всех остальных " +
            $"врагов в радиусе {Tx.Damage(Tx.Meters(radius.Value).Ru)} {Tx.Stack("+" + Tx.Meters(radiusPerStack.Value).Ru + " за копию")} " +
            $"на {Tx.Damage(Tx.Pct(damageFraction.Value) + " ОБЩЕГО урона")}.");

        public override Words Lore => new Words(
            "Do not shake. Do not drop. Do not hit anything with it. Ever.",
            "Не трясти. Не ронять. Ничего этим не бить. Никогда.");

        private ConfigEntry<float> chance, radius, radiusPerStack, damageFraction, explosionProc;

        protected override void BindConfig(ItemSettings s)
        {
            chance = s.Float("Chance", 0.40f, 0f, 1f, new Words("Explosion chance", "Шанс взрыва"),
                new Words("Chance on hit (0.4 = 40%), scaled by the attack's proc coefficient. Does not grow with stacks.",
                          "Шанс при попадании (0.4 = 40%), умножается на proc coefficient атаки. От копий не растёт."), "{0:P0}");
            radius = s.Float("Radius", 10f, 1f, 60f, new Words("Radius", "Радиус"),
                new Words("Explosion radius with the first copy, meters.", "Радиус взрыва с первой копией, м."), "{0:0.#} m");
            radiusPerStack = s.Float("RadiusPerStack", 4f, 0f, 30f, new Words("Radius per stack", "Радиус за копию"),
                new Words("Extra radius per additional copy, meters.", "Прибавка радиуса за каждую копию сверх первой, м."), "{0:0.#} m");
            damageFraction = s.Float("ExplosionDamage", 0.3f, 0f, 5f, new Words("Explosion damage", "Урон взрыва"),
                new Words("Fraction of the hit's TOTAL damage (0.3 = 30%).", "Доля ОБЩЕГО урона попадания (0.3 = 30%)."), "{0:P0}");
            explosionProc = s.Float("ExplosionProcCoefficient", 0f, 0f, 1f, new Words("Explosion proc coefficient", "Proc coefficient взрыва"),
                new Words("How strongly explosion hits trigger on-hit items. 0 like Brilliant Behemoth: no chains.",
                          "Насколько попадания взрыва запускают предметы «при попадании». 0 — как у Бегемота: без цепочек."), "{0:0.##}");
        }

        private bool exploding;
        private readonly List<HurtBox> hits = new List<HurtBox>();

        protected override void Hook() => HitEvents.OnHit += OnHit;

        private void OnHit(DamageInfo info, CharacterBody attacker, CharacterBody victim)
        {
            if (exploding) return; // взрыв из взрыва — нет
            int n = Count(attacker);
            if (n <= 0 || !attacker.teamComponent) return;
            if (!Roll(chance.Value, info.procCoefficient, attacker.master)) return;

            float r = Stacking.Linear(n, radius.Value, radiusPerStack.Value);
            Vector3 center = victim.corePosition;
            GameAssets.SpawnEffect(GameAssets.BehemothExplosion, center, r);

            hits.Clear();
            new SphereSearch
            {
                origin = center,
                radius = r,
                mask = LayerIndex.entityPrecise.mask,
                queryTriggerInteraction = QueryTriggerInteraction.UseGlobal,
            }
                .RefreshCandidates()
                .FilterCandidatesByHurtBoxTeam(TeamMask.GetEnemyTeams(attacker.teamComponent.teamIndex))
                .FilterCandidatesByDistinctHurtBoxEntities()
                .GetHurtBoxes(hits);

            float proc = explosionProc.Value;
            exploding = true;
            try
            {
                foreach (var hurtBox in hits)
                {
                    if (!hurtBox) continue;
                    var target = hurtBox.healthComponent;
                    if (!target || !target.alive || target == victim.healthComponent) continue; // саму цель не трогаем

                    var blast = new DamageInfo
                    {
                        attacker = attacker.gameObject,
                        inflictor = attacker.gameObject,
                        damage = info.damage * damageFraction.Value,
                        crit = info.crit,
                        position = hurtBox.gameObject.transform.position,
                        procCoefficient = proc,
                        procChainMask = info.procChainMask,
                        damageColorIndex = DamageColorIndex.Item,
                        damageType = DamageType.AOE,
                    };
                    target.TakeDamage(blast);
                    if (proc > 0f && GlobalEventManager.instance)
                        GlobalEventManager.instance.OnHitEnemy(blast, target.gameObject);
                }
            }
            finally
            {
                exploding = false;
            }
        }
    }
}
