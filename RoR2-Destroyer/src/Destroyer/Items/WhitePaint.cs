using BepInEx.Configuration;
using Destroyer.Content;
using Destroyer.Core;
using RoR2;
using UnityEngine.Networking;

namespace Destroyer.Items
{
    /// <summary>
    /// Белые краски (белый, 0.16.0). Сами ничего не делают. В начале следующего этапа (Stage.onServerStageBegin, сервер)
    /// каждая копия превращается в "Пустые банки" (WHITE_PAINT_EMPTY, без тира, как сломанные часы) и даёт 2 случайных
    /// белых предмета — из Run.availableTier1DropList (выключенные артефактами/DLC не выпадут), случай — Run.treasureRng.
    /// Могут выпасть и сами белые краски — тогда сработают на следующем этапе.
    /// Считаются только постоянные копии (GetItemCountPermanent): временные (Дрифтер) и "в аренду" не обналичиваются.
    /// Уведомления — SendTransformNotification (сетевое, видят и клиенты): краски → банки, краски → каждый новый предмет.
    /// Этап не важен: Базар и Луна тоже "следующий этап".
    /// </summary>
    internal sealed class WhitePaint : ItemBase
    {
        public override string Id => "WHITE_PAINT";
        public override ItemTier Tier => ItemTier.Tier1;
        public override string ModelFrom => "ScrapWhite";
        public override ItemTag[] Tags => new[] { ItemTag.Utility, ItemTag.AIBlacklist, ItemTag.CannotCopy };

        public override Words Name => new Words("White Paints", "Белые краски");

        public override Words Pickup => new Words(
            "Does nothing. On the next stage, turns into common items.",
            "Ничего не делает. На следующем этапе превращается в белые предметы.");

        public override Words Description => new Words(
            $"Does nothing. At the start of the next stage, each copy gives {Tx.Utility(Tx.Num(itemsPer.Value) + " random common items")} and is {Tx.Health("used up")}.",
            $"Ничего не делает. В начале следующего этапа каждая копия даёт {Tx.Utility(Tx.Num(itemsPer.Value) + " случайных белых предмета")} и {Tx.Health("расходуется")}.");

        public override Words Lore => new Words(
            "Wet paint. Do not touch until tomorrow.",
            "Осторожно, окрашено. Не трогать до завтра.");

        private ConfigEntry<int> itemsPer;

        protected override void BindConfig(ItemSettings s)
        {
            itemsPer = s.Int("ItemsPerCopy", 2, 0, 50, new Words("Items per copy", "Предметов за копию"),
                new Words("Random common items each copy gives on the next stage.", "Сколько случайных белых предметов даёт каждая копия на следующем этапе."));
        }

        internal static WhitePaint Instance;

        protected override void Hook()
        {
            Instance = this;
            Stage.onServerStageBegin += OnStageBegin;
        }

        private void OnStageBegin(Stage stage)
        {
            if (!NetworkServer.active || !Run.instance || Index == ItemIndex.None) return;
            var pool = Run.instance.availableTier1DropList;
            var empty = WhitePaintEmpty.Instance != null ? WhitePaintEmpty.Instance.Index : ItemIndex.None;

            foreach (var master in CharacterMaster.readOnlyInstancesList)
            {
                var inventory = master ? master.inventory : null;
                if (!inventory) continue;
                int n = inventory.GetItemCountPermanent(Index);
                if (n <= 0) continue;

                inventory.RemoveItemPermanent(Index, n);
                if (empty != ItemIndex.None)
                {
                    inventory.GiveItemPermanent(empty, n);
                    CharacterMasterNotificationQueue.SendTransformNotification(master, Index, empty, CharacterMasterNotificationQueue.TransformationType.Default);
                }

                int give = n * itemsPer.Value;
                if (pool == null || pool.Count == 0 || give <= 0) continue;
                for (int i = 0; i < give; i++)
                {
                    var pickup = PickupCatalog.GetPickupDef(Run.instance.treasureRng.NextElementUniform(pool));
                    if (pickup == null || pickup.itemIndex == ItemIndex.None) continue;
                    inventory.GiveItemPermanent(pickup.itemIndex, 1);
                    CharacterMasterNotificationQueue.SendTransformNotification(master, Index, pickup.itemIndex, CharacterMasterNotificationQueue.TransformationType.Default);
                }
            }
        }
    }

    /// <summary>Пустые банки — израсходованные белые краски. Без тира, ничего не делают, не отнимаются (как сломанные часы).</summary>
    internal sealed class WhitePaintEmpty : ItemBase
    {
        public override string Id => "WHITE_PAINT_EMPTY";
        public override ItemTier Tier => ItemTier.NoTier;
        public override bool CanRemove => false;
        public override ItemTag[] Tags => new[] { ItemTag.AIBlacklist, ItemTag.CannotCopy };

        public override Words Name => new Words("Empty Paint Cans", "Пустые банки");
        public override Words Pickup => new Words("All used up.", "Всё израсходовано.");
        public override Words Description => new Words(
            "A spent stack of White Paints. Does nothing.",
            "Израсходованные белые краски. Ничего не делают.");
        public override Words Lore => new Words("There is always a little left at the bottom.", "На дне всегда немного остаётся.");

        internal static WhitePaintEmpty Instance;

        protected override void Hook() => Instance = this;
    }
}
