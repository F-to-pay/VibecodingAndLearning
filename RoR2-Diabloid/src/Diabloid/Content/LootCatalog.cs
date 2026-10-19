using System.Collections.Generic;
using System.Reflection;
using Diabloid.Config;
using Diabloid.Gameplay;
using Diabloid.Loot;
using R2API;
using RoR2;
using UnityEngine;

namespace Diabloid.Content
{
    /// <summary>
    /// Регистрирует в игре 42 "предмета" (6 слотов × 7 качеств).
    ///
    /// Зачем 42, а не 6? Ванильная капля лута (droplet) несёт с собой только PickupIndex.
    /// Значит, слот И качество должны быть зашиты в сам предмет — тогда на земле сразу видно
    /// "Легендарный шлем" с жёлтым лучом. А конкретные цифры роллятся в момент подбора.
    ///
    /// В инвентарь эти предметы НИКОГДА не попадают: подбор перехвачен через PickupDef.attemptGrant.
    /// </summary>
    internal static class LootCatalog
    {
        private static readonly ItemDef[,] itemDefs = new ItemDef[Slots.Count, Qualities.Count];
        private static readonly PickupIndex[,] pickupIndices = new PickupIndex[Slots.Count, Qualities.Count];
        private static readonly Dictionary<int, (GearSlot slot, GearQuality quality)> byPickupIndex =
            new Dictionary<int, (GearSlot, GearQuality)>();

        /// <summary>Каталоги игры загружены, индексы известны — можно ронять лут.</summary>
        public static bool Ready { get; private set; }

        /// <summary>Шаг 1 (Awake плагина): создать ItemDef'ы и отдать их R2API.</summary>
        public static void RegisterItems()
        {
            for (int s = 0; s < Slots.Count; s++)
            {
                for (int q = 0; q < Qualities.Count; q++)
                {
                    var slot = (GearSlot)s;
                    var quality = (GearQuality)q;
                    string id = $"DIABLOID_{slot}_{quality}".ToUpperInvariant();

                    var def = ScriptableObject.CreateInstance<ItemDef>();
                    def.name = id; // внутреннее имя в ItemCatalog
                    def.nameToken = id + "_NAME";
                    def.pickupToken = id + "_PICKUP";
                    def.descriptionToken = id + "_DESC";
                    def.loreToken = id + "_LORE";

                    // Тир определяет цвет луча и капли на земле.
                    SetTier(def, TierFor(quality));

                    def.hidden = true;     // не показывать в логбуке
                    def.canRemove = false; // чтобы всякие "отнять предмет" эффекты его не трогали
                    def.tags = new[]
                    {
                        ItemTag.WorldUnique,              // не попадать в сундуки/принтеры
                        ItemTag.IgnoreForDropList,        // то же, новым способом
                        ItemTag.CommandArtifactBlacklist, // артефакт Command не превращает в кубик
                        ItemTag.CannotCopy,               // турели инженера не копируют
                        ItemTag.CannotSteal,              // Митрикс не ворует
                        ItemTag.CannotDuplicate,
                        ItemTag.AIBlacklist,
                        ItemTag.BrotherBlacklist,
                    };

                    // Тексты на двух языках: игра сама покажет нужный (и переключит, если сменить язык в настройках).
                    var slotName = Slots.Get(slot).DisplayName;
                    GameLanguage.AddToken(def.nameToken, new Words(
                        GearItem.GroundName(slot, quality, Lang.English),
                        GearItem.GroundName(slot, quality, Lang.Russian)));
                    GameLanguage.AddToken(def.pickupToken, new Words(
                        $"{slotName.En} slot. Press E to equip.",
                        $"Слот «{slotName.Ru}». Нажми E, чтобы надеть."));
                    GameLanguage.AddToken(def.descriptionToken, new Words(
                        $"Gear for the {slotName.En} slot. Stats are rolled when you pick it up. " +
                        "If the slot is taken, the old item drops to the ground." + QualityNoteEn(quality),
                        $"Шмотка для слота «{slotName.Ru}». Характеристики генерируются в момент подбора. " +
                        "Если слот занят — старая шмотка падает на землю." + QualityNoteRu(quality)));
                    LanguageAPI.Add(def.loreToken, "");

                    ItemAPI.Add(new CustomItem(def, new ItemDisplayRuleDict(null)));

                    itemDefs[s, q] = def;
                    pickupIndices[s, q] = PickupIndex.none; // до загрузки каталогов индекс неизвестен
                }
            }
        }

        /// <summary>Шаг 2 (после загрузки всех каталогов): узнать PickupIndex'ы, повесить свой обработчик подбора, модельки.</summary>
        public static void OnCatalogsReady()
        {
            byPickupIndex.Clear();

            for (int s = 0; s < Slots.Count; s++)
            {
                for (int q = 0; q < Qualities.Count; q++)
                {
                    var def = itemDefs[s, q];
                    var pickupIndex = PickupCatalog.FindPickupIndex(def.itemIndex);
                    if (!pickupIndex.isValid)
                    {
                        DiabloidPlugin.Log.LogError($"PickupIndex for {def.name} not found, this item will never drop.");
                        continue;
                    }

                    pickupIndices[s, q] = pickupIndex;
                    byPickupIndex[pickupIndex.value] = ((GearSlot)s, (GearQuality)q);

                    // Главный трюк: у каждого PickupDef есть делегат "что делать при подборе".
                    // Ванильные предметы кладут себя в инвентарь, а наши — надеваются в слот.
                    var pickupDef = PickupCatalog.GetPickupDef(pickupIndex);
                    pickupDef.attemptGrant = GearPickup.Grant;
                }
            }

            ApplyModels();
            Ready = true;
            DiabloidPlugin.Log.LogInfo($"Registered {byPickupIndex.Count} gear pickup types.");
        }

