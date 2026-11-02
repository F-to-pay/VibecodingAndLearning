using System;
using System.Collections.Generic;
using RoR2;
using RoR2.Skills;

namespace Destroyer.Content
{
    /// <summary>
    /// Множители перезарядки навыков в обход R2API (0.14.4, исправлено в 0.14.5).
    ///
    /// Почему ни R2API, ни cooldownScale не могут УДЛИНИТЬ перезарядку (IL GenericSkill.CalculateFinalRechargeInterval 1.5):
    ///   итог = Min(база, Max(0.5, база × cooldownScale − flatReduction)) + temporaryCooldownPenalty
    /// — Min(база, …): масштаб больше 1 игра молча срезает до базы. Сократить можно, удлинить — нет.
    ///
    /// Поэтому:
    ///  * общий множитель меньше 1 — домножаем cooldownScale четырёх навыков в On-хуке CharacterBody.RecalculateStats
    ///    после ванили (она выставляет его заново на каждом пересчёте, не копится);
    ///  * больше 1 — замедляем саму перезарядку: On-хук GenericSkill.RunRecharge(dt, continuous) делит dt на множитель
    ///    (через неё идут обычная перезарядка из SkillDef.OnFixedUpdate и "откаты" вроде крита/укуса Акрида).
    ///    Цифра на иконке показывает базовое время, но тикает в k раз медленнее.
    /// Предмет регистрирует функцию «тело → множитель»; множители всех предметов перемножаются. Работает на каждой машине.
    /// </summary>
    internal static class SkillCooldowns
    {
        private static readonly List<Func<CharacterBody, float>> scales = new List<Func<CharacterBody, float>>();
        private static readonly Dictionary<CharacterBody, float> slowdown = new Dictionary<CharacterBody, float>();
        private static readonly List<CharacterBody> stale = new List<CharacterBody>();
        private static readonly HashSet<SkillDef> exempt = new HashSet<SkillDef>();
        private static bool installed, exemptInstalled;

        /// <summary>
        /// Навык без каких-либо множителей перезарядки (0.15.1, переключатель Хладнокровия): ни наших, ни ванильных —
        /// On-хук CalculateFinalRechargeInterval отдаёт базу SkillDef, RunRecharge не замедляется.
        /// </summary>
        public static void Exempt(SkillDef def)
        {
            if (!def) return;
            exempt.Add(def);
            if (exemptInstalled) return;
            exemptInstalled = true;
            On.RoR2.GenericSkill.CalculateFinalRechargeInterval += (orig, self) =>
                self && self.skillDef && exempt.Contains(self.skillDef) ? self.skillDef.baseRechargeInterval : orig(self);
        }

        /// <summary>Добавить множитель: функция тела → множитель перезарядки (1 = без изменений).</summary>
        public static void Register(Func<CharacterBody, float> scale)
        {
            scales.Add(scale);
            if (installed) return;
            installed = true;
            On.RoR2.CharacterBody.RecalculateStats += OnRecalculateStats;
            On.RoR2.GenericSkill.RunRecharge_float_bool += OnRunRecharge;
        }

        private static void OnRecalculateStats(On.RoR2.CharacterBody.orig_RecalculateStats orig, CharacterBody self)
        {
            orig(self);
            if (!self) return;
            float m = 1f;
            foreach (var f in scales)
            {
                try { m *= f(self); }
                catch (Exception e) { DestroyerPlugin.Log.LogError($"Cooldown scale failed: {e.Message}"); }
            }

            if (m > 1f) slowdown[self] = m;
            else slowdown.Remove(self);

            if (m < 1f && m > 0f && self.skillLocator)
            {
                var loc = self.skillLocator;
                if (loc.primary) loc.primary.cooldownScale *= m;
                if (loc.secondary) loc.secondary.cooldownScale *= m;
                if (loc.utility) loc.utility.cooldownScale *= m;
                if (loc.special) loc.special.cooldownScale *= m;
            }

            if (slowdown.Count > 128)
            {
                stale.Clear();
                foreach (var key in slowdown.Keys) if (!key) stale.Add(key);
                foreach (var key in stale) slowdown.Remove(key);
            }
        }

        private static void OnRunRecharge(On.RoR2.GenericSkill.orig_RunRecharge_float_bool orig, GenericSkill self, float dt, bool continuous)
        {
            var body = self ? self.characterBody : null;
            if (body && slowdown.TryGetValue(body, out float m) && m > 1f && !(self.skillDef && exempt.Contains(self.skillDef))) dt /= m;
            orig(self, dt, continuous);
        }
    }
}
