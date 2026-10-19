using System;
using System.Globalization;

namespace Diabloid.Loot
{
    /// <summary>
    /// Перки — особые свойства шмоток (с редкого качества и выше). Значение уходит по сети байтом:
    /// НОВЫЕ перки только в конец.
    /// </summary>
    public enum PerkType : byte
    {
        DamageAura,       // аура: урон всем врагам в радиусе, чем шире — тем слабее
        ArmorOnHurt,      // накопил X% макс. ХП полученного урона → броня X% макс. ХП / 10 на 5 с
        PercentHpStrike,  // шанс нанести цели % её макс. ХП, чем чаще — тем слабее
        Berserk,          // +базовый урон за каждые 70 недостающего здоровья
        MissingHpRegen,   // регенерация 1% недостающего здоровья в секунду
        // --- 0.8.0 ---
        HealthArmor,      // +броня 2% макс. ХП; каждая такая броня даёт 0.1 базового урона
        WispOnKill,       // убийство: 15% шанс призвать союзный огонёк на 120 с
        GolemSummon,      // раз в 30 с — союзный мини-голем на 60 с (скорость ×2, урон ×0.3, размер ×0.5)
        SprintWarcry,     // пока бежишь: +20% базового урона себе и союзникам в 30 м
        ExtraHit,         // 100%: дополнительный удар в 10% урона, proc 0.1
        // --- 0.10.0 ---
        XpBonus,          // +10% получаемого опыта
        ExplodeOnHit,     // 10% (× proc): взрыв 200% урона в 20 м, proc 0.2
        SprintMomentum,   // бег: каждую секунду +1% × уровень шмотки к скорости бега, до 5 раз
        HurtWispSwarm,    // получив урон: 10% — 10 огоньков на 10 с (здоровье ×10, урон ×0.2)
        StandStillSolus,  // 10 с стоя на месте — элитный зонд Солус навсегда + корни всем врагам на 5 с, КД 3 мин
    }

