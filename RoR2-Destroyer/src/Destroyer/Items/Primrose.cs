using BepInEx.Configuration;
using Destroyer.Content;
using Destroyer.Core;
using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace Destroyer.Items
{
    /// <summary>
    /// Первоцвет (пустотный красный, перерабатывает ВСЕ лунные предметы — и наши куклу с Deus тоже). Все характеристики
    /// ×1.2 (+0.2 за копию сверх первой). Плюс 50 (+25) брони (0.10.3, было 200 сразу — имба с первой копии). Цена: лечения на 15% меньше (+15% за копию), без нижнего порога:
    /// с 7 копий лечение = 0, дальше ОТРИЦАТЕЛЬНОЕ — каждое лечение (и реген) снимает здоровье (0.10.2).
    /// 0.10.1: перевёрнуто по просьбе пользователя — было ×3 (−0.1) и −100% (−5%) лечения, стаки ослабляли предмет.
    ///
    /// Переработка — ItemBase.CorruptsAll: условие "тир Lunar"; пары собирает ItemRegistry в хуке
    /// ItemCatalog.SetItemRelationships по ItemCatalog.allItemDefs (каталог там уже собран — IL ItemCatalog.Init 1.5:
    /// SetItemDefs, потом SetItemRelationships). Скрытые предметы пропускаются.
    ///
    /// "Все характеристики" — RecalculateStatsAPI, честным множителем (TotalMult): здоровье, щит, реген, скорость бега,
    /// прыжок, урон, скорость атаки, шанс и урон крита; перезарядки быстрее во столько же раз (Content/SkillCooldowns — свой хук, через R2API не работало).
    /// Броня НЕ умножается — просто +50 (+25), иначе множитель раздул бы её ещё сильнее.
    /// Лечение — On-хук HealthComponent.Heal (сервер): amount × доля; ноль — сразу 0, ванильный Heal не зовём.
    /// Касается всего лечения, включая реген; щит и барьер — не лечение, они работают.
    /// </summary>
    internal sealed class Primrose : ItemBase
    {
        public override string Id => "PRIMROSE";
        public override ItemTier Tier => ItemTier.VoidTier3;
        public override System.Func<ItemDef, bool> CorruptsAll => def => def.tier == ItemTier.Lunar;
        public override string ModelFrom => "CloverVoid"; // Benthic Bloom — пустотный цветок
        public override ItemTag[] Tags => new[] { ItemTag.Damage, ItemTag.Utility };

        public override Words Name => new Words("Primrose", "Первоцвет");

        public override Words Pickup => new Words(
            $"Boost all your stats and gain armor... <style=cDeath>but healing is weaker.</style> {Tx.Void("Corrupts all lunar items")}.",
            $"Все характеристики выше, плюс броня... <style=cDeath>но лечение слабее.</style> {Tx.Void("Перерабатывает все лунные предметы")}.");

        public override Words Description => new Words(
            $"{Tx.Damage("All your stats")} are multiplied by {Tx.Damage("×" + Tx.Num(mult.Value))} {Tx.Stack("+" + Tx.Num(multPerStack.Value) + " per stack")}. " +
            $"Gain {Tx.Utility("+" + Tx.Num(armor.Value) + " armor")} {Tx.Stack("+" + Tx.Num(armorPerStack.Value) + " per stack")}. " +
            $"{Tx.Health("Healing is reduced by " + Tx.Pct(healPenalty.Value))} {Tx.Stack("+" + Tx.Pct(healPenaltyPerStack.Value) + " per stack")}{Tx.Health(" — past 100%, healing hurts you")}. " +
            $"{Tx.Void("Corrupts all lunar items")}.",
            $"{Tx.Damage("Все характеристики")} умножаются на {Tx.Damage("×" + Tx.Num(mult.Value))} {Tx.Stack("+" + Tx.Num(multPerStack.Value) + " за копию")}. " +
            $"{Tx.Utility("+" + Tx.Num(armor.Value) + " брони")} {Tx.Stack("+" + Tx.Num(armorPerStack.Value) + " за копию")}. " +
            $"{Tx.Health("Лечения на " + Tx.Pct(healPenalty.Value) + " меньше")} {Tx.Stack("+" + Tx.Pct(healPenaltyPerStack.Value) + " за копию")}{Tx.Health(" — за 100% лечение ранит")}. " +
            $"{Tx.Void("Перерабатывает все лунные предметы")}.");

        public override Words Lore => new Words(
            "It bloomed on the Moon first. It did not ask the Moon.",
            "Он расцвёл на Луне первым. Луну никто не спрашивал.");

        private ConfigEntry<float> mult, multPerStack, armor, armorPerStack, healPenalty, healPenaltyPerStack;

        protected override void BindConfig(ItemSettings s)
        {
            mult = s.Float("StatMultiplier", 1.2f, 1f, 20f, new Words("Stat multiplier", "Множитель характеристик"),
                new Words("All stats are multiplied by this with the first copy.", "Во сколько раз растут все характеристики с первой копией."), "×{0:0.##}");
            multPerStack = s.Float("StatMultiplierPerStack", 0.2f, 0f, 5f, new Words("Multiplier per stack", "Множитель за копию"),
                new Words("Each extra copy adds this to the multiplier. 0.2: ×1.2 → ×1.4 → ×1.6…",
                          "Каждая копия сверх первой добавляет это к множителю. 0.2: ×1.2 → ×1.4 → ×1.6…"), "{0:0.##}");
            armor = s.Float("Armor", 50f, 0f, 2000f, new Words("Armor", "Броня"),
                new Words("Flat armor with the first copy (not multiplied by the stat multiplier).", "Плоская броня с первой копией (множитель характеристик на неё не действует)."), "{0:0}");
            armorPerStack = s.Float("ArmorPerStack", 25f, 0f, 2000f, new Words("Armor per stack", "Броня за копию"),
                new Words("Extra armor per additional copy. 25: 50 → 75 → 100… → 200 at 7 copies.", "Прибавка брони за каждую копию сверх первой. 25: 50 → 75 → 100… → 200 на 7 копиях."), "{0:0}");
            healPenalty = s.Float("HealingPenalty", 0.15f, 0f, 1f, new Words("Healing penalty", "Штраф лечения"),
                new Words("How much less healing you get with the first copy (0.15 = -15%; 1 = none at all).",
                          "На сколько меньше лечения с первой копией (0.15 = −15%; 1 = нет вообще)."), "{0:P0}");
            healPenaltyPerStack = s.Float("HealingPenaltyPerStack", 0.15f, 0f, 1f, new Words("Healing penalty per stack", "Штраф лечения за копию"),
                new Words("Each extra copy cuts healing by this much more (0.15 = another -15%). Past 100% healing hurts you instead.",
                          "Каждая копия сверх первой режет лечение ещё на столько (0.15 = ещё −15%). За 100% лечение начинает ранить."), "{0:P0}");
        }

        private float MultFor(int n) => Mathf.Max(1f, mult.Value + multPerStack.Value * (n - 1));
        // Без нижнего порога (0.10.2): ниже нуля лечение РАНИТ.
        private float HealFactorFor(int n) => Mathf.Min(1f, 1f - (healPenalty.Value + healPenaltyPerStack.Value * (n - 1)));

        protected override void Hook()
        {
            RecalculateStatsAPI.GetStatCoefficients += (body, args) =>
            {
                int n = Count(body);
                if (n <= 0) return;
                float k = MultFor(n);
                args.healthTotalMult *= k;
                args.shieldTotalMult *= k;
                args.regenTotalMult *= k;
                args.moveSpeedTotalMult *= k;
                args.jumpPowerTotalMult *= k;
                args.damageTotalMult *= k;
                args.attackSpeedTotalMult *= k;
                args.critTotalMult *= k;
                args.critDamageTotalMult *= k;
                args.armorAdd += Stacking.Linear(n, armor.Value, armorPerStack.Value);
            };

            SkillCooldowns.Register(body => { int n = Count(body); return n > 0 ? 1f / MultFor(n) : 1f; }); // ÷k, своим хуком (0.14.4)
            On.RoR2.HealthComponent.Heal += OnHeal;
        }

        private float OnHeal(On.RoR2.HealthComponent.orig_Heal orig, HealthComponent self, float amount, ProcChainMask procChainMask, bool nonRegen)
        {
            if (NetworkServer.active && self && self.body && amount > 0f)
            {
                int n = Count(self.body);
                if (n > 0)
                {
                    float factor = HealFactorFor(n);
                    if (factor < 0f)
                    {
                        HurtByHealing(self, amount * -factor);
                        return 0f;
                    }
                    if (factor == 0f) return 0f;
                    amount *= factor;
                }
            }
            return orig(self, amount, procChainMask, nonRegen);
        }

        /// <summary>
        /// "Отрицательное лечение": снимаем здоровье напрямую, как ванильный отрицательный реген
        /// (HealthComponent.ServerFixedUpdate, IL 1.5: health += ceil(accumulator), до нуля — Suicide). Без события урона:
        /// не сбивает таймер "вне боя", не прокает предметы и не рисует цифры; щит не трогает.
        /// </summary>
        private static void HurtByHealing(HealthComponent health, float amount)
        {
            if (amount <= 0f || !health.alive) return;
            float left = health.health - amount;
            if (left <= 0f) health.Suicide(null, null, default);
            else health.Networkhealth = left;
        }
    }
}
