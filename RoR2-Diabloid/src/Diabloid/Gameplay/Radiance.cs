using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Diabloid.Config;
using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace Diabloid.Gameplay
{
    /// <summary>
    /// Сияние (0.19.0) — налог на силу шмота. Каждая надетая шмотка даёт уровни сияния по качеству
    /// (обычная 1, необычная 2, редкая 3, легендарка 5, пустотная 4, лунная 0; книга 1, свиток 2 — GearSet.Radiance).
    /// Действующее сияние — среднее по игрокам. Каждый уровень даёт врагам (команды монстров, Пустоты, Луны)
    /// +4% (0.20.1; было 2%) здоровья, урона, брони и регена (TotalMult — поверх всего) и +3% к скорости спавна (кредиты директоров).
    /// Пересчёт сразу при смене шмота: опрос 4 раза в секунду, изменилось — пересчитать статы всем врагам.
    /// Статы считаются на каждой машине сами (шмот клиентам синхронизирован), спавн — только сервер.
    /// 0.19.1: спавн — не хуком (DirectorMoneyWave — приватный вложенный тип, хук MMHOOK к нему недоступен:
    /// MethodAccessException в Awake, мод падал целиком), а рефлексией: множитель каждой волны кредитов
    /// = её исходный множитель × (1 + бонус спавна). Исходный запоминаем при первом касании.
    /// </summary>
    internal sealed class RadianceManager : MonoBehaviour
    {
        private const float PollInterval = 0.25f;

        /// <summary>Действующее сияние: среднее по игрокам (на этой машине).</summary>
        public static float Current { get; private set; }

        /// <summary>Прибавка к статам врагов: 0.34 = +34%.</summary>
        public static float EnemyStatBonus => Current * DiabloidConfig.RadianceEnemyStats.Value / 100f;

        /// <summary>Прибавка к скорости спавна: 0.51 = +51% кредитов директорам.</summary>
        public static float SpawnBonus => Current * DiabloidConfig.RadianceSpawnRate.Value / 100f;

        private float timer;

        private sealed class BaseMultiplier { public float Value; }
        private static readonly ConditionalWeakTable<object, BaseMultiplier> waveBase = new ConditionalWeakTable<object, BaseMultiplier>();
        private static FieldInfo moneyWavesField, waveMultiplierField;
        private static bool spawnReflectionReady, spawnReflectionFailed;

        public static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += OnGetStatCoefficients;
            Run.onRunDestroyGlobal += _ => Current = 0f;
        }

        /// <summary>Сияние одного игрока (для панели).</summary>
        public static int RadianceOf(CharacterMaster master)
        {
            if (!master) return 0;
            var gear = GearRegistry.Find(master);
            return gear != null ? gear.Radiance() : 0;
        }

        private static float Compute()
        {
            if (!Run.instance) return 0f;
            int players = 0, sum = 0;
            foreach (var player in PlayerCharacterMasterController.instances)
            {
                if (!player || !player.master) continue;
                players++;
                sum += RadianceOf(player.master);
            }
            return players > 0 ? (float)sum / players : 0f;
        }

        private void FixedUpdate()
        {
            timer -= Time.fixedDeltaTime;
            if (timer > 0f) return;
            timer = PollInterval;

            float value = Compute();
            ApplySpawnRate(); // каждый опрос: новые директоры появляются со сменой этапа и по ходу
            if (Mathf.Approximately(value, Current)) return;
            float oldMult = 1f + EnemyStatBonus;
            Current = value;
            float newMult = 1f + EnemyStatBonus;

            // 0.19.3: пересчёт прямо сейчас, а не «в следующем кадре», и проверка на живых: до трёх врагов (сначала боссы)
            // пишем в лог макс. здоровье до и после — видно, применилось ли сияние на самом деле.
            int enemies = 0, sampled = 0;
            var samples = new System.Text.StringBuilder();
            foreach (var pass in new[] { true, false })
                foreach (var body in CharacterBody.readOnlyInstancesList)
                {
                    if (!IsEnemy(body) || (body.isChampion || body.isBoss) != pass) continue;
                    float before = body.maxHealth;
                    body.RecalculateStats();
                    enemies++;
                    if (sampled < 3)
                    {
                        sampled++;
                        samples.Append($" {body.name.Replace("(Clone)", "")} {before:0}→{body.maxHealth:0} (expected ×{newMult / oldMult:0.###});");
                    }
                }
            DiabloidPlugin.Log.LogInfo($"Radiance: {Current:0.#} → enemies +{EnemyStatBonus * 100f:0}% health/damage/armor/regen, " +
                                       $"spawn +{SpawnBonus * 100f:0}%; recalculated {enemies} enemies.{samples}");
        }

        /// <summary>
        /// Сервер: директор копит кредиты волнами (CombatDirector.moneyWaves, приватный массив приватного типа);
        /// прибавка волны линейна по её multiplier — его и ставим = исходный × (1 + бонус спавна).
        /// </summary>
        private static void ApplySpawnRate()
        {
            if (!NetworkServer.active || spawnReflectionFailed) return;
            if (!spawnReflectionReady)
            {
                const BindingFlags any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                moneyWavesField = typeof(CombatDirector).GetField("moneyWaves", any);
                var waveType = moneyWavesField != null ? moneyWavesField.FieldType.GetElementType() : null;
                waveMultiplierField = waveType != null ? waveType.GetField("multiplier", any) : null;
                if (moneyWavesField == null || waveMultiplierField == null || waveMultiplierField.FieldType != typeof(float))
                {
                    spawnReflectionFailed = true;
                    DiabloidPlugin.Log.LogWarning("Radiance: CombatDirector.moneyWaves/multiplier not found, spawn rate bonus is off.");
                    return;
                }
                spawnReflectionReady = true;
                DiabloidPlugin.Log.LogInfo("Radiance: spawn rate hook (money waves) ready.");
            }

            float factor = 1f + SpawnBonus;
            foreach (var director in CombatDirector.instancesList)
            {
                if (!director) continue;
                if (!(moneyWavesField.GetValue(director) is Array waves)) continue;
                foreach (var wave in waves)
                {
                    if (wave == null) continue;
                    var baseValue = waveBase.GetValue(wave, w => new BaseMultiplier { Value = (float)waveMultiplierField.GetValue(w) });
                    waveMultiplierField.SetValue(wave, baseValue.Value * factor);
                }
            }
        }

        private static bool IsEnemy(CharacterBody body)
        {
            if (!body || !body.teamComponent) return false;
            var team = body.teamComponent.teamIndex;
            return team == TeamIndex.Monster || team == TeamIndex.Void || team == TeamIndex.Lunar;
        }

        private static void OnGetStatCoefficients(CharacterBody body, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (Current <= 0f || !IsEnemy(body)) return;
            float mult = 1f + EnemyStatBonus;
            args.healthTotalMult *= mult;
            args.damageTotalMult *= mult;
            args.armorTotalMult *= mult;
            args.regenTotalMult *= mult;
        }
    }
}