    /// <summary>
    /// Один перк на шмотке: тип + бросок t в [0; 1]. Все цифры выводятся из t, так что по сети хватает одного float.
    /// Связь "обратно пропорционально" — это когда одна величина растёт с t, а другая падает.
    /// </summary>
    public readonly struct Perk
    {
        public readonly PerkType Type;
        public readonly float Roll;

        public Perk(PerkType type, float roll)
        {
            Type = type;
            Roll = roll < 0f ? 0f : roll > 1f ? 1f : roll;
        }

        // --- Аура урона: радиус 10..50 м, урон 40..8% базового в секунду ---
        public float AuraRadius => Lerp(Perks.AuraRadiusMin, Perks.AuraRadiusMax, Roll);
        public float AuraDamagePercent => Lerp(Perks.AuraDamageMax, Perks.AuraDamageMin, Roll);

        // --- Броня от полученного урона: порог = сила = 10..50% макс. ХП ---
        public float ArmorThresholdPercent => Lerp(Perks.ArmorPercentMin, Perks.ArmorPercentMax, Roll);

        // --- Удар по % ХП: шанс 8..40%, урон 5..1% макс. ХП цели ---
        public float StrikeChance => Lerp(Perks.StrikeChanceMin, Perks.StrikeChanceMax, Roll);
        public float StrikeHpPercent => Lerp(Perks.StrikeHpMax, Perks.StrikeHpMin, Roll);

        /// <summary>Берсерк: базового урона за каждые 70 недостающего ХП — 1 × рост от уровня шмотки (как у стата базового урона).</summary>
        /// <summary>Разгон: % скорости бега за стак = 1 × уровень шмотки.</summary>
        public static float MomentumPerStack(int itemLevel) => Perks.MomentumPercentPerLevel * Math.Max(1, itemLevel);

        public static float BerserkDamagePerStep(int itemLevel) =>
            Perks.BerserkDamagePerStep * LootGenerator.LevelScale(StatType.BaseDamage, itemLevel, 1f);

        public string Describe(int itemLevel, Lang lang)
        {
            switch (Type)
            {
                case PerkType.DamageAura:
                    return lang == Lang.Russian
                        ? $"Аура: {N(AuraDamagePercent)}% базового урона в секунду всем врагам в {N(AuraRadius)} м"
                        : $"Aura: {N(AuraDamagePercent)}% base damage per second to all enemies within {N(AuraRadius)}m";
                case PerkType.ArmorOnHurt:
                    return lang == Lang.Russian
                        ? $"Получив урон в {N(ArmorThresholdPercent)}% здоровья — броня, равная {N(ArmorThresholdPercent)}% макс. здоровья ÷ {N(Perks.ArmorHealthPerArmor)}, на {N(Perks.ArmorDuration)} с"
                        : $"After taking {N(ArmorThresholdPercent)}% of max health as damage, gain armor equal to {N(ArmorThresholdPercent)}% of max health ÷ {N(Perks.ArmorHealthPerArmor)} for {N(Perks.ArmorDuration)}s";
                case PerkType.PercentHpStrike:
                    return lang == Lang.Russian
                        ? $"{N(StrikeChance)}% шанс нанести {N(StrikeHpPercent, 1)}% макс. здоровья цели (боссам телепорта — ×{N(Perks.StrikeBossMultiplier, 1)})"
                        : $"{N(StrikeChance)}% chance to deal {N(StrikeHpPercent, 1)}% of the target's max health (teleporter bosses: ×{N(Perks.StrikeBossMultiplier, 1)})";
                case PerkType.Berserk:
                    return lang == Lang.Russian
                        ? $"+{N(BerserkDamagePerStep(itemLevel), 1)} к базовому урону за каждые {N(Perks.BerserkHealthStep)} недостающего здоровья"
                        : $"+{N(BerserkDamagePerStep(itemLevel), 1)} base damage per {N(Perks.BerserkHealthStep)} missing health";
                case PerkType.MissingHpRegen:
                    return lang == Lang.Russian
                        ? $"Регенерация {N(Perks.MissingHpRegenPercent)}% недостающего здоровья в секунду"
                        : $"Regenerate {N(Perks.MissingHpRegenPercent)}% of missing health per second";
                case PerkType.HealthArmor:
                    return lang == Lang.Russian
                        ? $"+броня в {N(Perks.HealthArmorPercent)}% макс. здоровья; каждая единица этой брони — +{N(Perks.HealthArmorDamagePerArmor, 1)} к базовому урону"
                        : $"+armor equal to {N(Perks.HealthArmorPercent)}% of max health; each point of it gives +{N(Perks.HealthArmorDamagePerArmor, 1)} base damage";
                case PerkType.WispOnKill:
                    return lang == Lang.Russian
                        ? $"Убийство: {N(Perks.WispChance)}% шанс призвать союзный огонёк на {N(Perks.WispLifetime)} с"
                        : $"On kill: {N(Perks.WispChance)}% chance to summon an allied wisp for {N(Perks.WispLifetime)}s";
                case PerkType.GolemSummon:
                    return lang == Lang.Russian
                        ? $"Раз в {N(Perks.GolemInterval)} с призывает мини-голема на {N(Perks.GolemLifetime)} с (скорость ×{N(Perks.GolemSpeed)}, урон ×{N(Perks.GolemDamage, 1)})"
                        : $"Every {N(Perks.GolemInterval)}s summons a mini golem for {N(Perks.GolemLifetime)}s (speed ×{N(Perks.GolemSpeed)}, damage ×{N(Perks.GolemDamage, 1)})";
                case PerkType.SprintWarcry:
                    return lang == Lang.Russian
                        ? $"Пока бежишь: +{N(Perks.WarcryDamagePercent)}% базового урона тебе и союзникам в {N(Perks.WarcryRadius)} м"
                        : $"While sprinting: +{N(Perks.WarcryDamagePercent)}% base damage to you and allies within {N(Perks.WarcryRadius)}m";
                case PerkType.ExtraHit:
                    return lang == Lang.Russian
                        ? $"{N(Perks.ExtraHitChance)}% шанс (× коэф. атаки) нанести ещё {N(Perks.ExtraHitPercent)}% урона отдельным ударом (коэф. {N(Perks.ExtraHitProcCoefficient, 1)})"
                        : $"{N(Perks.ExtraHitChance)}% chance (× attack proc) to deal an extra {N(Perks.ExtraHitPercent)}% damage as a separate hit (proc {N(Perks.ExtraHitProcCoefficient, 1)})";
                case PerkType.XpBonus:
                    return lang == Lang.Russian
                        ? $"+{N(Perks.XpBonusPercent)}% к получаемому опыту"
                        : $"+{N(Perks.XpBonusPercent)}% experience gained";
                case PerkType.ExplodeOnHit:
                    return lang == Lang.Russian
                        ? $"{N(Perks.ExplodeChance)}% шанс (× коэф. атаки) взрыва: {N(Perks.ExplodeDamagePercent)}% урона в {N(Perks.ExplodeRadius)} м (коэф. {N(Perks.ExplodeProcCoefficient, 1)})"
                        : $"{N(Perks.ExplodeChance)}% chance (× attack proc) to explode for {N(Perks.ExplodeDamagePercent)}% damage within {N(Perks.ExplodeRadius)}m (proc {N(Perks.ExplodeProcCoefficient, 1)})";
                case PerkType.SprintMomentum:
                    return lang == Lang.Russian
                        ? $"Бег: каждую секунду +{N(MomentumPerStack(itemLevel))}% к скорости бега, до {Perks.MomentumMaxStacks} раз"
                        : $"Sprinting: +{N(MomentumPerStack(itemLevel))}% sprint speed every second, up to {Perks.MomentumMaxStacks} times";
                case PerkType.HurtWispSwarm:
                    return lang == Lang.Russian
                        ? $"Получив урон: {N(Perks.SwarmChance)}% шанс призвать {Perks.SwarmCount} огоньков на {N(Perks.SwarmLifetime)} с (здоровье ×{N(Perks.SwarmHealth)}, урон ×{N(Perks.SwarmDamage, 1)})"
                        : $"When hit: {N(Perks.SwarmChance)}% chance to summon {Perks.SwarmCount} wisps for {N(Perks.SwarmLifetime)}s (health ×{N(Perks.SwarmHealth)}, damage ×{N(Perks.SwarmDamage, 1)})";
                case PerkType.StandStillSolus:
                    return lang == Lang.Russian
                        ? $"Простой {N(Perks.SolusCastTime)} с на месте — навсегда призвать элитный зонд Солус (атака и скорость ×{N(Perks.SolusSpeed)}, урон и здоровье ×{N(Perks.SolusPower)}) и обездвижить всех врагов на {N(Perks.SolusRootDuration)} с. КД {N(Perks.SolusCooldown / 60f)} мин"
                        : $"Stand still for {N(Perks.SolusCastTime)}s to summon a permanent elite Solus Probe (attack & move speed ×{N(Perks.SolusSpeed)}, damage & health ×{N(Perks.SolusPower)}) and root all enemies for {N(Perks.SolusRootDuration)}s. Cooldown {N(Perks.SolusCooldown / 60f)} min";
                default:
                    return Type.ToString();
            }
        }

        public string Describe(int itemLevel) => Describe(itemLevel, Loc.Current);

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        private static string N(float value, int decimals = 0) =>
            Math.Round(value, decimals).ToString(decimals == 0 ? "0" : "0.#", CultureInfo.InvariantCulture);
    }

