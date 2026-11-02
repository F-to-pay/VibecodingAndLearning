using System;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace CharacterTest.Content
{
    /// <summary>
    /// Клон ванильного префаба — замена PrefabAPI.InstantiateClone, чтобы не тащить R2API.Prefab
    /// (его нет в профиле и в облачных refs).
    ///
    /// Как это работает:
    ///  1. Копия кладётся внутрь выключенного объекта-держателя (DontDestroyOnLoad). У самой копии
    ///     activeSelf = true, но раз родитель выключен — Awake/Start не вызываются, копия живёт как префаб.
    ///     Когда игра позже делает Instantiate(клон) без родителя, экземпляр сразу активен.
    ///  2. Сетевые префабы (тело, мастер) получают свой assetId — MD5 от GUID мода и имени клона.
    ///     Без этого у клона assetId Коммандо, и клиент в мультиплеере заспавнит Коммандо вместо нашего тела.
    ///     Регистрирует префаб в сети сама игра: ContentAddition.AddBody/AddMaster кладут его в ContentPack,
    ///     а NetworkManagerSystem регистрирует все тела/мастера из ContentManager.
    /// </summary>
    internal static class PrefabUtil
    {
        private static GameObject holder;

        // m_AssetId приватный. Публицированные GameLibs пустят к нему и напрямую, но Mono в игре
        // кинет FieldAccessException (грабля из Destroyer) — поэтому только рефлексией.
        private static readonly FieldInfo assetIdField =
            typeof(NetworkIdentity).GetField("m_AssetId", BindingFlags.Instance | BindingFlags.NonPublic);

        private static Transform Holder
        {
            get
            {
                if (!holder)
                {
                    holder = new GameObject("CharacterTest_Prefabs");
                    holder.SetActive(false);
                    Object.DontDestroyOnLoad(holder);
                    holder.hideFlags = HideFlags.HideAndDontSave;
                }
                return holder.transform;
            }
        }

        public static GameObject Clone(GameObject original, string newName, bool networked)
        {
            if (!original) throw new ArgumentNullException(nameof(original), $"Clone '{newName}': original is null");

            var clone = Object.Instantiate(original, Holder);
            clone.name = newName;

            if (networked)
            {
                var identity = clone.GetComponent<NetworkIdentity>();
                if (!identity)
                    throw new InvalidOperationException($"Clone '{newName}': networked, but no NetworkIdentity on {original.name}");
                if (assetIdField == null)
                    throw new MissingFieldException("NetworkIdentity", "m_AssetId");

                assetIdField.SetValue(identity, NetworkHash128.Parse(Md5Hex(CharacterTestPlugin.PluginGUID + ":" + newName)));
            }

            return clone;
        }

        private static string Md5Hex(string text)
        {
            using (var md5 = MD5.Create())
            {
                var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(text));
                var sb = new StringBuilder(32);
                foreach (var b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
