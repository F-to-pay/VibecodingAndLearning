using System.Collections.Generic;
using BepInEx.Configuration;
using Destroyer.Art;
using Destroyer.Content;
using Destroyer.Core;
using EntityStates;
using R2API;
using RoR2;
using RoR2.Skills;
using UnityEngine;

namespace Destroyer.Items
{
    /// <summary>
    /// Его холодные объятья (лунный). Заменяет основной навык (как Visions of Heresy): держишь кнопку, наведясь на врага, —
    /// связь без урона, копятся стаки 4 (+1) в секунду (скорость атаки на темп не влияет). Отпустил — все стаки разом:
    /// по 50% базового урона за стак × множитель скорости атаки, ОДНИМ ударом (proc 1 — бэнды срабатывают).
    /// Цель умерла, ушла из виду или из дальности — стаки сгорают, связь ищется заново. Предела стаков нет.
    /// Цена: скорость атаки ×0.9 за копию.
    ///
    /// Замена навыка — GenericSkill.SetSkillOverride(этот предмет, свой SkillDef, Replacement) на КАЖДОЙ машине при
    /// смене инвентаря (переопределения навыков не сетевые — так делает и ваниль с Heresy). Свой SkillDef и состояние
    /// ColdEmbraceState регистрируются через R2API ContentAddition. Состояние работает у владельца (authority):
    /// цель — BullseyeSearch по прицелу (8°, 80 м, видимость), удар — BulletAttack в цель (с клиента сам уходит на сервер),
    /// метка урона Primary (корона считает это атакой). Стаки видно баффом COLD_EMBRACE_STACKS (AddBuffAuthority).
    /// Предмет CannotCopy + AIBlacklist: ИИ (и копии Deus) этот навык не поймут.
    /// </summary>
    internal sealed class ColdEmbrace : ItemBase
    {
        public override string Id => "COLD_EMBRACE";
        public override ItemTier Tier => ItemTier.Lunar;
        public override string ModelFrom => "LunarPrimaryReplacement"; // Visions of Heresy
        public override ItemTag[] Tags => new[] { ItemTag.Damage, ItemTag.CannotCopy, ItemTag.AIBlacklist };

        public override Words Name => new Words("His Cold Embrace", "Его холодные объятья");

        public override Words Pickup => new Words(
            "Replace your primary skill with a freezing link that bursts on release... <style=cDeath>but attack slower.</style>",
            "Основной навык — ледяная связь, которая взрывается при отпускании... <style=cDeath>но атаки медленнее.</style>");

        public override Words Description => new Words(
            $"{Tx.Utility("Replace your primary skill")} with {Tx.Utility("Cold Embrace")}: hold on an enemy to link to it, gaining " +
            $"{Tx.Damage(Tx.Num(rate.Value) + " charges per second")} {Tx.Stack("+" + Tx.Num(ratePerStack.Value) + " per stack")} regardless of attack speed. " +
            $"Release to deal {Tx.Damage(Tx.Pct(chargeDamage.Value) + " base damage")} per charge in one hit, multiplied by your {Tx.Damage("attack speed")}. " +
            $"Losing the target burns all charges. {Tx.Health("Attack speed ×" + Tx.Num(1f - penalty.Value))} {Tx.Stack("×" + Tx.Num(1f - penalty.Value) + " per stack")}.",
            $"{Tx.Utility("Основной навык заменяется")} на {Tx.Utility("Холодные объятья")}: держи на враге — возникает связь, копятся " +
            $"{Tx.Damage(Tx.Num(rate.Value) + " заряда в секунду")} {Tx.Stack("+" + Tx.Num(ratePerStack.Value) + " за копию")}, скорость атаки на это не влияет. " +
            $"Отпусти — один удар на {Tx.Damage(Tx.Pct(chargeDamage.Value) + " базового урона")} за каждый заряд, умноженный на {Tx.Damage("скорость атаки")}. " +
            $"Потерял цель — заряды сгорают. {Tx.Health("Скорость атаки ×" + Tx.Num(1f - penalty.Value))} {Tx.Stack("×" + Tx.Num(1f - penalty.Value) + " за копию")}.");

        public override Words Lore => new Words(
            "It holds you gently. It never lets go. Then it does.",
            "Оно держит нежно. Никогда не отпускает. А потом отпускает.");

        internal ConfigEntry<float> rate, ratePerStack, chargeDamage, penalty, range, proc;

