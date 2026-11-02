using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CharacterTest.Core;
using EntityStates;
using R2API;
using RoR2;
using RoR2.Skills;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CharacterTest.Content
{
    /// <summary>
    /// Описание одного навыка. Выживший отдаёт список таких описаний, остальное делает <see cref="SkillFactory"/>.
    /// Несколько описаний на один слот = варианты в лоадауте (первый — по умолчанию).
    /// Поля по умолчанию — как у типичного навыка "с перезарядкой" (не основного).
    /// </summary>
    public sealed class SkillSpec
    {
        public SkillSlot Slot;

        /// <summary>Ключ навыка внутри выжившего: A-Z, 0-9, _. Из него — токены и имя SkillDef. После релиза не менять.</summary>
        public string Key;

        public Words Name;
        public Words Description;

        /// <summary>Состояние, в которое переходит машина при активации. Регистрируется само.</summary>
        public Type State;

        /// <summary>"Weapon" — атаки, "Body" — движение (рывки), как у ванильных выживших.</summary>
        public string Machine = "Weapon";

        public float Cooldown = 5f;
        public int MaxStock = 1;
        public int RechargeStock = 1;
        public int RequiredStock = 1;
        public int StockToConsume = 1;

        public InterruptPriority Priority = InterruptPriority.Skill;
        public bool IsCombatSkill = true;
        public bool CancelSprinting = true;
        public bool CanceledFromSprinting = false;
        public bool ForceSprint = false;
        public bool MustKeyPress = false;
        public bool BeginCooldownOnSkillEnd = false;
        public bool FullRestockOnAssign = true;
        public bool ResetCooldownOnUse = false;

        /// <summary>Иконка. null — берётся иконка навыка по умолчанию из этого слота у тела-донора.</summary>
        public Sprite Icon;

        /// <summary>Токены ключевых слов (KEYWORD_AGILE и т.п.) — подсказки в описании.</summary>
        public string[] Keywords;

        /// <summary>Свой тип SkillDef (например, с зарядами по нажатию). null — обычный SkillDef.</summary>
        public Func<SkillDef> Factory;

        /// <summary>
        /// Описание, собранное из настроек. Если задано — используется вместо Description и пересобирается
        /// при каждой смене любой настройки мода (<see cref="SkillFactory.RefreshTexts"/>).
        /// </summary>
        public Func<Words> DynamicDescription;

        /// <summary>Последняя правка готового SkillDef — для всего, чего нет в полях выше.</summary>
        public Action<SkillDef> Tweak;

        /// <summary>
        /// Копия навыка донора: индекс варианта в донорском SkillFamily этого слота
        /// (<see cref="DonorDefault"/> — его вариант по умолчанию). −1 — свой навык из полей выше.
        /// У копии донорские состояние, тексты, иконка и цифры; Name/Description/State/... игнорируются,
        /// кроме Tweak. Замок донора (ачивка) на копию не переходит.
        /// </summary>
        public int DonorVariant = -1;

        public const int DonorDefault = int.MaxValue;

        public bool IsDonorCopy => DonorVariant >= 0;

        // --- Заготовки ---

        /// <summary>Копия навыка донора в этом слоте (по умолчанию — его стандартный).</summary>
        public static SkillSpec FromDonor(SkillSlot slot, string key, int variant = DonorDefault, Action<SkillDef> tweak = null) => new SkillSpec
        {
            Slot = slot,
            Key = key,
            DonorVariant = variant,
            Tweak = tweak,
        };

        /// <summary>Основная атака: без перезарядки, по зажатию, прерывается чем угодно.</summary>
        public static SkillSpec Primary(string key, Type state, Words name, Words description) => new SkillSpec
        {
            Slot = SkillSlot.Primary,
            Key = key,
            State = state,
            Name = name,
            Description = description,
            Cooldown = 0f,
            Priority = InterruptPriority.Any,
            CancelSprinting = true,
        };

        public static SkillSpec Secondary(string key, Type state, Words name, Words description, float cooldown) => new SkillSpec
        {
            Slot = SkillSlot.Secondary, Key = key, State = state, Name = name, Description = description, Cooldown = cooldown,
        };

        /// <summary>Мобильность: машина Body, не сбивает спринт и не боевая (как рывок Коммандо).</summary>
        public static SkillSpec Utility(string key, Type state, Words name, Words description, float cooldown) => new SkillSpec
        {
            Slot = SkillSlot.Utility,
            Key = key,
            State = state,
            Name = name,
            Description = description,
            Cooldown = cooldown,
            Machine = "Body",
            Priority = InterruptPriority.PrioritySkill,
            IsCombatSkill = false,
            CancelSprinting = false,
            MustKeyPress = true,
        };

        public static SkillSpec Special(string key, Type state, Words name, Words description, float cooldown) => new SkillSpec
        {
            Slot = SkillSlot.Special, Key = key, State = state, Name = name, Description = description, Cooldown = cooldown,
            Priority = InterruptPriority.PrioritySkill, MustKeyPress = true,
        };
    }

    /// <summary>SkillSpec → SkillDef/SkillFamily в каталогах + подмена семейств на клоне тела.</summary>
    internal static class SkillFactory
    {
        // _skillFamily приватный — только рефлексией (см. PrefabUtil про FieldAccessException).
        private static readonly FieldInfo skillFamilyField =
            typeof(GenericSkill).GetField("_skillFamily", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly HashSet<Type> registeredStates = new HashSet<Type>();
        private static readonly List<(string token, Func<Words> build)> dynamicTexts = new List<(string, Func<Words>)>();

        /// <summary>Пересобрать описания из настроек (плагин зовёт на ConfigFile.SettingChanged).</summary>
        public static void RefreshTexts()
        {
            foreach (var (token, build) in dynamicTexts)
            {
                try { GameLanguage.SetText(token, build()); }
                catch (Exception e) { CharacterTestPlugin.Log.LogWarning($"Text {token}: {e.Message}"); }
            }
        }

        /// <summary>Состояние должно быть в EntityStateCatalog, иначе сеть не сможет его передать.</summary>
        public static void RegisterState(Type stateType)
        {
            if (stateType == null || !registeredStates.Add(stateType)) return;
            ContentAddition.AddEntityState(stateType, out bool added);
            if (!added) CharacterTestPlugin.Log.LogWarning($"EntityState {stateType.FullName} was not added (already there?)");
        }

        /// <summary>
        /// Копия донорского SkillDef. Instantiate сохраняет подкласс (у некоторых навыков он свой) и все поля.
        /// Имя/skillName — свои, чтобы не совпасть с оригиналом в SkillCatalog. Тексты — донорские токены.
        /// </summary>
        public static SkillDef CopyFromDonor(SkillSpec spec, string tokenPrefix, SkillFamily donorFamily)
        {
            if (!donorFamily || donorFamily.variants == null || donorFamily.variants.Length == 0)
                throw new InvalidOperationException($"Skill {spec.Key}: donor has no skills in {spec.Slot}");

            int index = spec.DonorVariant == SkillSpec.DonorDefault ? (int)donorFamily.defaultVariantIndex : spec.DonorVariant;
            if (index < 0 || index >= donorFamily.variants.Length)
                throw new ArgumentOutOfRangeException(nameof(spec.DonorVariant),
                    $"Skill {spec.Key}: donor {spec.Slot} has {donorFamily.variants.Length} variant(s), asked #{index}");

            var original = donorFamily.variants[index].skillDef;
            if (!original) throw new InvalidOperationException($"Skill {spec.Key}: donor variant #{index} is empty");

            var def = Object.Instantiate(original);
            string token = tokenPrefix + spec.Key;
            ((ScriptableObject)def).name = token;
            def.skillName = token;

            spec.Tweak?.Invoke(def);

            ContentAddition.AddSkillDef(def);
            CharacterTestPlugin.Log.LogInfo($"  {spec.Slot}: copy of {((ScriptableObject)original).name} ({original.activationState.stateType?.Name}) as {token}");
            return def;
        }

        public static SkillDef Build(SkillSpec spec, string tokenPrefix, Sprite fallbackIcon)
        {
            if (spec.State == null) throw new ArgumentException($"Skill {spec.Key}: State is null");
            if (!typeof(EntityState).IsAssignableFrom(spec.State))
                throw new ArgumentException($"Skill {spec.Key}: {spec.State.FullName} is not an EntityState");

            RegisterState(spec.State);

            var def = spec.Factory != null ? spec.Factory() : ScriptableObject.CreateInstance<SkillDef>();
            string token = tokenPrefix + spec.Key;

            ((ScriptableObject)def).name = token;
            def.skillName = token;
            def.skillNameToken = token + "_NAME";
            def.skillDescriptionToken = token + "_DESC";
            def.keywordTokens = spec.Keywords ?? Array.Empty<string>();
            def.icon = spec.Icon ? spec.Icon : fallbackIcon;

            def.activationStateMachineName = spec.Machine;
            def.activationState = new SerializableEntityStateType(spec.State);
            def.interruptPriority = spec.Priority;

            def.baseRechargeInterval = spec.Cooldown;
            def.baseMaxStock = spec.MaxStock;
            def.rechargeStock = spec.RechargeStock;
            def.requiredStock = spec.RequiredStock;
            def.stockToConsume = spec.StockToConsume;

            def.isCombatSkill = spec.IsCombatSkill;
            def.cancelSprintingOnActivation = spec.CancelSprinting;
            def.canceledFromSprinting = spec.CanceledFromSprinting;
            def.forceSprintDuringState = spec.ForceSprint;
            def.mustKeyPress = spec.MustKeyPress;
            def.beginSkillCooldownOnSkillEnd = spec.BeginCooldownOnSkillEnd;
            def.fullRestockOnAssign = spec.FullRestockOnAssign;
            def.resetCooldownTimerOnUse = spec.ResetCooldownOnUse;
            def.dontAllowPastMaxStocks = false;

            spec.Tweak?.Invoke(def);

            GameLanguage.SetText(def.skillNameToken, spec.Name);
            if (spec.DynamicDescription != null)
            {
                dynamicTexts.Add((def.skillDescriptionToken, spec.DynamicDescription));
                GameLanguage.SetText(def.skillDescriptionToken, spec.DynamicDescription());
            }
            else GameLanguage.SetText(def.skillDescriptionToken, spec.Description);

            ContentAddition.AddSkillDef(def);
            return def;
        }

        /// <summary>
        /// Для каждого слота из списка: новое SkillFamily с нашими навыками вместо семейства донора.
        /// Слот, которого нет в списке, остаётся донорским (удобно, пока навык не написан).
        /// </summary>
        public static void ApplyToBody(GameObject body, IEnumerable<SkillSpec> specs, string tokenPrefix)
        {
            if (skillFamilyField == null) throw new MissingFieldException("GenericSkill", "_skillFamily");

            var locator = body.GetComponent<SkillLocator>();
            if (!locator) throw new InvalidOperationException($"{body.name}: no SkillLocator");

            foreach (var group in specs.GroupBy(s => s.Slot))
            {
                GenericSkill slot = SlotOf(locator, group.Key);
                if (!slot) throw new InvalidOperationException($"{body.name}: donor has no {group.Key} skill slot");

                // Иконка донора на запас — пока своих нет.
                var donorFamily = slot.skillFamily;
                Sprite fallbackIcon = donorFamily && donorFamily.defaultSkillDef ? donorFamily.defaultSkillDef.icon : null;

                var family = ScriptableObject.CreateInstance<SkillFamily>();
                ((ScriptableObject)family).name = $"{tokenPrefix}{group.Key.ToString().ToUpperInvariant()}_FAMILY";
                family.variants = group
                    .Select(spec => spec.IsDonorCopy
                        ? CopyFromDonor(spec, tokenPrefix, donorFamily)
                        : Build(spec, tokenPrefix, fallbackIcon))
                    .Select(def => new SkillFamily.Variant
                    {
                        skillDef = def,
                        // skillName, а не токен: у копии токен донорский и совпал бы с оригиналом.
                        viewableNode = new ViewablesCatalog.Node(def.skillName, false, null),
                    })
                    .ToArray();
                family.defaultVariantIndex = 0;

                skillFamilyField.SetValue(slot, family);
                ContentAddition.AddSkillFamily(family);
            }
        }

        private static GenericSkill SlotOf(SkillLocator locator, SkillSlot slot)
        {
            switch (slot)
            {
                case SkillSlot.Primary: return locator.primary;
                case SkillSlot.Secondary: return locator.secondary;
                case SkillSlot.Utility: return locator.utility;
                case SkillSlot.Special: return locator.special;
                default: throw new ArgumentOutOfRangeException(nameof(slot), slot, "only Primary/Secondary/Utility/Special");
            }
        }
    }
}