        /// <summary>Своих 3D-моделей нет, поэтому одалживаем у ванильных предметов.</summary>
        private static void ApplyModels()
        {
            for (int s = 0; s < Slots.Count; s++)
            {
                string sourceName = DiabloidConfig.SlotModels[s].Value;
                var sourceIndex = ItemCatalog.FindItemIndex(sourceName);
                if (sourceIndex == ItemIndex.None)
                {
                    DiabloidPlugin.Log.LogWarning($"Item '{sourceName}' for the {(GearSlot)s} model was not found, '?' model will be used.");
                    continue;
                }

                var sourceDef = ItemCatalog.GetItemDef(sourceIndex);
                var sourcePickup = PickupCatalog.GetPickupDef(PickupCatalog.FindPickupIndex(sourceIndex));

                for (int q = 0; q < Qualities.Count; q++)
                {
                    var def = itemDefs[s, q];
                    def.pickupModelPrefab = sourceDef.pickupModelPrefab;
                    def.pickupModelReference = sourceDef.pickupModelReference;
                    def.pickupIconSprite = sourceDef.pickupIconSprite;
                    def.pickupIconSpriteReference = sourceDef.pickupIconSpriteReference;

                    if (sourcePickup == null || !pickupIndices[s, q].isValid) continue;

                    // PickupDef уже собран из ItemDef до нас, поэтому поля копируем и в него.
                    var pickupDef = PickupCatalog.GetPickupDef(pickupIndices[s, q]);
                    pickupDef.displayPrefab = sourcePickup.displayPrefab;
                    pickupDef.displayPrefabReference = sourcePickup.displayPrefabReference;
                    pickupDef.iconSprite = sourcePickup.iconSprite;
                    pickupDef.iconSpriteReference = sourcePickup.iconSpriteReference;
                    pickupDef.iconTexture = sourcePickup.iconTexture;
                }
            }
        }

        private static string QualityNoteEn(GearQuality quality)
        {
            switch (quality)
            {
                case GearQuality.Lunar: return " Lunar: 3 huge bonuses and 2 harsh curses.";
                case GearQuality.Void: return " Void: its level always equals the current monster level, but it always cuts regen.";
                default: return "";
            }
        }

        private static string QualityNoteRu(GearQuality quality)
        {
            switch (quality)
            {
                case GearQuality.Lunar: return " Лунная: 3 жирных бонуса и 2 злых проклятия.";
                case GearQuality.Void: return " Пустотная: её уровень всегда равен текущему уровню монстров, но она всегда режет регенерацию.";
                default: return "";
            }
        }

        public static bool IsGear(PickupIndex pickupIndex) => byPickupIndex.ContainsKey(pickupIndex.value);

        public static bool TryGet(PickupIndex pickupIndex, out GearSlot slot, out GearQuality quality)
        {
            if (byPickupIndex.TryGetValue(pickupIndex.value, out var entry))
            {
                slot = entry.slot;
                quality = entry.quality;
                return true;
            }
            slot = default;
            quality = default;
            return false;
        }

        public static PickupIndex GetPickupIndex(GearSlot slot, GearQuality quality) => pickupIndices[(int)slot, (int)quality];

        // ItemDef.deprecatedTier в игре private. NuGet-библиотеки игры "публицированы", поэтому
        // компилятор пропускает прямую запись, но Mono в рантайме бьёт по рукам: FieldAccessException.
        // (Первая версия мода на этом и упала.) Рефлексии проверки доступа не мешают — пишем через неё.
        // Публичный сеттер ItemDef.tier не годится: он ищет тир в ItemTierCatalog, а тот на этапе Awake ещё пуст.
        private static readonly FieldInfo deprecatedTierField =
            typeof(ItemDef).GetField("deprecatedTier", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private static void SetTier(ItemDef def, ItemTier tier)
        {
            if (deprecatedTierField == null)
            {
                DiabloidPlugin.Log.LogError("ItemDef.deprecatedTier not found (game update?), all gear will look white.");
                return;
            }
            deprecatedTierField.SetValue(def, tier);
        }

        /// <summary>
        /// Цвет на земле. Эпик (вырезан, не выпадает) — фиолетовый (void), легендарка — жёлтая (boss), лунное — голубое (lunar),
        /// пустотное — тоже фиолетовое (другого подходящего цвета у игры нет), различаются по названию.
        /// </summary>
        private static ItemTier TierFor(GearQuality quality)
        {
            switch (quality)
            {
                case GearQuality.Common: return ItemTier.Tier1;
                case GearQuality.Uncommon: return ItemTier.Tier2;
                case GearQuality.Rare: return ItemTier.Tier3;
                case GearQuality.Epic: return ItemTier.VoidTier1;
                case GearQuality.Lunar: return ItemTier.Lunar;
                case GearQuality.Void: return ItemTier.VoidTier3;
                default: return ItemTier.Boss;
            }
        }
    }
}
