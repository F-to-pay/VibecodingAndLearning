using System;
using System.Collections.Generic;
using System.Linq;
using Diabloid.Gameplay;
using RoR2;
using RoR2.Navigation;
using RoR2.Orbs;
using UnityEngine;
using UnityEngine.Networking;

namespace Diabloid.Books
{
    /// <summary>
    /// Эффекты книг 0.11.0. Всё на сервере (PerformEquipmentAction зовётся на сервере). Отложенные действия
    /// (метеор через секунду, ножи по одному, смена формы перевёртыша, метка авантюриста) крутит BookRunner.
    /// Возврат true = книга сработала (пошёл кулдаун), false = не сработала (нет цели и т. п.) — кулдауна нет.
    /// </summary>
    internal static class BookEffects
    {
        // --- Метеор ---
        public const float MeteorCooldown = 6f, MeteorDelay = 1f, MeteorRadius = 5f, MeteorDamage = 3.8f, MeteorProc = 1f;
        // --- Скрытность ---
        public const float StealthCooldown = 25f, StealthDuration = 4f, StealthSpeed = 0.3f;
        // --- Перевёртыш ---
        public const float ShapeCooldown = 10f;  // КД против абуза неуязвимости при смене тела
        public const float ShapeDuration = 30f;  // через столько секунд после превращения — автоматически обратно
        // --- Шквал ножей ---
        public const float KnivesCooldown = 80f, KnivesDuration = 5f, KnivesDamage = 0.2f, KnivesProc = 0.1f;
        public const int KnivesCount = 50;
        // --- Философский камень ---
        public const float StoneCooldown = 30f;
        public const int StoneGold = 25;
        // --- Авантюрист ---
        public const float AdventureCooldown = 60f, AdventureRadius = 10f;

        public static BuffDef StealthSpeedBuff { get; private set; }

        private static GameObject explosionEffect, meteorWarningEffect, poiIndicator;

        public static void Register()
        {
            StealthSpeedBuff = ScriptableObject.CreateInstance<BuffDef>();
            StealthSpeedBuff.name = "bdDiabloidStealthSpeed";
            StealthSpeedBuff.buffColor = new Color32(150, 150, 255, 255);
            StealthSpeedBuff.canStack = false;
            StealthSpeedBuff.isDebuff = false;
            R2API.ContentAddition.AddBuffDef(StealthSpeedBuff);

            R2API.RecalculateStatsAPI.GetStatCoefficients += (body, args) =>
            {
                if (body && StealthSpeedBuff && body.HasBuff(StealthSpeedBuff)) args.moveSpeedMultAdd += StealthSpeed;
            };

            // Перевёртыш: при смерти тело игрока должно снова стать своим (воскрешение — в своём теле).
            GlobalEventManager.onCharacterDeathGlobal += report =>
            {
                if (NetworkServer.active && report != null && report.victimMaster) Shapeshift.OnOwnerDeath(report.victimMaster);
            };
            Run.onRunStartGlobal += _ => { Shapeshift.Clear(); Adventure.Clear(); };
        }

        public static void OnCatalogsReady()
        {
            if (RoR2Content.Buffs.CloakSpeed) StealthSpeedBuff.iconSprite = RoR2Content.Buffs.CloakSpeed.iconSprite;
            explosionEffect = TryLoad("Prefabs/Effects/OmniEffect/OmniExplosionVFXQuick");
            meteorWarningEffect = TryLoad("Prefabs/Effects/MeteorStrikePredictionEffect");
            poiIndicator = TryLoad("Prefabs/PositionIndicators/PoiPositionIndicator");
        }

        private static GameObject TryLoad(string path)
        {
            try { return LegacyResourcesAPI.Load<GameObject>(path); }
            catch (Exception e) { DiabloidPlugin.Log.LogWarning($"Books: '{path}' not loaded: {e.Message}"); return null; }
        }

        // ---------------------------------------------------------------- общие помощники

        private static TeamIndex TeamOf(CharacterBody body) => body.teamComponent ? body.teamComponent.teamIndex : TeamIndex.Player;

