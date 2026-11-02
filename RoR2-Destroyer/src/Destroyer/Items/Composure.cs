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
using UnityEngine.Networking;

namespace Destroyer.Items
{
    /// <summary>
    /// Хладнокровие (лунный, 0.15.0). Заменяет навык мобильности (Shift) на переключатель. Включено: +80 (+80) брони,
    /// +40% скорости бега, лечение запрещено, и ты тихо тающее теряешь 0.5% (+0.5%) макс. здоровья в секунду (может убить).
    ///
    /// Переключатель — свой ComposureSkillDef без машины состояний (0.15.1; в 0.15.0 было состояние на машине "Weapon",
    /// сбивало основную атаку), перезарядка 0.5 с от спама, множители перезарядок не действуют:
    /// у владельца (authority) он вешает/снимает бафф COMPOSURE_ACTIVE через AddBuffAuthority/RemoveBuffAuthority — бафф
    /// синкается сам, по нему все машины считают статы. Бег навык не сбивает и не отменяет.
    /// Замена навыка — как у объятий: SetSkillOverride(Replacement) на каждой машине при смене инвентаря.
    /// Лечение — On-хук HealthComponent.Heal (сервер): с баффом — 0. Таяние — сервер, каждый физкадр копит
    /// fullHealth·0.005·n·dt и снимает целые единицы с health напрямую, как ванильный отрицательный реген
    /// (без события урона: щит перезаряжается, предметы на получение урона не срабатывают); на нуле — Suicide.
    /// Потерял предмет — бафф снимается. Новый этап — новое тело, бафф не переносится: начинаешь выключенным.
    /// </summary>
    internal sealed class Composure : ItemBase
    {
        public override string Id => "COMPOSURE";
        public override ItemTier Tier => ItemTier.Lunar;
        public override string ModelFrom => "LunarUtilityReplacement"; // Strides of Heresy
        public override ItemTag[] Tags => new[] { ItemTag.Utility, ItemTag.CannotCopy, ItemTag.AIBlacklist };

        public override Words Name => new Words("Composure", "Хладнокровие");

        public override Words Pickup => new Words(
            "Replace your utility skill with a toggle: armor and speed... <style=cDeath>but no healing, and you slowly wither.</style>",
            "Навык мобильности — переключатель: броня и скорость... <style=cDeath>но без лечения, и ты медленно таешь.</style>");

        public override Words Description => new Words(
            $"{Tx.Utility("Replace your utility skill")} with {Tx.Utility("Composure")}, a toggle. While active: {Tx.Utility("+" + Tx.Num(armor.Value) + " armor")} {Tx.Stack("+" + Tx.Num(armor.Value) + " per stack")} " +
            $"and {Tx.Utility("+" + Tx.Pct(speed.Value) + " movement speed")}, " +
            $"{Tx.Health("you cannot heal and lose " + Tx.Pct(drain.Value) + " of your maximum health per second")} {Tx.Stack("+" + Tx.Pct(drain.Value) + " per stack")}. {Tx.Health("It can kill you.")}",
            $"{Tx.Utility("Навык мобильности заменяется")} на переключатель {Tx.Utility("Хладнокровие")}. Пока включено: {Tx.Utility("+" + Tx.Num(armor.Value) + " брони")} {Tx.Stack("+" + Tx.Num(armor.Value) + " за копию")} " +
            $"и {Tx.Utility("+" + Tx.Pct(speed.Value) + " к скорости бега")}, " +
            $"{Tx.Health("лечения нет, и ты теряешь " + Tx.Pct(drain.Value) + " макс. здоровья в секунду")} {Tx.Stack("+" + Tx.Pct(drain.Value) + " за копию")}. {Tx.Health("Может убить.")}");

        public override Words Lore => new Words(
            "Nothing hurts if you stop feeling. Nothing heals either.",
            "Ничего не болит, если перестать чувствовать. И ничего не заживает.");

