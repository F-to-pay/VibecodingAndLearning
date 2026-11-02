using BepInEx;
using BepInEx.Logging;
using Destroyer.Config;
using Destroyer.Content;
using Destroyer.Core;
using Destroyer.Integration;
using Destroyer.UI;
using R2API;
using R2API.ContentManagement;
using R2API.Utils;
using RoR2;

namespace Destroyer
{
    // Модули R2API: BepInEx загрузит их раньше нас.
    [BepInDependency(ItemAPI.PluginGUID)]
    [BepInDependency(LanguageAPI.PluginGUID)]
    [BepInDependency(RecalculateStatsAPI.PluginGUID)]
    [BepInDependency(R2APIContentManager.PluginGUID)]
    // Risk of Options — по желанию: если стоит, загрузится раньше нас и появится меню настроек.
    [BepInDependency(RiskOfOptionsCompat.PluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
    // Мод добавляет предметы в каталог: у всех в лобби он должен быть одной версии,
    // иначе индексы предметов у хоста и клиента разъедутся.
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.EveryoneNeedSameModVersion)]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public sealed class DestroyerPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "FToPay.Destroyer";
        public const string PluginName = "RoR2-Destroyer";
        public const string PluginVersion = "0.16.1";

        internal static ManualLogSource Log;

        /// <summary>Сам плагин — на его GameObject предметы вешают свои MonoBehaviour (трекеры, тикеры).</summary>
        internal static DestroyerPlugin Instance;

        private void Awake()
        {
            Log = Logger;
            Instance = this;

            // Язык всех наших текстов = язык игры, спрашивается каждый раз заново.
            Loc.Provider = () => GameLanguage.Current;

            DestroyerConfig.Bind(Config);

            // 1) Предметы регистрируются ДО того, как игра соберёт каталоги.
            ItemRegistry.Register(Config);
            // 2) PickupIndex'ы и ванильные модельки доступны только ПОСЛЕ загрузки.
            RoR2Application.onLoad += ItemRegistry.OnCatalogsReady;

            gameObject.AddComponent<DebugMenu>();

            // Проверка через IsInstalled отдельно от вызова Init: так типы Risk of Options
            // не трогаются вообще, если мода нет (см. RiskOfOptionsCompat).
            if (RiskOfOptionsCompat.IsInstalled)
                RiskOfOptionsCompat.Init();

            Log.LogInfo($"{PluginName} {PluginVersion} loaded: {ItemRegistry.All.Count} item(s). Debug menu: {DestroyerConfig.DebugKey.Value} (if enabled).");
        }
    }
}