        /// <summary>Враг под прицелом: конус 10° вдоль луча прицела, до 120 м, ближайший к центру.</summary>
        private static HurtBox AimedEnemy(CharacterBody owner)
        {
            if (!owner.inputBank) return null;
            var ray = owner.inputBank.GetAimRay();
            var search = new BullseyeSearch
            {
                searchOrigin = ray.origin,
                searchDirection = ray.direction,
                maxDistanceFilter = 120f,
                maxAngleFilter = 10f,
                teamMaskFilter = TeamMask.GetEnemyTeams(TeamOf(owner)),
                sortMode = BullseyeSearch.SortMode.Angle,
                filterByLoS = true,
            };
            search.RefreshCandidates();
            search.FilterOutGameObject(owner.gameObject);
            return search.GetResults().FirstOrDefault();
        }

        // ---------------------------------------------------------------- метеор

        public static bool Meteor(CharacterBody owner)
        {
            if (!owner.inputBank) return false;
            var ray = owner.inputBank.GetAimRay();
            if (!Physics.Raycast(ray, out var hit, 300f, LayerIndex.world.mask, QueryTriggerInteraction.Ignore)) return false;

            var point = hit.point;
            var attacker = owner.gameObject;
            var team = TeamOf(owner);
            float damage = owner.damage * MeteorDamage;
            bool crit = owner.RollCrit();

            if (meteorWarningEffect)
                EffectManager.SpawnEffect(meteorWarningEffect, new EffectData { origin = point, scale = MeteorRadius, start = point }, true);

            BookRunner.Schedule(MeteorDelay, () =>
            {
                new BlastAttack
                {
                    attacker = attacker,
                    inflictor = attacker,
                    teamIndex = team,
                    baseDamage = damage,
                    baseForce = 2500f,                // отталкивание от центра
                    bonusForce = Vector3.up * 1000f,  // и немного вверх
                    position = point,
                    radius = MeteorRadius,
                    procCoefficient = MeteorProc,
                    crit = crit,
                    damageColorIndex = DamageColorIndex.Item,
                    falloffModel = BlastAttack.FalloffModel.None,
                    attackerFiltering = AttackerFiltering.NeverHitSelf,
                }.Fire();
                if (explosionEffect)
                    EffectManager.SpawnEffect(explosionEffect, new EffectData { origin = point, scale = MeteorRadius * 2f }, true);
            });
            return true;
        }

        // ---------------------------------------------------------------- скрытность

        public static bool Stealth(CharacterBody owner)
        {
            owner.AddTimedBuff(RoR2Content.Buffs.Cloak, StealthDuration);
            owner.AddTimedBuff(StealthSpeedBuff, StealthDuration);
            return true;
        }

        // ---------------------------------------------------------------- шквал ножей

        public static bool Knives(CharacterBody owner)
        {
            var target = AimedEnemy(owner);
            if (!target) return false;

            var attacker = owner.gameObject;
            var master = owner.master;
            float interval = KnivesDuration / KnivesCount;
            for (int i = 0; i < KnivesCount; i++)
            {
                BookRunner.Schedule(i * interval, () =>
                {
                    // Цель мертва — шквал кончился (решение пользователя: не перекидывать).
                    if (!target || !target.healthComponent || !target.healthComponent.alive) return;
                    var body = master ? master.GetBody() : null;
                    if (!body) return;
                    OrbManager.instance.AddOrb(new LightningOrb
                    {
                        origin = body.corePosition,
                        damageValue = body.damage * KnivesDamage,
                        isCrit = body.RollCrit(),
                        bouncesRemaining = 0,
                        teamIndex = TeamOf(body),
                        attacker = attacker,
                        procCoefficient = KnivesProc,
                        lightningType = LightningOrb.LightningType.RazorWire,
                        damageColorIndex = DamageColorIndex.Item,
                        target = target,
                    });
                });
            }
            return true;
        }

        // ---------------------------------------------------------------- философский камень

