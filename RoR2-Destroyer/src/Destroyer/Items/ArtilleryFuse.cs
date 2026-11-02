using System.Collections.Generic;
using BepInEx.Configuration;
using Destroyer.Content;
using Destroyer.Core;
using RoR2;
using RoR2.Projectile;
using UnityEngine;
using UnityEngine.Networking;

namespace Destroyer.Items
{
    /// <summary>
    /// Артиллерийский запал (красный). 10% при попадании — залп из 20 ракет-фейерверков над тобой, каждая на 10% базового
    /// урона с радиусом взрыва 2 (+1) м и proc coefficient 0.1.
    ///
    /// Логика — повтор ванильного FireworkLauncher (IL 1.5): пусковая в 2 м над тобой, раз в launchInterval одна ракета
    /// из случайной точки круга randomCircleRange, направление "вверх + чуть-чуть случайно", сила 200; крит — один бросок
    /// на весь залп. Префаб снаряда, интервал и круг берём с ванильного Prefabs/FireworkLauncher. Ракеты Pocket I.C.B.M.
    /// не утраиваем: залп и так 20 штук, зато множитель урона ICBM учитываем, как MissileUtils.
    ///
    /// Свой пускатель, а не ванильный, потому что снаряд надо поправить ПОСЛЕ появления (радиус, proc): ProjectileManager
    /// может отложить обычный FireProjectile на следующий кадр, а FireProjectileImmediateServer отдаёт объект сразу.
    /// Ракеты помечены компонентом — их попадания запал не перезапускают (иначе 20 × 1% → цепочка).
    /// </summary>
    internal sealed class ArtilleryFuse : ItemBase
    {
        public override string Id => "ARTILLERY_FUSE";
        public override ItemTier Tier => ItemTier.Tier3;
        public override string ModelFrom => "Firework"; // Bundle of Fireworks
        public override ItemTag[] Tags => new[] { ItemTag.Damage };

        public override Words Name => new Words("Artillery Fuse", "Артиллерийский запал");

        public override Words Pickup => new Words(
            "Chance on hit to launch a swarm of rockets.",
            "Шанс при попадании выпустить рой ракет.");

        public override Words Description => new Words(
            $"{Tx.Damage(Tx.Pct(chance.Value))} chance on hit to launch {Tx.Damage(count.Value + " rockets")} " +
            $"that deal {Tx.Damage(Tx.Pct(damage.Value) + " base damage")} each in a {Tx.Damage(Tx.Meters(radius.Value).En)} {Tx.Stack("+" + Tx.Meters(radiusPerStack.Value).En + " per stack")} radius. " +
            $"Rockets have a proc coefficient of {Tx.Num(missileProc.Value)}.",
            $"{Tx.Damage(Tx.Pct(chance.Value))} шанс при попадании выпустить {Tx.Damage(count.Value + " ракет")} " +
            $"по {Tx.Damage(Tx.Pct(damage.Value) + " базового урона")} каждая, радиус взрыва {Tx.Damage(Tx.Meters(radius.Value).Ru)} {Tx.Stack("+" + Tx.Meters(radiusPerStack.Value).Ru + " за копию")}. " +
            $"Proc coefficient ракет — {Tx.Num(missileProc.Value)}.");

        public override Words Lore => new Words(
            "Somebody lit it. Nobody wants to say who.",
            "Кто-то его поджёг. Никто не признаётся.");

        private ConfigEntry<float> chance, damage, radius, radiusPerStack, missileProc;
        private ConfigEntry<int> count;

        protected override void BindConfig(ItemSettings s)
        {
            chance = s.Float("Chance", 0.1f, 0f, 1f, new Words("Chance", "Шанс"),
                new Words("Chance on hit (0.1 = 10%), scaled by the attack's proc coefficient.",
                          "Шанс при попадании (0.1 = 10%), умножается на proc coefficient атаки."), "{0:P0}");
            count = s.Int("Rockets", 20, 1, 200, new Words("Rockets per swarm", "Ракет в залпе"),
                new Words("How many rockets one trigger launches.", "Сколько ракет выпускает одно срабатывание."));
            damage = s.Float("RocketDamage", 0.1f, 0f, 20f, new Words("Rocket damage", "Урон ракеты"),
                new Words("Damage of each rocket in base damage (0.1 = 10%).", "Урон каждой ракеты в базовом уроне (0.1 = 10%)."), "{0:P0}");
            radius = s.Float("BlastRadius", 2f, 0.5f, 30f, new Words("Blast radius", "Радиус взрыва"),
                new Words("Explosion radius with the first copy, meters.", "Радиус взрыва с первой копией, м."), "{0:0.#} m");
            radiusPerStack = s.Float("BlastRadiusPerStack", 1f, 0f, 30f, new Words("Blast radius per stack", "Радиус за копию"),
                new Words("Extra radius per additional copy, meters.", "Прибавка радиуса за каждую копию сверх первой, м."), "{0:0.#} m");
            missileProc = s.Float("RocketProcCoefficient", 0.1f, 0f, 3f, new Words("Rocket proc coefficient", "Proc coefficient ракет"),
                new Words("Proc coefficient of each rocket.", "Proc coefficient каждой ракеты."), "{0:0.##}");
        }

