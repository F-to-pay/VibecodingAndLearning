using System.Collections.Generic;
using BepInEx.Configuration;
using Destroyer.Content;
using Destroyer.Core;
using RoR2;
using RoR2.UI;
using UnityEngine;
using UnityEngine.Networking;

namespace Destroyer.Items
{
    /// <summary>
    /// Смартфон (зелёный). Метки "сквозь стены", как у Радара, и +5% денег за каждую копию.
    ///   1 копия: ближайший неоткрытый сундук;
    ///   2 копии: ещё ближайший дрон (и турель — всё, что покупается как SummonMaster);
    ///   3 копии: ещё ближайшая бочка и ВСЕ интерактивы со знаком вопроса (алтари ньюта — проход в Базар, порталы, лунные штуки).
    ///
    /// Откуда берём интерактивы — как Радар в 1.5 (ChestRevealer.Pulse, IL): InterfaceInstanceTracker&lt;IInteractable&gt;,
    /// фильтр IInteractable.ShouldShowOnScanner() (у покупаемых = available, у бочки = не открыта).
    /// Категория — повтор логики PingIndicator.GetInteractableIcon (IL 1.5), см. Classify: "знак вопроса" = то, чему игра
    /// сама рисует texMysteryIcon. Метка — префаб PoiPositionIndicator с иконкой из GetInteractableIcon,
    /// как ChestRevealer.RevealedObject (он приватный — собрано руками).
    /// Считается на КАЖДОЙ машине для своего игрока: инвентарь и доступность синхронизирует игра, сеть не нужна.
    ///
    /// Деньги — хук CharacterMaster.GiveMoney(uint, bool) на сервере, дробный остаток копится у игрока.
    /// </summary>
    internal sealed class Smartphone : ItemBase
    {
        public override string Id => "SMARTPHONE";
        public override ItemTier Tier => ItemTier.Tier2;
        public override string ModelFrom => "TreasureCache"; // Ржавый ключ
        public override ItemTag[] Tags => new[] { ItemTag.Utility, ItemTag.InteractableRelated, ItemTag.AIBlacklist };

        public override Words Name => new Words("Smartphone", "Смартфон");

        public override Words Pickup => new Words(
            "Marks the nearest chest. More copies mark more. Earn more gold.",
            "Отмечает ближайший сундук. Больше копий — больше меток. Больше денег.");

        public override Words Description => new Words(
            $"Marks the nearest {Tx.Utility("unopened chest")} through walls. " +
            $"With {Tx.Utility(droneCopies.Value + " copies")}, also the nearest {Tx.Utility("drone")}; " +
            $"with {Tx.Utility(extraCopies.Value + " copies")}, also the nearest {Tx.Utility("barrel")} and {Tx.Utility("every mystery interactable")} " +
            $"{Tx.Sub("(the ones with a ? mark: Newt Altars, portals...)")}. " +
            $"Gain {Tx.Shrine("+" + Tx.Pct(goldPerStack.Value) + " gold")} {Tx.Stack("+" + Tx.Pct(goldPerStack.Value) + " per stack")}.",
            $"Отмечает сквозь стены ближайший {Tx.Utility("неоткрытый сундук")}. " +
            $"С {Tx.Utility(droneCopies.Value + " копиями")} — ещё ближайший {Tx.Utility("дрон")}, " +
            $"с {Tx.Utility(extraCopies.Value + " копиями")} — ещё ближайшую {Tx.Utility("бочку")} и {Tx.Utility("все загадочные интерактивы")} " +
            $"{Tx.Sub("(те, что со знаком вопроса: алтари ньюта, порталы...)")}. " +
            $"{Tx.Shrine("+" + Tx.Pct(goldPerStack.Value) + " денег")} {Tx.Stack("+" + Tx.Pct(goldPerStack.Value) + " за копию")}.");

        public override Words Lore => new Words(
            "37 unread messages. Battery 3%. GPS: enabled.",
            "37 непрочитанных. Заряд 3%. Геолокация: включена.");

        private ConfigEntry<float> goldPerStack;
        private ConfigEntry<bool> showMarker;
        private ConfigEntry<int> droneCopies, extraCopies;

