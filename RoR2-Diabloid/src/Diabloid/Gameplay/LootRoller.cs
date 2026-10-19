using System;
using Diabloid.Config;
using Diabloid.Loot;
using RoR2;

namespace Diabloid.Gameplay
{
    /// <summary>Мостик между генератором (чистый C#) и игрой: серверный рандом + уровень + конфиг.</summary>
    internal static class LootRoller
    {
        // Только сервер роллит лут, так что один общий Random достаточно.
        public static readonly Random Rng = new Random();

        /// <summary>Уровень предмета = текущий уровень монстров. Чем дольше забег, тем жирнее шмот.</summary>
        public static int CurrentItemLevel()
        {
            var run = Run.instance;
            return run ? Math.Max(1, run.ambientLevelFloor) : 1;
        }

        public static GearItem Generate(GearSlot slot, GearQuality quality)
        {
            return LootGenerator.Generate(slot, quality, CurrentItemLevel(), Rng, DiabloidConfig.ToLootSettings());
        }
    }
}