        public static bool Stone(CharacterBody owner)
        {
            if (!owner.master || !Run.instance) return false;
            // Как цены сундуков: 25 × (коэффициент сложности)^1.25 — растёт со временем забега.
            uint gold = (uint)Math.Max(1, Run.instance.GetDifficultyScaledCost(StoneGold));
            owner.master.GiveMoney(gold);
            return true;
        }

        // ---------------------------------------------------------------- авантюрист

        public static bool Adventurer(CharacterBody owner) => Adventure.Mark(owner, poiIndicator);

        // ---------------------------------------------------------------- перевёртыш

        public static bool Shapeshifter(CharacterBody owner) => Shapeshift.Use(owner, AimedEnemy(owner));

        /// <summary>
        /// Перевёртыш. Первое применение: цель под прицелом (не босс телепорта) исчезает без наград, её форма
        /// запоминается навсегда (на забег). Дальше книга — переключатель: превратиться / вернуться в своё тело.
        /// Через ShapeDuration секунд после превращения — возврат автоматически (не все формы могут жать снаряжение).
        /// Если этап сменился раньше — форма доживает таймер на новом этапе (master.bodyPrefab остаётся чужим).
        /// master.TransformBody — тело пересоздаётся; инвентарь, шмотки и перки на мастере остаются,
        /// доля здоровья сохраняется в обе стороны. КД 10 с — чтобы не абузить неуязвимость при смене тела.
        /// Вручную вернуться можно только если у чужого тела есть EquipmentSlot; иначе — ждать таймер.
        /// </summary>
        internal static class Shapeshift
        {
            private sealed class State
            {
                public string CapturedBody;   // имя тела в BodyCatalog ("LemurianBody")
                public string OriginalBody;   // своё тело, пока превращён
                public bool Transformed;      // сейчас в чужой форме
                public int Generation;        // номер превращения — чтобы старый таймер не вернул из нового
            }

            private static readonly Dictionary<CharacterMaster, State> states = new Dictionary<CharacterMaster, State>();

            public static void Clear() => states.Clear();

            public static bool Use(CharacterBody owner, HurtBox aimed)
            {
                var master = owner.master;
                if (!master) return false;
                if (!states.TryGetValue(master, out var state)) states[master] = state = new State();
                if (state.Transformed)
                {
                    // Возврат — тоже на следующем тике (мы внутри EquipmentSlot текущего тела).
                    BookRunner.Schedule(0.05f, () => Revert(master));
                    return true;
                }

                if (string.IsNullOrEmpty(state.CapturedBody))
                {
                    var health = aimed ? aimed.healthComponent : null;
                    var target = health ? health.body : null;
                    if (!target || !health.alive) return false;
                    if (target.master && target.master.isBoss) return false; // босса телепорта не трогаем
                    if (target.isPlayerControlled) return false;

                    state.CapturedBody = BodyCatalog.GetBodyName(target.bodyIndex);
                    string shown = target.GetDisplayName();
                    if (explosionEffect)
                        EffectManager.SpawnEffect(explosionEffect, new EffectData { origin = target.corePosition, scale = 3f }, true);

                    // "Удаляет": без смерти, без золота, опыта и шмоток.
                    if (target.master) NetworkServer.Destroy(target.master.gameObject);
                    NetworkServer.Destroy(target.gameObject);

                    Chat.AddMessage($"<color=#FFCC55>{Util.GetBestMasterName(master)}: {Diabloid.Loot.Loc.T("form captured", "форма захвачена")} — {shown}</color>");
                    return true;
                }

                // Превращение — на следующем тике: сейчас мы внутри EquipmentSlot старого тела.
                var captured = state.CapturedBody;
                BookRunner.Schedule(0.05f, () =>
                {
                    var body = master.GetBody();
                    if (!body || state.Transformed) return;
                    state.OriginalBody = BodyCatalog.GetBodyName(body.bodyIndex);
                    state.Transformed = true;
                    int generation = ++state.Generation;
                    SwapBody(master, captured);
                    BookRunner.Schedule(ShapeDuration, () =>
                    {
                        if (!master || !states.TryGetValue(master, out var current) || current != state) return;
                        if (state.Transformed && state.Generation == generation) Revert(master);
                    });
                });
                return true;
            }