        internal ConfigEntry<float> armor, speed, drain, toggleCooldown;

        protected override void BindConfig(ItemSettings s)
        {
            armor = s.Float("Armor", 80f, 0f, 2000f, new Words("Armor per stack", "Броня за копию"),
                new Words("Armor per copy while active.", "Броня за каждую копию, пока включено."), "{0:0}");
            speed = s.Float("MoveSpeed", 0.4f, 0f, 5f, new Words("Movement speed", "Скорость бега"),
                new Words("Movement speed bonus while active (0.4 = +40%). Does not grow with stacks.",
                          "Бонус скорости бега, пока включено (0.4 = +40%). От копий не растёт."), "{0:P0}");
            drain = s.Float("Drain", 0.005f, 0f, 0.5f, new Words("Health loss per second per stack", "Потеря здоровья в секунду за копию"),
                new Words("Share of maximum health lost per second per copy while active (0.005 = 0.5%).",
                          "Доля макс. здоровья в секунду за каждую копию, пока включено (0.005 = 0.5%)."), "{0:0.0#%}");
            toggleCooldown = s.Float("ToggleCooldown", 0.5f, 0f, 30f, new Words("Toggle cooldown", "Перезарядка переключения"),
                new Words("Seconds between toggles.", "Секунд между переключениями."), "{0:0.#} s", restartRequired: true);
        }

        internal static Composure Instance;
        internal SkillDef Skill;
        internal BuffDef Active;
        private readonly HashSet<CharacterBody> overridden = new HashSet<CharacterBody>();
        private readonly Dictionary<CharacterBody, float> drainAccumulator = new Dictionary<CharacterBody, float>();
        private readonly List<CharacterBody> bodies = new List<CharacterBody>();

        internal const string SkillNameToken = "DESTROYER_COMPOSURE_SKILL_NAME";
        internal const string SkillDescToken = "DESTROYER_COMPOSURE_SKILL_DESC";

        protected override void Hook()
        {
            Instance = this;
            Active = BuffFactory.Create("COMPOSURE_ACTIVE", BuffFactory.Hex("#7fd4ff"), isDebuff: false, canStack: false);

            Skill = ScriptableObject.CreateInstance<ComposureSkillDef>();
            ((ScriptableObject)Skill).name = "DestroyerComposureSkill";
            Skill.skillName = "DestroyerComposure";
            Skill.skillNameToken = SkillNameToken;
            Skill.skillDescriptionToken = SkillDescToken;
            Skill.icon = IconFactory.Create("DestroyerComposureSkill", IconBook.Get(Id));
            Skill.activationStateMachineName = "Weapon";
            Skill.activationState = new SerializableEntityStateType(typeof(Idle)); // не запускается: ComposureSkillDef переключает сам
            Skill.interruptPriority = InterruptPriority.Skill;
            Skill.baseRechargeInterval = toggleCooldown.Value;
            Skill.baseMaxStock = 1;
            Skill.rechargeStock = 1;
            Skill.requiredStock = 1;
            Skill.stockToConsume = 1;
            Skill.fullRestockOnAssign = true;
            Skill.isCombatSkill = false;
            Skill.mustKeyPress = true;
            Skill.cancelSprintingOnActivation = false;
            Skill.canceledFromSprinting = false;
            Skill.beginSkillCooldownOnSkillEnd = false;
            ContentAddition.AddSkillDef(Skill);
            SkillCooldowns.Exempt(Skill); // ни Ненависть, ни ускорители перезарядки переключатель не трогают (0.15.1)

            GameLanguage.SetText(SkillNameToken, new Words("Composure", "Хладнокровие"));
            GameLanguage.SetText(SkillDescToken, new Words(
                "Toggle. While active: more armor and speed, but no healing and you slowly lose health.",
                "Переключатель. Пока включено: больше брони и скорости, но лечения нет и здоровье медленно тает."));

            RecalculateStatsAPI.GetStatCoefficients += (body, args) =>
            {
                if (!Active || !body.HasBuff(Active)) return;
                int n = Count(body);
                if (n <= 0) return;
                args.armorAdd += armor.Value * n;
                args.moveSpeedMultAdd += speed.Value;
            };

            On.RoR2.HealthComponent.Heal += (orig, self, amount, mask, nonRegen) =>
            {
                if (NetworkServer.active && self && self.body && Active && self.body.HasBuff(Active)) return 0f;
                return orig(self, amount, mask, nonRegen);
            };

            RoR2Application.onFixedUpdate += ServerTick;
            CharacterBody.onBodyInventoryChangedGlobal += UpdateOverride;
            CharacterBody.onBodyStartGlobal += UpdateOverride;
            Run.onRunStartGlobal += run => drainAccumulator.Clear();
        }

