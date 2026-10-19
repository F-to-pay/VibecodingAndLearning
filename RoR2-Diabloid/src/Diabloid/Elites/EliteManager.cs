using System.Collections.Generic;
using Diabloid.Config;
using RoR2;
using RoR2.Navigation;
using UnityEngine;
using UnityEngine.Networking;

namespace Diabloid.Elites
{
    /// <summary>
    /// Мозг элиток. Висит на объекте плагина и каждый тик:
    ///   на сервере — высасывание кровопийц, аура часовщиков, призыв копий титанов, долечивание;
    ///   на всех машинах — рисует жгуты кровопийц.
    /// Один общий цикл вместо компонента на каждом монстре: проще и дешевле.
    /// </summary>
    internal sealed class EliteManager : MonoBehaviour
    {
        private const float SlowTickInterval = 0.5f;
        private const float SlowBuffDuration = 1.0f; // > интервала, чтобы дебафф не мигал

        public static EliteManager Instance { get; private set; }

        // Копия списка тел: во время тика тела могут умирать, а менять список, по которому идёт foreach, нельзя.
        private readonly List<CharacterBody> bodies = new List<CharacterBody>();

        private readonly Queue<CharacterBody> pendingTitanSummons = new Queue<CharacterBody>();
        private readonly List<CharacterBody> pendingFullHeals = new List<CharacterBody>();
        private readonly Dictionary<MasterCatalog.MasterIndex, CharacterSpawnCard> copyCards =
            new Dictionary<MasterCatalog.MasterIndex, CharacterSpawnCard>();

        private float drainTimer;
        private float slowTimer;
        private Xoroshiro128Plus rng;

        // Пул линий для жгутов кровопийц.
        private readonly List<LineRenderer> tethers = new List<LineRenderer>();
        private int tethersUsed;

        private void Awake()
        {
            Instance = this;
            rng = new Xoroshiro128Plus((ulong)System.DateTime.Now.Ticks);
        }

        // ---------------------------------------------------------------- очереди (сервер)

        /// <summary>Титан появился — призвать копии на следующем тике (не посреди чужого события).</summary>
        public void QueueTitanSummon(CharacterBody body) => pendingTitanSummons.Enqueue(body);

        /// <summary>
        /// Дать телу полное здоровье на следующем тике. Нужно после выдачи BoostHp:
        /// максимум здоровья вырастает, а текущее — нет, и босс выглядел бы недобитым.
        /// </summary>
        public void QueueFullHeal(CharacterBody body)
        {
            if (body) pendingFullHeals.Add(body);
        }

        private void FixedUpdate()
        {
            if (!NetworkServer.active || !Run.instance) return;

            float dt = Time.fixedDeltaTime;

            ProcessFullHeals();
            while (pendingTitanSummons.Count > 0)
                SummonTitanCopies(pendingTitanSummons.Dequeue());

            bodies.Clear();
            bodies.AddRange(CharacterBody.readOnlyInstancesList);

            drainTimer -= dt;
            if (drainTimer <= 0f)
            {
                float interval = Mathf.Max(0.1f, DiabloidConfig.BloodsuckerTickInterval.Value);
                drainTimer = interval;
                DrainTick(interval);
            }

            slowTimer -= dt;
            if (slowTimer <= 0f)
            {
                slowTimer = SlowTickInterval;
                SlowTick();
            }
        }

        private void ProcessFullHeals()
        {
            if (pendingFullHeals.Count == 0) return;
            foreach (var body in pendingFullHeals)
            {
                if (body && body.healthComponent && body.healthComponent.alive)
                    body.healthComponent.HealFraction(1f, default(ProcChainMask));
            }
            pendingFullHeals.Clear();
        }

        // ---------------------------------------------------------------- кровопийца (сервер)

        private void DrainTick(float interval)
        {
            float radius = DiabloidConfig.BloodsuckerRadius.Value;
            float radiusSqr = radius * radius;
            float damageFraction = DiabloidConfig.BloodsuckerDrainPercent.Value / 100f * interval; // урон за этот тик
            float lifesteal = DiabloidConfig.BloodsuckerLifestealPercent.Value / 100f;
            bool drainMonsters = DiabloidConfig.BloodsuckerDrainsMonsters.Value;

            foreach (var source in bodies)
            {
                if (!IsActiveBloodsucker(source)) continue;

                float damage = source.damage * damageFraction;
                if (damage <= 0f) continue;

                float stolen = 0f;
                foreach (var victim in bodies)
                {
                    if (!IsDrainTarget(source, victim, radiusSqr, drainMonsters)) continue;

                    var health = victim.healthComponent;
                    float before = health.combinedHealth;
                    health.TakeDamage(new DamageInfo
                    {
                        attacker = source.gameObject,
                        inflictor = source.gameObject,
                        damage = damage,
                        position = victim.corePosition,
                        crit = false,
                        procCoefficient = 0f,        // не триггерит предметы — это "аура", а не удар
                        procChainMask = default(ProcChainMask),
                        damageType = DamageType.Generic,
                        damageColorIndex = DamageColorIndex.Bleed,
                        force = Vector3.zero,
                    });
                    stolen += Mathf.Max(0f, before - health.combinedHealth);
                }

                if (stolen > 0f && lifesteal > 0f)
                    source.healthComponent.Heal(stolen * lifesteal, default(ProcChainMask), true);
            }
        }

