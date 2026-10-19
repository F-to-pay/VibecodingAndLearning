using Diabloid.Books;
using Diabloid.Config;
using Diabloid.Content;
using Diabloid.Elites;
using Diabloid.Loot;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace Diabloid.Gameplay
{
    /// <summary>Выпадение шмоток с убитых врагов.</summary>
    internal static class LootDrops
    {
        public static void Init()
        {
            GlobalEventManager.onCharacterDeathGlobal += OnCharacterDeath;
        }

        private static void OnCharacterDeath(DamageReport report)
        {
            // Событие срабатывает у всех, но лут роняет только сервер — иначе каждый клиент уронил бы свой.
            if (!NetworkServer.active || !LootCatalog.Ready || !Run.instance) return;
            if (report == null || !report.victimBody) return;

            // Только настоящие враги. Нейтральные — это, например, горшки на Акведуке.
            var team = report.victimTeamIndex;
            if (team != TeamIndex.Monster && team != TeamIndex.Void && team != TeamIndex.Lunar) return;

            // Копии титана — бесплатное мясо, а не лутбоксы: дропает только оригинал.
            if (TitanMarker.IsCopyMaster(report.victimMaster)) return;

            bool champion = report.victimIsChampion || report.victimIsBoss;
            bool elite = report.victimIsElite;

            float chance = champion ? DiabloidConfig.ChampionDropChance.Value
                         : elite ? DiabloidConfig.EliteDropChance.Value
                         : DiabloidConfig.NormalDropChance.Value;

            var rng = LootRoller.Rng;
            if (rng.NextDouble() * 100.0 >= chance) return;

            var settings = DiabloidConfig.ToLootSettings();
            var slot = LootGenerator.RollSlot(rng);
            var quality = LootGenerator.RollQuality(
                rng,
                settings.QualityWeights,
                champion ? GearQuality.Rare : GearQuality.Common,
                champion ? DiabloidConfig.ChampionLuck.Value : elite ? DiabloidConfig.EliteLuck.Value : 0f);

            // Лунные и пустотные мобы иногда роняют шмотку своего особого качества.
            if (IsLunar(report))
            {
                if (rng.NextDouble() * 100.0 < DiabloidConfig.LunarMonsterLunarChance.Value) quality = GearQuality.Lunar;
            }
            else if (IsVoid(report))
            {
                if (rng.NextDouble() * 100.0 < DiabloidConfig.VoidMonsterVoidChance.Value) quality = GearQuality.Void;
            }

            var pickupIndex = LootCatalog.GetPickupIndex(slot, quality);

            // Иногда вместо шмотки — книга (активное снаряжение).
            if (rng.NextDouble() * 100.0 < DiabloidConfig.BookDropChance.Value)
            {
                var book = GearBooks.RandomBookPickup(rng);
                if (book.isValid) pickupIndex = book;
            }
            if (!pickupIndex.isValid) return;

            // Подбрасываем вверх и чуть вбок, как ванильный лут.
            var velocity = new Vector3(
                (float)(rng.NextDouble() * 2.0 - 1.0) * 5f,
                20f,
                (float)(rng.NextDouble() * 2.0 - 1.0) * 5f);

            // В 1.5.0 простые перегрузки CreatePickupDroplet помечены [Obsolete], актуальная — через CreatePickupInfo.
            // ignoreArtifactBasedPickupReplacements — чтобы артефакт Command не превратил шмотку в кубик выбора.
            var info = new GenericPickupController.CreatePickupInfo { rotation = Quaternion.identity };
            info.pickup = new UniquePickup(pickupIndex) { ignoreArtifactBasedPickupReplacements = true };
            PickupDropletController.CreatePickupDroplet(info, report.victimBody.corePosition, velocity);
        }

        /// <summary>Лунный — из лунной команды (химеры Луны) или Совершенная элитка (лунный аспект).</summary>
        private static bool IsLunar(DamageReport report)
        {
            return report.victimTeamIndex == TeamIndex.Lunar ||
                   (RoR2Content.Buffs.AffixLunar && report.victimBody.HasBuff(RoR2Content.Buffs.AffixLunar));
        }

        /// <summary>Пустотный — из команды Пустоты или с флагом тела Void (так игра помечает тварей Пустоты).</summary>
        private static bool IsVoid(DamageReport report)
        {
            return report.victimTeamIndex == TeamIndex.Void ||
                   (report.victimBody.bodyFlags & CharacterBody.BodyFlags.Void) != 0;
        }
    }
}
