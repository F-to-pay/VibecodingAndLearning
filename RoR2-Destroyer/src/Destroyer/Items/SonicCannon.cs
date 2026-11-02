using BepInEx.Configuration;
using Destroyer.Content;
using Destroyer.Core;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace Destroyer.Items
{
    /// <summary>
    /// Звуковая пушка (пустотный белый, перерабатывает пылесосы). Раз в 3 с (каждая копия сверх первой ×0.95; 0.16.1, было ×0.7)
    /// бьёт ближайшего врага в 50 м звуковой волной на 320% базового урона (0.7.1, было 4 с / ×0.9 / 30 м / 380%).
    ///
    /// Цель — BullseyeSearch (вражеские команды, ближайший, с прямой видимостью). Нет цели — пушка "заряжена"
    /// и выстрелит сразу, как цель появится. Выстрел — серверный BulletAttack толщиной 1.2 м (как у турелей):
    /// крит по шансу владельца, proc 1, урон без спада по дистанции; трассер и эффекты рассылает сам BulletAttack.
    /// </summary>
    internal sealed class SonicCannon : ItemBase
    {
        public override string Id => "SONIC_CANNON";
        public override ItemTier Tier => ItemTier.VoidTier1;
        public override string Corrupts => "VACUUM";
        public override string ModelFrom => "MissileVoid"; // Plasma Shrimp
        public override ItemTag[] Tags => new[] { ItemTag.Damage };

        public override Words Name => new Words("Sonic Cannon", "Звуковая пушка");

        public override Words Pickup => new Words(
            $"Periodically blast the nearest enemy with a sound wave. {Tx.Void("Corrupts all Vacuums")}.",
            $"Время от времени бьёт ближайшего врага звуковой волной. {Tx.Void("Перерабатывает все пылесосы")}.");

        public override Words Description => new Words(
            $"Every {Tx.Damage(Tx.Seconds(interval.Value).En)} {Tx.Stack("-" + Tx.Pct(intervalCut.Value) + " per stack")}, " +
            $"blast the nearest enemy within {Tx.Damage(Tx.Meters(range.Value).En)} with a sound wave for {Tx.Damage(Tx.Pct(blastDamage.Value) + " base damage")}. " +
            $"{Tx.Void("Corrupts all Vacuums")}.",
            $"Каждые {Tx.Damage(Tx.Seconds(interval.Value).Ru)} {Tx.Stack("-" + Tx.Pct(intervalCut.Value) + " за копию")} " +
            $"бьёт ближайшего врага в {Tx.Damage(Tx.Meters(range.Value).Ru)} звуковой волной на {Tx.Damage(Tx.Pct(blastDamage.Value) + " базового урона")}. " +
            $"{Tx.Void("Перерабатывает все пылесосы")}.");

        public override Words Lore => new Words(
            "It used to pull things in. Now it pushes back. Hard.",
            "Раньше оно всасывало. Теперь выдувает. Сильно.");

        private ConfigEntry<float> interval, intervalCut, range, blastDamage, procCoefficient;

        protected override void BindConfig(ItemSettings s)
        {
            interval = s.Float("Interval", 3f, 0.2f, 60f, new Words("Interval", "Интервал"),
                new Words("Seconds between blasts with the first copy.", "Секунд между выстрелами с первой копией."), "{0:0.#} s");
            intervalCut = s.Float("IntervalCutPerStack", 0.05f, 0f, 0.9f, new Words("Interval cut per stack", "Ускорение за копию"),
                new Words("Each extra copy multiplies the interval by (1 - this). 0.05: 3 → 2.85 → 2.71 s.",
                          "Каждая копия сверх первой умножает интервал на (1 - это). 0.05: 3 → 2.85 → 2.71 с."), "{0:P0}");
            range = s.Float("Range", 50f, 5f, 200f, new Words("Range", "Дальность"),
                new Words("Search radius for the target, meters.", "Радиус поиска цели, м."), "{0:0} m");
            blastDamage = s.Float("Damage", 3.2f, 0f, 100f, new Words("Damage", "Урон"),
                new Words("Blast damage in base damage (3.2 = 320%).", "Урон волны в базовом уроне (3.2 = 320%)."), "{0:P0}");
            procCoefficient = s.Float("ProcCoefficient", 1f, 0f, 3f, new Words("Proc coefficient", "Proc coefficient"),
                new Words("Proc coefficient of the blast.", "Proc coefficient волны."), "{0:0.##}");
        }

        internal static SonicCannon Instance;

        protected override void Hook()
        {
            Instance = this;
            UseBehavior<CannonBehavior>(); // сервер
        }

        private float IntervalFor(int n) => interval.Value * Mathf.Pow(1f - intervalCut.Value, n - 1);

        private HurtBox FindTarget(CharacterBody body)
        {
            var search = new BullseyeSearch
            {
                viewer = body,
                searchOrigin = body.corePosition,
                maxDistanceFilter = range.Value,
                teamMaskFilter = TeamMask.GetEnemyTeams(body.teamComponent ? body.teamComponent.teamIndex : TeamIndex.Player),
                filterByLoS = true,
                filterByDistinctEntity = true,
                sortMode = BullseyeSearch.SortMode.Distance,
            };
            search.RefreshCandidates();
            search.FilterOutGameObject(body.gameObject);
            foreach (var hurtBox in search.GetResults())
                if (hurtBox && hurtBox.healthComponent && hurtBox.healthComponent.alive) return hurtBox;
            return null;
        }

        private void Fire(CharacterBody body, HurtBox target)
        {
            Vector3 origin = body.corePosition;
            Vector3 to = target.transform.position - origin;
            var bullet = new BulletAttack
            {
                owner = body.gameObject,
                weapon = body.gameObject,
                origin = origin,
                damage = body.damage * blastDamage.Value,
                isCrit = body.RollCrit(),
                force = 400f,
                procCoefficient = procCoefficient.Value,
                damageColorIndex = DamageColorIndex.Void,
                falloffModel = BulletAttack.FalloffModel.None,
                radius = 1.2f,
                smartCollision = true,
                tracerEffectPrefab = GameAssets.Load(GameAssets.SmokeTracer),
                hitEffectPrefab = GameAssets.Load(GameAssets.KnockbackImpact),
            };
            bullet.aimVector = to.normalized;
            bullet.maxDistance = to.magnitude + 2f;
            bullet.Fire();
            GameAssets.SpawnEffect(GameAssets.BootShockwave, origin, 3f);
        }

        /// <summary>Сервер: таймер и выстрел.</summary>
        private sealed class CannonBehavior : CharacterBody.ItemBehavior
        {
            private float timer;

            private void FixedUpdate()
            {
                if (!NetworkServer.active || !body || !body.healthComponent || !body.healthComponent.alive) return;
                var self = Instance;
                if (self == null) return;
                if (timer > 0f) { timer -= Time.fixedDeltaTime; return; }

                var target = self.FindTarget(body);
                if (!target) { timer = 0.2f; return; } // заряжена — ищем цель 5 раз в секунду
                self.Fire(body, target);
                timer = self.IntervalFor(System.Math.Max(1, self.Count(body)));
            }
        }
    }
}
