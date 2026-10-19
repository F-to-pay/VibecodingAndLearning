using BepInEx;
using BepInEx.Logging;
using Diabloid.Artifacts;
using Diabloid.Books;
using Diabloid.Config;
using Diabloid.Content;
using Diabloid.Elites;
using Diabloid.Gameplay;
using Diabloid.Integration;
using Diabloid.Loot;
using Diabloid.Network;
using Diabloid.UI;
using R2API;
using R2API.ContentManagement;
using R2API.Networking;
using R2API.Utils;
using RoR2;

namespace Diabloid
{
    // Зависимости от модулей R2API: BepInEx загрузит их раньше нас.
    [BepInDependency(ItemAPI.PluginGUID)]
    [BepInDependency(LanguageAPI.PluginGUID)]
    [BepInDependency(RecalculateStatsAPI.PluginGUID)]
    [BepInDependency(NetworkingAPI.PluginGUID)]
    [BepInDependency(EliteAPI.PluginGUID)]
    [BepInDependency(R2APIContentManager.PluginGUID)]
    // Risk of Options — по желанию: если стоит, загрузится раньше нас и появится меню настроек.
    [BepInDependency(RiskOfOptionsCompat.PluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
    // Мод добавляет предметы в каталог, поэтому у всех в лобби он должен быть одинаковой версии,
    // иначе индексы предметов у хоста и клиента разъедутся.
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.EveryoneNeedSameModVersion)]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public sealed class DiabloidPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "FToPay.Diabloid";
        public const string PluginName = "Diabloid";
        public const string PluginVersion = "0.11.2";

        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;

            // Если кто-то (ты через месяц) сломал порядок в таблицах — лучше упасть сразу, чем ловить чудеса в игре.
            Stats.Validate();
            Qualities.Validate();
            Slots.Validate();

            // Язык всех наших текстов = язык игры (русский или английский), спрашивается каждый раз заново —
            // поэтому смена языка в настройках игры подхватывается без перезапуска.
            Loc.Provider = () => GameLanguage.Current;

            DiabloidConfig.Bind(Config);

            // Пустотные шмотки живут на текущем уровне монстров; генератор не знает про игру, поэтому подсказываем ему.
            GearItem.MonsterLevelProvider = LootRoller.CurrentItemLevel;
            GearItem.LevelScalingProvider = () => DiabloidConfig.LevelScalingMultiplier.Value;

            // 1) Предметы нужно зарегистрировать ДО того, как игра соберёт каталоги.
            LootCatalog.RegisterItems();
            // 2) А PickupIndex'ы появляются только ПОСЛЕ — дожидаемся загрузки.
            RoR2Application.onLoad += LootCatalog.OnCatalogsReady;

            // Элитки и артефакт регистрируются там же, в Awake. Иконки и материалы — после загрузки.
            DiabloidElites.Register();
            ElitismArtifact.Register();
            GearArtifact.Register();
            EliteHooks.Init();
            RoR2Application.onLoad += DiabloidElites.OnCatalogsReady;
            PerkManager.Register();
            RoR2Application.onLoad += PerkManager.OnCatalogsReady;
            GearBooks.Register();
            RoR2Application.onLoad += GearBooks.OnCatalogsReady;

            GearNetwork.Init();
            GearPickup.Init();
            LootDrops.Init();
            GearStats.Init();
            GearLifecycle.Init();

            gameObject.AddComponent<GearPanel>();
            gameObject.AddComponent<EliteManager>();
            gameObject.AddComponent<PerkManager>();
            gameObject.AddComponent<BookRunner>();

            // Проверка через IsInstalled вынесена отдельно от вызова Init: так типы Risk of Options
            // не трогаются вообще, если мода нет (см. комментарий в RiskOfOptionsCompat).
            if (RiskOfOptionsCompat.IsInstalled)
                RiskOfOptionsCompat.Init();

            Log.LogInfo($"{PluginName} {PluginVersion} loaded. Equipment panel: {DiabloidConfig.PanelKey.Value}.");
        }
    }
}
