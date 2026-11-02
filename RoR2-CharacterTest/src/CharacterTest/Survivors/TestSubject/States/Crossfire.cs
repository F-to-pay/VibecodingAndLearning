using System.Collections.Generic;
using CharacterTest.Content;
using EntityStates;
using EntityStates.Commando.CommandoWeapon;
using RoR2;
using RoR2.Skills;
using UnityEngine;
using UnityEngine.Networking;
using CharacterTest.Survivors.TestSubject;

namespace CharacterTest.Survivors.TestSubject.States
{
    /// <summary>
    /// Перекрёстный огонь (особая, машина Body). Цифры — TestSubjectConfig (Crossfire.*).
    ///  - Выстрелов: floor(Shots × СА), СА = скорость атаки / базовая. Длительность: Duration × СА^k, темп — остальное
    ///    (k = Crossfire.AttackSpeedToDuration; 0.5 → длительность и темп растут оба в √СА раз).
    ///  - Цели: враги в Range метров в прямой видимости; каждый выстрел — тому, в кого стреляли меньше всех,
    ///    при равенстве — ближайшему. Пересчёт на каждом выстреле. Стрелять не в кого — навык заканчивается.
    ///  - На время навыка: неуязвим (HiddenInvincibility), стоит на месте и не падает (гравитация выключена,
    ///    скорость в ноль), прочие навыки не нажимаются. Всё это — потому что навык занимает машину Body:
    ///    GenericCharacterMain (ходьба, прыжки, нажатия навыков) на это время не работает.
    ///  - Модель поворачивается к цели, камера не трогается. Без целей навык не нажимается (CrossfireSkillDef).
    /// </summary>
    public class Crossfire : ModSkillState
    {
        private static float Range => TestSubjectConfig.CrossRange.Value;

        private static readonly BullseyeSearch search = new BullseyeSearch();
        private static readonly List<HurtBox> found = new List<HurtBox>();

        private readonly Dictionary<HealthComponent, int> shotsAt = new Dictionary<HealthComponent, int>();
        private int totalShots;
        private int fired;
        private float interval;
        private bool leftHand;
        private Vector3 facing;
        private float duration;
        private bool rooted;

        public override void OnEnter()
        {
            base.OnEnter();
            float attackSpeed = AttackSpeedMultiplier(characterBody);
            float share = Mathf.Clamp01(TestSubjectConfig.CrossDurationShare.Value);
            totalShots = Mathf.Max(1, Mathf.FloorToInt(TestSubjectConfig.CrossShots.Value * attackSpeed + 0.0001f));
            duration = TestSubjectConfig.CrossDuration.Value * Mathf.Pow(Mathf.Max(attackSpeed, 0.01f), share);
            interval = duration / totalShots;

            // Оружие в покой: иначе зажатая M1 продолжила бы очередь ПП на машине Weapon.
            var weapon = EntityStateMachine.FindByCustomName(gameObject, "Weapon");
            if (weapon) weapon.SetNextStateToMain();

            // Старый "режим прицеливания" (после основной) тянул бы модель к камере — гасим, крутим сами.
            if (characterBody) characterBody.SetAimTimer(0f);

            if (NetworkServer.active && characterBody) characterBody.AddBuff(RoR2Content.Buffs.HiddenInvincibility);
            Root(true);

            FireShot();
        }

        public override void OnExit()
        {
            Root(false);
            if (NetworkServer.active && characterBody && characterBody.HasBuff(RoR2Content.Buffs.HiddenInvincibility))
                characterBody.RemoveBuff(RoR2Content.Buffs.HiddenInvincibility);
            base.OnExit();
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (isAuthority)
            {
                if (characterBody) characterBody.isSprinting = false;
                if (characterMotor)
                {
                    characterMotor.velocity = Vector3.zero;
                    characterMotor.moveDirection = Vector3.zero;
                }
            }

            while (fired < totalShots && fixedAge >= fired * interval)
            {
                if (!FireShot()) break;
            }
            FaceTarget();

            if (isAuthority && fired >= totalShots && fixedAge >= duration)
                outer.SetNextStateToMain();
        }

        public override void Update()
        {
            base.Update();
            FaceTarget();
        }

        /// <summary>Висеть на месте: гравитацию — "канальным" счётчиком антигравитации (как ванильные зависания), скорость — в ноль.</summary>
        private void Root(bool on)
        {
            if (!isAuthority || !characterMotor || rooted == on) return;
            rooted = on;
            var gravity = characterMotor.gravityParameters;
            gravity.channeledAntiGravityGranterCount += on ? 1 : -1;
            characterMotor.gravityParameters = gravity;
            characterMotor.velocity = Vector3.zero;
        }

