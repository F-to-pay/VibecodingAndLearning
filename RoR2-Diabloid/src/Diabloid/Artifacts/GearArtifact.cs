using System;
using Diabloid.Books;
using Diabloid.Config;
using Diabloid.Content;
using Diabloid.Elites;
using Diabloid.Gameplay;
using Diabloid.Loot;
using MonoMod.Cil;
using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace Diabloid.Artifacts
{
    /// <summary>
    /// Артефакт снаряжения: обычные предметы больше не выпадают — всё, что должно было упасть,
    /// превращается в шмотку Diabloid. А сложность растёт медленнее (по умолчанию вдвое), чтобы
    /// одной экипировкой можно было вытянуть забег.
    ///
    /// Как превращаем. Почти весь лут в игре (сундуки, святилища удачи, награда босса телепорта,
    /// колыбели Пустоты, лунные капсулы, Артефакт Жертвы...) в итоге зовёт ОДНУ функцию —
    /// PickupDropletController.CreatePickupDroplet(CreatePickupInfo, ...). Все остальные её перегрузки
    /// сводятся к ней. Перехватываем её и подменяем предмет на шмотку. Магазины (мультишопы, Базар)
    /// показывают предмет ДО покупки — их ловим отдельно в ShopTerminalBehavior.SetPickup.
    ///
    /// Тир предмета → качество шмотки:
    ///   белый → обычное (шанс необычного), зелёный → редкое, красный → эпическое (шанс легендарного),
    ///   жёлтый (босс) → легендарное, снаряжение → редкое, лунное → ЛУННОЕ, пустотное → ПУСТОТНОЕ.
    /// Не трогаем: монеты, ключи, артефакты, дронов, временные предметы и уникальные предметы
    /// (WorldUnique — ключ артефактов, жемчуг, семя Халкионита): без них ломаются квесты.
    /// </summary>
    internal static class GearArtifact
    {
        public static ArtifactDef Def { get; private set; }

        public static bool IsEnabled =>
            Def && RunArtifactManager.instance && RunArtifactManager.instance.IsArtifactEnabled(Def);

        public static void Register()
        {
            Def = ScriptableObject.CreateInstance<ArtifactDef>();
            ((ScriptableObject)Def).name = "DiabloidGear";
            Def.cachedName = "DiabloidGear";
            Def.nameToken = "DIABLOID_ARTIFACT_GEAR_NAME";
            Def.descriptionToken = "DIABLOID_ARTIFACT_GEAR_DESC";
            Def.smallIconSelectedSprite = TextureFactory.CreateGearArtifactIcon(true);
            Def.smallIconDeselectedSprite = TextureFactory.CreateGearArtifactIcon(false);

            GameLanguage.AddToken(Def.nameToken, new Words("Artifact of Gear", "Артефакт снаряжения"));
            GameLanguage.AddToken(Def.descriptionToken, new Words(
                "Items no longer drop: chests, shrines and bosses give Diabloid gear instead. Difficulty grows slower.",
                "Предметы больше не выпадают: сундуки, святилища и боссы дают шмотки Diabloid. Сложность растёт медленнее."));

            ContentAddition.AddArtifactDef(Def);

            On.RoR2.PickupDropletController.CreatePickupDroplet_CreatePickupInfo_Vector3_Vector3 += OnCreatePickupDroplet;
            On.RoR2.ShopTerminalBehavior.SetPickup += OnShopSetPickup;
            SceneDirector.onGenerateInteractableCardSelection += OnGenerateInteractableCardSelection;
            SceneDirector.onPostPopulateSceneServer += OnPostPopulateSceneServer;
            IL.RoR2.Run.RecalculateDifficultyCoefficentInternal += SlowDownDifficulty;
        }

        // ------------------------------------------------------------------
        // Лут → шмотки

        private static void OnCreatePickupDroplet(On.RoR2.PickupDropletController.orig_CreatePickupDroplet_CreatePickupInfo_Vector3_Vector3 orig,
                                                  GenericPickupController.CreatePickupInfo pickupInfo, Vector3 position, Vector3 velocity)
        {
            if (NetworkServer.active && IsEnabled && LootCatalog.Ready)
                ConvertDroplet(ref pickupInfo);
            orig(pickupInfo, position, velocity);
        }

        private static void ConvertDroplet(ref GenericPickupController.CreatePickupInfo info)
        {
            // Капля "на выбор" (потенциал Пустоты, лунный котёл Пещер...) несёт не предмет, а значок тира
            // и список вариантов. Качество берём по первому варианту.
            var source = info.pickup;
            var options = info.pickerOptions;
            if (options != null)
            {
                foreach (var option in options)
                {
                    if (!IsItemOrEquipment(option.pickup)) continue;
                    source = option.pickup;
                    break;
                }
            }

            if (!TryMakeGear(source, out var gear)) return;

            info.pickup = gear;
            // Шмотка — всегда обычная капля: никаких вариантов, своего префаба и обманок Артефакта Обмана.
            info.pickerOptions = null;
            info.maxOptionCount = null;
            info.prefabOverride = null;
            info.delusionItemIndex = ItemIndex.None;
            info.falseChoice1 = ItemIndex.None;
            info.falseChoice2 = ItemIndex.None;
        }

        private static void OnShopSetPickup(On.RoR2.ShopTerminalBehavior.orig_SetPickup orig, ShopTerminalBehavior self,
                                            UniquePickup newPickup, bool newHidden)
        {
            // Терминал сразу показывает шмотку, а при покупке роняет её же — капля уже не перехватывается (это шмотка).
            if (NetworkServer.active && IsEnabled && LootCatalog.Ready && TryMakeGear(newPickup, out var gear))
                newPickup = gear;
            orig(self, newPickup, newHidden);
        }

        private static bool IsItemOrEquipment(UniquePickup pickup)
        {
            if (!pickup.isValid) return false;
            var def = PickupCatalog.GetPickupDef(pickup.pickupIndex);
            return def != null && (def.itemIndex != ItemIndex.None || def.equipmentIndex != EquipmentIndex.None);
        }

        /// <summary>Предмет → случайная шмотка подходящего качества. false — этот пикап не трогаем.</summary>
        private static bool TryMakeGear(UniquePickup pickup, out UniquePickup gear)
        {
            gear = pickup;
            if (!pickup.isValid || pickup.isTempItem) return false; // временные предметы — механика выживших (Дрифтер), не лут
            if (LootCatalog.IsGear(pickup.pickupIndex)) return false;

            var def = PickupCatalog.GetPickupDef(pickup.pickupIndex);
            if (def == null) return false;

            // Снаряжение: книги не трогаем, обычное (не лунное, не жёлтое) становится случайной книгой.
            if (def.equipmentIndex != EquipmentIndex.None)
            {
                if (GearBooks.IsBook(def.equipmentIndex)) return false;
                var equipment = EquipmentCatalog.GetEquipmentDef(def.equipmentIndex);
                if (equipment && !equipment.isLunar && !equipment.isBoss)
                {
                    var book = GearBooks.RandomBookPickup(LootRoller.Rng);
                    if (!book.isValid) return false;
                    gear = new UniquePickup(book) { ignoreArtifactBasedPickupReplacements = true };
                    return true;
                }
            }

            if (!TryGetQuality(def, out var quality)) return false;

            var gearIndex = LootCatalog.GetPickupIndex(LootGenerator.RollSlot(LootRoller.Rng), quality);
            if (!gearIndex.isValid) return false;

            // ignoreArtifactBasedPickupReplacements — чтобы Артефакт Командования не сделал из шмотки кубик выбора.
            gear = new UniquePickup(gearIndex) { ignoreArtifactBasedPickupReplacements = true };
            return true;
        }

        private static bool TryGetQuality(PickupDef def, out GearQuality quality)
        {
            quality = GearQuality.Common;
            var rng = LootRoller.Rng;

            if (def.itemIndex != ItemIndex.None)
            {
                var item = ItemCatalog.GetItemDef(def.itemIndex);
                if (!item || item.ContainsTag(ItemTag.WorldUnique)) return false;

                switch (item.tier)
                {
                    case ItemTier.Tier1:
                        quality = rng.NextDouble() * 100.0 < DiabloidConfig.GearArtifactWhiteUncommonChance.Value
                            ? GearQuality.Uncommon : GearQuality.Common;
                        return true;
                    case ItemTier.Tier2:
                        quality = GearQuality.Rare;
                        return true;
                    case ItemTier.Tier3:   // эпик вырезан — красное сразу легендарка
                    case ItemTier.Boss:
                        quality = GearQuality.Legendary;
                        return true;
                    case ItemTier.Lunar:
                        quality = GearQuality.Lunar;
                        return true;
                    case ItemTier.VoidTier1:
                    case ItemTier.VoidTier2:
                    case ItemTier.VoidTier3:
                    case ItemTier.VoidBoss:
                        quality = GearQuality.Void;
                        return true;
                    case ItemTier.FoodTier:
                        quality = GearQuality.Uncommon;
                        return true;
                    default:
                        // NoTier, проклятые, страницы, хексы и прочая механика — не лут, не трогаем.
                        return false;
                }
            }

            if (def.equipmentIndex != EquipmentIndex.None)
            {
                var equipment = EquipmentCatalog.GetEquipmentDef(def.equipmentIndex);
                if (!equipment) return false;
                quality = equipment.isLunar ? GearQuality.Lunar
                        : equipment.isBoss ? GearQuality.Legendary
                        : GearQuality.Rare;
                return true;
            }

            // Монеты, ключи, артефакты, дроны — не предметы.
            return false;
        }

        // ------------------------------------------------------------------
        // Принтеры, утилизаторы, котлы — кормить их нечем, так что убираем

        private static void OnGenerateInteractableCardSelection(SceneDirector director, DirectorCardCategorySelection selection)
        {
            if (!IsEnabled || !DiabloidConfig.GearArtifactRemoveItemStations.Value || !selection) return;

            // Директор не потратит на них очки — значит, на этапе будет больше сундуков.
            selection.RemoveCardsThatFailFilter(card => !IsItemStation(card?.GetSpawnCard()?.prefab));
        }

        /// <summary>То, что стоит на карте само по себе (котлы Базара и т.п.), чистим после расстановки.</summary>
        private static void OnPostPopulateSceneServer(SceneDirector director)
        {
            if (!NetworkServer.active || !IsEnabled || !DiabloidConfig.GearArtifactRemoveItemStations.Value) return;

            int removed = 0;
            foreach (var purchase in UnityEngine.Object.FindObjectsOfType<PurchaseInteraction>())
            {
                if (!purchase || !IsItemStation(purchase.gameObject)) continue;
                NetworkServer.Destroy(purchase.gameObject);
                removed++;
            }
            if (removed > 0) DiabloidPlugin.Log.LogInfo($"Artifact of Gear: removed {removed} printers/scrappers/cauldrons.");
        }

        private static bool IsItemStation(GameObject prefab)
        {
            if (!prefab) return false;
            if (prefab.GetComponent<ScrapperController>()) return true;

            var purchase = prefab.GetComponent<PurchaseInteraction>();
            if (purchase)
            {
                switch (purchase.costType)
                {
                    case CostTypeIndex.WhiteItem:
                    case CostTypeIndex.GreenItem:
                    case CostTypeIndex.RedItem:
                    case CostTypeIndex.BossItem:
                        return true;
                }
            }

            string name = prefab.name;
            return name.IndexOf("Duplicator", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Scrapper", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ------------------------------------------------------------------
        // Медленная сложность

        /// <summary>
        /// Run.RecalculateDifficultyCoefficentInternal считает коэффициент сложности так:
        ///   коэф = (база + скорость × минуты) × 1.15^этапы,   база = 0.7 + 0.3 × игроки
        /// и из него — уровень монстров: (коэф − база) / 0.33 + 1.
        /// От коэффициента зависят спавн монстров, цены, золото, а от уровня — статы монстров.
        ///
        /// IL-хук вставляет в САМ метод игры три вызова: перед записью обоих коэффициентов и перед
        /// ограничением уровня потолком. Каждый урезает рост: база + (значение − база) × множитель.
        /// Обычный On-хук тут не годится: игра внутри метода сравнивает старый и новый уровень и
        /// стреляет событием "уровень вырос", а подправить значения ПОСЛЕ — значит ловить это событие каждый кадр.
        /// Метод крутится и на клиентах — артефакт им известен, так что у всех цифры одинаковые.
        /// </summary>
        private static void SlowDownDifficulty(ILContext il)
        {
            var c = new ILCursor(il);
            int patched = 0;

            if (c.TryGotoNext(MoveType.Before, x => x.MatchStfld<Run>(nameof(Run.compensatedDifficultyCoefficient))))
            {
                c.EmitDelegate<Func<float, float>>(ScaleCoefficient);
                patched++;
            }

            if (c.TryGotoNext(MoveType.Before, x => x.MatchStfld<Run>(nameof(Run.difficultyCoefficient))))
            {
                c.EmitDelegate<Func<float, float>>(ScaleCoefficient);
                patched++;
            }

            if (c.TryGotoNext(MoveType.Before, x => x.MatchLdsfld<Run>("ambientLevelCap")))
            {
                c.EmitDelegate<Func<float, float>>(ScaleLevel);
                patched++;
            }

            if (patched == 3)
                DiabloidPlugin.Log.LogInfo("Artifact of Gear: difficulty hook installed.");
            else
                DiabloidPlugin.Log.LogError($"Artifact of Gear: difficulty hook matched {patched}/3 places (game update?), difficulty won't be slowed down.");
        }

        private static float DifficultyScale => Mathf.Clamp(DiabloidConfig.GearArtifactDifficultyScale.Value, 0.05f, 1f);

        private static float ScaleCoefficient(float coefficient)
        {
            var run = Run.instance;
            if (!run || !IsEnabled) return coefficient;
            float baseline = 0.7f + 0.3f * run.participatingPlayerCount;
            return baseline + (coefficient - baseline) * DifficultyScale;
        }

        private static float ScaleLevel(float level)
        {
            if (!IsEnabled) return level;
            return 1f + (level - 1f) * DifficultyScale;
        }
    }
}