        protected override void BindConfig(ItemSettings s)
        {
            rate = s.Float("ChargesPerSecond", 4f, 0.1f, 100f, new Words("Charges per second", "Зарядов в секунду"),
                new Words("Charges gained per second while linked, first copy.", "Зарядов в секунду, пока держится связь, с первой копией."), "{0:0.#}");
            ratePerStack = s.Float("ChargesPerSecondPerStack", 1f, 0f, 100f, new Words("Charges per second per stack", "Зарядов в секунду за копию"),
                new Words("Extra charges per second per additional copy.", "Прибавка за каждую копию сверх первой."), "{0:0.#}");
            chargeDamage = s.Float("DamagePerCharge", 0.5f, 0f, 20f, new Words("Damage per charge", "Урон за заряд"),
                new Words("Damage per charge in base damage (0.5 = 50%), then × attack speed.", "Урон за заряд в базовом уроне (0.5 = 50%), потом × скорость атаки."), "{0:P0}");
            penalty = s.Float("AttackSpeedPenalty", 0.1f, 0f, 0.9f, new Words("Attack speed penalty", "Штраф скорости атаки"),
                new Words("Attack speed is multiplied by (1 - this) per copy.", "Скорость атаки умножается на (1 - это) за каждую копию."), "{0:P0}");
            range = s.Float("Range", 80f, 5f, 500f, new Words("Range", "Дальность"),
                new Words("Maximum link range, meters.", "Наибольшая дальность связи, м."), "{0:0} m");
            proc = s.Float("ProcCoefficient", 1f, 0f, 3f, new Words("Proc coefficient", "Proc coefficient"),
                new Words("Proc coefficient of the release hit.", "Proc coefficient удара при отпускании."), "{0:0.##}");
        }

        internal static ColdEmbrace Instance;
        internal SkillDef Skill;
        internal BuffDef StacksBuff;
        private readonly HashSet<CharacterBody> overridden = new HashSet<CharacterBody>();

        internal const string SkillNameToken = "DESTROYER_COLD_EMBRACE_SKILL_NAME";
        internal const string SkillDescToken = "DESTROYER_COLD_EMBRACE_SKILL_DESC";

        internal float RateFor(int n) => Stacking.Linear(n, rate.Value, ratePerStack.Value);

        protected override void Hook()
        {
            Instance = this;
            StacksBuff = BuffFactory.Create("COLD_EMBRACE_STACKS", BuffFactory.Hex("#9ad8ff"), isDebuff: false, canStack: true);

            bool added = false;
            var state = ContentAddition.AddEntityState(typeof(ColdEmbraceState), out added);
            if (!added) DestroyerPlugin.Log.LogWarning("Cold Embrace: entity state was not registered (already added?).");

            Skill = ScriptableObject.CreateInstance<SkillDef>();
            ((ScriptableObject)Skill).name = "DestroyerColdEmbraceSkill";
            Skill.skillName = "DestroyerColdEmbrace";
            Skill.skillNameToken = SkillNameToken;
            Skill.skillDescriptionToken = SkillDescToken;
            Skill.icon = IconFactory.Create("DestroyerColdEmbraceSkill", IconBook.Get(Id));
            Skill.activationStateMachineName = "Weapon";
            Skill.activationState = state;
            Skill.interruptPriority = InterruptPriority.Any;
            Skill.baseRechargeInterval = 0f;
            Skill.baseMaxStock = 1;
            Skill.rechargeStock = 1;
            Skill.requiredStock = 0;
            Skill.stockToConsume = 0;
            Skill.fullRestockOnAssign = true;
            Skill.isCombatSkill = true;
            Skill.mustKeyPress = false;
            Skill.cancelSprintingOnActivation = true;
            Skill.canceledFromSprinting = false;
            Skill.beginSkillCooldownOnSkillEnd = false;
            Skill.hideStockCount = true;
            ContentAddition.AddSkillDef(Skill);

            GameLanguage.SetText(SkillNameToken, new Words("Cold Embrace", "Холодные объятья"));
            GameLanguage.SetText(SkillDescToken, new Words(
                "Hold on an enemy to link and gather charges. Release to unleash them in one freezing hit.",
                "Держи на враге — связь копит заряды. Отпусти — все заряды одним ледяным ударом."));

            RecalculateStatsAPI.GetStatCoefficients += (body, args) =>
            {
                int n = Count(body);
                if (n > 0) args.attackSpeedTotalMult *= Mathf.Pow(1f - Mathf.Clamp(penalty.Value, 0f, 0.9f), n);
            };

            CharacterBody.onBodyInventoryChangedGlobal += UpdateOverride;
            CharacterBody.onBodyStartGlobal += UpdateOverride;
        }

        /// <summary>Каждая машина: есть предмет — основной навык заменён, нет — вернуть.</summary>
        private void UpdateOverride(CharacterBody body)
        {
            if (!body || Skill == null) return;
            overridden.RemoveWhere(b => !b);
            var primary = body.skillLocator ? body.skillLocator.primary : null;
            bool want = primary && Count(body) > 0;
            bool has = overridden.Contains(body);
            if (want && !has)
            {
                primary.SetSkillOverride(this, Skill, GenericSkill.SkillOverridePriority.Replacement);
                overridden.Add(body);
            }
            else if (!want && has)
            {
                if (primary) primary.UnsetSkillOverride(this, Skill, GenericSkill.SkillOverridePriority.Replacement);
                overridden.Remove(body);
            }
        }
    }

