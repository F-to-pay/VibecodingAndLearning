using System;
using BepInEx.Configuration;
using CharacterTest.Content;
using EntityStates;
using EntityStates.Commando.CommandoWeapon;
using R2API;
using RoR2;
using RoR2.Projectile;
using UnityEngine;
using CharacterTest.Survivors.TestSubject;

namespace CharacterTest.Survivors.TestSubject.States
{
    /// <summary>
    /// Подствольные гранатомёты (вторичная): Grenade.Shots гранат по очереди (левая рука, правая), каждая —
    /// Grenade.Damage урона в Grenade.Radius метров, полёт по параболе.
    ///
    /// Снаряд — клон фраг-гранаты Коммандо (моделька, взрыв), но: взрывается от ЛЮБОГО касания (мир или враг),
    /// без отскоков и фитиля, урон по всему радиусу одинаковый (без SweetSpot). Траектория — три ползунка в меню:
    /// скорость, подъём ствола над прицелом и сила тяжести (1 = обычная, 0 = летит прямо). Все действуют сразу.
    /// </summary>
    public class UnderbarrelGrenades : ModSkillState
    {
        // Цифры — TestSubjectConfig (Grenade.*). Урон, количество, пауза, траектория — на каждом нажатии;
        // радиус, прок и тяжесть правятся прямо в префабе снаряда, как только меняешь настройку.
        public static float BaseDuration = 0.45f;
        public static float Force = 1200f;

        private static int Shots => TestSubjectConfig.GrenadeShots.Value;

        public static GameObject ProjectilePrefab;

        // Звук и вспышка — от ванильного ThrowGrenade (его поля заполняет EntityStateConfiguration в конструкторе).
        private static bool vanillaLooked;
        private static string throwSound;
        private static GameObject throwMuzzle;

        private float duration;
        private float interval;
        private int fired;
        private int shots;

        public override void OnEnter()
        {
            base.OnEnter();
            LookUpVanilla();
            duration = BaseDuration / attackSpeedStat;
            interval = TestSubjectConfig.GrenadeInterval.Value / attackSpeedStat;
            shots = Shots;
            duration = Mathf.Max(duration, interval * (shots - 1) + 0.05f);
            StartAimMode(2f);

            // Двуручная отдача, как у Phase Round: FireFMJ на обоих слоях жестов.
            PlayAnimationSafe("FireFMJ.playbackRate", duration, ("Gesture, Additive", "FireFMJ"));
            PlayAnimationSafe("FireFMJ.playbackRate", duration, ("Gesture, Override", "FireFMJ"));

            FireGrenade();
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            StartAimMode(2f);
            while (fired < shots && fixedAge >= fired * interval) FireGrenade();
            if (isAuthority && fired >= shots && fixedAge >= duration) outer.SetNextStateToMain();
        }

        public override void OnExit()
        {
            // Сбили посреди очереди — оставшиеся гранаты всё равно вылетают.
            while (fired < shots) FireGrenade();
            base.OnExit();
        }

        private void FireGrenade()
        {
            bool left = fired % 2 == 0;
            fired++;
            string muzzle = left ? "MuzzleLeft" : "MuzzleRight";

            if (!string.IsNullOrEmpty(throwSound)) Util.PlaySound(throwSound, gameObject);
            else Util.PlayAttackSpeedSound(FirePistol2.firePistolSoundString, gameObject, 0.5f);
            var flash = throwMuzzle ? throwMuzzle : FirePistol2.muzzleEffectPrefab;
            if (flash) EffectManager.SimpleMuzzleFlash(flash, gameObject, muzzle, false);
            AddRecoil(-1.5f, -2.5f, -0.5f, 0.5f);

            if (!isAuthority || !ProjectilePrefab) return;

            Ray aim = GetAimRay();
            Vector3 dir = PitchUp(aim.direction, TestSubjectConfig.GrenadeUpAngle.Value);

            ProjectileManager.instance.FireProjectile(new FireProjectileInfo
            {
                projectilePrefab = ProjectilePrefab,
                position = aim.origin,
                rotation = Util.QuaternionSafeLookRotation(dir),
                owner = gameObject,
                damage = TestSubjectConfig.GrenadeDamage.Value * damageStat,
                force = Force,
                crit = RollCrit(),
                damageColorIndex = DamageColorIndex.Default,
                useSpeedOverride = true,
                speedOverride = TestSubjectConfig.GrenadeSpeed.Value,
            });
        }

        /// <summary>Поднять направление на angle градусов вверх (в Unity поворот вокруг "вправо" на +угол опускает нос).</summary>
        private static Vector3 PitchUp(Vector3 dir, float angle)
        {
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            if (right.sqrMagnitude < 1e-4f || Mathf.Approximately(angle, 0f)) return dir;
            return Quaternion.AngleAxis(-angle, right.normalized) * dir;
        }