    /// <summary>Константы перков и правила выпадения.</summary>
    public static class Perks
    {
        public const float AuraRadiusMin = 10f, AuraRadiusMax = 50f;
        public const float AuraDamageMin = 20f, AuraDamageMax = 80f;
        public const float AuraProcCoefficient = 0.1f;

        public const float ArmorPercentMin = 10f, ArmorPercentMax = 50f;
        public const float ArmorHealthPerArmor = 10f; // 10 здоровья → 1 брони
        public const float ArmorDuration = 5f;

        public const float StrikeChanceMin = 8f, StrikeChanceMax = 40f;
        public const float StrikeHpMin = 1f, StrikeHpMax = 5f;
        public const float StrikeBossMultiplier = 0.2f; // по боссам телепорта — 1/5

        public const float BerserkHealthStep = 70f;
        public const float BerserkDamagePerStep = 1f;

        public const float MissingHpRegenPercent = 1f;

        public const float HealthArmorPercent = 2f;
        public const float HealthArmorDamagePerArmor = 0.1f;

        public const float WispChance = 15f;
        public const float WispLifetime = 120f;

        public const float GolemInterval = 30f;  // новый каждые 30 с
        public const float GolemLifetime = 60f;  // живёт 60 с — одновременно до двух
        public const float GolemSpeed = 2f;     // ×2 скорости
        public const float GolemDamage = 0.3f;  // ×0.3 урона
        public const float GolemScale = 0.5f;

        public const float WarcryDamagePercent = 20f;
        public const float WarcryRadius = 30f;

        public const float ExtraHitChance = 100f; // × proc coefficient попадания
        public const float ExtraHitPercent = 10f;
        public const float ExtraHitProcCoefficient = 0.1f;

        public const float XpBonusPercent = 10f;

        public const float ExplodeChance = 10f;          // × proc атаки
        public const float ExplodeDamagePercent = 200f;  // от урона героя
        public const float ExplodeRadius = 20f;
        public const float ExplodeProcCoefficient = 0.2f;

        public const float MomentumPercentPerLevel = 1f;
        public const int MomentumMaxStacks = 5;

        public const float SwarmChance = 10f;
        public const int SwarmCount = 10;
        public const float SwarmLifetime = 10f;
        public const float SwarmHealth = 10f;     // ×10 здоровья
        public const float SwarmDamage = 0.2f;    // ×0.2 урона
        public const float SwarmInternalCooldown = 2f; // не чаще раза в 2 с на перк — иначе от частых ударов сотни огоньков

        public const float SolusCastTime = 10f;
        public const float SolusCooldown = 180f;
        public const float SolusSpeed = 2f;       // скорость атаки и бега ×2
        public const float SolusPower = 3f;       // урон и здоровье ×3
        public const float SolusRootDuration = 5f;

        public static int Count => Enum.GetValues(typeof(PerkType)).Length;
    }
}
