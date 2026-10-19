using System.Collections.Generic;
using Diabloid.Gameplay;
using Diabloid.Loot;
using R2API.Networking;
using R2API.Networking.Interfaces;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace Diabloid.Network
{
    /// <summary>
    /// Сервер → клиенты: "вот полная экипировка игрока X".
    ///
    /// Зачем вообще синхронизировать? В RoR2 статы считаются ЛОКАЛЬНО на каждой машине
    /// (CharacterBody.RecalculateStats). Скорость бега, атаки, урон пуль клиента — всё это
    /// клиент считает сам. Если он не знает про шмотки, у хоста и у клиента будут разные статы.
    /// </summary>
    internal sealed class GearSyncMessage : INetMessage
    {
        /// <summary>"Никакой слот не менялся" — просто синхронизация (новый этап, кто-то зашёл).</summary>
        private const byte NoAnnounce = byte.MaxValue;

        private NetworkInstanceId masterId;
        private GearSet gear;

        /// <summary>Какой слот только что сменился — клиенты напишут об этом в свой чат на своём языке.</summary>
        private byte announceSlot = NoAnnounce;

        // Пустой конструктор обязателен: NetworkingAPI создаёт объект сам при получении сообщения.
        public GearSyncMessage() { }

        public GearSyncMessage(NetworkInstanceId masterId, GearSet gear, GearSlot? announceSlot = null)
        {
            this.masterId = masterId;
            this.gear = gear;
            this.announceSlot = announceSlot.HasValue ? (byte)announceSlot.Value : NoAnnounce;
        }

        public void Serialize(NetworkWriter writer)
        {
            writer.Write(masterId);
            writer.Write(announceSlot);
            for (int i = 0; i < Slots.Count; i++)
            {
                var item = gear?.Get((GearSlot)i);
                writer.Write(item != null);
                if (item != null) WriteItem(writer, item);
            }

            // 0.10.0: перк книги — bool есть? | byte тип | float бросок | int уровень
            var book = gear?.BookPerk;
            writer.Write(book.HasValue);
            if (book.HasValue)
            {
                writer.Write((byte)book.Value.Type);
                writer.Write(book.Value.Roll);
                writer.Write(gear.BookPerkLevel);
            }
        }

        public void Deserialize(NetworkReader reader)
        {
            masterId = reader.ReadNetworkId();
            announceSlot = reader.ReadByte();
            gear = new GearSet();
            for (int i = 0; i < Slots.Count; i++)
            {
                if (!reader.ReadBoolean()) continue;
                var item = ReadItem(reader);
                if (item != null) gear.Equip(item);
            }

            if (reader.ReadBoolean())
            {
                byte type = reader.ReadByte();
                float roll = reader.ReadSingle();
                int level = reader.ReadInt32();
                if (type < Perks.Count)
                {
                    gear.BookPerk = new Perk((PerkType)type, roll);
                    gear.BookPerkLevel = level;
                }
            }
        }

        public void OnReceived()
        {
            // У хоста и так актуальные данные — он их и отправил.
            if (NetworkServer.active) return;

            // Что было в сменившемся слоте ДО замены — это и есть шмотка, которая упала на землю.
            bool announce = announceSlot < Slots.Count;
            GearItem previous = announce ? GearRegistry.Find(masterId)?.Get((GearSlot)announceSlot) : null;

            GearRegistry.Set(masterId, gear);

            // Если тело этого игрока уже есть — пересчитать статы прямо сейчас (ему и его турелям Инженера).
            var masterObject = ClientScene.FindLocalObject(masterId);
            var master = masterObject ? masterObject.GetComponent<CharacterMaster>() : null;
            GearRegistry.MarkStatsDirty(master);

            if (announce)
                GearAnnouncer.Announce(master, gear.Get((GearSlot)announceSlot), previous);
        }

        // --- формат сообщения ---
        // netId мастера | byte announceSlot | 6 × (bool есть? | шмотка)
        // --- формат одной шмотки на проводе ---
        // byte slot | byte quality | int level | byte baseIndex | affix implicit | byte n | affix × n | byte p | perk × p
        // affix = byte stat | float value;  perk = byte type | float roll (0.6.0+)

        private static void WriteItem(NetworkWriter writer, GearItem item)
        {
            writer.Write((byte)item.Slot);
            writer.Write((byte)item.Quality);
            writer.Write(item.Level);
            writer.Write(item.BaseIndex);
            WriteAffix(writer, item.Implicit);
            writer.Write((byte)item.Affixes.Count);
            foreach (var affix in item.Affixes) WriteAffix(writer, affix);
            writer.Write((byte)item.Perks.Count);
            foreach (var perk in item.Perks)
            {
                writer.Write((byte)perk.Type);
                writer.Write(perk.Roll);
            }
        }

        private static GearItem ReadItem(NetworkReader reader)
        {
            // Сначала читаем ВСЁ (чтобы не сбить поток байтов), потом проверяем.
            byte slot = reader.ReadByte();
            byte quality = reader.ReadByte();
            int level = reader.ReadInt32();
            byte baseIndex = reader.ReadByte();
            var implicitAffix = ReadAffix(reader, out bool ok);
            int count = reader.ReadByte();
            var affixes = new List<Affix>(count);
            for (int i = 0; i < count; i++)
            {
                affixes.Add(ReadAffix(reader, out bool affixOk));
                ok &= affixOk;
            }

            int perkCount = reader.ReadByte();
            var perks = new List<Perk>(perkCount);
            for (int i = 0; i < perkCount; i++)
            {
                byte type = reader.ReadByte();
                float roll = reader.ReadSingle();
                ok &= type < Perks.Count;
                perks.Add(new Perk((PerkType)type, roll));
            }

            ok &= slot < Slots.Count && quality < Qualities.Count;
            if (!ok)
            {
                DiabloidPlugin.Log.LogWarning("GearSyncMessage: received a malformed item (different mod versions on host and client?), skipping.");
                return null;
            }

            return new GearItem((GearSlot)slot, (GearQuality)quality, level, baseIndex, implicitAffix, affixes, perks);
        }

        private static void WriteAffix(NetworkWriter writer, Affix affix)
        {
            writer.Write((byte)affix.Stat);
            writer.Write(affix.Value);
        }

        private static Affix ReadAffix(NetworkReader reader, out bool ok)
        {
            byte stat = reader.ReadByte();
            float value = reader.ReadSingle();
            ok = stat < Stats.Count;
            return new Affix(ok ? (StatType)stat : default, value);
        }
    }

    internal static class GearNetwork
    {
        public static void Init()
        {
            NetworkingAPI.RegisterMessageType<GearSyncMessage>();
        }

        /// <param name="announceSlot">Слот, который только что сменился (клиенты напишут об этом в чат), или null.</param>
        public static void Broadcast(CharacterMaster master, GearSet gear, GearSlot? announceSlot = null)
        {
            if (!NetworkServer.active || !master || gear == null) return;
            new GearSyncMessage(master.netId, gear, announceSlot).Send(NetworkDestination.Clients);
        }

        public static void BroadcastAll()
        {
            if (!NetworkServer.active) return;
            foreach (var pair in GearRegistry.All)
                new GearSyncMessage(pair.Key, pair.Value).Send(NetworkDestination.Clients);
        }
    }
}