        public override InterruptPriority GetMinimumInterruptPriority() =>
            fired < shots ? InterruptPriority.PrioritySkill : InterruptPriority.Skill;

        private static void LookUpVanilla()
        {
            if (vanillaLooked) return;
            vanillaLooked = true;
            try
            {
                var vanilla = new ThrowGrenade();
                throwSound = vanilla.attackSoundString;
                throwMuzzle = vanilla.effectPrefab;
            }
            catch (Exception e)
            {
                CharacterTestPlugin.Log.LogWarning($"UnderbarrelGrenades: no ThrowGrenade config ({e.Message}), pistol sound instead");
            }
        }

        // ---------------- Снаряд ----------------

        /// <summary>Клон фраг-гранаты Коммандо под гранатомёт. Зовётся из Awake (TestSubjectSurvivor.CreateAssets).</summary>
        internal static void CreateProjectile()
        {
            var proj = PrefabUtil.Clone(GameAssets.Load(GameAssets.CommandoGrenadeProjectile), "TestSubjectUnderbarrelGrenade", networked: true);

            var explosion = proj.GetComponent<ProjectileImpactExplosion>();
            if (!explosion) throw new InvalidOperationException("CommandoGrenadeProjectile has no ProjectileImpactExplosion (game update?)");

            ApplyBlast(explosion);
            explosion.blastDamageCoefficient = 1f;     // урон уже посчитан в FireProjectileInfo.damage
            explosion.falloffModel = BlastAttack.FalloffModel.None;
            explosion.preserveExplosionScale = false;  // эффект взрыва растягивается под радиус
            explosion.fireChildren = false;
            explosion.destroyOnEnemy = true;
            explosion.detonateOnEnemy = true;
            explosion.destroyOnWorld = true;
            explosion.impactOnWorld = true;
            explosion.timerAfterImpact = false;
            explosion.lifetime = 10f;                  // улетела в небо — сама взорвётся через 10 с
            explosion.lifetimeAfterImpact = 0f;
            explosion.lifetimeRandomOffset = 0f;

            var simple = proj.GetComponent<ProjectileSimple>();
            if (simple)
            {
                simple.desiredForwardSpeed = TestSubjectConfig.GrenadeSpeed.Value;
                simple.lifetime = 10f;
                simple.updateAfterFiring = false;
                simple.enableVelocityOverLifetime = false;
                simple.oscillate = false;
            }

            var damage = proj.GetComponent<ProjectileDamage>();
            if (damage) damage.damageType.damageSource = DamageSource.Secondary;

            var rb = proj.GetComponent<Rigidbody>();
            if (rb)
            {
                rb.useGravity = true;
                rb.drag = 0f;
                var antiGravity = proj.GetComponent<AntiGravityForce>();
                if (!antiGravity) antiGravity = proj.AddComponent<AntiGravityForce>();
                antiGravity.rb = rb;
                antiGravity.useCurve = false;
                ApplyGravity(antiGravity);
                // Ползунок силы тяжести действует сразу: новые гранаты создаются из этого же префаба.
                TestSubjectConfig.GrenadeGravity.SettingChanged += (_, __) => ApplyGravity(antiGravity);
            }
            else CharacterTestPlugin.Log.LogWarning("UnderbarrelGrenades: grenade has no Rigidbody — gravity slider will do nothing");

            TestSubjectConfig.GrenadeRadius.SettingChanged += (_, __) => ApplyBlast(explosion);
            TestSubjectConfig.GrenadeProc.SettingChanged += (_, __) => ApplyBlast(explosion);

            ContentAddition.AddProjectile(proj);
            ProjectilePrefab = proj;

            var parts = new System.Collections.Generic.List<string>();
            foreach (var c in proj.GetComponents<Component>()) parts.Add(c.GetType().Name);
            CharacterTestPlugin.Log.LogInfo($"  projectile {proj.name}: {string.Join(", ", parts)}");
        }

        private static void ApplyBlast(ProjectileImpactExplosion explosion)
        {
            if (!explosion) return;
            explosion.blastRadius = TestSubjectConfig.GrenadeRadius.Value;
            explosion.blastProcCoefficient = TestSubjectConfig.GrenadeProc.Value;
        }

        /// <summary>AntiGravityForce добавляет −g × coef: 1 − множитель тяжести (0.5 → вполсилы, 2 → вдвое тяжелее).</summary>
        private static void ApplyGravity(AntiGravityForce antiGravity)
        {
            if (!antiGravity) return;
            float scale = TestSubjectConfig.GrenadeGravity.Value;
            antiGravity.antiGravityCoefficient = 1f - scale;
        }
    }
}
