using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Diabloid.Config;
using Diabloid.Content;
using Diabloid.Loot;
using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Diabloid.Elites
{
    /// <summary>
    /// Регистрация наших элиток. Элитка в RoR2 — это связка из трёх вещей:
    ///   EquipmentDef ("аспект") — монстр "носит" его в слоте снаряжения;
    ///   BuffDef — пассивный бафф аспекта, по нему все проверяют "это титан?";
    ///   EliteDef — имя ("{0} титанизма"), цвет, бусты хп/урона, в какие тиры спавна входит.
    /// Директор спавна выбирает элитку, выдаёт монстру аспект и BoostHp/BoostDamage — дальше всё работает само.
    /// </summary>
    internal static class DiabloidElites
    {
        internal sealed class EliteEntry
        {
            public EliteKind Kind;
            public Words Modifier;          // "Titanic {0}" / "{0} титанизма"
            public Words AspectName;        // имя аспекта (почти нигде не видно, но пусть будет)
            public Color32 Color;
            public string IconSourceBuff;   // у какого ванильного баффа одолжить иконку
            public BuffDef Buff;
            public EquipmentDef Equipment;
            public EliteDef Elite;
        }

        private static readonly EliteEntry[] entries =
        {
            new EliteEntry
            {
                Kind = EliteKind.Titan,
                Modifier = new Words("Titanic {0}", "{0} титанизма"),
                AspectName = new Words("Aspect of the Titan", "Аспект титана"),
                Color = new Color32(176, 141, 87, 255),   // бронза
                IconSourceBuff = nameof(RoR2Content.Buffs.ArmorBoost),
            },
            new EliteEntry
            {
                Kind = EliteKind.Bloodsucker,
                Modifier = new Words("Bloodsucking {0}", "{0} кровопийца"),
                AspectName = new Words("Aspect of the Bloodsucker", "Аспект кровопийцы"),
                Color = new Color32(158, 10, 40, 255),    // тёмно-красный
                IconSourceBuff = nameof(RoR2Content.Buffs.LifeSteal),
            },
            new EliteEntry
            {
                Kind = EliteKind.Clockmaker,
                Modifier = new Words("Clockwork {0}", "{0} часовщик"),
                AspectName = new Words("Aspect of the Clockmaker", "Аспект часовщика"),
                Color = new Color32(79, 195, 247, 255),   // холодный голубой
                IconSourceBuff = nameof(RoR2Content.Buffs.Slow60),
            },
            new EliteEntry
            {
                Kind = EliteKind.Power,
                Modifier = new Words("{0} of Power", "{0} силы"),
                AspectName = new Words("Aspect of Power", "Аспект силы"),
                Color = new Color32(255, 112, 32, 255),   // раскалённо-оранжевый
                IconSourceBuff = nameof(RoR2Content.Buffs.WarCryBuff),
            },
        };

        /// <summary>Дебафф "замедлен часовщиком" — висит на жертвах ауры.</summary>
        public static BuffDef ClockSlowDebuff { get; private set; }

        /// <summary>Материалы из игры для визуала (могут быть null, если путь сломается в патче).</summary>
        public static Material DrainTetherMaterial { get; private set; }
        public static Material AuraSphereMaterial { get; private set; }

        public static EliteEntry Get(EliteKind kind) => entries[(int)kind];
        public static BuffDef BuffOf(EliteKind kind) => entries[(int)kind].Buff;

        public static bool Is(CharacterBody body, EliteKind kind) => body && body.HasBuff(entries[(int)kind].Buff);

        /// <summary>Шаг 1 (Awake): создать и отдать R2API все определения.</summary>
        public static void Register()
        {
            foreach (var entry in entries)
            {
                string id = "Diabloid" + entry.Kind;
                var settings = DiabloidConfig.Get(entry.Kind);

                var buff = ScriptableObject.CreateInstance<BuffDef>();
                buff.name = "bd" + id;
                buff.buffColor = entry.Color;
                buff.canStack = false;
                buff.isDebuff = false;

                var equipment = ScriptableObject.CreateInstance<EquipmentDef>();
                equipment.name = id + "Aspect";
                equipment.nameToken = $"DIABLOID_ELITE_{entry.Kind}_ASPECT_NAME".ToUpperInvariant();
                equipment.pickupToken = equipment.nameToken;
                equipment.descriptionToken = equipment.nameToken;
                equipment.loreToken = "";
                equipment.passiveBuffDef = buff;
                // Аспекты наших элиток не выпадают и не попадаются в сундуках.
                equipment.canDrop = false;
                equipment.dropOnDeathChance = 0f;
                equipment.appearsInSinglePlayer = false;
                equipment.appearsInMultiPlayer = false;
                equipment.canBeRandomlyTriggered = false;
                equipment.enigmaCompatible = false;
                equipment.cooldown = 10f;
                GameLanguage.AddToken(equipment.nameToken, entry.AspectName);

                string modifierToken = $"DIABLOID_ELITE_{entry.Kind}_MODIFIER".ToUpperInvariant();
                GameLanguage.AddToken(modifierToken, entry.Modifier);

                var custom = new CustomElite(id, equipment, entry.Color, modifierToken,
                                             TierDefsFor(settings.Tier), TextureFactory.CreateEliteRamp(entry.Color));
                custom.EliteDef.healthBoostCoefficient = BoostCoefficient(settings.HealthMultiplier.Value);
                custom.EliteDef.damageBoostCoefficient = BoostCoefficient(settings.DamageMultiplier.Value);
                buff.eliteDef = custom.EliteDef; // замыкаем связку бафф ↔ элитка

                entry.Buff = buff;
                entry.Equipment = equipment;
                entry.Elite = custom.EliteDef;

                ContentAddition.AddBuffDef(buff);
                ContentAddition.AddEquipmentDef(equipment);
                EliteAPI.Add(custom);

                // Бусты читаются директором в момент спавна, поэтому их можно крутить прямо в забеге.
                var eliteDef = custom.EliteDef;
                settings.HealthMultiplier.SettingChanged += (_, __) => eliteDef.healthBoostCoefficient = BoostCoefficient(settings.HealthMultiplier.Value);
                settings.DamageMultiplier.SettingChanged += (_, __) => eliteDef.damageBoostCoefficient = BoostCoefficient(settings.DamageMultiplier.Value);
            }

            ClockSlowDebuff = ScriptableObject.CreateInstance<BuffDef>();
            ClockSlowDebuff.name = "bdDiabloidClockSlow";
            ClockSlowDebuff.buffColor = Get(EliteKind.Clockmaker).Color;
            ClockSlowDebuff.canStack = false;
            ClockSlowDebuff.isDebuff = true;
            ContentAddition.AddBuffDef(ClockSlowDebuff);
        }

        /// <summary>
        /// Директор превращает множитель в предметы BoostHp/BoostDamage: (множитель − 1) × 10 штук, +10% каждая.
        /// Множитель меньше 1 так не сделать (предметов в минус не бывает), поэтому директору отдаём минимум 1,
        /// а уменьшение здоровья (множитель меньше 1 в конфиге) делаем сами в EliteHooks через healthTotalMult.
        /// </summary>
        private static float BoostCoefficient(float multiplier) => Mathf.Max(1f, multiplier);

        /// <summary>
        /// Тиры спавна. ВАЖНО: это ленивый итератор. R2API перебирает его внутри CombatDirector.Init,
        /// когда ванильные тиры уже известны. Если бы мы вызвали GetVanillaEliteTierDef прямо в Awake —
        /// получили бы null (тиров ещё нет).
        /// Honor-версии — для артефакта Чести, иначе с ним наши элитки бы не появлялись.
        /// </summary>
        private static IEnumerable<CombatDirector.EliteTierDef> TierDefsFor(ConfigEntry<EliteTierChoice> tier)
        {
            VanillaEliteTier[] tiers;
            switch (tier.Value)
            {
                case EliteTierChoice.Tier1:
                    tiers = new[] { VanillaEliteTier.BaseTier1, VanillaEliteTier.BaseTier1Honor, VanillaEliteTier.FullTier1, VanillaEliteTier.FullTier1Honor };
                    break;
                case EliteTierChoice.Tier1Late:
                    tiers = new[] { VanillaEliteTier.FullTier1, VanillaEliteTier.FullTier1Honor };
                    break;
                case EliteTierChoice.Tier2:
                    tiers = new[] { VanillaEliteTier.Tier2 };
                    break;
                default:
                    tiers = Array.Empty<VanillaEliteTier>();
                    break;
            }

            foreach (var t in tiers)
            {
                var def = EliteAPI.GetVanillaEliteTierDef(t);
                if (def != null) yield return def;
            }
        }

        /// <summary>Шаг 2 (после загрузки игры): иконки баффов и материалы для визуала.</summary>
        public static void OnCatalogsReady()
        {
            foreach (var entry in entries)
                entry.Buff.iconSprite = FindVanillaBuffIcon(entry.IconSourceBuff);
            ClockSlowDebuff.iconSprite = FindVanillaBuffIcon(nameof(RoR2Content.Buffs.Slow50));

            DrainTetherMaterial = LoadMaterial("RoR2/Base/ClayBoss/matDrainSiphon.mat");               // жгут глиняного дюнохода
            AuraSphereMaterial = LoadMaterial("RoR2/Base/Common/matTeamAreaIndicatorFullMonster.mat"); // зона монстров
        }

        private static Sprite FindVanillaBuffIcon(string fieldName)
        {
            // RoR2Content.Buffs.X — статические поля; берём через рефлексию по имени, чтобы держать таблицу компактной.
            var field = typeof(RoR2Content.Buffs).GetField(fieldName);
            var buff = field?.GetValue(null) as BuffDef;
            return buff ? buff.iconSprite : null;
        }

        private static Material LoadMaterial(string key)
        {
            try
            {
                return Addressables.LoadAssetAsync<Material>(key).WaitForCompletion();
            }
            catch (Exception e)
            {
                DiabloidPlugin.Log.LogWarning($"Could not load material '{key}': {e.Message}");
                return null;
            }
        }
    }
}