            private static void Revert(CharacterMaster master)
            {
                if (!master || !states.TryGetValue(master, out var state) || !state.Transformed) return;
                state.Transformed = false;
                if (!string.IsNullOrEmpty(state.OriginalBody) && master.GetBody()) SwapBody(master, state.OriginalBody);
                else RestorePrefab(master, state);
            }

            /// <summary>Сменить тело, сохранив долю здоровья.</summary>
            private static void SwapBody(CharacterMaster master, string bodyName)
            {
                var old = master.GetBody();
                float fraction = old && old.healthComponent ? Mathf.Clamp01(old.healthComponent.combinedHealthFraction) : 1f;
                master.TransformBody(bodyName);
                Action<CharacterBody> apply = null;
                apply = b =>
                {
                    master.onBodyStart -= apply;
                    BookRunner.Schedule(0.2f, () =>
                    {
                        if (b && b.healthComponent && fraction < 1f)
                            b.healthComponent.Networkhealth = Mathf.Max(1f, b.healthComponent.fullHealth * fraction);
                    });
                };
                master.onBodyStart += apply;
            }

            private static void RestorePrefab(CharacterMaster master, State state)
            {
                if (string.IsNullOrEmpty(state.OriginalBody)) return;
                var prefab = BodyCatalog.FindBodyPrefab(state.OriginalBody);
                if (prefab) master.bodyPrefab = prefab;
            }

            /// <summary>Смерть в чужой форме: воскрешение (Dio и т. п.) должно вернуть своё тело.</summary>
            public static void OnOwnerDeath(CharacterMaster master)
            {
                if (!states.TryGetValue(master, out var state) || !state.Transformed) return;
                state.Transformed = false;
                RestorePrefab(master, state);
            }
        }

        /// <summary>
        /// Авантюрист: случайная точка на наземной навигации в 40–200 м, на ней сфера 10 м и метка на экране
        /// (только у хоста). Дошёл — в центре падает предмет как из малого сундука. Новая метка заменяет старую.
        /// </summary>
        internal static class Adventure
        {
            private sealed class Marker
            {
                public Vector3 Position;
                public GameObject Visual;
            }

            private static readonly Dictionary<CharacterMaster, Marker> markers = new Dictionary<CharacterMaster, Marker>();

            public static void Clear()
            {
                foreach (var marker in markers.Values)
                    if (marker.Visual) UnityEngine.Object.Destroy(marker.Visual);
                markers.Clear();
            }

            public static bool Mark(CharacterBody owner, GameObject indicatorPrefab)
            {
                var master = owner.master;
                var nodes = SceneInfo.instance ? SceneInfo.instance.groundNodes : null;
                if (!master || !nodes) return false;

                var indices = nodes.GetActiveNodesForHullMask(HullMask.Human);
                if (indices == null || indices.Count == 0) return false;

                var candidates = new List<Vector3>();
                var all = new List<Vector3>();
                foreach (var index in indices)
                {
                    if (!nodes.GetNodePosition(index, out var p)) continue;
                    all.Add(p);
                    float d = Vector3.Distance(p, owner.footPosition);
                    if (d >= 40f && d <= 200f) candidates.Add(p);
                }
                var pool = candidates.Count > 0 ? candidates : all;
                if (pool.Count == 0) return false;
                var point = pool[Diabloid.Gameplay.LootRoller.Rng.Next(pool.Count)];

                if (markers.TryGetValue(master, out var old) && old.Visual) UnityEngine.Object.Destroy(old.Visual);
                markers[master] = new Marker { Position = point, Visual = MakeVisual(point, indicatorPrefab) };
                return true;
            }

