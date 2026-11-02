using System.Collections.Generic;
using BepInEx.Configuration;
using CharacterTest.Content;
using CharacterTest.Core;
using RoR2;
using UnityEngine;

namespace CharacterTest.Survivors
{
    /// <summary>
    /// Основа выжившего. Наследник задаёт донора (ванильное тело, витрину, ИИ), тексты, статы и навыки —
    /// <see cref="SurvivorRegistry"/> клонирует донора, подменяет навыки и кладёт всё в каталоги.
    ///
    /// Без Unity у нас нет своих моделек и анимаций: внешность и аниматор — донорские. Значит, в состояниях
    /// навыков проигрываются анимации донора (имена слоёв/состояний его аниматора).
    /// </summary>
    public abstract class SurvivorBase
    {
        /// <summary>Id: A-Z, 0-9, _. Из него токены (CHARTEST_ID_...) и секция настроек. После релиза не менять.</summary>
        public abstract string Id { get; }

        /// <summary>Имя префаба тела, PascalCase с "Body" на конце (по нему BodyCatalog.FindBodyIndex). Не менять.</summary>
        public abstract string BodyName { get; }

        // --- Донор ---
        public abstract AssetRef DonorBody { get; }
        public abstract AssetRef DonorDisplay { get; }
        /// <summary>ИИ-мастер донора (для Goobo, Deus Ex Machina и прочих "копий"). null — без ИИ.</summary>
        public virtual AssetRef? DonorMaster => null;

        // --- Тексты ---
        public abstract Words Name { get; }
        public abstract Words Subtitle { get; }
        /// <summary>Описание на экране выбора (разметка игры: &lt;style=cIsDamage&gt; и т.д.).</summary>
        public abstract Words Description { get; }
        public virtual Words Lore => new Words("...");
        public virtual Words OutroWin => new Words("..and so it left, mission accomplished.", "..и так оно ушло, задание выполнено.");
        public virtual Words OutroFail => new Words("..and so it vanished, test incomplete.", "..и так оно исчезло, тест не пройден.");

        public virtual Color Color => Color.white;
        /// <summary>Место в списке выбора. Ванила: 1 (Коммандо) … ~17. Больше — правее.</summary>
        public virtual float SortPosition => 100f;

        /// <summary>Навыки. Слот без единого описания остаётся донорским.</summary>
        public abstract IEnumerable<SkillSpec> Skills();

        /// <summary>Статы и прочие правки клона тела. Токены имени, цвет и навыки уже стоят.</summary>
        protected internal virtual void SetupBody(CharacterBody body) { }

        /// <summary>Свои ассеты (клоны снарядов и т.п.). Зовётся в Awake после BindConfig, до навыков.</summary>
        protected internal virtual void CreateAssets() { }

        /// <summary>Свои настройки в секции "Survivor: ID" (Enabled уже есть).</summary>
        public virtual void BindConfig(ConfigFile config, string section) { }

        /// <summary>Подписки на события игры. Зовётся после регистрации, только если выживший включён.</summary>
        public virtual void Hook() { }

        // --- Заполняет реестр ---
        public string TokenPrefix => "CHARTEST_" + Id + "_";
        public GameObject BodyPrefab { get; internal set; }
        public GameObject DisplayPrefab { get; internal set; }
        public GameObject MasterPrefab { get; internal set; }
        public SurvivorDef SurvivorDef { get; internal set; }
    }
}
