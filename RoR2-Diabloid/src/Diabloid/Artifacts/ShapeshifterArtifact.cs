using Diabloid.Content;
using Diabloid.Books;
using Diabloid.Elites;
using Diabloid.Loot;
using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace Diabloid.Artifacts
{
    /// <summary>
    /// Артефакт перевёртыша (0.16.0): каждый игрок начинает забег с книгой перевёртыша в слоте снаряжения.
    /// Run.onPlayerFirstCreatedServer — один раз на игрока за забег (и на тех, кто зашёл позже).
    /// Слот уже занят (другой мод выдал снаряжение) — книга падает под ноги, когда появится тело.
    /// </summary>
    internal static class ShapeshifterArtifact
    {
        public static ArtifactDef Def { get; private set; }

        public static bool IsEnabled =>
            RunArtifactManager.instance && RunArtifactManager.instance.IsArtifactEnabled(Def);

        public static void Register()
        {
            Def = ScriptableObject.CreateInstance<ArtifactDef>();
            ((ScriptableObject)Def).name = "DiabloidShapeshifter";
            Def.cachedName = "DiabloidShapeshifter";
            Def.nameToken = "DIABLOID_ARTIFACT_SHAPESHIFTER_NAME";
            Def.descriptionToken = "DIABLOID_ARTIFACT_SHAPESHIFTER_DESC";
            Def.smallIconSelectedSprite = TextureFactory.CreateShapeshifterArtifactIcon(true);
            Def.smallIconDeselectedSprite = TextureFactory.CreateShapeshifterArtifactIcon(false);

            GameLanguage.AddToken(Def.nameToken, new Words("Artifact of the Shapeshifter", "Артефакт перевёртыша"));
            GameLanguage.AddToken(Def.descriptionToken, new Words(
                "Start the run with a Tome of the Shapeshifter.",
                "Начинаешь забег с книгой перевёртыша."));

            ContentAddition.AddArtifactDef(Def);

            Run.onPlayerFirstCreatedServer += OnPlayerFirstCreated;
        }

        private static void OnPlayerFirstCreated(Run run, PlayerCharacterMasterController player)
        {
            if (!NetworkServer.active || !IsEnabled || !player) return;
            var master = player.master;
            var inventory = master ? master.inventory : null;
            var book = GearBooks.EquipmentOf(BookType.Shapeshifter);
            if (!inventory || book == EquipmentIndex.None) return;

            if (inventory.currentEquipmentIndex == EquipmentIndex.None)
            {
                inventory.SetEquipmentIndex(book);
                return;
            }

            // Слот занят — уронить книгу рядом, когда тело появится.
            System.Action<CharacterBody> drop = null;
            drop = body =>
            {
                master.onBodyStart -= drop;
                var pickup = PickupCatalog.FindPickupIndex(book);
                if (!body || !pickup.isValid) return;
                var info = new GenericPickupController.CreatePickupInfo { rotation = Quaternion.identity };
                info.pickup = new UniquePickup(pickup) { ignoreArtifactBasedPickupReplacements = true };
                PickupDropletController.CreatePickupDroplet(info, body.corePosition + Vector3.up, Vector3.up * 15f);
            };
            master.onBodyStart += drop;
        }
    }
}
