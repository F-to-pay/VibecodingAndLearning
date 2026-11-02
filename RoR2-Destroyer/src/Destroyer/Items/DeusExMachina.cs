using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using Destroyer.Content;
using Destroyer.Core;
using R2API;
using RoR2;
using RoR2.CharacterAI;
using UnityEngine;
using UnityEngine.Networking;

namespace Destroyer.Items
{
    /// <summary>
    /// Deus Ex Machina (лунный с 0.9.0, был красный). Полная копия тебя (+1 копия за каждую копию предмета) со всеми твоими
    /// предметами. Цена: твои здоровье, урон и регенерация ×0.93 за каждую копию предмета (0.93^n — никогда не ноль; 0.9.1, было 0.9), и копии
    /// получают тот же множитель. Погибшая копия возвращается через 10 с.
    ///
    /// Как ванильные "ИИ-двойники" (Goobo Jr., призрак элиты NetworkedAffixDeathBehaviour.TrySpawnGhostBodyViaMasterSummon,
    /// IL 1.5): ИИ-мастер для тела — MasterCatalog.FindAiMasterIndexForBody (тот же, что у Артефакта Мести), призыв —
    /// MasterSummon с summonerBodyObject = ты, то есть копия — твой миньон, а AIOwnership сам ставит ей leader = ты.
    /// Лоадаут (навыки, скин) — копия твоего. Снаряжения нет.
    ///
    /// Предметы — "живьём", как у турелей Инженера: при каждой смене твоего инвентаря
    /// Inventory.CopyItemsFrom(твой, фильтр) (это CleanInventory + AddItemsFrom); фильтр — ванильный
    /// defaultItemCopyFilterDelegate (выкидывает CannotCopy) и сам Deus Ex Machina (иначе копии плодят копии).
    /// После копирования — +1 MinionLeash (телепорт к хозяину, если отстал; копирование его стирает).
    ///
    /// Следование: у ИИ выживших нет "бежать к лидеру", поэтому ставим в начало списка свой AISkillDriver
    /// (BaseAI.ReplaceSkillDrivers): дальше 30 м от тебя — бежит к тебе спринтом, ближе — обычный бой.
    /// Статы: у хозяина — по числу предметов, у копии — скрытый бафф-метка, стаки = копии хозяина (у копии самого предмета
    /// нет); обоим damageTotalMult, healthTotalMult, regenTotalMult × (1 − 0.07)^n.
    /// Смерть: CharacterMaster.IsDeadAndOutOfLivesServer — Dio's Best Friend копии работает как обычно.
    /// </summary>
    internal sealed class DeusExMachina : ItemBase
    {
        public override string Id => "DEUS_EX_MACHINA";
        public override ItemTier Tier => ItemTier.Lunar;
        public override string ModelFrom => "ExtraLife"; // Dio's Best Friend
        public override ItemTag[] Tags => new[] { ItemTag.Utility, ItemTag.CannotCopy, ItemTag.AIBlacklist };

        public override Words Name => new Words("Deus Ex Machina", "Deus Ex Machina");

        public override Words Pickup => new Words(
            "Summon a full copy of yourself with all your items... <style=cDeath>but you and your copies are weaker.</style>",
            "Призывает полную копию тебя со всеми предметами... <style=cDeath>но ты и копии слабее.</style>");

        public override Words Description => new Words(
            $"Summon {Tx.Utility("1")} {Tx.Stack("+1 per stack")} {Tx.Utility("full copy of yourself")} with {Tx.Utility("all your items")}. " +
            $"A fallen copy returns after {Tx.Utility(Tx.Seconds(respawn.Value).En)}. " +
            $"{Tx.Health("Your health, damage and health regeneration are reduced by " + Tx.Pct(penalty.Value))} {Tx.Stack("×" + Tx.Num(1f - penalty.Value) + " per stack")}" +
            $"{Tx.Health(", and so are your copies'.")}",
            $"Призывает {Tx.Utility("1")} {Tx.Stack("+1 за копию")} {Tx.Utility("полную копию тебя")} со {Tx.Utility("всеми твоими предметами")}. " +
            $"Погибшая копия возвращается через {Tx.Utility(Tx.Seconds(respawn.Value).Ru)}. " +
            $"{Tx.Health("Твои здоровье, урон и регенерация ниже на " + Tx.Pct(penalty.Value))} {Tx.Stack("×" + Tx.Num(1f - penalty.Value) + " за копию")}" +
            $"{Tx.Health(" — и у копий тоже.")}");

        public override Words Lore => new Words(
            "When the plot needs saving, it shows up. It looks a lot like you.",
            "Когда сюжет пора спасать, оно появляется. Подозрительно похожее на тебя.");

        private ConfigEntry<float> penalty, respawn, followDistance;

