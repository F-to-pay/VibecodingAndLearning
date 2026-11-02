using System.Collections.Generic;
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
    /// Пешка (белый). Все союзники-неигроки твоей команды (дроны, турели, миньоны, копии Deus, шарики — и чужие тоже)
    /// получают +7% (+7%) здоровья и +3 (+3) регена вне опасности.
    ///
    /// Сервер 2 раза в секунду: по каждой команде — максимум копий у носителей в ней (несколько пешек не складываются),
    /// союзникам (!isPlayerControlled) — стаки баффа PAWN_GUARD = это число. Статы — RecalculateStatsAPI на каждой машине.
    /// "Вне боя" — outOfDanger (7 с без полученного урона, как у Cautious Slug): игра сама пересчитывает статы при смене
    /// (CharacterBody.OnOutOfDangerChanged), а outOfCombat у дронов не бывает — они всё время стреляют.
    /// Реген растёт с уровнем, как ванильный: baseRegenAdd 3·n, levelRegenAdd 0.6·n (+20% за уровень).
    /// </summary>
    internal sealed class Pawn : ItemBase
    {
        public override string Id => "PAWN";
        public override ItemTier Tier => ItemTier.Tier1;
        public override string ModelFrom => "SecondarySkillMagazine"; // Backup Magazine — мелкая фигурка
        public override ItemTag[] Tags => new[] { ItemTag.Utility, ItemTag.Healing };

        public override Words Name => new Words("Pawn", "Пешка");

        public override Words Pickup => new Words(
            "Allied units have more health and regenerate out of danger.",
            "Союзные юниты крепче и восстанавливаются вне опасности.");

        public override Words Description => new Words(
            $"Allied non-player units have {Tx.Healing("+" + Tx.Pct(health.Value) + " health")} {Tx.Stack("+" + Tx.Pct(health.Value) + " per stack")} " +
            $"and {Tx.Healing("+" + Tx.Num(regen.Value) + " hp/s regeneration")} {Tx.Stack("+" + Tx.Num(regen.Value) + " hp/s per stack")} while out of danger.",
            $"Союзные юниты (не игроки) получают {Tx.Healing("+" + Tx.Pct(health.Value) + " здоровья")} {Tx.Stack("+" + Tx.Pct(health.Value) + " за копию")} " +
            $"и {Tx.Healing("+" + Tx.Num(regen.Value) + " ОЗ/с регенерации")} {Tx.Stack("+" + Tx.Num(regen.Value) + " ОЗ/с за копию")} вне опасности.");

        public override Words Lore => new Words("Every king needs someone in front.", "Каждому королю нужен кто-то впереди.");

        private ConfigEntry<float> health, regen;

        protected override void BindConfig(ItemSettings s)
        {
            health = s.Float("HealthBonus", 0.07f, 0f, 5f, new Words("Ally health per stack", "Здоровье союзников за копию"),
                new Words("Health bonus for allied units per copy (0.07 = +7%).", "Бонус здоровья союзных юнитов за копию (0.07 = +7%)."), "{0:P0}");
            regen = s.Float("Regen", 3f, 0f, 200f, new Words("Ally regen per stack", "Реген союзников за копию"),
                new Words("Out-of-danger regeneration per copy at level 1, hp/s (grows 20% per level like vanilla regen).",
                          "Реген вне опасности за копию на 1-м уровне, ОЗ/с (растёт на 20% за уровень, как ванильный)."), "{0:0.#}");
        }

        private BuffDef guard;
        private readonly Dictionary<TeamIndex, int> best = new Dictionary<TeamIndex, int>();
        private readonly Dictionary<CharacterBody, int> applied = new Dictionary<CharacterBody, int>();
        private readonly List<CharacterBody> stale = new List<CharacterBody>();
        private float tickTimer;

        protected override void Hook()
        {
            guard = BuffFactory.Create("PAWN_GUARD", BuffFactory.Hex("#e8e0c8"), isDebuff: false, canStack: true);

            RecalculateStatsAPI.GetStatCoefficients += (body, args) =>
            {
                int n = guard ? body.GetBuffCount(guard) : 0;
                if (n <= 0) return;
                args.healthMultAdd += health.Value * n;
                if (body.outOfDanger)
                {
                    args.baseRegenAdd += regen.Value * n;
                    args.levelRegenAdd += regen.Value * 0.2f * n;
                }
            };

            RoR2Application.onFixedUpdate += Tick;
            Run.onRunStartGlobal += run => applied.Clear();
        }

        private void Tick()
        {
            if (!NetworkServer.active || !Run.instance || guard == null) return;
            tickTimer -= Time.fixedDeltaTime;
            if (tickTimer > 0f) return;
            tickTimer = 0.5f;

            best.Clear();
            foreach (var body in CharacterBody.readOnlyInstancesList)
            {
                if (!body || !body.teamComponent || !body.healthComponent || !body.healthComponent.alive) continue;
                int n = Count(body);
                if (n <= 0) continue;
                var team = body.teamComponent.teamIndex;
                if (!best.TryGetValue(team, out int have) || have < n) best[team] = n;
            }

            foreach (var body in CharacterBody.readOnlyInstancesList)
            {
                if (!body || body.isPlayerControlled || !body.teamComponent) continue;
                best.TryGetValue(body.teamComponent.teamIndex, out int n);
                applied.TryGetValue(body, out int had);
                if (n == had) continue;
                body.SetBuffCount(guard.buffIndex, n);
                if (n > 0) applied[body] = n; else applied.Remove(body);
            }

            stale.Clear();
            foreach (var key in applied.Keys) if (!key) stale.Add(key);
            foreach (var key in stale) applied.Remove(key);
        }
    }
}
