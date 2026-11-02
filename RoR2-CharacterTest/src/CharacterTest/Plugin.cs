using BepInEx;
using BepInEx.Logging;
using CharacterTest.Config;
using CharacterTest.Content;
using CharacterTest.Core;
using CharacterTest.Integration;
using CharacterTest.Survivors;
using R2API;
using R2API.ContentManagement;
using R2API.Utils;

namespace CharacterTest
{
    // Модули R2API: BepInEx загрузит их раньше нас.
    [BepInDependency(LanguageAPI.PluginGUID)]
    [BepInDependency(R2APIContentManager.PluginGUID)]
    // Risk of Options — по желанию: есть — появится меню настроек.
    [BepInDependency(RiskOfOptionsCompat.PluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
    // Мод добавляет тела, навыки и состояния в каталоги: у всех в лобби он должен быть одной версии,
    // иначе индексы у хоста и клиента разъедутся.
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.EveryoneNeedSameModVersion)]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public sealed class CharacterTestPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "FToPay.CharacterTest";
        public const string PluginName = "RoR2-CharacterTest";
        public const string PluginVersion = "0.6.0";

        internal static ManualLogSource Log;
        internal static CharacterTestPlugin Instance;

        private void Awake()
        {
            Log = Logger;
            Instance = this;

            // Язык всех наших текстов = язык игры, спрашивается каждый раз заново.
            Loc.Provider = () => GameLanguage.Current;

            CharacterTestConfig.Bind(Config);

            // Всё — клоны префабов, навыки, состояния, SurvivorDef — регистрируется ДО того,
            // как игра соберёт каталоги (ContentManager), то есть прямо здесь, в Awake.
            SurvivorRegistry.Register(Config);

            // IsInstalled отдельно от Init: без мода типы Risk of Options не трогаются вообще.
            if (RiskOfOptionsCompat.IsInstalled)
                RiskOfOptionsCompat.Init();

            // Цифры в описаниях навыков следуют за настройками.
            Config.SettingChanged += (_, __) => SkillFactory.RefreshTexts();

            Log.LogInfo($"{PluginName} {PluginVersion} loaded: {SurvivorRegistry.All.Count} survivor(s).");
        }
    }
}