        protected override void Hook()
        {
            HitEvents.OnHit += OnHit;
            RoR2Application.onFixedUpdate += Tick;
            Run.onRunStartGlobal += run => salvos.Clear();
        }

        // ---------- Срабатывание ----------

        private sealed class RocketTag : MonoBehaviour { }

        private sealed class Salvo
        {
            public CharacterBody Owner;
            public int Remaining;
            public float Timer;
            public bool Crit;
            public float Radius;
        }

        private readonly List<Salvo> salvos = new List<Salvo>();

        private void OnHit(DamageInfo info, CharacterBody attacker, CharacterBody victim)
        {
            int n = Count(attacker);
            if (n <= 0) return;
            if (info.inflictor && info.inflictor.GetComponent<RocketTag>()) return; // ракеты запал не запускают
            if (!Roll(chance.Value, info.procCoefficient, attacker.master)) return;
            if (!LoadLauncher()) return;

            salvos.Add(new Salvo
            {
                Owner = attacker,
                Remaining = count.Value,
                Timer = 0f,
                Crit = attacker.RollCrit(),
                Radius = Stacking.Linear(n, radius.Value, radiusPerStack.Value),
            });
        }

        // ---------- Пускатель (как FireworkLauncher) ----------

        private GameObject rocketPrefab;
        private float launchInterval = 0.1f, circleRange = 1f;
        private bool launcherLoaded, launcherFailed;

        private bool LoadLauncher()
        {
            if (launcherLoaded) return true;
            if (launcherFailed) return false;
            var launcherPrefab = GameAssets.Load(GameAssets.FireworkLauncher);
            var vanilla = launcherPrefab ? launcherPrefab.GetComponent<FireworkLauncher>() : null;
            if (!vanilla || !vanilla.projectilePrefab)
            {
                DestroyerPlugin.Log.LogError("Artillery fuse: vanilla FireworkLauncher / its projectile not found, no rockets.");
                launcherFailed = true;
                return false;
            }
            rocketPrefab = vanilla.projectilePrefab;
            if (vanilla.launchInterval > 0f) launchInterval = vanilla.launchInterval;
            circleRange = vanilla.randomCircleRange;
            launcherLoaded = true;
            return true;
        }

        private bool warnedNoExplosion;

        private void Tick()
        {
            if (!NetworkServer.active || salvos.Count == 0 || !ProjectileManager.instance) return;
            float dt = Time.fixedDeltaTime;
            for (int i = salvos.Count - 1; i >= 0; i--)
            {
                var salvo = salvos[i];
                if (!salvo.Owner || salvo.Remaining <= 0 || !salvo.Owner.healthComponent || !salvo.Owner.healthComponent.alive)
                {
                    salvos.RemoveAt(i);
                    continue;
                }
                salvo.Timer -= dt;
                if (salvo.Timer > 0f) continue;
                salvo.Timer = launchInterval;
                salvo.Remaining--;
                Fire(salvo);
            }
        }

        private void Fire(Salvo salvo)
        {
            var owner = salvo.Owner;
            int icbm = owner.inventory && DLC1Content.Items.MoreMissile ? owner.inventory.GetItemCountEffective(DLC1Content.Items.MoreMissile) : 0;
            float damageMult = MissileUtils.GetMoreMissileDamageMultiplier(icbm);

            Vector2 circle = Random.insideUnitCircle * circleRange;
            Vector3 origin = owner.corePosition + Vector3.up * 2f + new Vector3(circle.x, 0f, circle.y);
            Vector3 direction = Vector3.up + Random.insideUnitSphere * 0.1f;

            var info = new FireProjectileInfo
            {
                projectilePrefab = rocketPrefab,
                position = origin,
                rotation = Util.QuaternionSafeLookRotation(direction),
                owner = owner.gameObject,
                damage = owner.damage * damage.Value * damageMult,
                crit = salvo.Crit,
                force = 200f,
                damageColorIndex = DamageColorIndex.Item,
            };
            var rocket = ProjectileManager.instance.FireProjectileImmediateServer(info, null, 0, 0.0);
            if (!rocket) return;

            rocket.AddComponent<RocketTag>();
            var controller = rocket.GetComponent<ProjectileController>();
            if (controller) controller.procCoefficient = missileProc.Value;
            var explosion = rocket.GetComponent<ProjectileExplosion>();
            if (explosion)
            {
                explosion.blastRadius = salvo.Radius;
                explosion.blastProcCoefficient = 1f; // итоговый proc = controller.procCoefficient × это
            }
            else if (!warnedNoExplosion)
            {
                DestroyerPlugin.Log.LogWarning("Artillery fuse: firework projectile has no ProjectileExplosion, blast radius not applied.");
                warnedNoExplosion = true;
            }
        }
    }
}
