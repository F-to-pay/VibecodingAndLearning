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
    /// Шарики (зелёный; был красным до 0.6.0). Пять маленьких цветных зондов Солус (Solus Probe, RoboBallMiniMaster) за тобой:
    /// в 4 раза меньше, 20% (+10%) урона и 80% (+20%) здоровья обычного зонда. Погибшие возвращаются: +1 раз в 10 с,
    /// пока их меньше 5. Получают предметы дронов хозяина: Spare Drone Parts и Drones Drop Dynamite.
    ///
    /// Сервер: призыв ванильным MasterSummon (как у Diabloid, проверено в игре), +1 MinionLeash (телепорт к хозяину, если
    /// отстали), флаг Mechanical на теле — для батареек и ванильных "механических" проверок. Метка — скрытый бафф,
    /// число стаков = копии у хозяина: из него на каждой машине считаются статы (RecalculateStatsAPI *TotalMult), и по нему
    /// же на каждой машине вешается визуал: масштаб модели (каждый кадр, как ModelScaler в Diabloid) и яркий цвет.
    ///
    /// Предметы дронов (IL 1.5): ванильные DroneWeaponsBehavior / DronesDropDynamiteBehaviour раздают механическим миньонам
    /// DroneWeaponsBoost / DroneDynamiteDisplay ровно по числу копий хозяина. При MasterSummon тело зонда часто ещё не
    /// появилось и ваниль его пропускает — поэтому раздаём сами тем же способом (ровно по числу, без накопления).
    /// </summary>
    internal sealed class Balloons : ItemBase
    {
        public override string Id => "BALLOONS";
        public override ItemTier Tier => ItemTier.Tier2;
        public override string ModelFrom => "RoboBallBuddy"; // Empathy Cores — шарики Солус
        public override ItemTag[] Tags => new[] { ItemTag.Utility, ItemTag.AIBlacklist, ItemTag.CannotCopy };

        private const string ProbeMaster = "RoboBallMiniMaster";

        public override Words Name => new Words("Balloons", "Шарики");

        public override Words Pickup => new Words(
            "A swarm of tiny colorful Solus Probes fights for you.",
            "За тобой летает рой маленьких цветных зондов Солус.");

        public override Words Description => new Words(
            $"Summon {Tx.Utility(count.Value + " tiny Solus Probes")} with {Tx.Damage(Tx.Pct(damage.Value) + " damage")} {Tx.Stack("+" + Tx.Pct(damagePerStack.Value) + " per stack")} " +
            $"and {Tx.Healing(Tx.Pct(health.Value) + " health")} {Tx.Stack("+" + Tx.Pct(healthPerStack.Value) + " per stack")} of a normal probe. " +
            $"A lost probe returns every {Tx.Utility(Tx.Seconds(interval.Value).En)}. Probes {Tx.Utility("get your drone items")}.",
            $"Призывает {Tx.Utility(count.Value + " крошечных зондов Солус")}: {Tx.Damage(Tx.Pct(damage.Value) + " урона")} {Tx.Stack("+" + Tx.Pct(damagePerStack.Value) + " за копию")} " +
            $"и {Tx.Healing(Tx.Pct(health.Value) + " здоровья")} {Tx.Stack("+" + Tx.Pct(healthPerStack.Value) + " за копию")} обычного зонда. " +
            $"Погибший возвращается раз в {Tx.Utility(Tx.Seconds(interval.Value).Ru)}. Зонды {Tx.Utility("получают твои предметы для дронов")}.");

        public override Words Lore => new Words(
            "Happy birthday. Please do not let them near the cake.",
            "С днём рождения. Только к торту их не подпускай.");

        private ConfigEntry<int> count;
        private ConfigEntry<float> interval, damage, damagePerStack, health, healthPerStack, scale;
        private ConfigEntry<bool> colorize;

        protected override void BindConfig(ItemSettings s)
        {
            count = s.Int("Count", 5, 1, 20, new Words("Probes", "Зондов"),
                new Words("How many probes fly with you.", "Сколько зондов летает с тобой."));
            interval = s.Float("RespawnInterval", 10f, 1f, 120f, new Words("Respawn interval", "Интервал возврата"),
                new Words("Every this many seconds one lost probe comes back.", "Раз в столько секунд возвращается один погибший зонд."), "{0:0.#} s");
            damage = s.Float("Damage", 0.2f, 0f, 5f, new Words("Damage", "Урон"),
                new Words("Damage of a normal probe with the first copy (0.2 = 20%).", "Урон от обычного зонда с первой копией (0.2 = 20%)."), "{0:P0}");
            damagePerStack = s.Float("DamagePerStack", 0.1f, 0f, 5f, new Words("Damage per stack", "Урон за копию"),
                new Words("Extra damage share per additional copy.", "Прибавка за каждую копию сверх первой."), "{0:P0}");
            health = s.Float("Health", 0.8f, 0.05f, 10f, new Words("Health", "Здоровье"),
                new Words("Health of a normal probe with the first copy (0.8 = 80%).", "Здоровье от обычного зонда с первой копией (0.8 = 80%)."), "{0:P0}");
            healthPerStack = s.Float("HealthPerStack", 0.2f, 0f, 10f, new Words("Health per stack", "Здоровье за копию"),
                new Words("Extra health share per additional copy.", "Прибавка за каждую копию сверх первой."), "{0:P0}");
            scale = s.Float("Scale", 0.25f, 0.05f, 2f, new Words("Size", "Размер"),
                new Words("Model size compared to a normal probe (0.25 = 4 times smaller).", "Размер модели от обычного зонда (0.25 — в 4 раза меньше)."), "{0:0.##}");
            colorize = s.Bool("Colorize", true, new Words("Bright colors", "Яркие цвета"),
                new Words("Paint the probes in bright colors. Client-side.", "Красить зонды в яркие цвета. Настройка своя у каждого игрока."));
        }

        private BuffDef marker;
        internal static Balloons Instance;

        protected override void Hook()
        {
            Instance = this;
            marker = BuffFactory.Create("BALLOON", BuffFactory.Hex("#ff6fb5"), isDebuff: false, canStack: true);
            marker.isHidden = true;

            RecalculateStatsAPI.GetStatCoefficients += (body, args) =>
            {
                int n = body.GetBuffCount(marker);
                if (n <= 0) return;
                args.damageTotalMult *= Stacking.Linear(n, damage.Value, damagePerStack.Value);
                args.healthTotalMult *= Stacking.Linear(n, health.Value, healthPerStack.Value);
            };

            RoR2Application.onFixedUpdate += ServerTick;
            RoR2Application.onFixedUpdate += VisualTick;
            CharacterBody.onBodyInventoryChangedGlobal += OnOwnerInventoryChanged;
            Stage.onServerStageBegin += stage => owners.Clear();
            Run.onRunStartGlobal += run => owners.Clear();
        }

        // ---------- Сервер: призыв и возврат ----------

        private sealed class OwnerState
        {
            public readonly List<CharacterMaster> Probes = new List<CharacterMaster>();
            public float Timer;
            public bool Filled; // первый раз — сразу все
        }

        private readonly Dictionary<CharacterMaster, OwnerState> owners = new Dictionary<CharacterMaster, OwnerState>();
        private readonly List<CharacterMaster> stale = new List<CharacterMaster>();
        private readonly List<CharacterBody> bodies = new List<CharacterBody>();

        private void ServerTick()
        {
            if (!NetworkServer.active || !Run.instance) return;
            float dt = Time.fixedDeltaTime;

            // Хозяева без предмета — зонды долой.
            stale.Clear();
            foreach (var pair in owners)
                if (!pair.Key || Count(pair.Key) <= 0) stale.Add(pair.Key);
            foreach (var master in stale)
            {
                if (owners.TryGetValue(master, out var gone))
                    foreach (var probe in gone.Probes)
                        if (probe) probe.TrueKill();
                owners.Remove(master);
            }

            // Снимок списка: MasterSummon.Perform создаёт тело зонда СРАЗУ, и оно добавляется в readOnlyInstancesList
            // прямо во время обхода → InvalidOperationException (0.5.0, лог пользователя).
            bodies.Clear();
            bodies.AddRange(CharacterBody.readOnlyInstancesList);
            foreach (var body in bodies)
            {
                if (!body || !body.master || !body.healthComponent || !body.healthComponent.alive) continue;
                if (Count(body) <= 0) continue;
                // Сами зонды предмет не получают (CannotCopy), но на всякий случай: зонд не призывает зондов.
                if (body.HasBuff(marker)) continue;

                if (!owners.TryGetValue(body.master, out var state))
                {
                    state = new OwnerState();
                    owners[body.master] = state;
                }

                // Погибший зонд: мастер уничтожен (ИИ-мастера удаляются после смерти тела) или тела нет дольше 3 с после призыва.
                state.Probes.RemoveAll(p => !p || (!p.GetBody() && Time.time - SpawnTime(p) > 3f));

                int wanted = count.Value;
                if (state.Probes.Count >= wanted) { state.Timer = interval.Value; continue; }

                if (!state.Filled)
                {
                    while (state.Probes.Count < wanted)
                    {
                        var probe = Summon(body);
                        if (!probe) break;
                        state.Probes.Add(probe);
                    }
                    state.Filled = true;
                    state.Timer = interval.Value;
                    continue;
                }

                state.Timer -= dt;
                if (state.Timer > 0f) continue;
                state.Timer = interval.Value;
                var next = Summon(body);
                if (next) state.Probes.Add(next);
            }
        }

        private static float SpawnTime(CharacterMaster probe)
        {
            var info = probe.GetComponent<ProbeInfo>();
            return info ? info.SpawnTime : 0f;
        }

        private sealed class ProbeInfo : MonoBehaviour
        {
            public float SpawnTime;
            public CharacterMaster Owner;
        }

        private CharacterMaster Summon(CharacterBody owner)
        {
            var prefab = MasterCatalog.FindMasterPrefab(ProbeMaster);
            if (!prefab)
            {
                DestroyerPlugin.Log.LogWarning($"Balloons: master prefab '{ProbeMaster}' not found.");
                return null;
            }

            Vector3 offset = Random.insideUnitSphere * 2f;
            offset.y = Mathf.Abs(offset.y) + 1.5f;
            var master = new MasterSummon
            {
                masterPrefab = prefab,
                position = owner.corePosition + offset,
                rotation = Quaternion.identity,
                summonerBodyObject = owner.gameObject,
                teamIndexOverride = owner.teamComponent ? owner.teamComponent.teamIndex : TeamIndex.Player,
                ignoreTeamMemberLimit = true,
            }.Perform();
            if (!master) return null;

            var info = master.gameObject.AddComponent<ProbeInfo>();
            info.SpawnTime = Time.time;
            info.Owner = owner.master;

            if (master.inventory && RoR2Content.Items.MinionLeash)
                master.inventory.GiveItemPermanent(RoR2Content.Items.MinionLeash, 1);

            var body = master.GetBody();
            if (body) SetUpProbe(body, owner.master);
            else
            {
                System.Action<CharacterBody> handler = null;
                handler = b =>
                {
                    master.onBodyStart -= handler;
                    SetUpProbe(b, info.Owner);
                };
                master.onBodyStart += handler;
            }
            return master;
        }

        private void SetUpProbe(CharacterBody probe, CharacterMaster owner)
        {
            if (!probe || !owner) return;
            probe.bodyFlags |= CharacterBody.BodyFlags.Mechanical;
            probe.SetBuffCount(marker.buffIndex, Mathf.Max(1, Count(owner)));
            SyncDroneItems(owner, probe.master);
        }

        /// <summary>Копии у хозяина поменялись — пересчитать стаки метки и предметы дронов у его зондов.</summary>
        private void OnOwnerInventoryChanged(CharacterBody body)
        {
            if (!NetworkServer.active || !body || !body.master) return;
            if (!owners.TryGetValue(body.master, out var state)) return;
            int n = Count(body);
            foreach (var probe in state.Probes)
            {
                var probeBody = probe ? probe.GetBody() : null;
                if (!probeBody) continue;
                if (n > 0 && probeBody.GetBuffCount(marker) != n) probeBody.SetBuffCount(marker.buffIndex, n);
                SyncDroneItems(body.master, probe);
            }
        }

        private static void SyncDroneItems(CharacterMaster owner, CharacterMaster probe)
        {
            var from = owner ? owner.inventory : null;
            var to = probe ? probe.inventory : null;
            if (!from || !to) return;
            SetCount(to, DLC1Content.Items.DroneWeaponsBoost, Have(from, DLC1Content.Items.DroneWeapons));
            SetCount(to, DLC3Content.Items.DroneDynamiteDisplay, Have(from, DLC3Content.Items.DronesDropDynamite));
        }

        private static int Have(Inventory inventory, ItemDef def) =>
            def && def.itemIndex != ItemIndex.None ? inventory.GetItemCountEffective(def) : 0;

        private static void SetCount(Inventory inventory, ItemDef def, int wanted)
        {
            if (!def || def.itemIndex == ItemIndex.None) return;
            int have = inventory.GetItemCountPermanent(def);
            if (have < wanted) inventory.GiveItemPermanent(def, wanted - have);
            else if (have > wanted) inventory.RemoveItemPermanent(def, have - wanted);
        }

        // ---------- Каждая машина: размер и цвет ----------

        private const float VisualInterval = 0.25f;
        private float visualTimer;

        private void VisualTick()
        {
            if (!Run.instance || marker == null) return;
            visualTimer -= Time.fixedDeltaTime;
            if (visualTimer > 0f) return;
            visualTimer = VisualInterval;
            foreach (var body in CharacterBody.readOnlyInstancesList)
                if (body && body.HasBuff(marker) && !body.GetComponent<BalloonVisual>())
                    body.gameObject.AddComponent<BalloonVisual>();
        }

        internal float Scale => scale.Value;
        internal bool Colorize => colorize.Value;

        /// <summary>Держит масштаб модели каждый кадр (анимации появления его сбрасывают) и один раз красит материалы.</summary>
        private sealed class BalloonVisual : MonoBehaviour
        {
            private static readonly Color[] Palette =
            {
                new Color(1f, 0.25f, 0.55f), new Color(0.2f, 0.85f, 1f), new Color(0.55f, 1f, 0.2f),
                new Color(1f, 0.85f, 0.15f), new Color(1f, 0.5f, 0.1f), new Color(0.7f, 0.35f, 1f), new Color(1f, 0.2f, 0.2f),
            };

            private CharacterBody body;
            private Transform model;
            private Vector3 originalScale;
            private bool painted;
            private float paintDelay = 0.5f;

            private void Awake() => body = GetComponent<CharacterBody>();

            private void LateUpdate()
            {
                var item = Instance;
                var current = body && body.modelLocator ? body.modelLocator.modelTransform : null;
                if (item == null || !current) return;
                if (current != model)
                {
                    model = current;
                    originalScale = model.localScale;
                    painted = false;
                }
                var wanted = originalScale * item.Scale;
                if (model.localScale != wanted) model.localScale = wanted;

                // Красим с задержкой: материалы модели в 1.5 могут подгрузиться (Addressables) уже после появления тела.
                if (painted || !item.Colorize) return;
                paintDelay -= Time.deltaTime;
                if (paintDelay > 0f) return;
                painted = true;
                Paint(model, Palette[(int)(body.netId.Value % (uint)Palette.Length)]);
            }

            private static void Paint(Transform model, Color color)
            {
                var characterModel = model.GetComponent<CharacterModel>();
                if (characterModel && characterModel.baseRendererInfos != null)
                {
                    var infos = characterModel.baseRendererInfos;
                    for (int i = 0; i < infos.Length; i++)
                    {
                        var info = infos[i];
                        var source = info.defaultMaterial ? info.defaultMaterial : (info.renderer ? info.renderer.sharedMaterial : null);
                        if (!source) continue;
                        var material = new Material(source);
                        Tint(material, color);
                        info.defaultMaterial = material; // CharacterModel сам ставит defaultMaterial рендереру
                        infos[i] = info;
                        if (info.renderer) info.renderer.sharedMaterial = material;
                    }
                    return;
                }
                foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                    if (renderer) Tint(renderer.material, color);
            }

            private static void Tint(Material material, Color color)
            {
                if (material.HasProperty("_Color")) material.SetColor("_Color", color);
                if (material.HasProperty("_TintColor")) material.SetColor("_TintColor", color);
            }
        }
    }
}
