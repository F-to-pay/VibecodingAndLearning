using System.Collections.Generic;
using BepInEx.Configuration;
using Destroyer.Content;
using Destroyer.Core;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace Destroyer.Items
{
    /// <summary>
    /// Пылесос (белый). Дальше достаёшь до интерактивов (E) и издалека притягиваешь мелочь.
    ///
    /// * Дальность E — Interactor.maxInteractionDistance тела: по нему InteractionDriver на клиенте ищет, с чем
    ///   взаимодействовать (IL 1.5), а сервер в PerformInteraction расстояние не перепроверяет. Ставим на каждой машине
    ///   при смене инвентаря: исходное значение + N м за копию.
    /// * Мелочь — всё с ванильным GravitatePickup (деньги, сферы лечения, патроны Bandolier...). Сервер раз в 0.25 с
    ///   зовёт им ForceGravitate(коллайдер игрока) в радиусе пылесоса; дальше ванильная логика сама проверяет команду,
    ///   флаги тела и "не лечить при полном здоровье" — и летит к тебе.
    /// </summary>
    internal sealed class Vacuum : ItemBase
    {
        public override string Id => "VACUUM";
        public override ItemTier Tier => ItemTier.Tier1;
        public override string ModelFrom => "Bandolier";
        public override ItemTag[] Tags => new[] { ItemTag.Utility, ItemTag.AIBlacklist };

        public override Words Name => new Words("Vacuum Cleaner", "Пылесос");

        public override Words Pickup => new Words(
            "Interact from further away. Pull in gold and orbs from afar.",
            "Взаимодействуешь издалека. Деньги и сферы летят к тебе издалека.");

        public override Words Description => new Words(
            $"Interact from {Tx.Utility("+" + Tx.Meters(reachPerStack.Value).En + " further")} {Tx.Stack("+" + Tx.Meters(reachPerStack.Value).En + " per stack")}. " +
            $"Gold, healing orbs and other pickups within {Tx.Utility(Tx.Meters(magnet.Value).En)} {Tx.Stack("+" + Tx.Meters(magnetPerStack.Value).En + " per stack")} {Tx.Utility("fly to you")}.",
            $"Дальность взаимодействия {Tx.Utility("+" + Tx.Meters(reachPerStack.Value).Ru)} {Tx.Stack("+" + Tx.Meters(reachPerStack.Value).Ru + " за копию")}. " +
            $"Деньги, сферы лечения и прочая мелочь в радиусе {Tx.Utility(Tx.Meters(magnet.Value).Ru)} {Tx.Stack("+" + Tx.Meters(magnetPerStack.Value).Ru + " за копию")} {Tx.Utility("летят к тебе")}.");

        public override Words Lore => new Words(
            "It sucks. In a good way.",
            "Он сосёт. В хорошем смысле.");

        private ConfigEntry<float> reachPerStack, magnet, magnetPerStack;

        protected override void BindConfig(ItemSettings s)
        {
            reachPerStack = s.Float("ReachPerStack", 3f, 0f, 30f, new Words("Reach per stack", "Дальность E за копию"),
                new Words("Extra interaction distance per copy, meters.", "Прибавка дальности взаимодействия за каждую копию, м."), "{0:0.#} m");
            magnet = s.Float("MagnetRadius", 12f, 0f, 100f, new Words("Pull radius", "Радиус притяжения"),
                new Words("Pickups within this radius fly to you (first copy), meters.", "Мелочь в этом радиусе летит к тебе (первая копия), м."), "{0:0.#} m");
            magnetPerStack = s.Float("MagnetRadiusPerStack", 6f, 0f, 100f, new Words("Pull radius per stack", "Радиус за копию"),
                new Words("Extra pull radius per additional copy, meters.", "Прибавка радиуса за каждую копию сверх первой, м."), "{0:0.#} m");
        }

        protected override void Hook()
        {
            CharacterBody.onBodyInventoryChangedGlobal += UpdateReach;
            reachPerStack.SettingChanged += (sender, args) =>
            {
                foreach (var body in CharacterBody.readOnlyInstancesList) UpdateReach(body);
            };
            RoR2Application.onFixedUpdate += Magnet;
        }

        // ---------- Дальность E (каждая машина) ----------

        /// <summary>Исходная дальность тела, чтобы прибавлять к ней, а не к уже увеличенной.</summary>
        private sealed class ReachState : MonoBehaviour
        {
            public float BaseDistance;
        }

        private void UpdateReach(CharacterBody body)
        {
            if (!body) return;
            var interactor = body.GetComponent<Interactor>();
            if (!interactor) return;
            int n = Count(body);
            var state = body.GetComponent<ReachState>();
            if (n <= 0 && !state) return;
            if (!state)
            {
                state = body.gameObject.AddComponent<ReachState>();
                state.BaseDistance = interactor.maxInteractionDistance;
            }
            interactor.maxInteractionDistance = state.BaseDistance + reachPerStack.Value * Mathf.Max(0, n);
        }

        // ---------- Притяжение мелочи (сервер) ----------

        private const float MagnetInterval = 0.25f;
        private float magnetTimer;
        private readonly List<(CharacterBody body, Collider collider, float radius)> vacuums = new List<(CharacterBody, Collider, float)>();

        private void Magnet()
        {
            if (!NetworkServer.active || !Run.instance) return;
            magnetTimer -= Time.fixedDeltaTime;
            if (magnetTimer > 0f) return;
            magnetTimer = MagnetInterval;

            vacuums.Clear();
            foreach (var body in CharacterBody.readOnlyInstancesList)
            {
                if (!body || !body.healthComponent || !body.healthComponent.alive) continue;
                int n = Count(body);
                if (n <= 0) continue;
                var collider = body.GetComponent<Collider>();
                if (!collider) continue;
                vacuums.Add((body, collider, Stacking.Linear(n, magnet.Value, magnetPerStack.Value)));
            }
            if (vacuums.Count == 0) return;

            // GravitatePickup нигде не регистрируется (нет InstanceTracker), поэтому ищем по сцене. Раз в 0.25 с — терпимо.
            foreach (var pickup in Object.FindObjectsOfType<GravitatePickup>())
            {
                if (!pickup || pickup.gravitateTarget) continue; // уже к кому-то летит
                Vector3 position = pickup.gameObject.transform.position;
                foreach (var v in vacuums)
                {
                    if ((v.body.corePosition - position).sqrMagnitude > v.radius * v.radius) continue;
                    pickup.ForceGravitate(v.collider);
                    if (pickup.gravitateTarget) break; // принял — дальше не предлагаем
                }
            }
        }
    }
}