        /// <summary>Один выстрел. false — стрелять не в кого (только у владельца; навык закончится).</summary>
        private bool FireShot()
        {
            fired++;
            leftHand = !leftHand;
            string muzzle = leftHand ? "MuzzleLeft" : "MuzzleRight";

            PlayAnimationSafe(leftHand ? "Gesture Left, Additive" : "Gesture Right, Additive",
                              leftHand ? "FirePistol, Left" : "FirePistol, Right");
            Util.PlaySound(FirePistol2.firePistolSoundString, gameObject);
            if (FirePistol2.muzzleEffectPrefab)
                EffectManager.SimpleMuzzleFlash(FirePistol2.muzzleEffectPrefab, gameObject, muzzle, false);

            if (!isAuthority) return true;

            Vector3 origin = inputBank ? inputBank.aimOrigin : characterBody.corePosition;
            HurtBox target = PickTarget(origin);
            if (!target)
            {
                fired = totalShots;
                outer.SetNextStateToMain();
                return false;
            }

            Vector3 toTarget = target.transform.position - origin;
            facing = new Vector3(toTarget.x, 0f, toTarget.z);
            FaceTarget();
            AddRecoil(-0.3f * FirePistol2.recoilAmplitude, -0.6f * FirePistol2.recoilAmplitude,
                      -0.2f * FirePistol2.recoilAmplitude, 0.2f * FirePistol2.recoilAmplitude);

            var bullet = new BulletAttack
            {
                owner = gameObject,
                weapon = gameObject,
                origin = origin,
                aimVector = toTarget.normalized,
                minSpread = 0f,
                maxSpread = 0f,
                maxDistance = Range + 5f,
                damage = TestSubjectConfig.CrossDamage.Value * damageStat,
                procCoefficient = TestSubjectConfig.CrossProc.Value,
                force = FirePistol2.force,
                isCrit = RollCrit(),
                tracerEffectPrefab = FirePistol2.tracerEffectPrefab,
                hitEffectPrefab = FirePistol2.hitEffectPrefab,
                muzzleName = muzzle,
                radius = 0.3f,
                smartCollision = true,
                falloffModel = BulletAttack.FalloffModel.None,
            };
            bullet.damageType.damageSource = DamageSource.Special;
            bullet.Fire();
            return true;
        }

        /// <summary>Тот, в кого стреляли меньше всех; при равенстве — ближайший (список уже по расстоянию).</summary>
        private HurtBox PickTarget(Vector3 origin)
        {
            FindTargets(characterBody, origin, found);
            HurtBox best = null;
            int bestCount = int.MaxValue;
            foreach (var hurtBox in found)
            {
                var hc = hurtBox ? hurtBox.healthComponent : null;
                if (!hc || !hc.alive) continue;
                shotsAt.TryGetValue(hc, out int count);
                if (count < bestCount)
                {
                    best = hurtBox;
                    bestCount = count;
                }
            }
            if (best) shotsAt[best.healthComponent] = bestCount + 1;
            return best;
        }

        /// <summary>Модель — к текущей цели. Тело (машина Body) каждый физический тик тянет её к движению, поэтому ставим и в Update.</summary>
        private void FaceTarget()
        {
            if (!isAuthority || !characterDirection || facing.sqrMagnitude < 0.0001f) return;
            characterDirection.moveVector = facing;
            characterDirection.forward = facing;
        }

        /// <summary>Враги в Range метров, в прямой видимости от origin, ближние первыми, по одному хёртбоксу на врага.</summary>
        internal static void FindTargets(CharacterBody body, Vector3 origin, List<HurtBox> results)
        {
            results.Clear();
            if (!body) return;
            search.teamMaskFilter = TeamMask.GetEnemyTeams(body.teamComponent ? body.teamComponent.teamIndex : TeamIndex.Player);
            search.filterByLoS = true;
            search.filterByDistinctEntity = true;
            search.searchOrigin = origin;
            search.searchDirection = Vector3.forward;
            search.minDistanceFilter = 0f;
            search.maxDistanceFilter = Range;
            search.maxAngleFilter = 180f;
            search.sortMode = BullseyeSearch.SortMode.Distance;
            search.viewer = body;
            search.queryTriggerInteraction = QueryTriggerInteraction.Ignore;
            search.RefreshCandidates();
            search.FilterOutGameObject(body.gameObject);
            search.GetResults(results);
        }

        internal static float AttackSpeedMultiplier(CharacterBody body)
        {
            if (!body) return 1f;
            float baseSpeed = body.baseAttackSpeed > 0f ? body.baseAttackSpeed : 1f;
            return body.attackSpeed / baseSpeed;
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.Frozen;
    }

    /// <summary>Навык не нажимается, пока в радиусе нет ни одной видимой цели — и перезарядка не тратится.</summary>
    public class CrossfireSkillDef : SkillDef
    {
        private static readonly List<HurtBox> probe = new List<HurtBox>();

        public override bool CanExecute(GenericSkill skillSlot)
        {
            if (!base.CanExecute(skillSlot)) return false;
            var body = skillSlot ? skillSlot.characterBody : null;
            if (!body) return false;
            Vector3 origin = body.inputBank ? body.inputBank.aimOrigin : body.corePosition;
            Crossfire.FindTargets(body, origin, probe);
            foreach (var hb in probe)
                if (hb && hb.healthComponent && hb.healthComponent.alive) return true;
            return false;
        }
    }
}
