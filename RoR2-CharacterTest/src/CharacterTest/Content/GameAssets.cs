using System;
using System.Collections.Generic;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace CharacterTest.Content
{
    /// <summary>
    /// Ванильные ассеты. Основной путь — старые пути LegacyResourcesAPI: они перечислены в самом
    /// LegacyResourcesAPI.dll игры 1.5, значит точно есть. Не нашлось — пробуем ключ Addressables.
    /// Всё кешируется. Ошибка = null + одна строка в лог.
    /// </summary>
    internal static class GameAssets
    {
        // --- Коммандо: донор тела, витрины и ИИ для тестового выжившего ---
        public static readonly AssetRef CommandoBody = new AssetRef(
            "Prefabs/CharacterBodies/CommandoBody", "RoR2/Base/Commando/CommandoBody.prefab");
        public static readonly AssetRef CommandoDisplay = new AssetRef(
            "Prefabs/CharacterDisplays/CommandoDisplay", "RoR2/Base/Commando/CommandoDisplay.prefab");
        public static readonly AssetRef CommandoMonsterMaster = new AssetRef(
            "Prefabs/CharacterMasters/CommandoMonsterMaster", "RoR2/Base/Commando/CommandoMonsterMaster.prefab");

        // --- Снаряды ---
        /// <summary>Фраг-граната Коммандо. Донор гранат подствольника.</summary>
        public static readonly AssetRef CommandoGrenadeProjectile = new AssetRef(
            "Prefabs/Projectiles/CommandoGrenadeProjectile", "RoR2/Base/Commando/CommandoGrenadeProjectile.prefab");

        // --- Эффекты ---
        /// <summary>Взрыв Brilliant Behemoth. EffectData.scale = радиус. Зарегистрирован в EffectCatalog.</summary>
        public static readonly AssetRef BehemothExplosion = new AssetRef(
            "Prefabs/Effects/OmniEffect/OmniExplosionVFXQuick", "RoR2/Base/Common/VFX/OmniExplosionVFXQuick.prefab");

        private static readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();

        public static GameObject Load(AssetRef asset)
        {
            if (cache.TryGetValue(asset.Legacy, out var cached)) return cached;

            GameObject result = null;
            try
            {
                result = LegacyResourcesAPI.Load<GameObject>(asset.Legacy);
            }
            catch (Exception e)
            {
                CharacterTestPlugin.Log.LogWarning($"LegacyResourcesAPI '{asset.Legacy}': {e.Message}");
            }

            if (!result && !string.IsNullOrEmpty(asset.Address))
            {
                try
                {
                    result = Addressables.LoadAssetAsync<GameObject>(asset.Address).WaitForCompletion();
                }
                catch (Exception e)
                {
                    CharacterTestPlugin.Log.LogWarning($"Addressables '{asset.Address}': {e.Message}");
                }
            }

            if (!result) CharacterTestPlugin.Log.LogError($"Asset '{asset.Legacy}' not found (game update?).");
            cache[asset.Legacy] = result;
            return result;
        }

        /// <summary>Эффект на всех машинах. Нет префаба — тихо ничего.</summary>
        public static void SpawnEffect(AssetRef asset, Vector3 position, float scale, bool transmit = true)
        {
            var prefab = Load(asset);
            if (!prefab) return;
            EffectManager.SpawnEffect(prefab, new EffectData { origin = position, scale = scale }, transmit);
        }
    }

    /// <summary>Ссылка на ванильный ассет: старый путь + ключ Addressables на запас.</summary>
    public readonly struct AssetRef
    {
        public readonly string Legacy;
        public readonly string Address;

        public AssetRef(string legacy, string address)
        {
            Legacy = legacy;
            Address = address;
        }

        public override string ToString() => Legacy;
    }
}
