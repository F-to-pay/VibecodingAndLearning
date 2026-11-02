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
    /// Дымовая завеса (пустотный белый, перерабатывает баллончики с краской). Ты всегда слеп, зато пока слеп —
    /// +15% (+5%) ко всем характеристикам и сверху ещё +5% (+5%) скорости бега (0.6.1, было +30% (+10%)). Убил врага — на 5 с прозрел: ни слепоты, ни бонуса.
    ///
    /// Слепота — тот же ванильный Blinded (DLC1), что и у баллончика: у игрока он включает пост-эффект
    /// VisionLimitEffect (туман на 15 м) и режет visionDistance, по которому работают BullseyeSearch (автонаводка,
    /// например у Охотницы) и прочие поиски целей. Вешаем ОБЫЧНЫМ AddBuff, не таймером: AddTimedBuff проходит через
    /// ImmuneToDebuffBehavior (Ben's Raincoat), и плащ сжирал бы заряды каждый тик. Blast Shower снимет слепоту —
    /// на следующем тике вернём.
    ///
    /// "Прозрел" — свой бафф SMOKE_CLEAR (таймер 5 с, повторное убийство обновляет). Пока он висит: слепоту снимаем,
    /// статы не даём. Статы считает RecalculateStats на каждой машине по баффу, слепоту ставит сервер.
    /// </summary>
    internal sealed class SmokeScreen : ItemBase
    {
        public override string Id => "SMOKE_SCREEN";
        public override ItemTier Tier => ItemTier.VoidTier1;
        public override string Corrupts => "PAINT_CAN";
        public override string ModelFrom => "CritGlassesVoid"; // Lost Seer's Lenses — пустотные очки
        public override ItemTag[] Tags => new[] { ItemTag.Damage, ItemTag.Utility };

        public override Words Name => new Words("Smoke Screen", "Дымовая завеса");

        public override Words Pickup => new Words(
            $"You are blind, but much stronger. Kills clear your sight for a moment. {Tx.Void("Corrupts all Spray Paint Cans")}.",
            $"Ты слеп, но намного сильнее. Убийство ненадолго возвращает зрение. {Tx.Void("Перерабатывает все баллончики с краской")}.");

        public override Words Description => new Words(
            $"You are permanently {Tx.Utility("blinded")}. While blinded, {Tx.Damage("all your stats")} are increased by " +
            $"{Tx.Damage(Tx.Pct(bonus.Value))} {Tx.Stack("+" + Tx.Pct(bonusPerStack.Value) + " per stack")}, " +
            $"and {Tx.Utility("movement speed")} by another {Tx.Utility(Tx.Pct(extraMove.Value))} {Tx.Stack("+" + Tx.Pct(extraMove.Value) + " per stack")}. " +
            $"Killing an enemy {Tx.Utility("removes the blindness and the bonus")} for {Tx.Utility(Tx.Seconds(clearDuration.Value).En)}. " +
            $"{Tx.Void("Corrupts all Spray Paint Cans")}.",
            $"Ты постоянно {Tx.Utility("ослеплён")}. Пока ослеплён, {Tx.Damage("все характеристики")} выше на " +
            $"{Tx.Damage(Tx.Pct(bonus.Value))} {Tx.Stack("+" + Tx.Pct(bonusPerStack.Value) + " за копию")}, " +
            $"а {Tx.Utility("скорость бега")} — ещё на {Tx.Utility(Tx.Pct(extraMove.Value))} {Tx.Stack("+" + Tx.Pct(extraMove.Value) + " за копию")}. " +
            $"Убийство врага {Tx.Utility("снимает и слепоту, и бонус")} на {Tx.Utility(Tx.Seconds(clearDuration.Value).Ru)}. " +
            $"{Tx.Void("Перерабатывает все баллончики с краской")}.");

        public override Words Lore => new Words(
            "It never cleared. You just learned to fight inside it.",
            "Дым так и не рассеялся. Ты просто научился в нём драться.");

        private ConfigEntry<float> bonus, bonusPerStack, extraMove, clearDuration;

        protected override void BindConfig(ItemSettings s)
        {
            bonus = s.Float("StatBonus", 0.15f, 0f, 5f, new Words("Stat bonus", "Бонус к характеристикам"),
                new Words("All-stat bonus with the first copy while blinded (0.15 = +15%).",
                          "Бонус ко всем характеристикам с первой копией, пока ослеплён (0.15 = +15%)."), "{0:P0}");
            bonusPerStack = s.Float("StatBonusPerStack", 0.05f, 0f, 5f, new Words("Stat bonus per stack", "Бонус за копию"),
                new Words("Extra all-stat bonus per additional copy (0.05 = +5%).",
                          "Прибавка бонуса за каждую копию сверх первой (0.05 = +5%)."), "{0:P0}");
            extraMove = s.Float("ExtraMoveSpeed", 0.05f, 0f, 5f, new Words("Extra movement speed", "Доп. скорость бега"),
                new Words("Movement speed on top of the all-stat bonus, per copy, while blinded (0.05 = +5%).",
                          "Скорость бега сверх общего бонуса, за каждую копию, пока ослеплён (0.05 = +5%)."), "{0:P0}");
            clearDuration = s.Float("ClearDuration", 5f, 0f, 60f, new Words("Clear sight duration", "Время без слепоты"),
                new Words("How long a kill removes the blindness and the bonus, seconds.",
                          "Сколько секунд после убийства нет ни слепоты, ни бонуса."), "{0:0.#} s");
        }

        private BuffDef clear;
        internal static SmokeScreen Instance;
        internal BuffDef Clear => clear;

        protected override void Hook()
        {
            Instance = this;
            clear = BuffFactory.Create("SMOKE_CLEAR", BuffFactory.Hex("#c9a6ff"), isDebuff: false, canStack: false);
            UseBehavior<SmokeBehavior>(); // сервер
            GlobalEventManager.onCharacterDeathGlobal += OnDeath;

            RecalculateStatsAPI.GetStatCoefficients += (body, args) =>
            {
                int n = Count(body);
                if (n <= 0 || (clear && body.HasBuff(clear))) return;
                float b = Stacking.Linear(n, bonus.Value, bonusPerStack.Value);
                float mult = 1f + b;

                args.healthMultAdd += b;
                args.shieldMultAdd += b;
                args.regenMultAdd += b;
                args.moveSpeedMultAdd += b + Stacking.Linear(n, extraMove.Value);
                args.jumpPowerMultAdd += b;
                args.damageMultAdd += b;
                args.attackSpeedMultAdd += b;
                args.critDamageMultAdd += b;
                args.armorTotalMult *= mult;
                args.critTotalMult *= mult;
            };
            SkillCooldowns.Register(body =>
            {
                int n = Count(body);
                if (n <= 0 || (clear && body.HasBuff(clear))) return 1f;
                return 1f / (1f + Stacking.Linear(n, bonus.Value, bonusPerStack.Value)); // ÷mult, своим хуком (0.14.4)
            });
        }

        private void OnDeath(DamageReport report)
        {
            if (!NetworkServer.active || clear == null || clearDuration.Value <= 0f) return;
            if (!report.victimMaster || !report.victimBody) return; // фальшивые смерти куклы — без мастера
            var killer = report.attackerBody;
            if (!killer || report.attackerTeamIndex == report.victimTeamIndex) return;
            if (Count(killer) <= 0) return;
            killer.AddTimedBuff(clear, clearDuration.Value); // нестакаемый — просто обновит таймер
        }

        private static bool warnedNoBlind;

        /// <summary>Сервер: держит слепоту, пока нет "прозрел".</summary>
        private sealed class SmokeBehavior : CharacterBody.ItemBehavior
        {
            private bool added;

            private void FixedUpdate()
            {
                if (!NetworkServer.active || !body) return;
                var blinded = DLC1Content.Buffs.Blinded;
                if (!blinded)
                {
                    if (!warnedNoBlind) DestroyerPlugin.Log.LogWarning("Smoke screen: vanilla Blinded buff (DLC1) not found, no blindness.");
                    warnedNoBlind = true;
                    return;
                }
                var clearBuff = Instance != null ? Instance.Clear : null;
                bool wantBlind = !(clearBuff && body.HasBuff(clearBuff));

                if (wantBlind)
                {
                    if (!body.HasBuff(blinded)) { body.AddBuff(blinded); added = true; }
                }
                else Unblind(blinded);
            }

            private void Unblind(BuffDef blinded)
            {
                if (!added || !blinded) return;
                added = false;
                if (body && body.HasBuff(blinded)) body.RemoveBuff(blinded);
            }

            private void OnDisable()
            {
                if (NetworkServer.active && body) Unblind(DLC1Content.Buffs.Blinded);
            }
        }
    }
}
