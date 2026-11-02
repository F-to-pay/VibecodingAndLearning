using System.Collections.Generic;
using Diabloid.Content;
using Diabloid.Loot;
using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace Diabloid.Gameplay
{
    /// <summary>
    /// Перки 0.14.0: души (урон за убийство), заражение, замедляющая аура, удача дропа, отражение урона.
    ///   души — скрытый предмет-счётчик в инвентаре: живёт весь забег, синхронизирует игра, статы честные у всех;
    ///   заражение — свой DoT на сервере (InfectionDot на жертве) + дебафф для иконки;
    ///   замедление — дебафф врагам (сервер вешает, игра синхронизирует), отнятые м/с — стаки баффа на игроке
    ///     (движение игрока считает его машина, поэтому скорость должна доехать до клиента — через число стаков);
    ///   удача — LootDrops спрашивает LuckBonus(убийца);
    ///   отражение — сервер, при получении урона.
    /// </summary>
    internal sealed partial class PerkManager
    {
        /// <summary>Скрытый предмет: число = сколько раз по +0.05 базового урона (с убийств перком "души").</summary>
        public static ItemDef KillDamageItem { get; private set; }

        public static BuffDef InfectionBuff { get; private set; }   // дебафф-иконка заражения, урон считает InfectionDot
        public static BuffDef SlowAuraBuff { get; private set; }    // враг замедлен на 10%
        public static BuffDef StolenSpeedBuff { get; private set; } // стаки × 0.1 м/с к базовой скорости игрока

        /// <summary>Сейчас тикает заражение — его урон не заражает заново (иначе висело бы вечно).</summary>
        internal static bool inInfection;

        private static bool inReflect;

        private static void RegisterNewPerks()
        {
            var def = ScriptableObject.CreateInstance<ItemDef>();
            def.name = "DIABLOID_KILLDAMAGE";
            def.nameToken = "DIABLOID_KILLDAMAGE_NAME";
            def.pickupToken = "DIABLOID_KILLDAMAGE_PICKUP";
            def.descriptionToken = "DIABLOID_KILLDAMAGE_DESC";
            def.loreToken = "DIABLOID_KILLDAMAGE_LORE";
            LootCatalog.SetTier(def, ItemTier.NoTier);
            def.hidden = true;
            def.canRemove = false;
            def.tags = new[]
            {
                ItemTag.WorldUnique, ItemTag.IgnoreForDropList, ItemTag.CommandArtifactBlacklist,
                ItemTag.CannotCopy, ItemTag.CannotSteal, ItemTag.CannotDuplicate,
                ItemTag.AIBlacklist, ItemTag.BrotherBlacklist,
            };
            GameLanguage.AddToken(def.nameToken, new Words("Souls", "Души"));
            GameLanguage.AddToken(def.pickupToken, new Words("Base damage from kills.", "Базовый урон с убийств."));
            GameLanguage.AddToken(def.descriptionToken, new Words("Base damage from kills.", "Базовый урон с убийств."));
            GameLanguage.AddToken(def.loreToken, new Words("", ""));
            ItemAPI.Add(new CustomItem(def, new ItemDisplayRuleDict(null)));
            KillDamageItem = def;

            InfectionBuff = NewBuff("bdDiabloidInfection", new Color32(120, 220, 60, 255), stack: false);
            InfectionBuff.isDebuff = true;
            SlowAuraBuff = NewBuff("bdDiabloidSlowAura", new Color32(120, 160, 255, 255), stack: false);
            SlowAuraBuff.isDebuff = true;
            StolenSpeedBuff = NewBuff("bdDiabloidStolenSpeed", new Color32(120, 160, 255, 255), stack: true);
        }

        private static void OnCatalogsReadyNew()
        {
            if (RoR2Content.Buffs.Poisoned) InfectionBuff.iconSprite = RoR2Content.Buffs.Poisoned.iconSprite;
            if (RoR2Content.Buffs.Slow50) SlowAuraBuff.iconSprite = RoR2Content.Buffs.Slow50.iconSprite;
            if (RoR2Content.Buffs.CloakSpeed) StolenSpeedBuff.iconSprite = RoR2Content.Buffs.CloakSpeed.iconSprite;
        }

        /// <summary>Сколько базового урона дали души (для статов и панели F2).</summary>
        public static float KillDamageOf(Inventory inventory)
        {
            if (!inventory || !KillDamageItem || KillDamageItem.itemIndex == ItemIndex.None) return 0f;
            return inventory.GetItemCountPermanent(KillDamageItem.itemIndex) * Perks.KillDamagePerKill;
        }

        /// <summary>Статы новых перков — на всех машинах (предмет и баффы синхронизирует игра).</summary>
        private static void NewPerkStats(CharacterBody body, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (body.inventory) args.baseDamageAdd += KillDamageOf(body.inventory);

            // −10% скорости ровно: делитель 1 + 1/9.
            if (SlowAuraBuff && body.HasBuff(SlowAuraBuff))
                args.moveSpeedReductionMultAdd += Perks.SlowPercent / (100f - Perks.SlowPercent);

            int stolen = StolenSpeedBuff ? body.GetBuffCount(StolenSpeedBuff) : 0;
            if (stolen > 0) args.baseMoveSpeedAdd += stolen * Perks.StolenSpeedUnit;
        }

        /// <summary>Удача качества дропа с жертвы этого убийцы: +0.2 за каждый перк удачи (турель — перки хозяина).</summary>
        public static float LuckBonus(CharacterMaster killer)
        {
            if (!killer) return 0f;
            float luck = 0f;
            foreach (var (perk, _) in PerksOf(killer))
                if (perk.Type == PerkType.LootLuck) luck += Perks.LootLuckBonus;
            return luck;
        }

        /// <summary>
        /// Души: за каждый перк +1 к счётчику за обычного, +5 за элитку, +50 за босса/чемпиона (0.17.1).
        /// Копии титана считаются обычными. Убила турель — счётчик хозяину (перк-то его).
        /// </summary>
        private static void KillDamageOnKill(DamageReport report, List<(Perk perk, int level)> perks)
        {
            var killer = report.attackerMaster;
            int count = 0;
            foreach (var (perk, _) in perks)
                if (perk.Type == PerkType.KillDamage) count++;
            if (count == 0 || !KillDamageItem) return;

            var receiver = GearRegistry.InheritsGear(killer) ? GearRegistry.OwnerOf(killer) : killer;
            int souls = report.victimIsBoss || report.victimIsChampion ? Perks.SoulsBoss
                      : report.victimIsElite && !Elites.TitanMarker.IsCopyMaster(report.victimMaster) ? Perks.SoulsElite
                      : Perks.SoulsNormal;
            if (receiver && receiver.inventory) receiver.inventory.GiveItemPermanent(KillDamageItem.itemIndex, count * souls);
        }

        /// <summary>Заражение: шанс 100% × proc попадания. Не стакается — повторное заражение обновляет 20 с и хозяина.</summary>
        private static void TryInfect(DamageReport report, List<(Perk perk, int level)> attackerPerks, float proc)
        {
            if (inInfection) return;
            bool has = false;
            foreach (var (perk, _) in attackerPerks)
                if (perk.Type == PerkType.Infection) { has = true; break; }
            if (!has) return;

            var victim = report.victimBody;
            if (!victim || !victim.healthComponent || !victim.healthComponent.alive) return;
            if (!Util.CheckRoll(Perks.InfectionChance * proc, report.attackerMaster)) return;

            var dot = victim.GetComponent<InfectionDot>();
            if (!dot) dot = victim.gameObject.AddComponent<InfectionDot>();
            dot.Infect(report.attackerBody);
            victim.AddTimedBuff(InfectionBuff, Perks.InfectionDuration);
        }

        /// <summary>
        /// Отражение: 50% полученного (после брони) урона — каждому врагу в 50 м, proc 0 (ничего не запускает),
        /// лечение на 10% от всего отражённого. Свой урон (кровь святилища и т. п.) не отражается.
        /// </summary>
        private static void Reflect(DamageReport report, List<(Perk perk, int level)> perks)
        {
            if (inReflect) return;
            var body = report.victimBody;
            if (report.attackerBody == body) return;

            int count = 0;
            foreach (var (perk, _) in perks)
                if (perk.Type == PerkType.ReflectDamage) count++;
            if (count == 0) return;

            float damage = report.damageDealt * Perks.ReflectPercent / 100f * count;
            if (damage <= 0f) return;

            var team = body.teamComponent ? body.teamComponent.teamIndex : TeamIndex.Player;
            float radiusSqr = Perks.ReflectRadius * Perks.ReflectRadius;
            var targets = new List<CharacterBody>();
            foreach (var other in CharacterBody.readOnlyInstancesList)
                if (IsEnemy(team, other) && (other.corePosition - body.corePosition).sqrMagnitude <= radiusSqr)
                    targets.Add(other);
            if (targets.Count == 0) return;

            inReflect = true;
            try
            {
                foreach (var target in targets)
                {
                    if (!target || !target.healthComponent || !target.healthComponent.alive) continue;
                    target.healthComponent.TakeDamage(new DamageInfo
                    {
                        attacker = body.gameObject,
                        inflictor = body.gameObject,
                        damage = damage,
                        position = target.corePosition,
                        crit = false,
                        procCoefficient = 0f,
                        procChainMask = default(ProcChainMask),
                        damageType = DamageType.Generic,
                        damageColorIndex = DamageColorIndex.Item,
                        force = Vector3.zero,
                    });
                }
            }
            finally
            {
                inReflect = false;
            }

        }

        /// <summary>
        /// Замедляющая аура, раз в 0.5 с: враги в 30 м получают дебафф на 1 с, игроку — сумма 10% их скорости (м/с)
        /// за каждый такой перк. Скорость врага берём "до нашего замедления": уже замедленный — делим на 0.9.
        /// </summary>
        private void SlowAuraTick(CharacterBody source, int perkCount)
        {
            var team = source.teamComponent ? source.teamComponent.teamIndex : TeamIndex.Player;
            float radiusSqr = Perks.SlowRadius * Perks.SlowRadius;
            float share = Perks.SlowPercent / 100f;
            float stolen = 0f;

            foreach (var victim in bodies)
            {
                if (!IsEnemy(team, victim)) continue;
                if ((victim.corePosition - source.corePosition).sqrMagnitude > radiusSqr) continue;
                float speed = victim.moveSpeed;
                if (SlowAuraBuff && victim.HasBuff(SlowAuraBuff)) speed /= 1f - share;
                stolen += speed * share;
                victim.AddTimedBuff(SlowAuraBuff, AuraTickInterval * 2f);
            }

            SetStolenSpeed(source, Mathf.RoundToInt(stolen * perkCount / Perks.StolenSpeedUnit));
        }

        private static void SetStolenSpeed(CharacterBody body, int stacks)
        {
            if (!StolenSpeedBuff) return;
            stacks = Mathf.Clamp(stacks, 0, 10000);
            if (body.GetBuffCount(StolenSpeedBuff) != stacks) body.SetBuffCount(StolenSpeedBuff.buffIndex, stacks);
        }
    }

    /// <summary>
    /// Заражение на жертве (только сервер): раз в секунду 10% урона заразившего (текущего, если он жив), proc 0.4,
    /// после урона — "попадание", чтобы сработали предметы. 20 с от последнего заражения, потом компонент уходит.
    /// </summary>
    internal sealed class InfectionDot : MonoBehaviour
    {
        private CharacterBody victim;
        private GameObject attackerObject;
        private CharacterBody attacker;
        private float lastDamage;
        private float endsAt;
        private float tickTimer;

        private void Awake() => victim = GetComponent<CharacterBody>();

        public void Infect(CharacterBody by)
        {
            attacker = by;
            attackerObject = by ? by.gameObject : null;
            if (by) lastDamage = by.damage;
            endsAt = Time.fixedTime + Perks.InfectionDuration;
            if (tickTimer <= 0f) tickTimer = Perks.InfectionTickInterval;
        }

        private void FixedUpdate()
        {
            if (!NetworkServer.active || !victim || !victim.healthComponent || !victim.healthComponent.alive ||
                Time.fixedTime >= endsAt)
            {
                Destroy(this);
                return;
            }

            tickTimer -= Time.fixedDeltaTime;
            if (tickTimer > 0f) return;
            tickTimer += Perks.InfectionTickInterval;

            if (attacker) lastDamage = attacker.damage;
            float damage = lastDamage * Perks.InfectionDamagePercent / 100f * Perks.InfectionTickInterval;
            if (damage <= 0f) return;

            var info = new DamageInfo
            {
                attacker = attackerObject,
                inflictor = attackerObject,
                damage = damage,
                position = victim.corePosition,
                crit = false,
                procCoefficient = Perks.InfectionProcCoefficient,
                procChainMask = default(ProcChainMask),
                damageType = DamageType.Generic,
                damageColorIndex = DamageColorIndex.Poison,
                force = Vector3.zero,
            };

            PerkManager.inInfection = true;
            try
            {
                victim.healthComponent.TakeDamage(info);
                if (attackerObject)
                {
                    GlobalEventManager.instance.OnHitEnemy(info, victim.gameObject);
                    GlobalEventManager.instance.OnHitAll(info, victim.gameObject);
                }
            }
            finally
            {
                PerkManager.inInfection = false;
            }
        }
    }
}