            private static GameObject MakeVisual(Vector3 point, GameObject indicatorPrefab)
            {
                var root = new GameObject("DiabloidAdventureMarker");
                root.transform.position = point;

                var material = PerkManager.AuraMaterial;
                if (material)
                {
                    var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    UnityEngine.Object.Destroy(sphere.GetComponent<Collider>());
                    sphere.GetComponent<MeshRenderer>().sharedMaterial = material;
                    sphere.transform.SetParent(root.transform, false);
                    sphere.transform.localScale = Vector3.one * AdventureRadius * 2f;
                }

                if (indicatorPrefab)
                {
                    var indicator = UnityEngine.Object.Instantiate(indicatorPrefab, point, Quaternion.identity);
                    var positionIndicator = indicator.GetComponent<PositionIndicator>();
                    if (positionIndicator) positionIndicator.targetTransform = root.transform;
                    indicator.transform.SetParent(root.transform, true);
                }
                return root;
            }

            /// <summary>Сервер, раз в 0.25 с: кто дошёл до своей метки — получает предмет.</summary>
            public static void Tick()
            {
                if (markers.Count == 0) return;
                List<CharacterMaster> reached = null;
                foreach (var pair in markers)
                {
                    var body = pair.Key ? pair.Key.GetBody() : null;
                    if (!body) continue;
                    var delta = body.footPosition - pair.Value.Position;
                    delta.y *= 0.5f; // по высоте помягче — точка на ноде, а герой может стоять на бугре
                    if (delta.magnitude > AdventureRadius) continue;
                    (reached ??= new List<CharacterMaster>()).Add(pair.Key);
                }
                if (reached == null) return;

                foreach (var master in reached)
                {
                    var marker = markers[master];
                    markers.Remove(master);
                    if (marker.Visual) UnityEngine.Object.Destroy(marker.Visual);
                    DropChestItem(marker.Position);
                }
            }

            /// <summary>Как малый сундук: 79.2% белый, 19.8% зелёный, 1% красный. С артефактом снаряжения станет шмоткой.</summary>
            private static void DropChestItem(Vector3 position)
            {
                var run = Run.instance;
                if (!run) return;
                double roll = Diabloid.Gameplay.LootRoller.Rng.NextDouble();
                var list = roll < 0.792 ? run.availableTier1DropList : roll < 0.99 ? run.availableTier2DropList : run.availableTier3DropList;
                if (list == null || list.Count == 0) list = run.availableTier1DropList;
                if (list == null || list.Count == 0) return;
                var pickup = list[Diabloid.Gameplay.LootRoller.Rng.Next(list.Count)];

                var info = new GenericPickupController.CreatePickupInfo { rotation = Quaternion.identity };
                info.pickup = new UniquePickup(pickup);
                PickupDropletController.CreatePickupDroplet(info, position + Vector3.up * 2f, Vector3.up * 20f);
            }
        }
    }

    /// <summary>Простой планировщик книг на сервере: "сделать это через N секунд".</summary>
    internal sealed class BookRunner : MonoBehaviour
    {
        private struct Job
        {
            public float At;
            public Action Action;
        }

        private static readonly List<Job> jobs = new List<Job>();
        private static readonly List<Job> due = new List<Job>();
        private float adventureTimer;

        public static void Schedule(float delay, Action action) => jobs.Add(new Job { At = Time.time + delay, Action = action });

        private void FixedUpdate()
        {
            if (!NetworkServer.active)
            {
                jobs.Clear();
                return;
            }

            if (jobs.Count > 0)
            {
                float now = Time.time;
                due.Clear();
                for (int i = jobs.Count - 1; i >= 0; i--)
                {
                    if (jobs[i].At > now) continue;
                    due.Add(jobs[i]);
                    jobs.RemoveAt(i);
                }
                for (int i = due.Count - 1; i >= 0; i--) // в порядке постановки
                {
                    try { due[i].Action(); }
                    catch (Exception e) { DiabloidPlugin.Log.LogError($"Books: scheduled action failed: {e}"); }
                }
            }

            adventureTimer -= Time.fixedDeltaTime;
            if (adventureTimer <= 0f)
            {
                adventureTimer = 0.25f;
                BookEffects.Adventure.Tick();
            }
        }
    }
}
