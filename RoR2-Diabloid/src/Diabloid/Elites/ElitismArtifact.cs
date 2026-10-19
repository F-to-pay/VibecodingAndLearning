using System.Collections.Generic;
using Diabloid.Config;
using Diabloid.Content;
using Diabloid.Loot;
using R2API;
using RoR2;
using RoR2.Artifacts;
using UnityEngine;
using UnityEngine.Networking;

namespace Diabloid.Elites
{
    /// <summary>
    /// Артефакт элитаризма: боссы телепорта всегда элитные.
    /// Финальных боссов (Митрикс и компания) не трогаем: у них свои скрипты, элитка может их сломать.
    /// </summary>
    internal static class ElitismArtifact
    {
        public static ArtifactDef Def { get; private set; }

        private static Xoroshiro128Plus rng;

        public static void Register()
        {
            Def = ScriptableObject.CreateInstance<ArtifactDef>();
            ((ScriptableObject)Def).name = "DiabloidElitism";
            Def.cachedName = "DiabloidElitism";
            Def.nameToken = "DIABLOID_ARTIFACT_ELITISM_NAME";
            Def.descriptionToken = "DIABLOID_ARTIFACT_ELITISM_DESC";
            Def.smallIconSelectedSprite = TextureFactory.CreateArtifactIcon(true);
            Def.smallIconDeselectedSprite = TextureFactory.CreateArtifactIcon(false);

            GameLanguage.AddToken(Def.nameToken, new Words("Artifact of Elitism", "Артефакт элитаризма"));
            GameLanguage.AddToken(Def.descriptionToken, new Words(
                "Teleporter bosses are always elite.",
                "Боссы телепорта всегда элитные."));

            ContentAddition.AddArtifactDef(Def);

            BossGroup.onBossGroupStartServer += OnBossGroupStartServer;
        }

        private static bool IsEnabled =>
            RunArtifactManager.instance && RunArtifactManager.instance.IsArtifactEnabled(Def);

        private static void OnBossGroupStartServer(BossGroup group)
        {
            if (!NetworkServer.active || !IsEnabled || !group) return;

            // Только босс-группа телепорта (туда же попадают лишние боссы от святилищ горы).
            var teleporter = TeleporterInteraction.instance;
            if (!teleporter || teleporter.bossGroup != group) return;

            var squad = group.combatSquad;
            if (!squad) return;

            // Кто-то мог успеть попасть в группу раньше нас, а остальных ловим по событию.
            foreach (var member in squad.readOnlyMembersList) Promote(member);
            squad.onMemberAddedServer += Promote;
        }

        private static void Promote(CharacterMaster master)
        {
            if (!master || !master.inventory || !IsEnabled) return;

            // Уже элитка (повезло с директором) — не трогаем.
            if (EliteCatalog.GetEliteDefFromEquipmentIndex(master.inventory.currentEquipmentIndex) != null) return;

            var elites = AvailableElites();
            if (elites.Length == 0) return;

            if (rng == null) rng = new Xoroshiro128Plus((ulong)System.DateTime.Now.Ticks);

            // Ванильный помощник артефакта Чести: выбирает элитку, выдаёт аспект и BoostHp/BoostDamage.
            EliteOnlyArtifactManager.PromoteAndApplyStats(master, rng, elites);

            var body = master.GetBody();
            if (body && EliteManager.Instance) EliteManager.Instance.QueueFullHeal(body);
        }

        /// <summary>
        /// Какие элитки сейчас разрешены: 1-й тир (с 3-го этапа — включая "поздние"),
        /// после лупа — ещё и 2-й тир. Сюда же автоматически попадают наши элитки, если они в этих тирах.
        /// </summary>
        private static EliteDef[] AvailableElites()
        {
            int stagesCleared = Run.instance ? Run.instance.stageClearCount : 0;
            var result = new List<EliteDef>();

            AddTier(result, stagesCleared >= 2 ? VanillaEliteTier.FullTier1 : VanillaEliteTier.BaseTier1);
            if (DiabloidConfig.ElitismTier2AfterLoop.Value && stagesCleared >= 5)
                AddTier(result, VanillaEliteTier.Tier2);

            return result.ToArray();
        }

        private static void AddTier(List<EliteDef> result, VanillaEliteTier tier)
        {
            var tierDef = EliteAPI.GetVanillaEliteTierDef(tier);
            if (tierDef?.eliteTypes == null) return;

            foreach (var elite in tierDef.eliteTypes)
            {
                if (elite && elite.eliteEquipmentDef && elite.IsAvailable() && !result.Contains(elite))
                    result.Add(elite);
            }
        }
    }
}