        protected override void BindConfig(ItemSettings s)
        {
            penalty = s.Float("Penalty", 0.07f, 0f, 0.9f, new Words("Stat penalty per stack", "Штраф за копию"),
                new Words("Your and your copies' health, damage and regen are multiplied by (1 - this) per copy of the item. 0.07: ×0.93 → ×0.865 → ×0.804…",
                          "Здоровье, урон и реген у тебя и копий умножаются на (1 - это) за каждую копию предмета. 0.07: ×0.93 → ×0.865 → ×0.804…"), "{0:P0}");
            respawn = s.Float("RespawnTime", 10f, 1f, 600f, new Words("Respawn time", "Возрождение"),
                new Words("Seconds before a fallen copy returns.", "Через сколько секунд возвращается погибшая копия."), "{0:0} s");
            followDistance = s.Float("FollowDistance", 30f, 5f, 200f, new Words("Follow distance", "Дистанция следования"),
                new Words("Farther than this from you, a copy stops fighting and runs back, meters.",
                          "Дальше этого от тебя копия бросает бой и бежит к тебе, м."), "{0:0} m");
        }

        private BuffDef marker;
        private float MultFor(int n) => Mathf.Pow(1f - Mathf.Clamp(penalty.Value, 0f, 0.99f), n);

        protected override void Hook()
        {
            marker = BuffFactory.Create("DEUS_CLONE", BuffFactory.Hex("#ffd84d"), isDebuff: false, canStack: true);
            marker.isHidden = true;

            RecalculateStatsAPI.GetStatCoefficients += (body, args) =>
            {
                int n = marker ? body.GetBuffCount(marker) : 0; // копия: стаки = копии хозяина
                if (n <= 0) n = Count(body);                       // хозяин: свои копии предмета
                if (n <= 0) return;
                float k = MultFor(n);
                args.damageTotalMult *= k;
                args.healthTotalMult *= k;
                args.regenTotalMult *= k;
            };

            RoR2Application.onFixedUpdate += ServerTick;
            CharacterBody.onBodyInventoryChangedGlobal += OnInventoryChanged;
            Stage.onServerStageBegin += stage => owners.Clear();
            Run.onRunStartGlobal += run => owners.Clear();
        }

        private bool Filter(ItemIndex index) =>
            Inventory.defaultItemCopyFilterDelegate(index) && (Def == null || index != Def.itemIndex);

        // ---------- Сервер ----------

        private sealed class OwnerState
        {
            public readonly List<CharacterMaster> Clones = new List<CharacterMaster>();
            public readonly List<float> Respawns = new List<float>(); // оставшееся время до возврата погибших
        }

        private sealed class CloneInfo : MonoBehaviour
        {
            public CharacterMaster Owner;
        }

        private readonly Dictionary<CharacterMaster, OwnerState> owners = new Dictionary<CharacterMaster, OwnerState>();
        private readonly List<CharacterMaster> stale = new List<CharacterMaster>();
        private readonly List<CharacterBody> bodies = new List<CharacterBody>();
        private const float TickInterval = 0.25f;
        private float tickTimer;

        private void ServerTick()
        {
            if (!NetworkServer.active || !Run.instance || marker == null) return;
            tickTimer -= Time.fixedDeltaTime;
            if (tickTimer > 0f) return;
            tickTimer = TickInterval;

            // Хозяин пропал или выбросил предмет — копии долой.
            stale.Clear();
            foreach (var pair in owners)
                if (!pair.Key || Count(pair.Key) <= 0) stale.Add(pair.Key);
            foreach (var master in stale)
            {
                if (owners.TryGetValue(master, out var gone))
                    foreach (var clone in gone.Clones)
                        if (clone) clone.TrueKill();
                owners.Remove(master);
            }

            // Снимок: MasterSummon.Perform сразу добавляет тело в readOnlyInstancesList (грабли шариков 0.5.0).
            bodies.Clear();
            bodies.AddRange(CharacterBody.readOnlyInstancesList);
            foreach (var body in bodies)
            {
                if (!body || !body.master || !body.healthComponent || !body.healthComponent.alive) continue;
                if (body.HasBuff(marker)) continue; // копия копий не призывает
                int n = Count(body);
                if (n <= 0) continue;

                if (!owners.TryGetValue(body.master, out var state))
                {
                    state = new OwnerState();
                    owners[body.master] = state;
                }

                // Погибшие (без жизней) — в очередь на возврат.
                for (int i = state.Clones.Count - 1; i >= 0; i--)
                {
                    var clone = state.Clones[i];
                    if (clone && !clone.IsDeadAndOutOfLivesServer()) continue;
                    state.Clones.RemoveAt(i);
                    state.Respawns.Add(respawn.Value);
                }

                // Таймеры возврата.
                for (int i = state.Respawns.Count - 1; i >= 0; i--)
                {
                    state.Respawns[i] -= TickInterval;
                    if (state.Respawns[i] > 0f) continue;
                    state.Respawns.RemoveAt(i);
                    var back = Summon(body, n);
                    if (back) state.Clones.Add(back);
                    else state.Respawns.Add(respawn.Value); // не вышло — попробуем позже
                }

                // Копий предмета стало больше — новые сразу; меньше — лишние уходят (сначала из очереди).
                while (state.Clones.Count + state.Respawns.Count < n)
                {
                    var fresh = Summon(body, n);
                    if (!fresh) break;
                    state.Clones.Add(fresh);
                }
                while (state.Clones.Count + state.Respawns.Count > n)
                {
                    if (state.Respawns.Count > 0) { state.Respawns.RemoveAt(state.Respawns.Count - 1); continue; }
                    var extra = state.Clones[state.Clones.Count - 1];
                    state.Clones.RemoveAt(state.Clones.Count - 1);
                    if (extra) extra.TrueKill();
                }

                // Метка-статы: держим стаки = копии хозяина (после Dio у копии новое тело без баффа).
                foreach (var clone in state.Clones)
                {
                    var cloneBody = clone ? clone.GetBody() : null;
                    if (cloneBody && cloneBody.GetBuffCount(marker) != n) cloneBody.SetBuffCount(marker.buffIndex, n);
                }
            }
        }

