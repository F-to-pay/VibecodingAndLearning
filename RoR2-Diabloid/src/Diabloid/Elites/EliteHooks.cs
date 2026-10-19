using Diabloid.Config;
using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace Diabloid.Elites
{
    /// <summary>Подписки на события игры, связанные с элитками.</summary>
    internal static class EliteHooks
    {
        // Enum.GetValues каждый раз создаёт новый массив, а RecalculateStats зовётся очень часто — кешируем.
        private static readonly EliteKind[] AllKinds = (EliteKind[])System.Enum.GetValues(typeof(EliteKind));

        public static void Init()
        {
            // Игра сама сообщает "у тела появился бафф" — и на сервере, и на клиентах.
            CharacterBody.onBuffFirstStackGained += OnBuffFirstStackGained;
            RecalculateStatsAPI.GetStatCoefficients += OnGetStatCoefficients;
            GlobalEventManager.onServerDamageDealt += OnServerDamageDealt;
        }

        /// <summary>
        /// Кровопийца сам теряет здоровье навсегда: часть ПОЛУЧЕННОГО им урона (от кого угодно) — неисцеляемая,
        /// как у Митрикса. Иначе вампиризм отхиливает его в ноль. Это ванильное проклятие PermanentCurse
        /// (то же, что у игроков в Затмении 8): каждый стак делит максимум здоровья на (1 + 0.01 × стаки).
        /// Стаков даём (урон / полное здоровье × 100 × доля), то есть 10% урона срезают ~10% от урона максимума.
        /// </summary>
        private static void OnServerDamageDealt(DamageReport report)
        {
            if (report == null || !report.victimBody) return;
            if (!DiabloidElites.Is(report.victimBody, EliteKind.Bloodsucker)) return;

            float share = DiabloidConfig.BloodsuckerUnhealablePercent.Value / 100f;
            var victim = report.victimBody;
            var health = victim.healthComponent;
            if (share <= 0f || !health || !health.alive || report.damageDealt <= 0f) return;

            float full = health.fullCombinedHealth;
            if (full <= 0f) return;

            var buffer = victim.GetComponent<UnhealableCurseBuffer>();
            if (!buffer) buffer = victim.gameObject.AddComponent<UnhealableCurseBuffer>();
            buffer.PendingStacks += report.damageDealt / full * 100f * share;

            int stacks = Mathf.FloorToInt(buffer.PendingStacks);
            if (stacks <= 0) return;
            buffer.PendingStacks -= stacks;
            for (int i = 0; i < stacks; i++) victim.AddBuff(RoR2Content.Buffs.PermanentCurse);
        }

        private static void OnBuffFirstStackGained(CharacterBody body, BuffDef buff)
        {
            if (!body || !buff) return;

            if (buff == DiabloidElites.BuffOf(EliteKind.Titan))
            {
                // Размер — на каждой машине.
                if (!body.GetComponent<TitanScaler>()) body.gameObject.AddComponent<TitanScaler>();

                // Призыв копий — только сервер и только один раз на оригинального титана.
                if (NetworkServer.active && body.master && EliteManager.Instance)
                {
                    var marker = body.master.GetComponent<TitanMarker>();
                    if (!marker)
                    {
                        body.master.gameObject.AddComponent<TitanMarker>();
                        EliteManager.Instance.QueueTitanSummon(body);
                    }
                }
            }
            else if (buff == DiabloidElites.BuffOf(EliteKind.Clockmaker))
            {
                if (!body.GetComponent<ClockAuraVisual>()) body.gameObject.AddComponent<ClockAuraVisual>();
            }
        }

        /// <summary>
        /// Замедление часовщика. В формуле R2API замедления стоят в ЗНАМЕНАТЕЛЕ:
        /// скорость = ... / (1 + reductionMultAdd). Чтобы срезать скорость на долю s,
        /// нужно добавить s / (1 − s): для 50% это ровно 1.
        /// Считается на каждой машине — дебафф синхронизирует игра, поэтому и клиент тормозит честно.
        /// </summary>
        private static void OnGetStatCoefficients(CharacterBody body, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!body) return;

            if (body.HasBuff(DiabloidElites.ClockSlowDebuff))
            {
                args.moveSpeedReductionMultAdd += SlowToReduction(DiabloidConfig.ClockmakerMoveSlowPercent.Value);
                args.attackSpeedReductionMultAdd += SlowToReduction(DiabloidConfig.ClockmakerAttackSlowPercent.Value);
            }

            // Множители здоровья меньше 1: директор их сделать не может, режем здесь.
            // Работает для любой нашей элитки — поставь силе 0.1 в конфиге, и получишь старое "стекло".
            foreach (var kind in AllKinds)
            {
                float health = DiabloidConfig.Get(kind).HealthMultiplier.Value;
                if (health < 1f && DiabloidElites.Is(body, kind))
                    args.healthTotalMult *= health;
            }
        }

        private static float SlowToReduction(float slowPercent)
        {
            float s = slowPercent / 100f;
            if (s <= 0f) return 0f;
            if (s > 0.95f) s = 0.95f;
            return s / (1f - s);
        }
    }
}
