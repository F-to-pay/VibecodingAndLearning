using System;
using Diabloid.Config;
using Diabloid.Loot;
using R2API;
using RoR2;

namespace Diabloid.Gameplay
{
    /// <summary>Превращает надетые шмотки в реальные статы персонажа.</summary>
    internal static class GearStats
    {
        // Буфер под суммы. RecalculateStats зовётся часто — не хочется каждый раз аллоцировать массив.
        private static readonly float[] totals = new float[Stats.Count];

        public static void Init()
        {
            RecalculateStatsAPI.GetStatCoefficients += OnGetStatCoefficients;
            GlobalEventManager.onServerDamageDealt += OnServerDamageDealt;
            Run.onRunAmbientLevelUp += OnAmbientLevelUp;
            On.RoR2.HealthComponent.TakeDamageProcess += OnTakeDamageProcess;
        }

        /// <summary>
        /// Уровень монстров вырос — пустотные шмотки выросли вместе с ним, пересчитываем статы их владельцам.
        /// Уровень считается на каждой машине сама по себе, поэтому и пересчёт локальный, без сети.
        /// </summary>
        private static void OnAmbientLevelUp(Run run)
        {
            // Игроки и их турели: у турели шмотки хозяина, пустотные растут и у неё.
            foreach (var (master, body, _) in GearRegistry.Wearers())
            {
                var gear = GearRegistry.FindWorn(master);
                if (gear != null && gear.HasLevelScaling) body.MarkAllStatsDirty();
            }
        }

        /// <summary>
        /// R2API собирает добавки от всех модов и применяет их в CharacterBody.RecalculateStats.
        /// Вызывается и на сервере, и на клиентах — поэтому клиентам нужна копия экипировки (см. GearSyncMessage).
        /// </summary>
        private static void OnGetStatCoefficients(CharacterBody body, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!body) return;
            var gear = GearRegistry.FindWorn(body.master);
            if (gear == null || gear.IsEmpty) return;

            Array.Clear(totals, 0, totals.Length);
            gear.AccumulateTotals(totals);

            // "...MultAdd" в R2API — это добавка к множителю: 0.12 = +12%.
            args.damageMultAdd       += Get(StatType.Damage) / 100f;
            args.attackSpeedMultAdd  += Get(StatType.AttackSpeed) / 100f;
            args.critAdd             += Get(StatType.CritChance);          // крит в игре и так в процентах
            args.critDamageMultAdd   += Get(StatType.CritDamage) / 100f;
            args.baseHealthAdd       += Get(StatType.MaxHealth);
            args.healthMultAdd       += Get(StatType.MaxHealthPercent) / 100f;
            args.baseRegenAdd        += Get(StatType.Regen);
            args.regenMultAdd        += Get(StatType.RegenPercent) / 100f; // проклятие пустотных: в минус
            args.armorAdd            += Get(StatType.Armor);
            args.baseShieldAdd       += Get(StatType.Shield);
            args.moveSpeedMultAdd    += Get(StatType.MoveSpeed) / 100f;
            args.jumpPowerMultAdd    += Get(StatType.JumpPower) / 100f;
            args.jumpCountAdd        += (int)Math.Round(Get(StatType.ExtraJumps));
            args.sprintSpeedAdd      += Get(StatType.SprintSpeed) / 100f;  // к множителю спринта (база 1.45)
            args.baseDamageAdd       += Get(StatType.BaseDamage);

            // Перезарядку режем с потолком, иначе на лупах можно уйти в 0 секунд.
            // Отрицательное значение (лунное проклятие) потолком не режется — перезарядка просто дольше.
            float cdr = Math.Min(Get(StatType.CooldownReduction), DiabloidConfig.CooldownReductionCap.Value);
            args.allSkills.cooldownMultAdd -= cdr / 100f;
        }

        private static float Get(StatType stat) => totals[(int)stat];

        /// <summary>
        /// Лечение за попадание и вампиризм. Урон считается на сервере, значит и лечим там.
        /// Оба умножаются на proc coefficient: у пулемёта он маленький, у снайперки большой,
        /// у кровотечений и прочих DoT — ноль, так что лечение от них не идёт.
        /// </summary>
        private static void OnServerDamageDealt(DamageReport report)
        {
            if (report == null || !report.attackerBody || report.attackerBody == report.victimBody) return;

            var gear = GearRegistry.FindWorn(report.attackerMaster);
            if (gear == null) return;

            float proc = report.damageInfo.procCoefficient;
            if (proc <= 0f) return;

            float amount = gear.GetTotal(StatType.HealOnHit) * proc
                         + report.damageDealt * gear.GetTotal(StatType.Lifesteal) / 100f * proc;
            var health = report.attackerBody.healthComponent;
            if (amount > 0f && health)
                health.Heal(amount, default(ProcChainMask), true);
        }

        /// <summary>
        /// Плоский резист: минус N с каждого удара до брони, но минимум 1 урона проходит (как у Repulsion Armor Plate).
        /// Только сервер — урон считает он. Урон в обход брони (туман Пустоты, проклятия) не трогаем.
        /// </summary>
        private static void OnTakeDamageProcess(On.RoR2.HealthComponent.orig_TakeDamageProcess orig, HealthComponent self, DamageInfo damageInfo)
        {
            if (self && damageInfo != null && damageInfo.damage > 1f && self.body &&
                (damageInfo.damageType.damageType & DamageType.BypassArmor) == 0)
            {
                var gear = GearRegistry.FindWorn(self.body.master);
                float block = gear != null ? gear.GetTotal(StatType.DamageBlock) : 0f;
                if (block > 0f) damageInfo.damage = Math.Max(1f, damageInfo.damage - block);
            }
            orig(self, damageInfo);
        }
    }
}
