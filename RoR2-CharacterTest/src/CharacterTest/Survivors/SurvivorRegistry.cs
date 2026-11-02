using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using BepInEx.Configuration;
using CharacterTest.Config;
using CharacterTest.Content;
using CharacterTest.Core;
using R2API;
using RoR2;
using UnityEngine;

namespace CharacterTest.Survivors
{
    /// <summary>
    /// Регистрация выживших (всё — в Awake плагина, до сборки каталогов):
    ///   донор → клон тела (сетевой) → токены, цвет, статы → свои SkillFamily вместо донорских
    ///   → клон витрины → клон ИИ-мастера (по желанию) → SurvivorDef → ContentAddition.
    /// Ошибка в одном выжившем пишется в лог и не роняет остальных.
    /// </summary>
    internal static class SurvivorRegistry
    {
        private static readonly Regex IdPattern = new Regex("^[A-Z0-9_]+$");
        private static readonly Regex BodyNamePattern = new Regex("^[A-Za-z0-9]+Body$");

        private static readonly List<SurvivorBase> all = new List<SurvivorBase>();
        public static IReadOnlyList<SurvivorBase> All => all;

        public static void Register(ConfigFile config)
        {
            var seen = new HashSet<string>();
            foreach (var survivor in SurvivorList.Create())
            {
                string id = survivor?.Id ?? "?";
                try
                {
                    if (!IdPattern.IsMatch(id)) throw new ArgumentException($"bad Id '{id}' (A-Z, 0-9, _)");
                    if (!seen.Add(id)) throw new ArgumentException($"duplicate Id '{id}'");
                    if (!BodyNamePattern.IsMatch(survivor.BodyName ?? ""))
                        throw new ArgumentException($"bad BodyName '{survivor.BodyName}' (PascalCase, ends with Body)");

                    string section = "Survivor: " + id;
                    var enabled = config.Bind(section, "Enabled", true,
                        "Выживший есть в игре. / Survivor is in the game. Restart required.");
                    CharacterTestConfig.Options.Add(new OptionInfo
                    {
                        Entry = enabled,
                        Name = new Words("Enabled", "Включён"),
                        Description = new Words("Survivor is in the game.", "Выживший есть в игре."),
                        RestartRequired = true,
                    });
                    survivor.BindConfig(config, section);
                    if (!enabled.Value)
                    {
                        CharacterTestPlugin.Log.LogInfo($"Survivor {id} disabled in config.");
                        continue;
                    }

                    Build(survivor);
                    survivor.Hook();
                    all.Add(survivor);
                    CharacterTestPlugin.Log.LogInfo($"Survivor {id} ({survivor.BodyName}) registered.");
                }
                catch (Exception e)
                {
                    CharacterTestPlugin.Log.LogError($"Survivor {id} failed: {e}");
                }
            }
        }

        private static void Build(SurvivorBase s)
        {
            string p = s.TokenPrefix;
            string baseName = s.BodyName.Substring(0, s.BodyName.Length - "Body".Length);

            // --- Тело ---
            var body = PrefabUtil.Clone(GameAssets.Load(s.DonorBody), s.BodyName, networked: true);
            var cb = body.GetComponent<CharacterBody>();
            if (!cb) throw new InvalidOperationException($"donor {s.DonorBody} has no CharacterBody");

            cb.baseNameToken = p + "NAME";
            cb.subtitleNameToken = p + "SUBTITLE";
            cb.bodyColor = s.Color;
            s.SetupBody(cb);

            // Свои снаряды, эффекты и т.п. — до навыков: состояниям они нужны уже готовыми.
            s.CreateAssets();

            SkillFactory.ApplyToBody(body, s.Skills(), p);

            // --- Витрина на экране выбора: не сетевая, просто копия ---
            var display = PrefabUtil.Clone(GameAssets.Load(s.DonorDisplay), baseName + "Display", networked: false);

            // --- ИИ-мастер (Goobo, Deus Ex Machina и т.п. ищут его по телу) ---
            GameObject master = null;
            if (s.DonorMaster.HasValue)
            {
                master = PrefabUtil.Clone(GameAssets.Load(s.DonorMaster.Value), baseName + "MonsterMaster", networked: true);
                var cm = master.GetComponent<CharacterMaster>();
                if (cm) cm.bodyPrefab = body;
                else CharacterTestPlugin.Log.LogWarning($"{s.Id}: donor master has no CharacterMaster");
            }

            // --- SurvivorDef ---
            var def = ScriptableObject.CreateInstance<SurvivorDef>();
            ((ScriptableObject)def).name = baseName;
            def.cachedName = baseName;
            def.bodyPrefab = body;
            def.displayPrefab = display;
            def.displayNameToken = p + "NAME";
            def.descriptionToken = p + "DESCRIPTION";
            def.outroFlavorToken = p + "OUTRO_FLAVOR";
            def.mainEndingEscapeFailureFlavorToken = p + "OUTRO_FAILURE";
            def.primaryColor = s.Color;
            def.desiredSortPosition = s.SortPosition;
            def.hidden = false;

            // --- Тексты. LORE — логбук берёт его из baseNameToken с _NAME → _LORE ---
            GameLanguage.SetText(p + "NAME", s.Name);
            GameLanguage.SetText(p + "SUBTITLE", s.Subtitle);
            GameLanguage.SetText(p + "DESCRIPTION", s.Description);
            GameLanguage.SetText(p + "LORE", s.Lore);
            GameLanguage.SetText(p + "OUTRO_FLAVOR", s.OutroWin);
            GameLanguage.SetText(p + "OUTRO_FAILURE", s.OutroFail);

            // --- В каталоги ---
            if (!ContentAddition.AddBody(body)) throw new InvalidOperationException("ContentAddition.AddBody refused " + s.BodyName);
            if (master && !ContentAddition.AddMaster(master)) CharacterTestPlugin.Log.LogWarning($"{s.Id}: AddMaster refused");
            if (!ContentAddition.AddSurvivorDef(def)) throw new InvalidOperationException("ContentAddition.AddSurvivorDef refused " + baseName);

            s.BodyPrefab = body;
            s.DisplayPrefab = display;
            s.MasterPrefab = master;
            s.SurvivorDef = def;
        }
    }
}