        /// <summary>Сервер: таяние здоровья и снятие баффа у тех, кто потерял предмет.</summary>
        private void ServerTick()
        {
            if (!NetworkServer.active || !Run.instance || !Active) return;
            float dt = Time.fixedDeltaTime;
            bodies.Clear();
            foreach (var body in CharacterBody.readOnlyInstancesList)
                if (body && body.HasBuff(Active)) bodies.Add(body);

            foreach (var body in bodies)
            {
                int n = Count(body);
                if (n <= 0) { body.RemoveBuff(Active); drainAccumulator.Remove(body); continue; }
                var health = body.healthComponent;
                if (!health || !health.alive) continue;

                drainAccumulator.TryGetValue(body, out float acc);
                acc += health.fullHealth * drain.Value * n * dt;
                if (acc >= 1f)
                {
                    float whole = Mathf.Floor(acc);
                    acc -= whole;
                    float left = health.health - whole;
                    if (left <= 0f) { drainAccumulator.Remove(body); health.Suicide(null, null, default); continue; }
                    health.Networkhealth = left;
                }
                drainAccumulator[body] = acc;
            }
        }

        /// <summary>Каждая машина: есть предмет — навык мобильности заменён, нет — вернуть.</summary>
        private void UpdateOverride(CharacterBody body)
        {
            if (!body || Skill == null) return;
            overridden.RemoveWhere(b => !b);
            var utility = body.skillLocator ? body.skillLocator.utility : null;
            bool want = utility && Count(body) > 0;
            bool has = overridden.Contains(body);
            if (want && !has)
            {
                utility.SetSkillOverride(this, Skill, GenericSkill.SkillOverridePriority.Replacement);
                overridden.Add(body);
            }
            else if (!want && has)
            {
                if (utility) utility.UnsetSkillOverride(this, Skill, GenericSkill.SkillOverridePriority.Replacement);
                overridden.Remove(body);
            }
        }
    }

    /// <summary>
    /// Переключатель "Хладнокровие" (0.15.1): без машины состояний. CanExecute — только заряд и задержка (машина "Weapon"
    /// не проверяется и не прерывается — основная атака не сбивается); OnExecute — у владельца (authority) меняет бафф,
    /// тратит заряд и сообщает игре об активации навыка, как ванильный SkillDef.OnExecute, но без SetInterruptState.
    /// </summary>
    public class ComposureSkillDef : SkillDef
    {
        public override bool CanExecute(GenericSkill skillSlot) => skillSlot && IsReady(skillSlot);

        public override void OnExecute(GenericSkill skillSlot)
        {
            var body = skillSlot ? skillSlot.characterBody : null;
            var item = Items.Composure.Instance;
            if (body && body.hasEffectiveAuthority && item != null && item.Active)
            {
                if (body.HasBuff(item.Active)) body.RemoveBuffAuthority(item.Active.buffIndex);
                else body.AddBuffAuthority(item.Active.buffIndex);
            }
            skillSlot.stock -= stockToConsume;
            if (body) body.OnSkillActivated(skillSlot);
        }
    }
}