        /// <summary>Кровопийца "включён", только когда он ранен — как дюноход во 2-й фазе.</summary>
        private static bool IsActiveBloodsucker(CharacterBody body)
        {
            if (!DiabloidElites.Is(body, EliteKind.Bloodsucker)) return false;
            var health = body.healthComponent;
            return health && health.alive && health.health < health.fullHealth;
        }

        private static bool IsDrainTarget(CharacterBody source, CharacterBody victim, float radiusSqr, bool drainMonsters)
        {
            if (!victim || victim == source) return false;
            if (DiabloidElites.Is(victim, EliteKind.Bloodsucker)) return false; // свои не трогают своих

            var health = victim.healthComponent;
            if (!health || !health.alive) return false;

            var team = TeamOf(victim);
            if (team == TeamIndex.Neutral || team == TeamIndex.None) return false; // горшки и прочий реквизит
            if (!drainMonsters && team == TeamOf(source)) return false;

            return (victim.corePosition - source.corePosition).sqrMagnitude <= radiusSqr;
        }

        private static TeamIndex TeamOf(CharacterBody body) =>
            body.teamComponent ? body.teamComponent.teamIndex : TeamIndex.None;

        // ---------------------------------------------------------------- часовщик (сервер)

        private void SlowTick()
        {
            var debuff = DiabloidElites.ClockSlowDebuff;
            float radius = DiabloidConfig.ClockmakerRadius.Value;
            float radiusSqr = radius * radius;

            foreach (var source in bodies)
            {
                if (!DiabloidElites.Is(source, EliteKind.Clockmaker)) continue;
                if (!source.healthComponent || !source.healthComponent.alive) continue;
                var sourceTeam = TeamOf(source);

                foreach (var victim in bodies)
                {
                    if (!victim || victim == source || !victim.healthComponent || !victim.healthComponent.alive) continue;
                    var team = TeamOf(victim);
                    // Только противники часовщика (игроки и их союзники для монстра).
                    if (team == sourceTeam || team == TeamIndex.Neutral || team == TeamIndex.None) continue;
                    if ((victim.corePosition - source.corePosition).sqrMagnitude > radiusSqr) continue;

                    victim.AddTimedBuff(debuff, SlowBuffDuration);
                }
            }
        }

        // ---------------------------------------------------------------- титан (сервер)

        private void SummonTitanCopies(CharacterBody original)
        {
            if (!original || !original.healthComponent || !original.healthComponent.alive) return;
            var master = original.master;
            var director = DirectorCore.instance;
            if (!master || !director) return;

            int count = DiabloidConfig.TitanCopyCount.Value;
            if (count <= 0) return;

            var card = GetCopyCard(master, original);
            if (!card) return;

            for (int i = 0; i < count; i++)
            {
                // Approximate — директор сам найдёт точку на навигационной сетке рядом с оригиналом,
                // чтобы копии не застряли в стене и летающие спавнились в воздухе.
                var placement = new DirectorPlacementRule
                {
                    placementMode = DirectorPlacementRule.PlacementMode.Approximate,
                    // НЕ original.transform: у CharacterBody есть приватное поле transform, и публицированные
                    // библиотеки подсовывают его вместо свойства Component.transform → FieldAccessException в игре.
                    spawnOnTarget = original.gameObject.transform,
                    minDistance = 3f,
                    maxDistance = 20f,
                };

                var request = new DirectorSpawnRequest(card, placement, rng)
                {
                    teamIndexOverride = master.teamIndex,
                    ignoreTeamMemberLimit = true,
                    onSpawnedServer = result => OnTitanCopySpawned(result, master),
                };
                director.TrySpawnObject(request);
            }
        }

