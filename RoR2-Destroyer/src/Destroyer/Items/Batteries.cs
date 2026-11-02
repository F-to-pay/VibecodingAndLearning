using BepInEx.Configuration;
using Destroyer.Content;
using Destroyer.Core;
using R2API;
using RoR2;
using RoR2.Orbs;
using UnityEngine;

namespace Destroyer.Items
{
    /// <summary>
    /// Батарейки (зелёный). Твои механические миньоны (дроны, турели — и Инженера тоже, шарики) быстрее на 15%,
    /// и их попадания с шансом 10% выпускают заряженный разряд на 100% (+100%) базового урона САМОГО дрона.
    ///
    /// "Механический миньон" — как у ванильных Spare Drone Parts (IL DroneWeaponsBehavior 1.5): у тела флаг Mechanical,
    /// у мастера есть хозяин (MinionOwnership). Разряд — ванильный LightningOrb (Tesla) с proc 0: ничего не прокает,
    /// поэтому сам себя не повторяет. Шанс × proc coefficient удара, удача — хозяина.
    /// </summary>
    internal sealed class Batteries : ItemBase
    {
        public override string Id => "BATTERIES";
        public override ItemTier Tier => ItemTier.Tier2;
        public override string ModelFrom => "EquipmentMagazine"; // Fuel Cell — та же батарейка
        public override ItemTag[] Tags => new[] { ItemTag.Damage, ItemTag.AIBlacklist, ItemTag.CannotCopy };

        public override Words Name => new Words("Batteries", "Батарейки");

        public override Words Pickup => new Words(
            "Your drones move faster and sometimes fire a charged shot.",
            "Дроны быстрее и иногда стреляют заряженным разрядом.");

        public override Words Description => new Words(
            $"Your drones and turrets gain {Tx.Utility("+" + Tx.Pct(speed.Value) + " movement speed")} and have a {Tx.Damage(Tx.Pct(chance.Value))} chance on hit " +
            $"to fire a {Tx.Damage("charged shot")} for {Tx.Damage(Tx.Pct(shotDamage.Value) + " base damage")} {Tx.Stack("+" + Tx.Pct(shotDamagePerStack.Value) + " per stack")}.",
            $"Твои дроны и турели получают {Tx.Utility("+" + Tx.Pct(speed.Value) + " к скорости")} и с шансом {Tx.Damage(Tx.Pct(chance.Value))} при попадании " +
            $"выпускают {Tx.Damage("заряженный разряд")} на {Tx.Damage(Tx.Pct(shotDamage.Value) + " базового урона")} {Tx.Stack("+" + Tx.Pct(shotDamagePerStack.Value) + " за копию")}.");

        public override Words Lore => new Words(
            "AA. Not rechargeable. Do not lick.",
            "АА. Не перезаряжаемые. Не лизать.");

        private ConfigEntry<float> speed, chance, shotDamage, shotDamagePerStack;

        protected override void BindConfig(ItemSettings s)
        {
            speed = s.Float("MoveSpeed", 0.15f, 0f, 2f, new Words("Drone speed", "Скорость дронов"),
                new Words("Movement speed bonus for your mechanical minions (does not grow with stacks).", "Прибавка к скорости механических миньонов (от копий не растёт)."), "{0:P0}");
            chance = s.Float("ShotChance", 0.10f, 0f, 1f, new Words("Charged shot chance", "Шанс разряда"),
                new Words("Chance on hit for a drone (0.1 = 10%), scaled by the attack's proc coefficient.", "Шанс при попадании дрона (0.1 = 10%), умножается на proc coefficient."), "{0:P0}");
            shotDamage = s.Float("ShotDamage", 1f, 0f, 20f, new Words("Charged shot damage", "Урон разряда"),
                new Words("Damage in the DRONE's base damage with the first copy (1 = 100%).", "Урон в базовом уроне ДРОНА с первой копией (1 = 100%)."), "{0:P0}");
            shotDamagePerStack = s.Float("ShotDamagePerStack", 1f, 0f, 20f, new Words("Charged shot damage per stack", "Урон разряда за копию"),
                new Words("Extra damage per additional copy.", "Прибавка за каждую копию сверх первой."), "{0:P0}");
        }

        protected override void Hook()
        {
            RecalculateStatsAPI.GetStatCoefficients += (body, args) =>
            {
                var owner = OwnerOfMechanical(body);
                if (owner && Count(owner) > 0) args.moveSpeedMultAdd += speed.Value;
            };

            // Хозяин взял/потерял батарейки — пересчитать статы его дронам.
            CharacterBody.onBodyInventoryChangedGlobal += ownerBody =>
            {
                if (!ownerBody || !ownerBody.master) return;
                foreach (var body in CharacterBody.readOnlyInstancesList)
                    if (body && OwnerOfMechanical(body) == ownerBody.master) body.MarkAllStatsDirty();
            };

            HitEvents.OnHit += OnHit;
        }

        /// <summary>Хозяин механического миньона или null.</summary>
        private static CharacterMaster OwnerOfMechanical(CharacterBody body)
        {
            if (!body || (body.bodyFlags & CharacterBody.BodyFlags.Mechanical) == 0) return null;
            var master = body.master;
            var ownership = master ? master.minionOwnership : null;
            return ownership ? ownership.ownerMaster : null;
        }

        private void OnHit(DamageInfo info, CharacterBody attacker, CharacterBody victim)
        {
            var owner = OwnerOfMechanical(attacker);
            int n = Count(owner);
            if (n <= 0 || !victim.mainHurtBox || !OrbManager.instance) return;
            if (!Roll(chance.Value, info.procCoefficient, owner)) return;

            OrbManager.instance.AddOrb(new LightningOrb
            {
                origin = attacker.corePosition,
                target = victim.mainHurtBox,
                attacker = attacker.gameObject,
                inflictor = attacker.gameObject,
                teamIndex = attacker.teamComponent ? attacker.teamComponent.teamIndex : TeamIndex.Player,
                damageValue = attacker.damage * Stacking.Linear(n, shotDamage.Value, shotDamagePerStack.Value),
                isCrit = attacker.RollCrit(),
                procCoefficient = 0f, // разряд ничего не прокает — и не порождает новый разряд
                procChainMask = info.procChainMask,
                lightningType = LightningOrb.LightningType.Tesla,
                bouncesRemaining = 0,
                damageColorIndex = DamageColorIndex.Item,
                range = 0f,
            });
        }
    }
}
