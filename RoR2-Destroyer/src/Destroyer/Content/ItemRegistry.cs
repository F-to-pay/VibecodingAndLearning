using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using BepInEx.Configuration;
using Destroyer.Items;

namespace Destroyer.Content
{
    /// <summary>Все предметы мода: регистрация в Awake и донастройка после загрузки каталогов.</summary>
    internal static class ItemRegistry
    {
        private static readonly List<ItemBase> registered = new List<ItemBase>();
        private static readonly Regex ValidId = new Regex("^[A-Z][A-Z0-9_]*$");

        /// <summary>Включённые и добавленные в игру предметы, в порядке ItemList.</summary>
        public static IReadOnlyList<ItemBase> All => registered;

        /// <summary>Каталоги загружены, PickupIndex'ы известны.</summary>
        public static bool Ready { get; private set; }

        /// <summary>Шаг 1 (Awake): настройки, ItemDef, тексты, хуки.</summary>
        public static void Register(ConfigFile cfg)
        {
            var seen = new HashSet<string>();
            foreach (var item in ItemList.Create())
            {
                if (item == null) continue;
                if (item.Id == null || !ValidId.IsMatch(item.Id))
                {
                    DestroyerPlugin.Log.LogError($"Item {item.GetType().Name}: bad Id '{item.Id}' (need A-Z, 0-9, _), skipped.");
                    continue;
                }
                if (!seen.Add(item.Id))
                {
                    DestroyerPlugin.Log.LogError($"Item {item.GetType().Name}: duplicate Id '{item.Id}', skipped.");
                    continue;
                }

                // Один сломанный предмет не должен ронять весь мод.
                try
                {
                    item.Register(cfg);
                    if (item.IsRegistered) registered.Add(item);
                }
                catch (Exception e)
                {
                    DestroyerPlugin.Log.LogError($"Item {item.Id} failed to register: {e}");
                    // Упал уже после ItemAPI.Add (например, в Hook) — предмет в игре, пусть хотя бы получит модельку.
                    if (item.IsRegistered) registered.Add(item);
                }
            }
            DestroyerPlugin.Log.LogInfo($"Registered {registered.Count} item(s).");

            foreach (var item in registered)
                if (!string.IsNullOrEmpty(item.Corrupts) || item.CorruptsAll != null)
                {
                    On.RoR2.ItemCatalog.SetItemRelationships += AddContagiousPairs;
                    break;
                }
        }

        /// <summary>
        /// Пары "пустотный ← обычный" для ванильного заражения (ContagiousItemManager читает их через
        /// ItemCatalog.GetItemPairsForRelationship(DLC1Content.ItemRelationshipTypes.ContagiousItem), IL 1.5).
        /// Добавляем свой ItemRelationshipProvider в момент, когда каталог собирает связи: предметы уже с индексами.
        /// </summary>
        private static void AddContagiousPairs(On.RoR2.ItemCatalog.orig_SetItemRelationships orig, RoR2.ItemRelationshipProvider[] providers)
        {
            var type = RoR2.DLC1Content.ItemRelationshipTypes.ContagiousItem;
            var pairs = new List<RoR2.ItemDef.Pair>();
            if (type)
            {
                foreach (var item in registered)
                {
                    if (item.CorruptsAll != null && item.Def != null)
                    {
                        int before = pairs.Count;
                        foreach (var def in RoR2.ItemCatalog.allItemDefs)
                        {
                            if (!def || def == item.Def || def.hidden) continue;
                            bool match;
                            try { match = item.CorruptsAll(def); }
                            catch (System.Exception e) { DestroyerPlugin.Log.LogError($"Item {item.Id}: corruption filter failed on {def.name}: {e.Message}"); match = false; }
                            if (match) pairs.Add(new RoR2.ItemDef.Pair { itemDef1 = def, itemDef2 = item.Def });
                        }
                        DestroyerPlugin.Log.LogInfo($"Item {item.Id}: corrupts {pairs.Count - before} item(s).");
                    }
                    if (string.IsNullOrEmpty(item.Corrupts) || item.Def == null) continue;
                    var ours = Find(item.Corrupts);
                    RoR2.ItemDef source = ours != null ? ours.Def : null;
                    if (source == null)
                    {
                        var index = RoR2.ItemCatalog.FindItemIndex(item.Corrupts);
                        if (index != RoR2.ItemIndex.None) source = RoR2.ItemCatalog.GetItemDef(index);
                    }
                    if (source == null)
                    {
                        DestroyerPlugin.Log.LogWarning($"Item {item.Id}: corrupted item '{item.Corrupts}' not found.");
                        continue;
                    }
                    pairs.Add(new RoR2.ItemDef.Pair { itemDef1 = source, itemDef2 = item.Def });
                }
            }
            else DestroyerPlugin.Log.LogWarning("DLC1 ContagiousItem relationship type not found, void items will not corrupt.");

            if (pairs.Count > 0)
            {
                var provider = UnityEngine.ScriptableObject.CreateInstance<RoR2.ItemRelationshipProvider>();
                provider.name = "DestroyerContagiousItems";
                provider.relationshipType = type;
                provider.relationships = pairs.ToArray();
                var list = new List<RoR2.ItemRelationshipProvider>(providers ?? new RoR2.ItemRelationshipProvider[0]) { provider };
                providers = list.ToArray();
                DestroyerPlugin.Log.LogInfo($"Added {pairs.Count} void corruption pair(s).");
            }
            orig(providers);
        }

        /// <summary>Шаг 2 (RoR2Application.onLoad): PickupIndex, модельки, OnCatalogsReady предметов.</summary>
        public static void OnCatalogsReady()
        {
            foreach (var item in registered)
            {
                try
                {
                    item.CatalogsReady();
                }
                catch (Exception e)
                {
                    DestroyerPlugin.Log.LogError($"Item {item.Id} failed after catalogs load: {e}");
                }
            }
            Ready = true;
        }

        public static ItemBase Find(string id)
        {
            foreach (var item in registered)
                if (item.Id == id) return item;
            return null;
        }
    }
}