    /// <summary>Состояние навыка "Холодные объятья" (машина "Weapon"). Логика — только у владельца (authority).</summary>
    public class ColdEmbraceState : BaseSkillState
    {
        private HurtBox target;
        private float charges;
        private int shownCharges;
        private bool released;
        private float effectTimer;

        public override void OnEnter()
        {
            base.OnEnter();
            StartAimMode(2f, false);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (!isAuthority) return;
            var item = Items.ColdEmbrace.Instance;
            if (item == null || !characterBody) { outer.SetNextStateToMain(); return; }
            StartAimMode(1f, false);

            if (!IsKeyDownAuthority())
            {
                Release();
                outer.SetNextStateToMain();
                return;
            }

            if (target && !Valid(target, item))
            {
                // Цель потеряна — заряды сгорают.
                target = null;
                charges = 0f;
                SyncBuff(item);
            }
            if (!target) target = Acquire(item);
            if (!target) return;

            charges += item.RateFor(System.Math.Max(1, item.Count(characterBody))) * GetDeltaTime();
            SyncBuff(item);

            effectTimer -= GetDeltaTime();
            if (effectTimer <= 0f)
            {
                effectTimer = 0.3f;
                var prefab = GameAssets.Load(GameAssets.FrostImpact);
                if (prefab) EffectManager.SpawnEffect(prefab, new EffectData { origin = target.transform.position, scale = 0.6f }, false);
            }
        }

        public override void OnExit()
        {
            if (isAuthority && !released) Release(); // прервали другим навыком/оглушением — тоже отпускаем
            base.OnExit();
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.Skill;

        private bool Valid(HurtBox box, Items.ColdEmbrace item)
        {
            if (!box || !box.healthComponent || !box.healthComponent.alive) return false;
            Ray aim = GetAimRay();
            Vector3 to = box.transform.position - aim.origin;
            if (to.magnitude > item.range.Value) return false;
            return !Physics.Raycast(aim.origin, to.normalized, to.magnitude, LayerIndex.world.mask, QueryTriggerInteraction.Ignore);
        }

        private HurtBox Acquire(Items.ColdEmbrace item)
        {
            Ray aim = GetAimRay();
            var search = new BullseyeSearch
            {
                viewer = characterBody,
                searchOrigin = aim.origin,
                searchDirection = aim.direction,
                maxDistanceFilter = item.range.Value,
                teamMaskFilter = TeamMask.GetEnemyTeams(teamComponent ? teamComponent.teamIndex : TeamIndex.Player),
                filterByLoS = true,
                filterByDistinctEntity = true,
                sortMode = BullseyeSearch.SortMode.Angle,
            };
            search.maxAngleFilter = 8f;
            search.RefreshCandidates();
            search.FilterOutGameObject(gameObject);
            foreach (var box in search.GetResults())
                if (box && box.healthComponent && box.healthComponent.alive) return box;
            return null;
        }

        private void Release()
        {
            released = true;
            var item = Items.ColdEmbrace.Instance;
            int stacks = Mathf.FloorToInt(charges);
            charges = 0f;
            if (item != null) SyncBuff(item);
            if (item == null || stacks <= 0 || !target || !target.healthComponent || !target.healthComponent.alive) return;

            float attackSpeed = characterBody.baseAttackSpeed > 0f ? characterBody.attackSpeed / characterBody.baseAttackSpeed : 1f;
            Ray aim = GetAimRay();
            Vector3 to = target.transform.position - aim.origin;
            var bullet = new BulletAttack
            {
                owner = gameObject,
                weapon = gameObject,
                origin = aim.origin,
                damage = damageStat * item.chargeDamage.Value * stacks * attackSpeed,
                isCrit = RollCrit(),
                force = 300f,
                procCoefficient = item.proc.Value,
                falloffModel = BulletAttack.FalloffModel.None,
                radius = 0.6f,
                smartCollision = true,
                damageType = new DamageTypeCombo { damageType = DamageType.Generic, damageSource = DamageSource.Primary },
                hitEffectPrefab = GameAssets.Load(GameAssets.FrostImpact),
            };
            bullet.aimVector = to.normalized;
            bullet.maxDistance = to.magnitude + 2f;
            bullet.Fire();
        }

        /// <summary>Бафф-счётчик зарядов = целая часть зарядов (команды AddBuffAuthority/RemoveBuffAuthority).</summary>
        private void SyncBuff(Items.ColdEmbrace item)
        {
            if (!characterBody || item.StacksBuff == null) return;
            int want = Mathf.Min(Mathf.FloorToInt(charges), 999);
            while (shownCharges < want) { characterBody.AddBuffAuthority(item.StacksBuff.buffIndex); shownCharges++; }
            while (shownCharges > want) { characterBody.RemoveBuffAuthority(item.StacksBuff.buffIndex); shownCharges--; }
        }
    }
}
