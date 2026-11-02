using System;
using Diabloid.Books;
using Diabloid.Config;
using Diabloid.Content;
using Diabloid.Loot;
using R2API.Networking;
using R2API.Networking.Interfaces;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace Diabloid.Gameplay
{
    /// <summary>
    /// Переработка шмота в опыт (0.20.0).
    ///   Сжечь: смотришь на шмотку/книгу на земле (ту, что подсвечена для E) и жмёшь BurnKey (X) — шмотка исчезает,
    ///   к тебе летят шарики опыта, как с моба.
    ///   Забытое: всё наше, что лежит на земле при уходе с этапа, перерабатывается (50%) и приходит в начале следующего.
    /// Опыт = как за моба уровня шмотки × качество:
    ///   0.2 (коэф. опыта директора) × 20 (средняя цена моба) × сложность уровня (1 + 0.33 × (ур − 1))
    ///   × [обычная 1, необычная 2, редкая 4, эпик 6, легендарка 10, лунная 6, пустотная 6; книга 3, свиток 4]
    ///   × RecycleXp из настроек. Опыт в RoR2 общий на команду.
    /// </summary>
    internal sealed class GearRecycling : MonoBehaviour
    {
        private const float BurnRange = 15f;
        private const string RecycledToken = "DIABLOID_RECYCLED";

        public const float MobCost = 20f, DirectorXpCoefficient = 0.2f, DifficultyPerLevel = 0.33f;
        public const float BookXp = 3f, ScrollXp = 4f;

        private static ulong pendingXp;
        private static int pendingCount;

        public static void Init()
        {
            NetworkingAPI.RegisterMessageType<BurnRequestMessage>();
            SceneExitController.onBeginExit += OnBeginExit;
            Stage.onServerStageBegin += OnStageBegin;
            Run.onRunStartGlobal += _ => { pendingXp = 0; pendingCount = 0; };
            GameLanguage.AddToken(RecycledToken, new Words(
                "Forgotten gear recycled: {0} piece(s), +{1} XP.",
                "Забытый шмот переработан: {0} шт., +{1} опыта."));
        }

        public static float QualityXp(GearQuality quality)
        {
            switch (quality)
            {
                case GearQuality.Common: return 1f;
                case GearQuality.Uncommon: return 2f;
                case GearQuality.Rare: return 4f;
                case GearQuality.Epic: return 6f;
                case GearQuality.Legendary: return 10f;
                default: return 6f; // лунная, пустотная
            }
        }

        /// <summary>Опыт за обычного моба уровня level (примерно как даёт директор).</summary>
        public static float MobXp(int level) =>
            DirectorXpCoefficient * MobCost * (1f + DifficultyPerLevel * (Math.Max(1, level) - 1));

        private static bool IsRecyclable(PickupIndex index, out GearQuality quality, out bool book, out bool scroll)
        {
            quality = default;
            book = scroll = false;
            if (LootCatalog.IsGear(index)) return LootCatalog.TryGet(index, out _, out quality);
            var def = PickupCatalog.GetPickupDef(index);
            if (def == null || !GearBooks.TryGetBook(def.equipmentIndex, out _, out var perks)) return false;
            book = true;
            scroll = perks != null && perks.Length > 1;
            return true;
        }

        private static bool IsRecyclable(GenericPickupController pickup) =>
            pickup && IsRecyclable(pickup.pickupIndex, out _, out _, out _);

        /// <summary>Сколько опыта даст эта штука на земле (share: 1 — сжечь, 0.5 — забытая).</summary>
        private static ulong XpFor(GenericPickupController pickup, float share)
        {
            if (!pickup || !IsRecyclable(pickup.pickupIndex, out var quality, out bool book, out bool scroll)) return 0;
            int level = LootRoller.CurrentItemLevel();
            float mult;
            if (book) mult = scroll ? ScrollXp : BookXp;
            else
            {
                mult = QualityXp(quality);
                var dropped = pickup.GetComponent<DroppedGear>(); // снятая шмотка знает свой уровень
                if (dropped && dropped.Item != null) level = dropped.Item.Current().Level;
            }
            float xp = MobXp(level) * mult * DiabloidConfig.RecycleXp.Value * share;
            return xp <= 0f ? 0 : (ulong)Math.Max(1.0, Math.Round(xp));
        }

        // ---------------------------------------------------------------- сжечь по кнопке

        /// <summary>Клиент (и хост): кнопка нажата — просим сервер сжечь то, что подсвечено для E.</summary>
        private void Update()
        {
            if (!Run.instance || !DiabloidConfig.BurnKey.Value.IsDown()) return;
            var user = LocalUserManager.GetFirstLocalUser();
            if (user == null || user.isUIFocused) return;
            var body = user.cachedBody;
            var master = user.cachedMaster;
            if (!body || !master) return;

            var driver = body.GetComponent<InteractionDriver>();
            var target = driver ? driver.currentInteractable : null;
            var pickup = target ? target.GetComponent<GenericPickupController>() : null;
            if (!IsRecyclable(pickup)) return;
            var identity = pickup.GetComponent<NetworkIdentity>();
            if (!identity) return;

            new BurnRequestMessage(master.netId, identity.netId).Send(NetworkDestination.Server);
        }

        /// <summary>Сервер: проверить, что игрок рядом, убрать шмотку, выдать опыт шариками.</summary>
        public static void Burn(NetworkInstanceId masterId, NetworkInstanceId pickupId)
        {
            if (!NetworkServer.active) return;
            var masterObject = NetworkServer.FindLocalObject(masterId);
            var pickupObject = NetworkServer.FindLocalObject(pickupId);
            var master = masterObject ? masterObject.GetComponent<CharacterMaster>() : null;
            var body = master ? master.GetBody() : null;
            var pickup = pickupObject ? pickupObject.GetComponent<GenericPickupController>() : null;
            if (!body || !IsRecyclable(pickup)) return;

            var position = pickup.gameObject.transform.position;
            if ((position - body.corePosition).sqrMagnitude > BurnRange * BurnRange) return;

            ulong xp = XpFor(pickup, 1f);
            NetworkServer.Destroy(pickup.gameObject);
            if (xp == 0) return;
            if (ExperienceManager.instance) ExperienceManager.instance.AwardExperience(position, body, xp);
            else if (TeamManager.instance) TeamManager.instance.GiveTeamExperience(TeamIndex.Player, xp);
        }

        // ---------------------------------------------------------------- забытое при уходе с этапа

        /// <summary>Сервер: уход с этапа — всё наше на земле в копилку (и убрать, чтобы не посчитать дважды).</summary>
        private static void OnBeginExit(SceneExitController exit)
        {
            if (!NetworkServer.active) return;
            float share = DiabloidConfig.RecycleForgottenShare.Value / 100f;
            foreach (var pickup in UnityEngine.Object.FindObjectsOfType<GenericPickupController>())
            {
                if (!IsRecyclable(pickup)) continue;
                pendingXp += XpFor(pickup, share);
                pendingCount++;
                NetworkServer.Destroy(pickup.gameObject);
            }
            if (pendingCount > 0)
                DiabloidPlugin.Log.LogInfo($"Recycling: {pendingCount} forgotten piece(s) → {pendingXp} XP next stage.");
        }

        /// <summary>Сервер: начало этапа — выдать опыт за забытое и написать в чат всем.</summary>
        private static void OnStageBegin(Stage stage)
        {
            if (pendingCount == 0) return;
            ulong xp = pendingXp;
            int count = pendingCount;
            pendingXp = 0;
            pendingCount = 0;
            if (xp > 0 && TeamManager.instance) TeamManager.instance.GiveTeamExperience(TeamIndex.Player, xp);
            Chat.SendBroadcastChat(new Chat.SimpleChatMessage
            {
                baseToken = RecycledToken,
                paramTokens = new[] { count.ToString(), xp.ToString() },
            });
        }
    }

    /// <summary>Клиент → сервер: «сожги вот эту шмотку на земле».</summary>
    internal sealed class BurnRequestMessage : INetMessage
    {
        private NetworkInstanceId masterId;
        private NetworkInstanceId pickupId;

        public BurnRequestMessage() { }

        public BurnRequestMessage(NetworkInstanceId masterId, NetworkInstanceId pickupId)
        {
            this.masterId = masterId;
            this.pickupId = pickupId;
        }

        public void Serialize(NetworkWriter writer)
        {
            writer.Write(masterId);
            writer.Write(pickupId);
        }

        public void Deserialize(NetworkReader reader)
        {
            masterId = reader.ReadNetworkId();
            pickupId = reader.ReadNetworkId();
        }

        public void OnReceived() => GearRecycling.Burn(masterId, pickupId);
    }
}
