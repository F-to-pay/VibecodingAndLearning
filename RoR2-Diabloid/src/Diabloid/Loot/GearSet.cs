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
        /// Перк книги: пока в слоте снаряжения лежит книга, у игрока есть ещё один случайный перк.
        /// Выпадает при подборе книги (уровень — уровень монстров в тот момент), пропадает, когда книги нет.
        /// </summary>
        public Perk? BookPerk;
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
                if (BookPerk.HasValue) return false;
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
            foreach (var item in items)
            {
                if (item == null || item.Perks.Count == 0) continue;
                var current = item.Current();
                foreach (var perk in current.Perks) into.Add((perk, current.Level));
            }
            if (BookPerk.HasValue) into.Add((BookPerk.Value, BookPerkLevel));
        }

        public bool HasPerk(PerkType type)
        {
            if (BookPerk.HasValue && BookPerk.Value.Type == type) return true;
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
