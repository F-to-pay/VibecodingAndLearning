using System.Collections.Generic;
using Diabloid.Loot;
using RoR2;
using UnityEngine.Networking;

namespace Diabloid.Gameplay
{
    /// <summary>
    /// Кто что носит. Ключ — netId мастера игрока (CharacterMaster живёт весь забег,
    /// в отличие от тела, которое пересоздаётся на каждом этапе и после смерти).
    ///
    /// На сервере это источник правды. На клиентах — копия, которую присылает GearSyncMessage.
    /// </summary>
    internal static class GearRegistry
    {
        private static readonly Dictionary<NetworkInstanceId, GearSet> sets = new Dictionary<NetworkInstanceId, GearSet>();

        public static IEnumerable<KeyValuePair<NetworkInstanceId, GearSet>> All => sets;

        public static GearSet Find(CharacterMaster master)
        {
            if (!master) return null;
            return sets.TryGetValue(master.netId, out var set) ? set : null;
        }

        public static GearSet Find(NetworkInstanceId masterId) => sets.TryGetValue(masterId, out var set) ? set : null;

        // ---------------------------------------------------------------- турели Инженера

        // Турели Инженера носят шмотки хозяина, как в ванилле носят его предметы.
        // Другие миньоны (дроны, призраки, наши огоньки/големы) — нет.
        private static readonly string[] InheritingMasterNames = { "EngiTurretMaster", "EngiWalkerTurretMaster" };
        private static MasterCatalog.MasterIndex[] inheritingMasters;

        /// <summary>Эта шмотка-носитель — турель Инженера?</summary>
        public static bool InheritsGear(CharacterMaster master)
        {
            if (!master) return false;
            if (inheritingMasters == null)
            {
                var found = new List<MasterCatalog.MasterIndex>();
                foreach (var name in InheritingMasterNames)
                {
                    var index = MasterCatalog.FindMasterIndex(name);
                    if (index != MasterCatalog.MasterIndex.none) found.Add(index);
                }
                if (found.Count == 0) return false; // каталог ещё не готов — спросим в следующий раз
                if (found.Count < InheritingMasterNames.Length)
                    DiabloidPlugin.Log.LogWarning("Engineer turrets: one of the turret masters was not found, those turrets won't wear gear.");
                inheritingMasters = found.ToArray();
            }
            return System.Array.IndexOf(inheritingMasters, master.masterIndex) >= 0;
        }

        private static CharacterMaster OwnerOf(CharacterMaster master)
        {
            var ownership = master ? master.minionOwnership : null;
            return ownership ? ownership.ownerMaster : null;
        }

        /// <summary>
        /// Что носит этот мастер с точки зрения статов и перков: своё, а у турели Инженера — шмотки хозяина.
        /// Хозяина турели знает и клиент (MinionOwnership синхронизирует игра), экипировку хозяина — тоже.
        /// </summary>
        public static GearSet FindWorn(CharacterMaster master)
        {
            var own = Find(master);
            if (own != null) return own;
            return InheritsGear(master) ? Find(OwnerOf(master)) : null;
        }

        /// <summary>Все тела, которые носят шмотки: игроки и их турели. isTurret — чтобы не давать турелям лишнего.</summary>
        public static IEnumerable<(CharacterMaster master, CharacterBody body, bool isTurret)> Wearers()
        {
            foreach (var player in PlayerCharacterMasterController.instances)
            {
                var master = player ? player.master : null;
                var body = master ? master.GetBody() : null;
                if (body) yield return (master, body, false);
            }
            foreach (var master in CharacterMaster.readOnlyInstancesList)
            {
                if (!InheritsGear(master)) continue;
                var body = master.GetBody();
                if (body && Find(OwnerOf(master)) != null) yield return (master, body, true);
            }
        }

        /// <summary>Экипировка игрока сменилась — пересчитать статы ему и его турелям.</summary>
        public static void MarkStatsDirty(CharacterMaster owner)
        {
            if (!owner) return;
            var body = owner.GetBody();
            if (body) body.MarkAllStatsDirty();
            foreach (var master in CharacterMaster.readOnlyInstancesList)
            {
                if (!InheritsGear(master) || OwnerOf(master) != owner) continue;
                var turret = master.GetBody();
                if (turret) turret.MarkAllStatsDirty();
            }
        }

        public static GearSet GetOrCreate(CharacterMaster master)
        {
            if (!sets.TryGetValue(master.netId, out var set))
            {
                set = new GearSet();
                sets[master.netId] = set;
            }
            return set;
        }

        public static void Set(NetworkInstanceId masterId, GearSet set) => sets[masterId] = set;

        public static void Clear() => sets.Clear();
    }
}
