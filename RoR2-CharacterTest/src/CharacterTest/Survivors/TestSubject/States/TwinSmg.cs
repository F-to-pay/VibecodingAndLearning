using CharacterTest.Content;
using EntityStates;
using EntityStates.Commando.CommandoWeapon;
using RoR2;
using UnityEngine;
using CharacterTest.Survivors.TestSubject;

namespace CharacterTest.Survivors.TestSubject.States
{
    /// <summary>
    /// Парные пистолеты-пулемёты (основная). Вид и звук — как у Double Tap (статические поля FirePistol2).
    /// Слои рук у Коммандо 1.5: "Gesture Left, Additive" / "Gesture Right, Additive" (ванила просит несуществующие).
    ///  - Темп фиксированный: SMG.FireRate выстрелов в секунду, скорость атаки на него НЕ влияет.
    ///  - Скорость атаки множит урон: SMG.Damage × урон × (скорость атаки / базовая скорость атаки).
    ///  - Обойма = заряды навыка (SkillDef: baseMaxStock = rechargeStock = SMG.Magazine,
    ///    перезарядка SMG.ReloadDelay). Каждый выстрел обнуляет секундомер перезарядки, поэтому обойма
    ///    набивается целиком через SMG.ReloadDelay секунд БЕЗ стрельбы — и когда пусто, и когда не до конца.
    ///
    /// Одно состояние на всю очередь, а не по состоянию на пулю: смена состояния съедает тик, и 12/с
    /// превратились бы в 15/с, а 16/с — в 12/с. Первую пулю оплачивает сама активация навыка (stockToConsume = 1),
    /// остальные — DeductStock. Отпустил кнопку или кончились патроны — выход на границе выстрела,
    /// так что быстрыми нажатиями темп тоже не обогнать.
    /// </summary>
    public class TwinSmg : ModSkillState
    {
        // Цифры — TestSubjectConfig (SMG.*), читаются на каждом выстреле.
        public static float Range = 160f;

        // Чередование рук между очередями — косметика, общий static не страшен.
        private static bool leftHand;

        private float interval;
        private float shotTimer;

        public override void OnEnter()
        {
            base.OnEnter();
            interval = 1f / TestSubjectConfig.SmgFireRate.Value;
            FireShot();
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            StartAimMode(2f);

            shotTimer += Time.fixedDeltaTime;
            while (shotTimer >= interval)
            {
                shotTimer -= interval;

                if (isAuthority)
                {
                    var skill = activatorSkillSlot;
                    if (!IsKeyDownAuthority() || !skill || skill.stock <= 0)
                    {
                        outer.SetNextStateToMain();
                        return;
                    }
                    skill.DeductStock(1);
                }
                FireShot();
            }
        }

        private void FireShot()
        {
            leftHand = !leftHand;
            string muzzle = leftHand ? "MuzzleLeft" : "MuzzleRight";

            PlayAnimationSafe(leftHand ? "Gesture Left, Additive" : "Gesture Right, Additive",
                              leftHand ? "FirePistol, Left" : "FirePistol, Right");
            Util.PlaySound(FirePistol2.firePistolSoundString, gameObject);
            if (FirePistol2.muzzleEffectPrefab)
                EffectManager.SimpleMuzzleFlash(FirePistol2.muzzleEffectPrefab, gameObject, muzzle, false);

            float recoil = FirePistol2.recoilAmplitude * 0.5f;
            AddRecoil(-0.4f * recoil, -0.8f * recoil, -0.3f * recoil, 0.3f * recoil);

            if (isAuthority)
            {
                // Секундомер перезарядки — с нуля на каждом выстреле: обойма вернётся через SMG.ReloadDelay тишины.
                if (activatorSkillSlot) activatorSkillSlot.rechargeStopwatch = 0f;

                Ray aim = GetAimRay();
                var bullet = new BulletAttack
                {
                    owner = gameObject,
                    weapon = gameObject,
                    origin = aim.origin,
                    aimVector = aim.direction,
                    minSpread = 0f,
                    maxSpread = characterBody.spreadBloomAngle,
                    maxDistance = Range,
                    damage = TestSubjectConfig.SmgDamage.Value * damageStat * AttackSpeedMultiplier(),
                    procCoefficient = TestSubjectConfig.SmgProc.Value,
                    force = FirePistol2.force * 0.5f,
                    isCrit = RollCrit(),
                    tracerEffectPrefab = FirePistol2.tracerEffectPrefab,
                    hitEffectPrefab = FirePistol2.hitEffectPrefab,
                    muzzleName = muzzle,
                    radius = 0.1f,
                    smartCollision = true,
                    falloffModel = BulletAttack.FalloffModel.DefaultBullet,
                };
                bullet.damageType.damageSource = DamageSource.Primary;
                bullet.Fire();
            }

            characterBody.AddSpreadBloom(FirePistol2.spreadBloomValue * 0.5f);
        }

        /// <summary>Скорость атаки прямо сейчас (баффы меняются посреди очереди) относительно базовой тела.</summary>
        private float AttackSpeedMultiplier()
        {
            if (!characterBody) return attackSpeedStat;
            float baseSpeed = characterBody.baseAttackSpeed > 0f ? characterBody.baseAttackSpeed : 1f;
            return characterBody.attackSpeed / baseSpeed;
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.Skill;
    }
}
