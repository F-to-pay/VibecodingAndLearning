using System;
using System.Collections.Generic;
using Diabloid.Loot;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace Diabloid.Gameplay
{
    /// <summary>
    /// Банан (0.18.0) — сделка на весь забег:
    ///   +8 кармы (ванильная механика из обновления Hallowed Concepts: усложняет только Митрикса). Если карма
    ///   в лобби выключена — банан её включает;
    ///   остальные финальные боссы (Войдлинг, Ложный сын, Сердце Солуса, финальное Эхо Провидения) +50% скорости атаки;
    ///   все остальные перки разом — это делает GearSet.CollectPerks/HasPerk, тут ничего не нужно.
    /// 0.18.1: всё это — только у кого надето 3+ банана; меньше — банан не даёт ничего. Описание перка нарочно
    /// говорит лишь «+1 Банан. 3 Банана дает преимущество...».
    /// Сработал один раз — действует до конца забега, даже если бананы снять (карма и так не убывает).
    /// </summary>
    internal sealed partial class PerkManager
    {
        /// <summary>Метка финального босса в банановом забеге: +50% скорости атаки. Бафф — чтобы клиенты считали так же.</summary>
        public static BuffDef BananaBossBuff { get; private set; }

        // Финальные боссы, кроме Митрикса (ему — карма). Имена тел из RoR2.dll 1.5 (Войдлинг — все три фазы).
        private static readonly string[] finalBossBodyPrefixes =
            { "VoidRaidCrabBody", "MiniVoidRaidCrabBody", "FalseSonBossBody", "SolusHeartBody" };
        private const string ProvidenceFinalMasterName = "ProvidenceFinalMaster";

        private static readonly HashSet<BodyIndex> finalBossBodies = new HashSet<BodyIndex>();
        private static MasterCatalog.MasterIndex providenceFinalMaster = MasterCatalog.MasterIndex.none;

        /// <summary>Забег, в котором банан уже сработал (только сервер).</summary>
        private static Run bananaRun;

        private static bool BananaActive => bananaRun && bananaRun == Run.instance;

        private static void RegisterBanana()
        {
            BananaBossBuff = NewBuff("bdDiabloidBananaBoss", new Color32(255, 225, 53, 255), stack: false);
            CharacterBody.onBodyStartGlobal += OnBodyStartBanana;
        }

        private static void OnCatalogsReadyBanana()
        {
            if (RoR2Content.Buffs.WarCryBuff) BananaBossBuff.iconSprite = RoR2Content.Buffs.WarCryBuff.iconSprite;

            finalBossBodies.Clear();
            var found = new List<string>();
            foreach (var prefab in BodyCatalog.allBodyPrefabs)
            {
                if (!prefab) continue;
                foreach (var prefix in finalBossBodyPrefixes)
                {
                    if (!prefab.name.StartsWith(prefix, StringComparison.Ordinal)) continue;
                    var index = BodyCatalog.FindBodyIndex(prefab);
                    if (index != BodyIndex.None && finalBossBodies.Add(index)) found.Add(prefab.name);
                    break;
                }
            }
            providenceFinalMaster = MasterCatalog.FindMasterIndex(ProvidenceFinalMasterName);
            DiabloidPlugin.Log.LogInfo($"Banana: final bosses [{string.Join(", ", found)}], " +
                                       $"{ProvidenceFinalMasterName} {(providenceFinalMaster.isValid ? "found" : "NOT found")}.");
        }

        private static bool IsOtherFinalBoss(CharacterBody body)
        {
            if (!body) return false;
            if (finalBossBodies.Contains(body.bodyIndex)) return true;
            return providenceFinalMaster.isValid && body.master && body.master.masterIndex == providenceFinalMaster;
        }

        /// <summary>Сервер: у кого-то из игроков (или его турели) 3+ банана — сделка заключена.</summary>
        private static void CheckBanana(List<(Perk perk, int level)> perks)
        {
            if (BananaActive || !Run.instance) return;
            int bananas = 0;
            foreach (var (perk, _) in perks)
                if (perk.Type == PerkType.Banana) bananas++; // банан сам себя в набор не добавляет — это шмотки
            if (bananas >= Perks.BananasRequired) ActivateBanana();
        }

        private static void ActivateBanana()
        {
            bananaRun = Run.instance;

            // Карма: ModifyKarma ничего не делает при выключенной карме, поэтому сначала включаем (SyncVar — клиенты узнают).
            // Сумма × множитель кармы из правил и обрезается до максимума (8).
            var karma = KarmaManager.instance;
            if (karma)
            {
                if (!karma.IsKarmaEnabled()) karma.NetworkisKarmaEnabled = true;
                karma.ModifyKarma(KarmaManager.maxKarma);
                DiabloidPlugin.Log.LogInfo($"Banana: karma is now {karma.GetEffectiveKarma()}.");
            }
            else DiabloidPlugin.Log.LogWarning("Banana: no KarmaManager in this run.");

            foreach (var body in CharacterBody.readOnlyInstancesList)
                MarkBananaBoss(body);
        }

        private static void OnBodyStartBanana(CharacterBody body)
        {
            if (NetworkServer.active && BananaActive) MarkBananaBoss(body);
        }

        private static void MarkBananaBoss(CharacterBody body)
        {
            if (IsOtherFinalBoss(body) && !body.HasBuff(BananaBossBuff)) body.AddBuff(BananaBossBuff);
        }

        private static void BananaStats(CharacterBody body, R2API.RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (BananaBossBuff && body.HasBuff(BananaBossBuff))
                args.attackSpeedMultAdd += Perks.BananaBossAttackSpeedPercent / 100f;
        }
    }
}
