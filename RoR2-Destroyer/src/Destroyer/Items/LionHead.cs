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
    /// Голова льва (зелёный). Враги в 20 м от тебя теряют 15 (+15) брони.
    ///
    /// Сервер 4 раза в секунду раздаёт врагам в радиусе дебафф LION_FEAR, стаки = копии носителя (несколько львов —
    /// берётся сильнейший, не складываются; общий помощник EnemyAura). Броня — RecalculateStatsAPI на каждой машине:
    /// armorAdd −= 15·стаки. Отрицательная броня в игре — это доп. урон (100/(100+броня) больше 1).
    /// </summary>
    internal sealed class LionHead : ItemBase
    {
        public override string Id => "LION_HEAD";
        public override ItemTier Tier => ItemTier.Tier2;
        public override string ModelFrom => "ArmorReductionOnHit"; // Shattering Justice — тоже про снятие брони
        public override ItemTag[] Tags => new[] { ItemTag.Damage };

        public override Words Name => new Words("Lion's Head", "Голова льва");

        public override Words Pickup => new Words(
            "Nearby enemies lose armor.",
            "Враги рядом теряют броню.");

        public override Words Description => new Words(
            $"Enemies within {Tx.Damage(Tx.Meters(radius.Value).En)} of you {Tx.Damage("lose " + Tx.Num(armor.Value) + " armor")} {Tx.Stack("+" + Tx.Num(armor.Value) + " per stack")}.",
            $"Враги в {Tx.Damage(Tx.Meters(radius.Value).Ru)} от тебя {Tx.Damage("теряют " + Tx.Num(armor.Value) + " брони")} {Tx.Stack("+" + Tx.Num(armor.Value) + " за копию")}.");

        public override Words Lore => new Words(
            "It still roars. Quietly. Only the brave can't hear it.",
            "Она всё ещё рычит. Тихо. Не слышат только храбрые.");

        private ConfigEntry<float> radius, armor;

        protected override void BindConfig(ItemSettings s)
        {
            radius = s.Float("Radius", 20f, 1f, 200f, new Words("Radius", "Радиус"),
                new Words("Aura radius, meters.", "Радиус ауры, м."), "{0:0} m");
            armor = s.Float("ArmorReduction", 15f, 0f, 500f, new Words("Armor reduction per stack", "Снижение брони за копию"),
                new Words("Armor removed from enemies per copy.", "Сколько брони снимается с врагов за каждую копию."), "{0:0}");
        }

        private BuffDef debuff;
        private EnemyAura aura;
        private readonly List<EnemyAura.Source> sources = new List<EnemyAura.Source>();
        private float tickTimer;

        protected override void Hook()
        {
            debuff = BuffFactory.Create("LION_FEAR", BuffFactory.Hex("#e0a03a"), isDebuff: true, canStack: true);
            aura = new EnemyAura(debuff);

            RecalculateStatsAPI.GetStatCoefficients += (body, args) =>
            {
                int n = debuff ? body.GetBuffCount(debuff) : 0;
                if (n > 0) args.armorAdd -= armor.Value * n;
            };

            RoR2Application.onFixedUpdate += Tick;
            Run.onRunStartGlobal += run => aura.Clear();
        }

        private void Tick()
        {
            if (!NetworkServer.active || !Run.instance) return;
            tickTimer -= Time.fixedDeltaTime;
            if (tickTimer > 0f) return;
            tickTimer = 0.25f;

            sources.Clear();
            foreach (var body in CharacterBody.readOnlyInstancesList)
            {
                if (!body || !body.healthComponent || !body.healthComponent.alive) continue;
                int n = Count(body);
                if (n > 0) sources.Add(new EnemyAura.Source { Holder = body, Radius = radius.Value, Stacks = n });
            }
            aura.Apply(sources);
        }
    }
}
