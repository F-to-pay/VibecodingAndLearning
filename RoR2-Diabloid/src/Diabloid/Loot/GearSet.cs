using System;
using System.Collections.Generic;

namespace Diabloid.Loot
{
    /// <summary>
    /// Надетая экипировка одного игрока: 6 слотов, в каждом максимум одна шмотка.
    /// Именно здесь живёт правило "не бывает двух шлемов".
    /// </summary>
    public sealed class GearSet
    {
        private readonly GearItem[] items = new GearItem[Slots.Count];

        /// <summary>
        /// Перки снаряжения: пока в слоте лежит книга — её постоянный перк (0.15.0; бросок — при подборе),
        /// свиток — 3 разных случайных. Уровень — уровень монстров при подборе; нет книги — список пуст.
        /// </summary>
        public readonly List<Perk> BookPerks = new List<Perk>();
        public int BookPerkLevel = 1;

        public GearItem Get(GearSlot slot) => items[(int)slot];

        /// <summary>Надеть шмотку в её слот. Возвращает то, что было надето раньше (или null).</summary>
        public GearItem Equip(GearItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            int index = (int)item.Slot;
            var previous = items[index];
            items[index] = item;
            return previous;
        }

        public bool IsEmpty
        {
            get
            {
                if (BookPerks.Count > 0) return false;
                foreach (var item in items)
                    if (item != null) return false;
                return true;
            }
        }

        /// <summary>Надета ли хоть одна шмотка, растущая с уровнем монстров (пустотная).</summary>
        public bool HasLevelScaling
        {
            get
            {
                foreach (var item in items)
                    if (item != null && item.ScalesWithMonsterLevel) return true;
                return false;
            }
        }

        /// <summary>
        /// Складывает статы всех надетых шмоток в массив totals (индекс == (int)StatType).
        /// Массив не очищается — так вызывающий может переиспользовать буфер без аллокаций.
        /// Проклятия лунных шмоток идут со своим минусом.
        /// </summary>
        public void AccumulateTotals(float[] totals)
        {
            foreach (var item in items)
            {
                if (item == null) continue;
                foreach (var affix in item.Current().AllAffixes)
                    totals[(int)affix.Stat] += affix.Value;
            }
        }

        /// <summary>
        /// Все перки надетых шмоток вместе с уровнем шмотки (у пустотных — текущим). Одинаковые перки
        /// с разных шмоток работают по отдельности, поэтому список, а не сумма. Буфер не очищается.
        /// </summary>
        public void CollectPerks(List<(Perk perk, int level)> into)
        {
            Perk? banana = null;
            int bananaLevel = 1, bananas = 0;
            foreach (var item in items)
            {
                if (item == null || item.Perks.Count == 0) continue;
                var current = item.Current();
                foreach (var perk in current.Perks)
                {
                    into.Add((perk, current.Level));
                    if (perk.Type != PerkType.Banana) continue;
                    bananas++;
                    if (banana == null) { banana = perk; bananaLevel = current.Level; }
                }
            }
            foreach (var perk in BookPerks) into.Add((perk, BookPerkLevel));

            // Банан (0.18.0): все остальные перки разом, с броском и уровнем первого банана. 0.18.1: только от трёх
            // бананов (меньше — ничего), и сколько бы их ни было сверху — набор один. Совпавшие с настоящими перками
            // работают вдобавок к ним (как два одинаковых перка).
            if (banana != null && bananas >= Perks.BananasRequired)
                foreach (var type in Perks.Rollable())
                    if (Perks.GrantedByBanana(type)) into.Add((new Perk(type, banana.Value.Roll), bananaLevel));
        }

        /// <summary>Сияние книги и свитка в слоте снаряжения (0.19.0).</summary>
        public const int BookRadiance = 1, ScrollRadiance = 2;

        /// <summary>
        /// Сияние (0.19.0): сумма по надетым шмоткам (по качеству, от уровня не зависит) + книга/свиток.
        /// Что оно делает — Gameplay/Radiance.cs.
        /// </summary>
        public int Radiance()
        {
            int sum = 0;
            foreach (var item in items)
                if (item != null) sum += Qualities.Get(item.Quality).Radiance;
            if (BookPerks.Count > 1) sum += ScrollRadiance;
            else if (BookPerks.Count == 1) sum += BookRadiance;
            return sum;
        }

        /// <summary>Сколько надето шмоток с бананом.</summary>
        public int BananaCount()
        {
            int count = 0;
            foreach (var item in items)
            {
                if (item == null) continue;
                foreach (var perk in item.Perks)
                    if (perk.Type == PerkType.Banana) count++;
            }
            return count;
        }

        /// <summary>Бананов хватает (3+) — банан работает.</summary>
        public bool BananaUnlocked => BananaCount() >= Perks.BananasRequired;

        public bool HasPerk(PerkType type)
        {
            if (Perks.GrantedByBanana(type) && BananaUnlocked) return true;
            foreach (var perk in BookPerks)
                if (perk.Type == type) return true;
            foreach (var item in items)
            {
                if (item == null) continue;
                foreach (var perk in item.Perks)
                    if (perk.Type == type) return true;
            }
            return false;
        }

        public float GetTotal(StatType stat)
        {
            float sum = 0f;
            foreach (var item in items)
            {
                if (item == null) continue;
                foreach (var affix in item.Current().AllAffixes)
                    if (affix.Stat == stat) sum += affix.Value;
            }
            return sum;
        }
    }
}
