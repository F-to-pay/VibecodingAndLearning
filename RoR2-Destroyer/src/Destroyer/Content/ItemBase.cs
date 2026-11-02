using System;
using System.Reflection;
using BepInEx.Configuration;
using Destroyer.Art;
using Destroyer.Core;
using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace Destroyer.Content
{
    /// <summary>
    /// Основа любого предмета мода. Один предмет = один класс-наследник в папке Items/ + строчка в Items/ItemList.cs.
    /// Шаблон с примерами всех частей — Items/ItemTemplate.cs.txt. Разбор — RAZBOR.md.
    ///
    /// Жизненный цикл (всё вызывает ItemRegistry):
    ///   1. Awake плагина:   BindConfig → (если включён) ItemDef + иконка + тексты → ItemAPI.Add → Hook
    ///   2. Каталоги готовы: PickupIndex, моделька с ванильного предмета → OnCatalogsReady
    /// </summary>
    internal abstract class ItemBase
    {
        public const string TokenPrefix = "DESTROYER_";

        // ---------- Что предмет обязан сказать о себе ----------

        /// <summary>
        /// Внутреннее имя: ЗАГЛАВНЫЕ латинские буквы, цифры, подчёркивание ("BLAST_CORE").
        /// Становится ItemDef.name = "DESTROYER_BLAST_CORE". После релиза НЕ МЕНЯТЬ: по нему предмет ищут сейвы
        /// (ProperSave), конфиги и другие моды.
        /// </summary>
        public abstract string Id { get; }

        /// <summary>Тир: Tier1 белый, Tier2 зелёный, Tier3 красный, Boss жёлтый, Lunar, VoidTier1..3, VoidBoss.</summary>
        public abstract ItemTier Tier { get; }

        /// <summary>Название.</summary>
        public abstract Words Name { get; }

        /// <summary>Короткая строка при подборе ("Ваши атаки взрываются.").</summary>
        public abstract Words Pickup { get; }

        /// <summary>
        /// Полное описание для логбука и всплывашки, с разметкой Tx.Damage(...) и Tx.Stack(...).
        /// Свойство пересчитывается при смене настроек — можно (и нужно) вставлять в текст текущие значения.
        /// </summary>
        public abstract Words Description { get; }

        // ---------- Необязательное ----------

        /// <summary>Лор для логбука.</summary>
        public virtual Words Lore => new Words("", "");

        /// <summary>
        /// Теги. Категория (Damage / Healing / Utility) нужна артефакту Командования и печатям;
        /// AIBlacklist — не давать монстрам (артефакт Эволюции, Пустотные поля); CannotCopy — турели Инженера не копируют;
        /// OnKillEffect, SprintRelated и т.п. — для синергий ванильной логики.
        /// </summary>
        public virtual ItemTag[] Tags => Array.Empty<ItemTag>();

        /// <summary>
        /// Чья моделька лежит на земле: внутреннее имя ванильного предмета ("Crowbar", "Behemoth", "BleedOnHit")
        /// или снаряжения ("DeathProjectile" — кукла Forgive Me Please). Сначала ищется предмет, потом снаряжение.
        /// null — знак вопроса. Своих 3D-моделей у мода нет, иконка — своя (Art/IconBook).
        /// </summary>
        public virtual string ModelFrom => null;

        /// <summary>
        /// Для пустотных предметов: какой предмет этот "заражает" (перерабатывает), как ванильные пустотные.
        /// Id нашего предмета ("PAINT_CAN") или внутреннее имя ванильного ("Crowbar"). null — никакой.
        /// </summary>
        public virtual string Corrupts => null;

        /// <summary>
        /// Для пустотных предметов, перерабатывающих СРАЗУ МНОГО предметов (например, все лунные): условие по ItemDef.
        /// Вызывается, когда каталог предметов уже собран (ItemCatalog.SetItemRelationships). Скрытые предметы и сам этот
        /// предмет не предлагаются. null — не используется.
        /// </summary>
        public virtual System.Func<RoR2.ItemDef, bool> CorruptsAll => null;

        /// <summary>Можно ли предмет отнять (Утилизатор, Котёл, жертва Пустоте...).</summary>
        public virtual bool CanRemove => true;

        /// <summary>Скрыть из логбука и всплывашек.</summary>
        public virtual bool Hidden => false;

        // ---------- То, что предмет переопределяет по делу ----------

        /// <summary>Завести настройки (числа предмета). Вызывается до создания ItemDef — даже у выключенного предмета.</summary>
        protected virtual void BindConfig(ItemSettings settings) { }

        /// <summary>Подписаться на события игры. Один раз в Awake, только у включённого предмета.</summary>
        protected abstract void Hook();

        /// <summary>Каталоги игры готовы: можно искать ванильные предметы, баффы, PickupIndex.</summary>
        protected virtual void OnCatalogsReady() { }

        // ---------- Что есть у предмета после регистрации ----------

        public ItemDef Def { get; private set; }

        public string InternalName => TokenPrefix + Id;

        /// <summary>Предмет включён в настройках и добавлен в игру.</summary>
        public bool IsRegistered => Def != null;

        /// <summary>Индекс в ItemCatalog. До загрузки каталогов — ItemIndex.None.</summary>
        public ItemIndex Index => Def != null ? Def.itemIndex : ItemIndex.None;

        /// <summary>Для дропа на землю. Появляется после загрузки каталогов.</summary>
        public PickupIndex PickupIndex { get; private set; } = PickupIndex.none;

        public ConfigEntry<bool> Enabled { get; private set; }

        public ItemSettings Settings { get; private set; }

        // ---------- Подсчёт копий ----------

        /// <summary>
        /// Сколько копий действует. "Effective" = постоянные + временные (Дрифтер) + "в аренду" и т.п.,
        /// с учётом отключённых предметов. Ванильный GetItemCount в 1.5 зовёт ровно это (проверено по IL).
        /// </summary>
        public int Count(Inventory inventory) =>
            inventory && Index != ItemIndex.None ? inventory.GetItemCountEffective(Index) : 0;

        public int Count(CharacterBody body) => body ? Count(body.inventory) : 0;

        public int Count(CharacterMaster master) => master ? Count(master.inventory) : 0;

        // ---------- Помощники для Hook ----------

        /// <summary>
        /// Бросок шанса "при попадании" по-ванильному: шанс в долях (0.05 = 5%) × proc coefficient атаки,
        /// с удачей хозяина (Клевер / 57-листник). Util.CheckRoll сам хочет проценты 0..100.
        /// </summary>
        protected static bool Roll(float chance, float procCoefficient, CharacterMaster master) =>
            chance > 0f && Util.CheckRoll(chance * 100f * procCoefficient, master);

        /// <summary>
        /// Повесить на тело ванильный "поведенческий" компонент, пока у него есть предмет:
        /// body.AddItemBehavior&lt;T&gt;(копий) — 0 копий снимает компонент. В T доступны body и stack.
        /// serverOnly: логика только на сервере (урон, спавн) — почти всегда так.
        /// </summary>
        protected void UseBehavior<T>(bool serverOnly = true) where T : CharacterBody.ItemBehavior
        {
            CharacterBody.onBodyInventoryChangedGlobal += body =>
            {
                if (serverOnly && !NetworkServer.active) return;
                body.AddItemBehavior<T>(Count(body));
            };
        }

        // ---------- Внутреннее (зовёт ItemRegistry) ----------

        internal void Register(ConfigFile file)
        {
            Settings = new ItemSettings(file, Id, Name);
            Enabled = Settings.Bool("Enabled", true,
                new Words("Enabled", "Включён"),
                new Words("Add this item to the game. Needs a game restart. Everyone in a lobby must have the same items enabled.",
                          "Добавить предмет в игру. Нужен перезапуск игры. У всех в лобби набор включённых предметов должен совпадать."),
                restartRequired: true);
            BindConfig(Settings);

            if (!Enabled.Value)
            {
                DestroyerPlugin.Log.LogInfo($"Item {Id} is disabled in the config.");
                return;
            }

            var def = ScriptableObject.CreateInstance<ItemDef>();
            def.name = InternalName;
            def.nameToken = InternalName + "_NAME";
            def.pickupToken = InternalName + "_PICKUP";
            def.descriptionToken = InternalName + "_DESC";
            def.loreToken = InternalName + "_LORE";
            def.tags = Tags ?? Array.Empty<ItemTag>();
            def.canRemove = CanRemove;
            def.hidden = Hidden;
            def.pickupIconSprite = IconFactory.Create(InternalName, IconBook.Get(Id));
            SetTier(def, Tier);
            SetTexts(def);

            // Своих моделек на персонаже нет — пустые правила отображения.
            ItemAPI.Add(new CustomItem(def, new ItemDisplayRuleDict(null)));
            Def = def; // с этого момента предмет в игре

            file.SettingChanged += (sender, args) =>
            {
                if (args.ChangedSetting.Definition.Section == Settings.Section) RefreshText();
            };

            Hook();
        }

        internal void CatalogsReady()
        {
            PickupIndex = PickupCatalog.FindPickupIndex(Def.itemIndex);
            if (!PickupIndex.isValid)
                DestroyerPlugin.Log.LogError($"PickupIndex for {InternalName} not found: the item will never drop.");

            BorrowModel();
            OnCatalogsReady();
        }

        /// <summary>Пересобрать тексты из текущих настроек.</summary>
        internal void RefreshText()
        {
            if (Def != null) SetTexts(Def);
        }

        private void SetTexts(ItemDef def)
        {
            GameLanguage.SetText(def.nameToken, Name);
            GameLanguage.SetText(def.pickupToken, Pickup);
            GameLanguage.SetText(def.descriptionToken, Description);
            GameLanguage.SetText(def.loreToken, Lore);
        }

        /// <summary>
        /// Моделька на земле с ванильного предмета. В 1.5 у ванильных ItemDef моделька часто только ссылкой
        /// (Addressables, pickupModelReference) — копируем и ссылку, и префаб. PickupDef к этому моменту уже собран
        /// из нашего ItemDef, поэтому правим и его.
        /// </summary>
        private void BorrowModel()
        {
            if (Config.DestroyerConfig.FlatPickupModels != null && Config.DestroyerConfig.FlatPickupModels.Value && UseFlatModel()) return;
            if (string.IsNullOrEmpty(ModelFrom)) return;

            PickupIndex sourcePickupIndex;
            var sourceIndex = ItemCatalog.FindItemIndex(ModelFrom);
            if (sourceIndex != ItemIndex.None)
            {
                var source = ItemCatalog.GetItemDef(sourceIndex);
                Def.pickupModelPrefab = source.pickupModelPrefab;
                Def.pickupModelReference = source.pickupModelReference;
                sourcePickupIndex = PickupCatalog.FindPickupIndex(sourceIndex);
            }
            else
            {
                var equipmentIndex = EquipmentCatalog.FindEquipmentIndex(ModelFrom);
                if (equipmentIndex == EquipmentIndex.None)
                {
                    DestroyerPlugin.Log.LogWarning($"{InternalName}: model source '{ModelFrom}' is neither an item nor an equipment (typo or DLC missing), '?' model will be used.");
                    return;
                }
                var source = EquipmentCatalog.GetEquipmentDef(equipmentIndex);
                Def.pickupModelPrefab = source.pickupModelPrefab;
                Def.pickupModelReference = source.pickupModelReference;
                sourcePickupIndex = PickupCatalog.FindPickupIndex(equipmentIndex);
            }

            if (!PickupIndex.isValid) return;
            var sourcePickup = PickupCatalog.GetPickupDef(sourcePickupIndex);
            var pickup = PickupCatalog.GetPickupDef(PickupIndex);
            if (sourcePickup == null || pickup == null) return;
            pickup.displayPrefab = sourcePickup.displayPrefab;
            pickup.displayPrefabReference = sourcePickup.displayPrefabReference;
        }

        /// <summary>2D-моделька из иконки (Content/FlatPickupModel). Не вышло — false, остаётся одолженная.</summary>
        private bool UseFlatModel()
        {
            var prefab = FlatPickupModel.Create(InternalName, Def.pickupIconSprite);
            if (!prefab) return false;
            Def.pickupModelPrefab = prefab;
            if (!PickupIndex.isValid) return true;
            var pickup = PickupCatalog.GetPickupDef(PickupIndex);
            if (pickup == null) return true;
            pickup.displayPrefab = prefab;
            pickup.displayPrefabReference = null; // PickupDisplay проверяет на null и берёт displayPrefab (IL 1.5)
            return true;
        }

        // ItemDef.deprecatedTier в игре private. Публицированные GameLibs пускают компилятор, но Mono в рантайме
        // кидает FieldAccessException (так упала первая версия Diabloid). Через рефлексию проверки нет.
        // Публичный сеттер ItemDef.tier не годится: он ищет ItemTierDef в ItemTierCatalog, а тот в Awake ещё пуст.
        // Геттер tier сам откатывается на deprecatedTier, если ItemTierDef не задан (проверено по IL 1.5).
        private static readonly FieldInfo deprecatedTierField =
            typeof(ItemDef).GetField("deprecatedTier", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private static void SetTier(ItemDef def, ItemTier tier)
        {
            if (deprecatedTierField == null)
            {
                DestroyerPlugin.Log.LogError("ItemDef.deprecatedTier not found (game update?), items will have no tier.");
                return;
            }
            deprecatedTierField.SetValue(def, tier);
        }
    }
}