        private bool warnedNoAi;

        private CharacterMaster Summon(CharacterBody owner, int n)
        {
            var aiIndex = MasterCatalog.FindAiMasterIndexForBody(owner.bodyIndex);
            var prefab = MasterCatalog.GetMasterPrefab(aiIndex);
            if (!prefab)
            {
                if (!warnedNoAi) DestroyerPlugin.Log.LogWarning($"Deus Ex Machina: no AI master for body '{owner.name}', no copy.");
                warnedNoAi = true;
                return null;
            }

            Loadout loadout = null;
            if (owner.master.loadout != null)
            {
                loadout = new Loadout();
                owner.master.loadout.Copy(loadout);
            }

            var ownerInventory = owner.inventory;
            Vector2 circle = Random.insideUnitCircle.normalized * 3f;
            var master = new MasterSummon
            {
                masterPrefab = prefab,
                position = owner.footPosition + new Vector3(circle.x, 0.5f, circle.y),
                rotation = Quaternion.identity,
                summonerBodyObject = owner.gameObject,
                teamIndexOverride = owner.teamComponent ? owner.teamComponent.teamIndex : TeamIndex.Player,
                ignoreTeamMemberLimit = true,
                loadout = loadout,
                preSpawnSetupCallback = m => CopyItems(m, ownerInventory), // до появления тела — сразу с предметами
            }.Perform();
            if (!master) return null;

            master.gameObject.AddComponent<CloneInfo>().Owner = owner.master;
            AddFollowDriver(master);

            var body = master.GetBody();
            if (body) body.SetBuffCount(marker.buffIndex, n);
            return master;
        }

        private void CopyItems(CharacterMaster clone, Inventory source)
        {
            if (!clone || !clone.inventory || !source) return;
            clone.inventory.CopyItemsFrom(source, Filter);
            if (RoR2Content.Items.MinionLeash) clone.inventory.GiveItemPermanent(RoR2Content.Items.MinionLeash, 1);
        }

        private void AddFollowDriver(CharacterMaster master)
        {
            var ai = master.GetComponent<BaseAI>();
            if (!ai) return;
            var follow = master.gameObject.AddComponent<AISkillDriver>();
            follow.customName = "DestroyerDeusFollowLeader";
            follow.skillSlot = SkillSlot.None;
            follow.requireSkillReady = false;
            follow.moveTargetType = AISkillDriver.TargetType.CurrentLeader;
            follow.minDistance = followDistance.Value;
            follow.maxDistance = float.PositiveInfinity;
            follow.movementType = AISkillDriver.MovementType.ChaseMoveTarget;
            follow.aimType = AISkillDriver.AimType.MoveDirection;
            follow.moveInputScale = 1f;
            follow.shouldSprint = true;
            follow.ignoreNodeGraph = false;
            follow.minUserHealthFraction = float.NegativeInfinity;
            follow.maxUserHealthFraction = float.PositiveInfinity;
            follow.minTargetHealthFraction = float.NegativeInfinity;
            follow.maxTargetHealthFraction = float.PositiveInfinity;
            follow.minUserTimeSinceDamaged = float.NegativeInfinity;
            follow.maxUserTimeSinceDamaged = float.PositiveInfinity;
            follow.maxTimesSelected = -1;
            follow.driverUpdateTimerOverride = -1f;
            var drivers = ai.skillDrivers ?? new AISkillDriver[0];
            ai.ReplaceSkillDrivers(new[] { follow }.Concat(drivers.Where(d => d && d != follow)).ToArray());
        }

        /// <summary>Сервер: твой инвентарь сменился — копиям то же самое.</summary>
        private void OnInventoryChanged(CharacterBody body)
        {
            if (!NetworkServer.active || !body || !body.master || !body.inventory) return;
            if (!owners.TryGetValue(body.master, out var state)) return;
            foreach (var clone in state.Clones)
                if (clone) CopyItems(clone, body.inventory);
        }
    }
}