        private void OnTitanCopySpawned(SpawnCard.SpawnResult result, CharacterMaster originalMaster)
        {
            if (!result.success || !result.spawnedInstance) return;
            var copy = result.spawnedInstance.GetComponent<CharacterMaster>();
            if (!copy) return;

            // Метку ставим ДО выдачи аспекта: когда у копии появится бафф титана, она уже будет знать, что она копия.
            copy.gameObject.AddComponent<TitanMarker>().IsCopy = true;

            if (originalMaster && originalMaster.inventory && copy.inventory)
            {
                // Точный клон: все предметы (включая BoostHp от элиты и предметы артефакта Эволюции) + аспект.
                copy.inventory.CopyItemsFrom(originalMaster.inventory);
                copy.inventory.CopyEquipmentFrom(originalMaster.inventory);
            }
            else if (copy.inventory)
            {
                var titan = DiabloidElites.Get(EliteKind.Titan);
                copy.inventory.SetEquipmentIndex(titan.Equipment.equipmentIndex);
                Util.GrantMonsterBonusStats(copy, titan.Elite.healthBoostCoefficient, titan.Elite.damageBoostCoefficient, false);
            }

            var copyBody = copy.GetBody();
            if (!copyBody) return;

            // Шмоток копии не роняют никогда (см. LootDrops), золото и опыт — по настройке, по умолчанию нет.
            if (DiabloidConfig.TitanCopiesGiveRewards.Value && originalMaster)
            {
                var originalBody = originalMaster.GetBody();
                var from = originalBody ? originalBody.GetComponent<DeathRewards>() : null;
                var to = copyBody.GetComponent<DeathRewards>();
                if (from && to)
                {
                    to.goldReward = from.goldReward;
                    to.expReward = from.expReward;
                    to.spawnValue = from.spawnValue;
                }
            }

            QueueFullHeal(copyBody);
        }

        /// <summary>Карточка спавна "ещё один такой же монстр". Кешируется по типу мастера.</summary>
        private CharacterSpawnCard GetCopyCard(CharacterMaster master, CharacterBody body)
        {
            var index = master.masterIndex;
            if (copyCards.TryGetValue(index, out var cached) && cached) return cached;

            var prefab = MasterCatalog.GetMasterPrefab(index);
            if (!prefab) return null;

            var card = ScriptableObject.CreateInstance<CharacterSpawnCard>();
            card.name = "cscDiabloidTitanCopy_" + prefab.name;
            card.prefab = prefab;
            card.sendOverNetwork = true;
            card.hullSize = body.hullClassification;
            card.nodeGraphType = body.isFlying ? MapNodeGroup.GraphType.Air : MapNodeGroup.GraphType.Ground;
            card.noElites = true;       // элиту выдаём сами (копируем аспект)
            card.forbiddenAsBoss = true;
            card.occupyPosition = false;

            copyCards[index] = card;
            return card;
        }

        // ---------------------------------------------------------------- жгуты (все машины)

        private void LateUpdate()
        {
            tethersUsed = 0;

            if (Run.instance && DiabloidElites.DrainTetherMaterial)
            {
                float radius = DiabloidConfig.BloodsuckerRadius.Value;
                float radiusSqr = radius * radius;
                bool drainMonsters = DiabloidConfig.BloodsuckerDrainsMonsters.Value;
                var all = CharacterBody.readOnlyInstancesList;

                // Здоровье и позиции синхронизирует игра, поэтому клиент может сам повторить
                // ту же проверку, что и сервер, и нарисовать те же жгуты. Без единого сетевого сообщения.
                for (int i = 0; i < all.Count; i++)
                {
                    var source = all[i];
                    if (!IsActiveBloodsucker(source)) continue;

                    for (int j = 0; j < all.Count; j++)
                    {
                        var victim = all[j];
                        if (!IsDrainTarget(source, victim, radiusSqr, drainMonsters)) continue;
                        DrawTether(source.corePosition, victim.corePosition);
                    }
                }
            }

            for (int i = tethersUsed; i < tethers.Count; i++)
                if (tethers[i] && tethers[i].enabled) tethers[i].enabled = false;
        }

        private void DrawTether(Vector3 from, Vector3 to)
        {
            LineRenderer line;
            if (tethersUsed < tethers.Count && tethers[tethersUsed])
            {
                line = tethers[tethersUsed];
            }
            else
            {
                var go = new GameObject("DiabloidDrainTether");
                DontDestroyOnLoad(go);
                line = go.AddComponent<LineRenderer>();
                line.sharedMaterial = DiabloidElites.DrainTetherMaterial;
                line.positionCount = 2;
                line.useWorldSpace = true;
                line.startWidth = 0.6f;
                line.endWidth = 0.2f;
                line.textureMode = LineTextureMode.Stretch;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                if (tethersUsed < tethers.Count) tethers[tethersUsed] = line;
                else tethers.Add(line);
            }

            line.enabled = true;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            tethersUsed++;
        }
    }
}
