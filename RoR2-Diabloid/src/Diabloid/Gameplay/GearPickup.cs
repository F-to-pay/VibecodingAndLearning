using Diabloid.Content;
using Diabloid.Loot;
using Diabloid.Network;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace Diabloid.Gameplay
{
    /// <summary>
    /// Висит на шмотке, которую игрок снял при замене. Хранит её точные статы,
    /// чтобы при повторном подборе не роллить заново. Существует только на сервере.
    /// </summary>
    internal sealed class DroppedGear : MonoBehaviour
    {
        public GearItem Item;
    }

    /// <summary>Подбор шмоток: только по E, со сбросом старой шмотки на землю.</summary>
    internal static class GearPickup
    {
        public static void Init()
        {
            // Ванильные предметы подбираются, когда ты в них входишь (OnTriggerStay).
            // Для наших это отключаем — иначе при замене старая шмотка, упав под ноги,
            // тут же подбиралась бы обратно, и так до бесконечности.
            On.RoR2.GenericPickupController.OnTriggerStay += OnTriggerStay;
        }

        private static void OnTriggerStay(On.RoR2.GenericPickupController.orig_OnTriggerStay orig,
                                          GenericPickupController self, Collider other)
        {
            if (self && LootCatalog.IsGear(self.pickupIndex)) return; // только по кнопке взаимодействия
            orig(self, other);
        }

        /// <summary>
        /// Вызывается игрой (через PickupDef.attemptGrant) на сервере, когда игрок жмёт E на нашей шмотке.
        /// Если оставить shouldDestroy = false — шмотка останется лежать.
        /// </summary>
        public static void Grant(ref PickupDef.GrantContext context)
        {
            context.shouldDestroy = false;
            context.shouldNotify = false; // ванильное "X подобрал Y" не нужно, пишем своё (GearAnnouncer)

            if (!NetworkServer.active) return;

            var body = context.body;
            var controller = context.controller;
            if (!body || !controller) return;

            // Надевать могут только игроки (не дроны и не турели).
            var master = body.master;
            if (!master || !master.playerCharacterMasterController) return;

            if (!LootCatalog.TryGet(controller.pickupIndex, out var slot, out var quality)) return;

            // Либо это ранее снятая шмотка с известными статами, либо свежая — роллим прямо сейчас.
            var dropped = controller.GetComponent<DroppedGear>();
            var item = dropped && dropped.Item != null ? dropped.Item : LootRoller.Generate(slot, quality);

            var gear = GearRegistry.GetOrCreate(master);
            var previous = gear.Equip(item);
            context.shouldDestroy = true; // игра сама удалит подобранную шмотку с земли

            if (previous != null)
                DropOnGround(previous, controller.transform.position);

            GearRegistry.MarkStatsDirty(master);              // пересчитать статы на сервере — себе и турелям Инженера
            GearNetwork.Broadcast(master, gear, item.Slot);   // рассказать клиентам (они сами напишут в свой чат)
            GearAnnouncer.Announce(master, item, previous);   // а хост пишет в свой чат сам
        }

        /// <summary>Кладёт снятую шмотку на землю, запоминая её точные статы.</summary>
        private static void DropOnGround(GearItem item, Vector3 position)
        {
            var pickupIndex = LootCatalog.GetPickupIndex(item.Slot, item.Quality);
            if (!pickupIndex.isValid) return;

            var info = new GenericPickupController.CreatePickupInfo
            {
                position = position,
                rotation = Quaternion.identity, // у структуры по умолчанию (0,0,0,0) — это невалидный кватернион
            };
            info.pickupIndex = pickupIndex;

            var pickup = GenericPickupController.CreatePickup(in info);
            if (pickup)
                pickup.gameObject.AddComponent<DroppedGear>().Item = item;
        }
    }
}