        protected override void BindConfig(ItemSettings s)
        {
            goldPerStack = s.Float("GoldPerStack", 0.05f, 0f, 1f, new Words("Gold per stack", "Деньги за копию"),
                new Words("Extra gold for every copy, including the first (0.05 = +5%).", "Прибавка денег за каждую копию, включая первую (0.05 = +5%)."), "{0:P0}");
            droneCopies = s.Int("DroneFromCopies", 2, 1, 10, new Words("Drone mark from copies", "Метка дрона с копий"),
                new Words("How many copies you need to also mark the nearest drone.", "Сколько копий нужно, чтобы отмечался ещё и ближайший дрон."));
            extraCopies = s.Int("BarrelAndMysteryFromCopies", 3, 1, 10, new Words("Barrel and ? marks from copies", "Бочка и «?» с копий"),
                new Words("How many copies you need to also mark the nearest barrel and every mystery (?) interactable.",
                          "Сколько копий нужно, чтобы отмечались ещё ближайшая бочка и все интерактивы со знаком вопроса."));
            showMarker = s.Bool("ShowMarker", true, new Words("Show marks", "Показывать метки"),
                new Words("Show the marks at all. Client-side: each player decides for themselves.",
                          "Показывать метки вообще. Настройка своя у каждого игрока."));
        }

        internal static Smartphone Instance;

        protected override void Hook()
        {
            Instance = this;
            On.RoR2.CharacterMaster.GiveMoney_uint_bool += GiveMoney;
            Run.onRunStartGlobal += run => remainders.Clear();
            DestroyerPlugin.Instance.gameObject.AddComponent<Tracker>();
        }

        // ---------- Деньги (сервер) ----------

        private readonly Dictionary<CharacterMaster, float> remainders = new Dictionary<CharacterMaster, float>();

        private void GiveMoney(On.RoR2.CharacterMaster.orig_GiveMoney_uint_bool orig, CharacterMaster self, uint amount, bool bypassStatManager)
        {
            int n = NetworkServer.active ? Count(self) : 0;
            if (n > 0 && amount > 0)
            {
                remainders.TryGetValue(self, out float carry);
                float total = amount * (1f + goldPerStack.Value * n) + carry;
                uint whole = (uint)Mathf.FloorToInt(total);
                remainders[self] = total - whole;
                amount = whole;
            }
            orig(self, amount, bypassStatManager);
        }

        // ---------- Метки (каждая машина, свой игрок) ----------

        internal enum Kind { Chest, Drone, Barrel, Mystery }

        /// <summary>
        /// Категория интерактива. Порядок — как в PingIndicator.GetInteractableIcon (IL 1.5): бочка → "Shrine" в имени →
        /// IChestBehavior → лут на земле → SummonMasterBehavior → телепорт → [своя иконка через PingInfoProvider] →
        /// статуя портала в Базар → покупка за лунные монеты → прочая покупка → TimedChest → всё остальное.
        /// "Знак вопроса" (texMysteryIcon) — статуя Базара, покупка за лунные монеты и "всё остальное".
        /// null — не интересно (святилища, принтеры, лут, телепорт, своя иконка).
        /// </summary>
        internal static Kind? Classify(GameObject go)
        {
            if (go.GetComponent<BarrelInteraction>()) return Kind.Barrel;
            if (go.name.Contains("Shrine")) return null;
            if (go.TryGetComponent<IChestBehavior>(out _)) return Kind.Chest;
            if (go.GetComponent<GenericPickupController>() || go.GetComponent<PickupPickerController>()) return null;
            if (go.GetComponent<SummonMasterBehavior>()) return Kind.Drone;
            if (go.GetComponent<TeleporterInteraction>()) return null;

            var ping = go.GetComponent<PingInfoProvider>();
            if (ping && ping.pingIconOverride) return null; // у объекта своя иконка — значит, не "?"

            var statue = go.GetComponent<PortalStatueBehavior>();
            if (statue && statue.portalType == PortalStatueBehavior.PortalType.Shop) return Kind.Mystery;

            var purchase = go.GetComponent<PurchaseInteraction>();
            if (purchase) return purchase.costType == CostTypeIndex.LunarCoin ? Kind.Mystery : (Kind?)null;

            if (go.GetComponent<TimedChestController>()) return Kind.Chest;
            return Kind.Mystery;
        }

