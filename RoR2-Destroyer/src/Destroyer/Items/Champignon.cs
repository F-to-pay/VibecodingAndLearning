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
    /// Шампиньон (белый). Стоишь на месте — растёт броня: 10 (+5) в секунду, без потолка. Сдвинулся (сам, прыжком,
    /// отбрасыванием) — вся броня сгорает. Получил урон — теряешь 90% накопленного.
    ///
    /// "Стоишь" — ванильный CharacterBody.GetNotMoving (как у Bustling Fungus): считается только на сервере
    /// (UpdateNotMoving, IL 1.5) по смещению за кадр, поэтому вся логика серверная. Клиентам броня приходит
    /// стаками баффа (1 стак = 1 брони): стаки синхронизирует игра, а RecalculateStats на каждой машине
    /// превращает их в броню — цифры у всех одинаковые, и на иконке баффа видно, сколько набрано.
    /// </summary>
    internal sealed class Champignon : ItemBase
    {
        public override string Id => "CHAMPIGNON";
        public override ItemTier Tier => ItemTier.Tier1;
        public override string ModelFrom => "Mushroom"; // Bustling Fungus
        public override ItemTag[] Tags => new[] { ItemTag.Utility };

        public override Words Name => new Words("Champignon", "Шампиньон");

        public override Words Pickup => new Words(
            "Grow armor while standing still. Moving or getting hit loses it.",
            "Стоя на месте, обрастаешь бронёй. Движение и урон её отнимают.");

        public override Words Description => new Words(
            $"While standing still, gain {Tx.Utility("+" + Tx.Num(armorPerSecond.Value) + " armor")} {Tx.Stack("+" + Tx.Num(armorPerSecondPerStack.Value) + " per stack")} " +
            $"every second, {Tx.Utility("without limit")}. Moving {Tx.Health("loses all of it")}; taking damage {Tx.Health("removes " + Tx.Pct(damageCut.Value))} of it.",
            $"Пока стоишь на месте, получаешь {Tx.Utility("+" + Tx.Num(armorPerSecond.Value) + " брони")} {Tx.Stack("+" + Tx.Num(armorPerSecondPerStack.Value) + " за копию")} " +
            $"в секунду, {Tx.Utility("без предела")}. Движение {Tx.Health("сжигает её целиком")}, получение урона {Tx.Health("отнимает " + Tx.Pct(damageCut.Value))}.");

        public override Words Lore => new Words(
            "It does not move. It does not need to.",
            "Он не двигается. Ему и не нужно.");

        private ConfigEntry<float> armorPerSecond, armorPerSecondPerStack, damageCut;
        private ConfigEntry<bool> ignoreDot;

        protected override void BindConfig(ItemSettings s)
        {
            armorPerSecond = s.Float("ArmorPerSecond", 10f, 0f, 200f, new Words("Armor per second", "Броня в секунду"),
                new Words("Armor gained per second of standing still with the first copy.", "Броня за секунду на месте с первой копией."), "{0:0.#}");
            armorPerSecondPerStack = s.Float("ArmorPerSecondPerStack", 5f, 0f, 200f, new Words("Armor per second per stack", "Броня в секунду за копию"),
                new Words("Extra armor per second per additional copy.", "Прибавка за каждую копию сверх первой."), "{0:0.#}");
            damageCut = s.Float("DamageCut", 0.9f, 0f, 1f, new Words("Loss on damage", "Потеря от урона"),
                new Words("Fraction of the stored armor lost when you take damage (0.9 = 90%).", "Доля накопленной брони, теряемая при получении урона (0.9 = 90%)."), "{0:P0}");
            ignoreDot = s.Bool("IgnoreDamageOverTime", false, new Words("Ignore damage over time", "Не считать урон со временем"),
                new Words("Burning, bleeding and other DoT ticks do not cut the armor.", "Тики горения, кровотечения и прочего DoT не отнимают броню."));
        }

        private BuffDef armorBuff;

        protected override void Hook()
        {
            armorBuff = BuffFactory.Create("CHAMPIGNON_ARMOR", BuffFactory.Hex("#e8dcc8"), isDebuff: false, canStack: true);
            UseBehavior<ChampignonBehavior>(); // сервер

            RecalculateStatsAPI.GetStatCoefficients += (body, args) =>
            {
                int stacks = body.GetBuffCount(armorBuff);
                if (stacks > 0) args.armorAdd += stacks;
            };

            GlobalEventManager.onServerDamageDealt += report =>
            {
                var victim = report.victimBody;
                if (!victim || report.damageDealt <= 0f) return;
                if (ignoreDot.Value && report.dotType != DotController.DotIndex.None) return;
                var behavior = victim.GetComponent<ChampignonBehavior>();
                if (behavior) behavior.OnDamaged(damageCut.Value);
            };
        }

        internal float ArmorRate(int stack) => Stacking.Linear(stack, armorPerSecond.Value, armorPerSecondPerStack.Value);

        internal BuffDef ArmorBuff => armorBuff;

        internal static Champignon Instance;

        public Champignon() => Instance = this;

        /// <summary>Висит на теле, пока есть предмет (сервер). Копит броню и раз в 0.25 с отдаёт её стаками баффа.</summary>
        private sealed class ChampignonBehavior : CharacterBody.ItemBehavior
        {
            private const float SyncInterval = 0.25f;
            private float armor;
            private float syncTimer;

            private void FixedUpdate()
            {
                if (!NetworkServer.active || !body) return;
                var item = Instance;
                if (item == null) return;

                if (body.GetNotMoving()) armor += item.ArmorRate(stack) * Time.fixedDeltaTime;
                else if (armor > 0f) { armor = 0f; Sync(); return; }

                syncTimer -= Time.fixedDeltaTime;
                if (syncTimer <= 0f) Sync();
            }

            public void OnDamaged(float cut)
            {
                if (armor <= 0f) return;
                armor *= 1f - Mathf.Clamp01(cut);
                Sync();
            }

            private void Sync()
            {
                syncTimer = SyncInterval;
                var buff = Instance?.ArmorBuff;
                if (!buff || !body) return;
                int wanted = Mathf.FloorToInt(armor);
                if (body.GetBuffCount(buff) != wanted) body.SetBuffCount(buff.buffIndex, wanted);
            }

            private void OnDisable()
            {
                // Предмет пропал (или тело умерло) — броня тоже.
                armor = 0f;
                var buff = Instance?.ArmorBuff;
                if (NetworkServer.active && buff && body && body.GetBuffCount(buff) > 0) body.SetBuffCount(buff.buffIndex, 0);
            }
        }
    }
}
