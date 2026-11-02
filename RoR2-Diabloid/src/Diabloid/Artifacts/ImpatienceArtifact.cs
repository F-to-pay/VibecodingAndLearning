using Diabloid.Content;
using Diabloid.Elites;
using Diabloid.Loot;
using R2API;
using RoR2;
using UnityEngine;

namespace Diabloid.Artifacts
{
    /// <summary>
    /// Артефакт нетерпения: время зарядки телепорта −90% (×10 к скорости зарядки, 90 с → 9 с).
    ///
    /// 0.13.0 заливал заряд целиком, как только появлялся босс, — не работало: ChargingState.FixedUpdate
    /// проверяет monstersCleared с прошлого тика (UpdateMonstersClear в конце), босс-группа ещё считалась
    /// выключенной → ChargedState при живом боссе, телепорт вставал. Теперь — честный множитель через ванильное
    /// событие HoldoutZoneController.calcChargeRate (ref float rate): вся остальная логика (стоять в зоне,
    /// ждать смерти босса) ванильная. Подписка — на зону телепорта при начале зарядки; -= перед += делает её
    /// идемпотентной, телепорт и так пересоздаётся каждый этап. Первобытный телепорт тоже; пустотные поля и
    /// колонны Луны — другие HoldoutZone, их не трогаем.
    /// </summary>
    internal static class ImpatienceArtifact
    {
        /// <summary>−90% времени = в 10 раз быстрее.</summary>
        public const float ChargeRateMultiplier = 10f;

        public static ArtifactDef Def { get; private set; }

        public static bool IsEnabled =>
            RunArtifactManager.instance && RunArtifactManager.instance.IsArtifactEnabled(Def);

        public static void Register()
        {
            Def = ScriptableObject.CreateInstance<ArtifactDef>();
            ((ScriptableObject)Def).name = "DiabloidImpatience";
            Def.cachedName = "DiabloidImpatience";
            Def.nameToken = "DIABLOID_ARTIFACT_IMPATIENCE_NAME";
            Def.descriptionToken = "DIABLOID_ARTIFACT_IMPATIENCE_DESC";
            Def.smallIconSelectedSprite = TextureFactory.CreateImpatienceArtifactIcon(true);
            Def.smallIconDeselectedSprite = TextureFactory.CreateImpatienceArtifactIcon(false);

            GameLanguage.AddToken(Def.nameToken, new Words("Artifact of Impatience", "Артефакт нетерпения"));
            GameLanguage.AddToken(Def.descriptionToken, new Words(
                "Teleporter charge time is reduced by 90%.",
                "Время зарядки телепорта −90%."));

            ContentAddition.AddArtifactDef(Def);

            TeleporterInteraction.onTeleporterBeginChargingGlobal += OnBeginCharging;
        }

        private static void OnBeginCharging(TeleporterInteraction teleporter)
        {
            if (!teleporter) return;
            var zone = teleporter.holdoutZoneController;
            if (!zone) return;
            zone.calcChargeRate -= SpeedUp;
            zone.calcChargeRate += SpeedUp;
        }

        private static void SpeedUp(ref float rate)
        {
            if (IsEnabled) rate *= ChargeRateMultiplier;
        }
    }
}
