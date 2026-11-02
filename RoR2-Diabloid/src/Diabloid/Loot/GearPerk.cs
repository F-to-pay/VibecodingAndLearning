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
        ArmorOnHurt,      // УБРАН в 0.17.1 (не выпадает; место в enum — сетевой формат)
        PercentHpStrike,  // шанс нанести цели % её макс. ХП, чем чаще — тем слабее
        Berserk,          // +базовый урон за каждые 10% недостающего здоровья
        MissingHpRegen,   // лечение 0.5% макс. + 3% текущего здоровья в секунду (0.17.1)
        // --- 0.8.0 ---
        HealthArmor,      // +броня 4% макс. ХП; каждая такая броня даёт 0.1 базового урона
        WispOnKill,       // убийство: 15% шанс призвать союзный огонёк на 120 с
        GolemSummon,      // раз в 30 с — союзный мини-голем на 60 с (скорость ×2, урон ×0.3, размер ×0.5)
        SprintWarcry,     // бег копит заряды (1/с, до 15); остановка — +2% базового урона за заряд всем в 30 м на 10 с, КД 10 с
        ExtraHit,         // 100%: дополнительный удар в 10% урона, proc 0.4
        // --- 0.10.0 ---
        XpBonus,          // +50% получаемого опыта, но −20% основных статов героя
        ExplodeOnHit,     // 10% (× proc): взрыв 200% урона в 20 м, proc 0.2
        SprintMomentum,   // бег: каждую секунду +5% × 1.03^(уровень шмотки − 1) к скорости бега, до 5 раз
        HurtWispSwarm,    // УБРАН в 0.15.3 (не выпадает и не работает; место в enum — сетевой формат)
        StandStillSolus,  // 10 с стоя на месте — элитный зонд Солус навсегда + корни всем врагам на 5 с, КД 3 мин
        // --- 0.14.0 ---
        KillDamage,       // убийство: +0.01 / элитка +0.05 / босс +0.5 базового урона навсегда (на забег)
        Infection,        // 100% (× proc): заражение 10% базового урона в секунду, 20 с, не стакается, proc 0.4
        SlowAura,         // враги в 30 м −10% скорости, отнятые м/с — себе
        LootLuck,         // +0.5 к удаче качества дропа Diabloid с убитых тобой
        ReflectDamage,    // 50% полученного урона — каждому врагу в 50 м (лечение убрано в 0.17.1)
        // --- 0.18.0 ---
        Banana,           // нужно 3 банана: +8 кармы, остальные финальные боссы +50% скорости атаки, все остальные перки разом
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

        /// <summary>Разгон: % скорости бега за стак = 5 × 1.03^(уровень шмотки − 1) — растёт как процентные статы.</summary>
        public static float MomentumPerStack(int itemLevel) =>
            Perks.MomentumPercentBase * (float)Math.Pow(1.0 + Perks.MomentumGrowth, Math.Max(1, itemLevel) - 1);

        /// <summary>Берсерк: базового урона за каждые 10% недостающего ХП — 1 × рост от уровня шмотки (как у стата базового урона).</summary>

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
                        ? $"+{N(BerserkDamagePerStep(itemLevel), 1)} к базовому урону за каждые {N(Perks.BerserkHealthStepPercent)}% недостающего здоровья"
                        : $"+{N(BerserkDamagePerStep(itemLevel), 1)} base damage per {N(Perks.BerserkHealthStepPercent)}% missing health";
                case PerkType.MissingHpRegen:
                    return lang == Lang.Russian
                        ? $"Регенерация {N(Perks.RegenMaxHpPercent, 1)}% макс. + {N(Perks.RegenCurrentHpPercent, 1)}% текущего здоровья в секунду"
                        : $"Regenerate {N(Perks.RegenMaxHpPercent, 1)}% of max + {N(Perks.RegenCurrentHpPercent, 1)}% of current health per second";
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
                        ? $"Бег копит клич: заряд в секунду, до {Perks.WarcryMaxStacks}. Остановился — тебе и союзникам в {N(Perks.WarcryRadius)} м +{N(Perks.WarcryDamagePercent)}% базового урона за заряд на {N(Perks.WarcryDuration)} с. КД {N(Perks.WarcryCooldown)} с"
                        : $"Sprinting charges a warcry: 1 per second, up to {Perks.WarcryMaxStacks}. On stopping, you and allies within {N(Perks.WarcryRadius)}m gain +{N(Perks.WarcryDamagePercent)}% base damage per charge for {N(Perks.WarcryDuration)}s. CD {N(Perks.WarcryCooldown)}s";
                case PerkType.ExtraHit:
                    return lang == Lang.Russian
                        ? $"{N(Perks.ExtraHitChance)}% шанс (× коэф. атаки) нанести ещё {N(Perks.ExtraHitPercent)}% урона отдельным ударом (коэф. {N(Perks.ExtraHitProcCoefficient, 1)})"
                        : $"{N(Perks.ExtraHitChance)}% chance (× attack proc) to deal an extra {N(Perks.ExtraHitPercent)}% damage as a separate hit (proc {N(Perks.ExtraHitProcCoefficient, 1)})";
                case PerkType.XpBonus:
                    return lang == Lang.Russian
                        ? $"+{N(Perks.XpBonusPercent)}% к получаемому опыту, но −{N(Perks.XpPenaltyPercent)}% к урону, скорости атаки, здоровью, щиту, регенерации, броне и скорости"
                        : $"+{N(Perks.XpBonusPercent)}% experience gained, but −{N(Perks.XpPenaltyPercent)}% damage, attack speed, health, shield, regen, armor and movement speed";
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
                        ? $"Стой {N(Perks.SolusCastTime)} с — вечный элитный зонд Солус, корни врагам {N(Perks.SolusRootDuration)} с. КД {N(Perks.SolusCooldown / 60f)} мин"
                        : $"Stand still {N(Perks.SolusCastTime)}s: permanent elite Solus Probe, roots enemies {N(Perks.SolusRootDuration)}s. CD {N(Perks.SolusCooldown / 60f)} min";
                case PerkType.KillDamage:
                    return lang == Lang.Russian
                        ? $"Убийство: +{N(Perks.KillDamagePerKill * Perks.SoulsNormal, 2)} к базовому урону навсегда, элитка +{N(Perks.KillDamagePerKill * Perks.SoulsElite, 2)}, босс +{N(Perks.KillDamagePerKill * Perks.SoulsBoss, 2)} (на забег, остаётся и без шмотки)"
                        : $"On kill: +{N(Perks.KillDamagePerKill * Perks.SoulsNormal, 2)} base damage permanently, elite +{N(Perks.KillDamagePerKill * Perks.SoulsElite, 2)}, boss +{N(Perks.KillDamagePerKill * Perks.SoulsBoss, 2)} (for the run, stays even without this gear)";
                case PerkType.Infection:
                    return lang == Lang.Russian
                        ? $"{N(Perks.InfectionChance)}% шанс (× коэф. атаки) заразить: {N(Perks.InfectionDamagePercent)}% базового урона в секунду, {N(Perks.InfectionDuration)} с, не стакается (коэф. {N(Perks.InfectionProcCoefficient, 1)})"
                        : $"{N(Perks.InfectionChance)}% chance (× attack proc) to infect: {N(Perks.InfectionDamagePercent)}% base damage per second for {N(Perks.InfectionDuration)}s, doesn't stack (proc {N(Perks.InfectionProcCoefficient, 1)})";
                case PerkType.SlowAura:
                    return lang == Lang.Russian
                        ? $"Враги в {N(Perks.SlowRadius)} м медленнее на {N(Perks.SlowPercent)}%, отнятая скорость (м/с) — тебе"
                        : $"Enemies within {N(Perks.SlowRadius)}m are {N(Perks.SlowPercent)}% slower, you gain the stolen speed (m/s)";
                case PerkType.LootLuck:
                    return lang == Lang.Russian
                        ? $"+{N(Perks.LootLuckBonus, 1)} к удаче качества шмоток с убитых тобой"
                        : $"+{N(Perks.LootLuckBonus, 1)} luck for the quality of gear dropped by your kills";
                case PerkType.Banana:
                    return lang == Lang.Russian
                        // Описание — ровно такое, задумано (0.18.1). Что на самом деле даёт банан — см. README.
                        ? $"+1 Банан. {Perks.BananasRequired} Банана дает преимущество..."
                        : $"+1 Banana. {Perks.BananasRequired} Bananas grant an advantage...";
                case PerkType.ReflectDamage:
                    return lang == Lang.Russian
                        ? $"Отражает {N(Perks.ReflectPercent)}% полученного урона в каждого врага в {N(Perks.ReflectRadius)} м"
                        : $"Reflects {N(Perks.ReflectPercent)}% of damage taken to every enemy within {N(Perks.ReflectRadius)}m";
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

        public const float BerserkHealthStepPercent = 10f; // 0.17.1: было 70 ХП — бонус рос квадратично с уровнем
        public const float BerserkDamagePerStep = 1f;

        // 0.17.1: вместо 1% недостающего — 0.5% макс. + 3% текущего в секунду (сильнее всего почти на фул ХП)
        public const float RegenMaxHpPercent = 0.5f;
        public const float RegenCurrentHpPercent = 3f;

        public const float HealthArmorPercent = 4f; // 0.17.1: было 2
        public const float HealthArmorDamagePerArmor = 0.1f;

        public const float WispChance = 15f;
        public const float WispLifetime = 120f;

        public const float GolemInterval = 30f;  // новый каждые 30 с
        public const float GolemLifetime = 60f;  // живёт 60 с — одновременно до двух
        public const float GolemSpeed = 2f;     // ×2 скорости
        public const float GolemDamage = 0.3f;  // ×0.3 урона
        public const float GolemScale = 0.5f;

        // 0.17.0: клич копится бегом и срабатывает при остановке
        public const float WarcryDamagePercent = 2f;   // базового урона за заряд
        public const int WarcryMaxStacks = 15;
        public const float WarcryChargeInterval = 1f;  // заряд за секунду бега
        public const float WarcryDuration = 10f;
        public const float WarcryCooldown = 10f;       // с момента выкрика; в это время бег не заряжает
        public const float WarcryRadius = 30f;

        public const float ExtraHitChance = 100f; // × proc coefficient попадания
        public const float ExtraHitPercent = 10f;
        public const float ExtraHitProcCoefficient = 0.4f; // 0.17.0: было 0.1

        public const float XpBonusPercent = 50f;   // 0.17.0: было 10
        public const float XpPenaltyPercent = 20f; // за каждый перк основные статы героя ×0.8

        public const float ExplodeChance = 10f;          // × proc атаки
        public const float ExplodeDamagePercent = 200f;  // от урона героя
        public const float ExplodeRadius = 20f;
        public const float ExplodeProcCoefficient = 0.2f;

        public const float MomentumPercentBase = 5f;  // 0.17.1: было 1% × уровень шмотки
        public const float MomentumGrowth = 0.03f;     // ×1.03 за уровень шмотки
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

        // 0.14.0
        public const float KillDamagePerKill = 0.01f; // за одну душу (стак скрытого предмета)
        public const int SoulsNormal = 1, SoulsElite = 5, SoulsBoss = 50; // 0.17.1: было 0.05 за любое убийство

        public const float InfectionChance = 100f;          // × proc атаки
        public const float InfectionDamagePercent = 10f;    // от урона героя в секунду
        public const float InfectionDuration = 20f;
        public const float InfectionTickInterval = 1f;
        public const float InfectionProcCoefficient = 0.4f;

        public const float SlowRadius = 30f;
        public const float SlowPercent = 10f;
        public const float StolenSpeedUnit = 0.1f;          // м/с на стак баффа (по сети идёт число стаков)

        public const float LootLuckBonus = 0.5f; // 0.17.1: было 0.2

        public const float ReflectPercent = 50f;
        public const float ReflectRadius = 50f;

        // 0.18.0
        public const int BananaKarma = 8;
        public const int BananasRequired = 3; // 0.18.1: меньше трёх бананов — банан не даёт ничего
        public const float BananaBossAttackSpeedPercent = 50f;

        public static int Count => Enum.GetValues(typeof(PerkType)).Length;

        /// <summary>Перки, которые включает банан: все, что выпадают, кроме самого банана.</summary>
        public static bool GrantedByBanana(PerkType type) => type != PerkType.Banana && !IsRetired(type);

        /// <summary>Убранные перки: остаются в enum ради сетевого формата, но не выпадают и не работают.</summary>
        public static bool IsRetired(PerkType type) => type == PerkType.HurtWispSwarm || type == PerkType.ArmorOnHurt;

        /// <summary>Перки, которые могут выпасть на шмотке.</summary>
        public static PerkType[] Rollable() =>
            Array.FindAll((PerkType[])Enum.GetValues(typeof(PerkType)), t => !IsRetired(t));

        /// <summary>Короткое название перка (описания книг и свитков).</summary>
        public static Words Title(PerkType type)
        {
            switch (type)
            {
                case PerkType.DamageAura: return new Words("Aura", "Аура");
                case PerkType.ArmorOnHurt: return new Words("Armor on Hurt", "Броня от урона");
                case PerkType.PercentHpStrike: return new Words("Percent Strike", "Удар по % здоровья");
                case PerkType.Berserk: return new Words("Berserk", "Берсерк");
                case PerkType.MissingHpRegen: return new Words("Regeneration", "Регенерация");
                case PerkType.HealthArmor: return new Words("Health Armor", "Броня от здоровья");
                case PerkType.WispOnKill: return new Words("Wisps", "Огоньки");
                case PerkType.GolemSummon: return new Words("Mini Golem", "Мини-голем");
                case PerkType.SprintWarcry: return new Words("Warcry", "Боевой клич");
                case PerkType.ExtraHit: return new Words("Extra Hit", "Добивка");
                case PerkType.XpBonus: return new Words("Experience", "Опыт");
                case PerkType.ExplodeOnHit: return new Words("Explosion", "Взрыв");
                case PerkType.SprintMomentum: return new Words("Momentum", "Разгон");
                case PerkType.HurtWispSwarm: return new Words("Swarm", "Рой");
                case PerkType.StandStillSolus: return new Words("Solus", "Призыв Солуса");
                case PerkType.KillDamage: return new Words("Souls", "Души");
                case PerkType.Infection: return new Words("Infection", "Заражение");
                case PerkType.SlowAura: return new Words("Slow", "Замедление");
                case PerkType.LootLuck: return new Words("Luck", "Удача");
                case PerkType.ReflectDamage: return new Words("Reflect", "Отражение");
                case PerkType.Banana: return new Words("Banana", "Банан");
                default: return new Words(type.ToString(), type.ToString());
            }
        }
    }
}
