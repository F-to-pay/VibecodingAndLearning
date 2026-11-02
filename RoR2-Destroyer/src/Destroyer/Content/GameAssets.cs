using System;
using System.Collections.Generic;
using RoR2;
using UnityEngine;

namespace Destroyer.Content
{
    /// <summary>
    /// Ванильные префабы по старым путям (LegacyResourcesAPI). Только пути, которые сама игра 1.5 грузит в коде —
    /// значит, они точно есть и эффекты зарегистрированы в EffectCatalog. Рядом — кто их использует.
    /// Грузим лениво, при первом использовании (в забеге), и кешируем. Не нашлось — null и одна ошибка в лог.
    /// </summary>
    internal static class GameAssets
    {
        /// <summary>Взрыв Brilliant Behemoth (GlobalEventManager.OnHitAllProcess). EffectData.scale = радиус.</summary>
        public const string BehemothExplosion = "Prefabs/Effects/OmniEffect/OmniExplosionVFXQuick";

        /// <summary>Кровавый всплеск Shatterspleen (GlobalEventManager.CommonAssets).</summary>
        public const string BloodBurst = "Prefabs/Effects/ImpactEffects/BleedOnHitAndExplode_Impact";

        /// <summary>Метка "сквозь стены" Радара (ChestRevealer.RevealedObject).</summary>
        public const string PoiIndicator = "Prefabs/PositionIndicators/PoiPositionIndicator";

        /// <summary>Трассер-дымка (GhostGunController.Fire). Звуковая пушка — след волны.</summary>
        public const string SmokeTracer = "Prefabs/Effects/Tracers/TracerSmokeChase";

        /// <summary>Удар с отбрасыванием (Breaching Fin, KnockBackHitEnemies). Звуковая пушка — попадание.</summary>
        public const string KnockbackImpact = "Prefabs/Effects/ImpactEffects/KnockBackHitEnemiesImpact";

        /// <summary>Ударная волна приземления H3AD-5T (scale = радиус). Звуковая пушка — выстрел.</summary>
        public const string BootShockwave = "Prefabs/Effects/ImpactEffects/BootShockwave";

        /// <summary>Пускатель Bundle of Fireworks (GlobalEventManager.OnInteractionBegin). Запал берёт с него снаряд и темп.</summary>
        public const string FireworkLauncher = "Prefabs/FireworkLauncher";


        /// <summary>Обычная искра попадания (Prefabs/Effects/ImpactEffects/Hitspark1). Бензопила — тик.</summary>
        public const string Hitspark = "Prefabs/Effects/ImpactEffects/Hitspark1";

        /// <summary>Ледяной удар Glacial-элиты (Prefabs/Effects/ImpactEffects/AffixWhiteImpactEffect). Холодные объятья.</summary>
        public const string FrostImpact = "Prefabs/Effects/ImpactEffects/AffixWhiteImpactEffect";

        private static readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();

        public static GameObject Load(string path)
        {
            if (cache.TryGetValue(path, out var cached)) return cached;
            GameObject result = null;
            try
            {
                result = LegacyResourcesAPI.Load<GameObject>(path);
            }
            catch (Exception e)
            {
                DestroyerPlugin.Log.LogError($"Failed to load '{path}': {e.Message}");
            }
            if (!result) DestroyerPlugin.Log.LogError($"Asset '{path}' not found (game update?).");
            cache[path] = result;
            return result;
        }

        /// <summary>Эффект на всех машинах (зовётся на сервере). Нет префаба — тихо ничего.</summary>
        public static void SpawnEffect(string path, Vector3 position, float scale)
        {
            var prefab = Load(path);
            if (!prefab) return;
            EffectManager.SpawnEffect(prefab, new EffectData { origin = position, scale = scale }, transmit: true);
        }
    }
}