        /// <summary>Что отметить сейчас для тела с n копиями.</summary>
        internal void CollectTargets(Vector3 from, int n, HashSet<GameObject> result)
        {
            result.Clear();
            bool drones = n >= droneCopies.Value;
            bool extra = n >= extraCopies.Value;

            GameObject chest = null, drone = null, barrel = null;
            float chestSqr = float.MaxValue, droneSqr = float.MaxValue, barrelSqr = float.MaxValue;

            foreach (var interactable in InterfaceInstanceTracker<IInteractable>.GetInstancesList())
            {
                var behaviour = interactable as MonoBehaviour;
                if (!behaviour || !behaviour.isActiveAndEnabled || !interactable.ShouldShowOnScanner()) continue;
                var go = behaviour.gameObject;
                var kind = Classify(go);
                if (kind == null) continue;

                float sqr = (go.transform.position - from).sqrMagnitude;
                switch (kind.Value)
                {
                    case Kind.Chest:
                        if (sqr < chestSqr) { chestSqr = sqr; chest = go; }
                        break;
                    case Kind.Drone:
                        if (drones && sqr < droneSqr) { droneSqr = sqr; drone = go; }
                        break;
                    case Kind.Barrel:
                        if (extra && sqr < barrelSqr) { barrelSqr = sqr; barrel = go; }
                        break;
                    case Kind.Mystery:
                        if (extra) result.Add(go);
                        break;
                }
            }

            if (chest) result.Add(chest);
            if (drone) result.Add(drone);
            if (barrel) result.Add(barrel);
        }

        internal bool MarkersEnabled => showMarker.Value;

        /// <summary>Раз в полсекунды пересобирает набор меток. Висит на объекте плагина.</summary>
        private sealed class Tracker : MonoBehaviour
        {
            private const float Interval = 0.5f;
            private float timer;
            private readonly Dictionary<GameObject, GameObject> markers = new Dictionary<GameObject, GameObject>();
            private readonly HashSet<GameObject> wanted = new HashSet<GameObject>();
            private readonly List<GameObject> stale = new List<GameObject>();

            private void Update()
            {
                timer -= Time.deltaTime;
                if (timer > 0f) return;
                timer = Interval;

                var item = Instance;
                var body = LocalBody();
                int n = item != null && body ? item.Count(body) : 0;
                if (n > 0 && item.MarkersEnabled) item.CollectTargets(body.corePosition, n, wanted);
                else wanted.Clear();

                // Убрать лишние метки (и те, что пропали со сменой сцены).
                stale.Clear();
                foreach (var pair in markers)
                    if (!pair.Key || !pair.Value || !wanted.Contains(pair.Key)) stale.Add(pair.Key);
                foreach (var key in stale)
                {
                    if (markers.TryGetValue(key, out var marker) && marker) Destroy(marker);
                    markers.Remove(key);
                }

                // Добавить новые.
                foreach (var target in wanted)
                    if (!markers.ContainsKey(target)) markers[target] = CreateMarker(target);
            }

            private void OnDisable()
            {
                foreach (var marker in markers.Values)
                    if (marker) Destroy(marker);
                markers.Clear();
            }

            private static GameObject CreateMarker(GameObject target)
            {
                var prefab = GameAssets.Load(GameAssets.PoiIndicator);
                if (!prefab) return null;
                var marker = Instantiate(prefab, target.transform.position, target.transform.rotation);
                var position = marker.GetComponent<PositionIndicator>();
                if (position)
                {
                    position.targetTransform = target.transform;
                    var icon = position.insideViewObject ? position.insideViewObject.GetComponent<SpriteRenderer>() : null;
                    if (icon) icon.sprite = PingIndicator.GetInteractableIcon(target);
                }
                return marker;
            }

            private static CharacterBody LocalBody()
            {
                var user = LocalUserManager.GetFirstLocalUser();
                var master = user != null ? user.cachedMaster : null;
                return master ? master.GetBody() : null;
            }
        }
    }
}
