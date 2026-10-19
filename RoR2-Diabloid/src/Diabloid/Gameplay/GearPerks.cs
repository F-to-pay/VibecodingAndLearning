using System;
using System.Collections.Generic;
using Diabloid.Config;
using Diabloid.Loot;
using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;

namespace Diabloid.Gameplay
{
    /// <summary>
    /// Перки шмоток в игре. Кто где считается:
    ///   аура урона, удар по % ХП, броня от урона, реген от недостающего ХП — сервер (урон и лечение считает он);
    ///   берсерк (базовый урон) — каждая машина сама: клиент стреляет со своим body.damage, здоровье синхронизирует игра;
    ///   сфера ауры — косметика, у каждого своя.
    /// Одинаковые перки с разных шмоток работают по отдельности: две ауры — два радиуса и два урона.
    /// </summary>
    internal sealed class PerkManager : MonoBehaviour
    {
        private const float AuraTickInterval = 0.5f;

        /// <summary>Бафф "броня от полученного урона" — только иконка и таймер; сама броня лежит в PerkBodyState на сервере.</summary>
        public static BuffDef ArmorBuff { get; private set; }

        public static Material AuraMaterial { get; private set; }

        /// <summary>Бафф "боевой клич бега": +20% базового урона. Вешает сервер, синхронизирует игра — статы честные у всех.</summary>
        public static BuffDef WarcryBuff { get; private set; }

        /// <summary>Метка мини-голема: скорость ×2, урон ×0.3, размер ×0.5. Бафф, а не компонент, — чтобы клиенты тоже знали.</summary>
        public static BuffDef MiniGolemBuff { get; private set; }

        private static readonly List<(Perk perk, int level)> noPerks = new List<(Perk, int)>();

        /// <summary>Сейчас наносится дополнительный удар — не порождать из него ещё один (иначе бесконечная цепочка).</summary>
        private static bool inExtraHit;

        /// <summary>Сейчас взрывается взрыв перка — взрывы от него не порождают новые взрывы.</summary>
        private static bool inExplosion;

        // 0.10.0
        public static BuffDef MomentumBuff { get; private set; }   // стаки разгона (до 5), сервер вешает, все считают
        public static BuffDef SwarmWispBuff { get; private set; }  // огонёк роя: здоровье ×10, урон ×0.2
        public static BuffDef SolusBuff { get; private set; }      // зонд Солус: атака и бег ×2, урон и здоровье ×3
        public static BuffDef CastingBuff { get; private set; }    // каст призыва Солуса: стаки = секунды простоя
        private static GameObject explosionEffect;
        private readonly List<CharacterBody> bodies = new List<CharacterBody>();
        private float auraTimer;

        // ---------------------------------------------------------------- регистрация

        public static void Register()
        {
            ArmorBuff = ScriptableObject.CreateInstance<BuffDef>();
            ArmorBuff.name = "bdDiabloidPerkArmor";
            ArmorBuff.buffColor = new Color32(255, 204, 85, 255);
            ArmorBuff.canStack = true;
            ArmorBuff.isDebuff = false;
            ContentAddition.AddBuffDef(ArmorBuff);

            WarcryBuff = ScriptableObject.CreateInstance<BuffDef>();
            WarcryBuff.name = "bdDiabloidPerkWarcry";
            WarcryBuff.buffColor = new Color32(255, 140, 60, 255);
            WarcryBuff.canStack = false;
            WarcryBuff.isDebuff = false;
            ContentAddition.AddBuffDef(WarcryBuff);

            MiniGolemBuff = ScriptableObject.CreateInstance<BuffDef>();
            MiniGolemBuff.name = "bdDiabloidMiniGolem";
            MiniGolemBuff.buffColor = new Color32(180, 180, 180, 255);
            MiniGolemBuff.canStack = false;
            MiniGolemBuff.isDebuff = false;
            ContentAddition.AddBuffDef(MiniGolemBuff);

            MomentumBuff = NewBuff("bdDiabloidMomentum", new Color32(120, 220, 255, 255), stack: true);
            SwarmWispBuff = NewBuff("bdDiabloidSwarmWisp", new Color32(200, 200, 255, 255), stack: false);
            SolusBuff = NewBuff("bdDiabloidSolusAlly", new Color32(255, 220, 120, 255), stack: false);
            CastingBuff = NewBuff("bdDiabloidSolusCast", new Color32(255, 220, 120, 255), stack: true);

            RecalculateStatsAPI.GetStatCoefficients += OnGetStatCoefficients;
            GlobalEventManager.onServerDamageDealt += OnServerDamageDealt;
            GlobalEventManager.onCharacterDeathGlobal += OnCharacterDeath;
            On.RoR2.TeamManager.GiveTeamExperience += OnGiveTeamExperience;
            CharacterBody.onBuffFirstStackGained += OnBuffFirstStackGained;
        }

        private static BuffDef NewBuff(string name, Color32 color, bool stack)
        {
            var buff = ScriptableObject.CreateInstance<BuffDef>();
            buff.name = name;
            buff.buffColor = color;
            buff.canStack = stack;
            buff.isDebuff = false;
            ContentAddition.AddBuffDef(buff);
            return buff;
        }

        /// <summary>Опыт команды игроков: +10% за каждый перк опыта у любого игрока (опыт в RoR2 общий на команду).</summary>
        private static void OnGiveTeamExperience(On.RoR2.TeamManager.orig_GiveTeamExperience orig, TeamManager self, TeamIndex teamIndex, ulong experience)
        {
            if (teamIndex == TeamIndex.Player && experience > 0)
            {
                int bonusPerks = 0;
                foreach (var player in PlayerCharacterMasterController.instances)
                {
                    var gear = player ? GearRegistry.Find(player.master) : null;
                    if (gear == null) continue;
                    foreach (var (perk, _) in PerksOf(player.master))
                        if (perk.Type == PerkType.XpBonus) bonusPerks++;
                }
                if (bonusPerks > 0)
                    experience = (ulong)Math.Round(experience * (1.0 + bonusPerks * Perks.XpBonusPercent / 100.0));
            }
            orig(self, teamIndex, experience);
        }

        /// <summary>Мини-голем уменьшается на каждой машине сама — бафф синхронизирует игра.</summary>
        private static void OnBuffFirstStackGained(CharacterBody body, BuffDef buff)
        {
            if (buff == MiniGolemBuff && body && !body.GetComponent<ModelScaler>())
                body.gameObject.AddComponent<ModelScaler>().Multiplier = Perks.GolemScale;
        }

        public static void OnCatalogsReady()
        {
            if (RoR2Content.Buffs.ArmorBoost) ArmorBuff.iconSprite = RoR2Content.Buffs.ArmorBoost.iconSprite;
            if (RoR2Content.Buffs.WarCryBuff) WarcryBuff.iconSprite = RoR2Content.Buffs.WarCryBuff.iconSprite;
            if (RoR2Content.Buffs.ArmorBoost) MiniGolemBuff.iconSprite = RoR2Content.Buffs.ArmorBoost.iconSprite;
            if (RoR2Content.Buffs.CloakSpeed) MomentumBuff.iconSprite = RoR2Content.Buffs.CloakSpeed.iconSprite;
            if (RoR2Content.Buffs.ArmorBoost) SwarmWispBuff.iconSprite = RoR2Content.Buffs.ArmorBoost.iconSprite;
            if (RoR2Content.Buffs.ArmorBoost) SolusBuff.iconSprite = RoR2Content.Buffs.ArmorBoost.iconSprite;
            if (RoR2Content.Buffs.Entangle) CastingBuff.iconSprite = RoR2Content.Buffs.Entangle.iconSprite;
            try { explosionEffect = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/OmniEffect/OmniExplosionVFXQuick"); }
            catch (Exception e) { DiabloidPlugin.Log.LogWarning($"Perks: explosion effect not loaded: {e.Message}"); }
            // Сфера ауры игрока — тот же материал, что у часовщика: другого готового у игры под рукой нет.
            AuraMaterial = LoadMaterial("RoR2/Base/Common/matTeamAreaIndicatorFullMonster.mat");
        }

        private static Material LoadMaterial(string key)
        {
            try { return Addressables.LoadAssetAsync<Material>(key).WaitForCompletion(); }
            catch (Exception e)
            {
                DiabloidPlugin.Log.LogWarning($"Could not load material '{key}': {e.Message}");
                return null;
            }
        }

        // ---------------------------------------------------------------- общие помощники

        /// <summary>
        /// Перки игрока (по мастеру). Каждый раз НОВЫЙ список, а не общий буфер: урон от перка тут же вызывает
        /// onServerDamageDealt, тот снова спрашивает перки — общий буфер поменялся бы прямо посреди foreach.
        /// Монстры (без экипировки) получают общий пустой список без аллокаций.
        /// </summary>
        private static List<(Perk perk, int level)> PerksOf(CharacterMaster master)
        {
            var gear = GearRegistry.FindWorn(master); // турель Инженера — перки хозяина
            if (gear == null) return noPerks;
            var list = new List<(Perk, int)>(2);
            gear.CollectPerks(list);
            return list.Count > 0 ? list : noPerks;
        }

        private static int BerserkSteps(CharacterBody body)
        {
            var health = body.healthComponent;
            if (!health || !health.alive) return 0;
            float missing = Mathf.Max(0f, health.fullHealth - health.health);
            return Mathf.FloorToInt(missing / Perks.BerserkHealthStep);
        }

        private static bool IsEnemy(TeamIndex mine, CharacterBody victim)
        {
            if (!victim || !victim.healthComponent || !victim.healthComponent.alive) return false;
            var team = victim.teamComponent ? victim.teamComponent.teamIndex : TeamIndex.None;
            return team != mine && team != TeamIndex.Neutral && team != TeamIndex.None;
        }

        // ---------------------------------------------------------------- статы (все машины)

        private static void OnGetStatCoefficients(CharacterBody body, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!body) return;

            // Боевой клич бега (на любом союзнике, не только игроке): +20% базового урона персонажа на его уровне.
            if (WarcryBuff && body.HasBuff(WarcryBuff))
                args.baseDamageAdd += (body.baseDamage + body.levelDamage * (body.level - 1f)) * Perks.WarcryDamagePercent / 100f;

            // Огонёк роя: здоровье ×10, урон ×0.2.
            if (SwarmWispBuff && body.HasBuff(SwarmWispBuff))
            {
                args.healthMultAdd += Perks.SwarmHealth - 1f;
                args.damageMultAdd += Perks.SwarmDamage - 1f;
            }

            // Зонд Солус: атака и бег ×2, урон и здоровье ×3.
            if (SolusBuff && body.HasBuff(SolusBuff))
            {
                args.attackSpeedMultAdd += Perks.SolusSpeed - 1f;
                args.moveSpeedMultAdd += Perks.SolusSpeed - 1f;
                args.damageMultAdd += Perks.SolusPower - 1f;
                args.healthMultAdd += Perks.SolusPower - 1f;
            }

            // Мини-голем: скорость ×2, урон ×0.3.
            if (MiniGolemBuff && body.HasBuff(MiniGolemBuff))
            {
                args.moveSpeedMultAdd += Perks.GolemSpeed - 1f;
                args.damageMultAdd += Perks.GolemDamage - 1f;
            }

            if (!body.master) return;
            var perks = PerksOf(body.master);
            if (perks.Count == 0) return;

            // Разгон: стаки баффа × (1% × уровень шмотки) с каждого такого перка — прибавка к множителю бега.
            int momentum = MomentumBuff ? body.GetBuffCount(MomentumBuff) : 0;
            if (momentum > 0)
                foreach (var (perk, level) in perks)
                    if (perk.Type == PerkType.SprintMomentum)
                        args.sprintSpeedAdd += momentum * Perk.MomentumPerStack(level) / 100f;

            // Броня от здоровья: 2% макс. ХП за каждый перк, и 0.1 базового урона за каждую единицу этой брони.
            // maxHealth — с прошлого пересчёта; когда он меняется, Update дёргает пересчёт ещё раз.
            int healthArmorPerks = 0;
            foreach (var (perk, _) in perks)
                if (perk.Type == PerkType.HealthArmor) healthArmorPerks++;
            if (healthArmorPerks > 0)
            {
                float armor = healthArmorPerks * body.maxHealth * Perks.HealthArmorPercent / 100f;
                args.armorAdd += armor;
                args.baseDamageAdd += armor * Perks.HealthArmorDamagePerArmor;
            }

            // Берсерк: + (1 × рост от уровня шмотки) базового урона за каждые 70 недостающего здоровья.
            int steps = BerserkSteps(body);
            if (steps > 0)
                foreach (var (perk, level) in perks)
                    if (perk.Type == PerkType.Berserk)
                        args.baseDamageAdd += steps * Perk.BerserkDamagePerStep(level);

            // Броня от урона — есть только на сервере (там и считается урон).
            var state = body.GetComponent<PerkBodyState>();
            if (state) args.armorAdd += state.ActiveArmor();
        }

        // ---------------------------------------------------------------- урон (сервер)

        private static void OnServerDamageDealt(DamageReport report)
        {
            if (report == null || !report.victimBody) return;

            // Жертва — игрок с бронёй от урона.
            if (report.victimMaster && report.damageDealt > 0f)
            {
                var perks = PerksOf(report.victimMaster);
                if (perks.Count > 0) OnPlayerHurt(report.victimBody, perks, report.damageDealt);
            }

            // Атакующий — игрок с ударом по % ХП. Шанс × proc coefficient, как у ванильных предметов;
            // сам удар идёт с proc 0, так что сам себя не вызывает.
            float proc = report.damageInfo != null ? report.damageInfo.procCoefficient : 0f;
            if (!report.attackerBody || !report.attackerMaster || proc <= 0f) return;
            if (report.attackerBody == report.victimBody) return;
            var attackerPerks = PerksOf(report.attackerMaster);
            if (attackerPerks.Count == 0) return;

            var victimHealth = report.victimBody.healthComponent;
            foreach (var (perk, _) in attackerPerks)
            {
                if (perk.Type != PerkType.PercentHpStrike) continue;
                if (!victimHealth || !victimHealth.alive) break;
                if (!Util.CheckRoll(perk.StrikeChance * proc, report.attackerMaster)) continue;

                // Боссы телепорта (и любые из босс-группы) получают 1/5.
                float bossMultiplier = report.victimIsBoss ? Perks.StrikeBossMultiplier : 1f;
                victimHealth.TakeDamage(new DamageInfo
                {
                    attacker = report.attackerBody.gameObject,
                    inflictor = report.attackerBody.gameObject,
                    damage = victimHealth.fullCombinedHealth * perk.StrikeHpPercent / 100f * bossMultiplier,
                    position = report.victimBody.corePosition,
                    crit = false,
                    procCoefficient = 0f,
                    procChainMask = default(ProcChainMask),
                    damageType = DamageType.Generic,
                    damageColorIndex = DamageColorIndex.Item,
                    force = Vector3.zero,
                });
            }

            // Взрыв: 10% × proc попадания, 200% урона героя в 20 м вокруг цели, proc 0.2. Взрывы от взрыва не взрываются.
            if (!inExplosion)
                foreach (var (perk, _) in attackerPerks)
                {
                    if (perk.Type != PerkType.ExplodeOnHit) continue;
                    if (!Util.CheckRoll(Perks.ExplodeChance * proc, report.attackerMaster)) continue;
                    Explode(report.attackerBody, report.victimBody.corePosition);
                }

            // Дополнительный удар: шанс 100% × proc coefficient попадания (у атаки с proc 0.2 — 20%), как у ванильных предметов.
            // 10% урона попадания (до брони и крита — их посчитает TakeDamage), сам удар с proc 0.1.
            // Пока он наносится, новые дополнительные удары не порождаются — иначе каждый вызывал бы следующий.
            if (inExtraHit || report.damageInfo == null) return;
            foreach (var (perk, _) in attackerPerks)
            {
                if (perk.Type != PerkType.ExtraHit) continue;
                if (!report.victimBody || !victimHealth || !victimHealth.alive) break;
                if (!Util.CheckRoll(Perks.ExtraHitChance * proc, report.attackerMaster)) continue;

                var info = new DamageInfo
                {
                    attacker = report.attackerBody.gameObject,
                    inflictor = report.attackerBody.gameObject,
                    damage = report.damageInfo.damage * Perks.ExtraHitPercent / 100f,
                    position = report.victimBody.corePosition,
                    crit = report.damageInfo.crit,
                    procCoefficient = Perks.ExtraHitProcCoefficient,
                    procChainMask = report.damageInfo.procChainMask,
                    damageType = DamageType.Generic,
                    damageColorIndex = DamageColorIndex.Item,
                    force = Vector3.zero,
                };
                var victimObject = report.victimBody.gameObject;
                inExtraHit = true;
                try
                {
                    victimHealth.TakeDamage(info);
                    GlobalEventManager.instance.OnHitEnemy(info, victimObject);
                    GlobalEventManager.instance.OnHitAll(info, victimObject);
                }
                finally
                {
                    inExtraHit = false;
                }
            }
        }

        private static void Explode(CharacterBody attacker, Vector3 position)
        {
            inExplosion = true;
            try
            {
                new BlastAttack
                {
                    attacker = attacker.gameObject,
                    inflictor = attacker.gameObject,
                    teamIndex = attacker.teamComponent ? attacker.teamComponent.teamIndex : TeamIndex.Player,
                    baseDamage = attacker.damage * Perks.ExplodeDamagePercent / 100f,
                    baseForce = 0f,
                    position = position,
                    radius = Perks.ExplodeRadius,
                    procCoefficient = Perks.ExplodeProcCoefficient,
                    crit = attacker.RollCrit(),
                    damageColorIndex = DamageColorIndex.Item,
                    falloffModel = BlastAttack.FalloffModel.None,
                    attackerFiltering = AttackerFiltering.NeverHitSelf,
                }.Fire();
                if (explosionEffect)
                    EffectManager.SpawnEffect(explosionEffect, new EffectData { origin = position, scale = Perks.ExplodeRadius }, true);
            }
            finally
            {
                inExplosion = false;
            }
        }

        /// <summary>Огонёк с убийства: 15% (с удачей) за каждый такой перк убийцы. Убийства самих огоньков не считаются — у них свой мастер.</summary>
        private static void OnCharacterDeath(DamageReport report)
        {
            if (!NetworkServer.active || report == null || !report.victimBody) return;

            // Диагностика: отчего и через сколько умер мини-голем (жалоба "живёт 10–20 с").
            if (MiniGolemBuff && report.victimBody.HasBuff(MiniGolemBuff))
            {
                var info = report.victimMaster ? report.victimMaster.GetComponent<PerkSummonInfo>() : null;
                float alive = info ? Time.time - info.SpawnTime : -1f;
                string killer = report.attackerBody ? report.attackerBody.name : "nobody";
                var damageType = report.damageInfo != null ? report.damageInfo.damageType.damageType.ToString() : "?";
                DiabloidPlugin.Log.LogInfo($"Perks: mini golem died after {alive:0.0}s (killer: {killer}, damage {report.damageDealt:0}, type {damageType}).");
            }

            if (!report.attackerBody || !report.attackerMaster) return;
            var perks = PerksOf(report.attackerMaster);
            if (perks.Count == 0) return;

            foreach (var (perk, _) in perks)
            {
                if (perk.Type != PerkType.WispOnKill) continue;
                if (!Util.CheckRoll(Perks.WispChance, report.attackerMaster)) continue;
                Summon("WispMaster", report.victimBody.corePosition, report.attackerBody, Perks.WispLifetime);
            }
        }

        /// <summary>
        /// Союзник на время: ванильный MasterSummon (как дроны и призраки маски) + MasterSuicideOnTimer.
        /// Команда и уровень — от хозяина. Союзники не дропают шмотки: LootDrops смотрит только на вражеские команды.
        /// </summary>
        private static CharacterMaster Summon(string masterName, Vector3 position, CharacterBody owner, float lifetime)
        {
            var prefab = MasterCatalog.FindMasterPrefab(masterName);
            if (!prefab)
            {
                DiabloidPlugin.Log.LogWarning($"Perks: master prefab '{masterName}' not found.");
                return null;
            }

            var master = new MasterSummon
            {
                masterPrefab = prefab,
                position = position,
                rotation = Quaternion.identity,
                summonerBodyObject = owner.gameObject,
                teamIndexOverride = owner.teamComponent ? owner.teamComponent.teamIndex : TeamIndex.Player,
                ignoreTeamMemberLimit = true,
            }.Perform();

            if (master)
            {
                master.gameObject.AddComponent<PerkSummonInfo>().SpawnTime = Time.time;
                if (lifetime > 0f) // 0 — живёт, пока не убьют (Солус)
                {
                    var timer = master.gameObject.AddComponent<MasterSuicideOnTimer>();
                    timer.lifeTimer = lifetime;
                }
            }
            return master;
        }

        /// <summary>Броня от урона: копим полученный урон отдельно на каждый такой перк; набрали порог — броня на 5 с, счётчик в ноль.</summary>
        private static void OnPlayerHurt(CharacterBody body, List<(Perk perk, int level)> perks, float damage)
        {
            var health = body.healthComponent;
            if (!health || !health.alive) return;

            // Рой огоньков: 10% при получении урона, не чаще раза в 2 с на перк. Турелям Инженера — нет (их бьют без конца).
            if (!GearRegistry.InheritsGear(body.master))
            {
                int swarmIndex = 0;
                foreach (var (perk, _) in perks)
                {
                    if (perk.Type != PerkType.HurtWispSwarm) continue;
                    var swarmState = body.GetComponent<PerkBodyState>();
                    if (!swarmState) swarmState = body.gameObject.AddComponent<PerkBodyState>();
                    if (swarmState.SwarmReady(swarmIndex++, Time.time) && Util.CheckRoll(Perks.SwarmChance, body.master))
                    {
                        swarmState.SwarmUsed(swarmIndex - 1, Time.time + Perks.SwarmInternalCooldown);
                        SummonSwarm(body);
                    }
                }
            }

            PerkBodyState state = null;
            int index = 0;
            foreach (var (perk, _) in perks)
            {
                if (perk.Type != PerkType.ArmorOnHurt) continue;
                if (!state)
                {
                    state = body.GetComponent<PerkBodyState>();
                    if (!state) state = body.gameObject.AddComponent<PerkBodyState>();
                }

                float accumulated = state.AddHurt(index, damage);
                float share = perk.ArmorThresholdPercent / 100f;
                if (accumulated >= share * health.fullHealth)
                {
                    state.ResetHurt(index);
                    state.AddArmor(share * health.fullHealth / Perks.ArmorHealthPerArmor, Time.fixedTime + Perks.ArmorDuration);
                    body.AddTimedBuff(ArmorBuff, Perks.ArmorDuration);
                    body.MarkAllStatsDirty();
                }
                index++;
            }
        }

        private static void SummonSwarm(CharacterBody owner)
        {
            for (int i = 0; i < Perks.SwarmCount; i++)
            {
                var position = owner.corePosition + UnityEngine.Random.insideUnitSphere * 4f + Vector3.up * 2f;
                var master = Summon("WispMaster", position, owner, Perks.SwarmLifetime);
                MarkWithBuff(master, SwarmWispBuff);
            }
        }

        /// <summary>Повесить бафф-метку на тело призванного: сразу или когда тело появится.</summary>
        private static void MarkWithBuff(CharacterMaster master, BuffDef buff)
        {
            if (!master || !buff) return;
            var body = master.GetBody();
            if (body) { body.AddBuff(buff); return; }
            Action<CharacterBody> handler = null;
            handler = b =>
            {
                if (b && !b.HasBuff(buff)) b.AddBuff(buff);
                master.onBodyStart -= handler;
            };
            master.onBodyStart += handler;
        }

        /// <summary>Разгон: каждую секунду бега +1 стак (до 5), перестал бежать — стаки сбрасываются.</summary>
        private static void TickMomentum(CharacterBody body, PerkBodyState state, float dt)
        {
            if (body.isSprinting)
            {
                state.MomentumTimer += dt;
                if (state.MomentumTimer >= 1f)
                {
                    state.MomentumTimer = 0f;
                    if (body.GetBuffCount(MomentumBuff) < Perks.MomentumMaxStacks) body.AddBuff(MomentumBuff);
                }
            }
            else
            {
                state.MomentumTimer = 0f;
                if (body.GetBuffCount(MomentumBuff) > 0) body.SetBuffCount(MomentumBuff.buffIndex, 0);
            }
        }

        /// <summary>
        /// Призыв Солуса: стоишь на месте — копится каст (стаки баффа = секунды), сдвинулся — сброс.
        /// 10 с — элитный зонд Солус навсегда + корни всем врагам на карте на 5 с, КД 3 мин на перк.
        /// </summary>
        private void TickSolus(CharacterBody body, PerkBodyState state, int index, bool still, float dt)
        {
            if (!state.SolusReady(index, Time.time) || !still)
            {
                state.SetSolusCast(index, 0f);
            }
            else
            {
                float cast = state.SetSolusCast(index, state.GetSolusCast(index) + dt);
                if (cast >= Perks.SolusCastTime)
                {
                    state.SetSolusCast(index, 0f);
                    state.SolusUsed(index, Time.time + Perks.SolusCooldown);
                    SummonSolus(body);
                }
            }

            // Индикатор: стаки = целые секунды самого продвинутого каста.
            int seconds = Mathf.FloorToInt(state.MaxSolusCast());
            if (body.GetBuffCount(CastingBuff) != seconds) body.SetBuffCount(CastingBuff.buffIndex, seconds);
        }

        private void SummonSolus(CharacterBody owner)
        {
            var master = Summon("RoboBallMiniMaster", owner.corePosition + Vector3.up * 3f, owner, 0f);
            if (master && master.inventory)
            {
                var elite = RandomTier1Elite();
                if (elite && elite.eliteEquipmentDef) master.inventory.SetEquipmentIndex(elite.eliteEquipmentDef.equipmentIndex);
            }
            MarkWithBuff(master, SolusBuff);

            // Обездвиживающее поле на всю карту: все враги — корни на 5 с.
            var team = owner.teamComponent ? owner.teamComponent.teamIndex : TeamIndex.Player;
            foreach (var victim in CharacterBody.readOnlyInstancesList)
                if (IsEnemy(team, victim)) victim.AddTimedBuff(RoR2Content.Buffs.Entangle, Perks.SolusRootDuration);
        }

        private static EliteDef RandomTier1Elite()
        {
            var tier = EliteAPI.GetVanillaEliteTierDef(VanillaEliteTier.BaseTier1);
            if (tier == null || tier.eliteTypes == null) return null;
            var list = new List<EliteDef>();
            foreach (var def in tier.eliteTypes)
                if (def && def.eliteEquipmentDef) list.Add(def);
            return list.Count > 0 ? list[LootRoller.Rng.Next(list.Count)] : null;
        }

        /// <summary>
        /// Перк книги: книга в активном слоте снаряжения → случайный перк (уровень = уровень монстров сейчас).
        /// Новая книга (другой тип) — новый перк; книги нет — перка нет. Рассылается клиентам вместе с экипировкой.
        /// </summary>
        private void TickBookPerk(CharacterMaster master)
        {
            var inventory = master.inventory;
            var index = inventory ? inventory.currentEquipmentIndex : EquipmentIndex.None;
            bool hasBook = Diabloid.Books.GearBooks.IsBook(index);
            var gear = GearRegistry.Find(master);

            if (hasBook)
            {
                if (gear != null && gear.BookPerk.HasValue && bookOf.TryGetValue(master.netId, out var last) && last == index) return;
                gear = GearRegistry.GetOrCreate(master);
                var types = (PerkType[])Enum.GetValues(typeof(PerkType));
                gear.BookPerk = new Perk(types[LootRoller.Rng.Next(types.Length)], (float)LootRoller.Rng.NextDouble());
                gear.BookPerkLevel = LootRoller.CurrentItemLevel();
                bookOf[master.netId] = index;
            }
            else
            {
                bookOf.Remove(master.netId);
                if (gear == null || !gear.BookPerk.HasValue) return;
                gear.BookPerk = null;
            }

            GearRegistry.MarkStatsDirty(master);
            Diabloid.Network.GearNetwork.Broadcast(master, gear);
        }

        private readonly Dictionary<NetworkInstanceId, EquipmentIndex> bookOf = new Dictionary<NetworkInstanceId, EquipmentIndex>();

        // ---------------------------------------------------------------- тики

        private void FixedUpdate()
        {
            if (!NetworkServer.active || !Run.instance) return;
            float dt = Time.fixedDeltaTime;

            auraTimer -= dt;
            bool auraTick = auraTimer <= 0f;
            if (auraTick)
            {
                auraTimer = AuraTickInterval;
                bodies.Clear();
                bodies.AddRange(CharacterBody.readOnlyInstancesList);
            }

            // Игроки и их турели Инженера. Турелям — всё, кроме големов (иначе по голему на каждую турель)
            // и клича (турели не бегают).
            foreach (var (master, body, isTurret) in GearRegistry.Wearers())
            {
                if (!body.healthComponent || !body.healthComponent.alive) continue;
                if (!isTurret) TickBookPerk(master);

                var state = body.GetComponent<PerkBodyState>();
                if (state && state.ExpireArmor(Time.fixedTime)) body.MarkAllStatsDirty();

                var perks = PerksOf(master);
                if (perks.Count == 0)
                {
                    // Перки сняли — убрать их следы (стаки разгона, индикатор каста).
                    if (MomentumBuff && body.GetBuffCount(MomentumBuff) > 0) body.SetBuffCount(MomentumBuff.buffIndex, 0);
                    if (CastingBuff && body.GetBuffCount(CastingBuff) > 0) body.SetBuffCount(CastingBuff.buffIndex, 0);
                    continue;
                }

                int regenPerks = 0, golemIndex = 0, solusIndex = 0;
                bool momentum = false;
                bool warcry = false;
                foreach (var (perk, _) in perks)
                {
                    switch (perk.Type)
                    {
                        case PerkType.MissingHpRegen: regenPerks++; break;
                        case PerkType.DamageAura: if (auraTick) AuraTick(body, perk); break;
                        case PerkType.SprintWarcry: warcry = !isTurret; break;
                        case PerkType.SprintMomentum: momentum = !isTurret; break;
                        case PerkType.StandStillSolus:
                            if (isTurret) break; // турели стоят всегда — призывали бы по Солусу каждые 3 мин
                            if (!state) state = body.gameObject.AddComponent<PerkBodyState>();
                            // "Стоит ли" считаем один раз на тело, до первого перка, — у всех перков одно и то же.
                            if (solusIndex == 0) state.UpdateStill(body.footPosition, dt);
                            TickSolus(body, state, solusIndex++, state.IsStill, dt);
                            break;
                        case PerkType.GolemSummon:
                            if (isTurret) break;
                            if (!state) state = body.gameObject.AddComponent<PerkBodyState>();
                            if (state.TickGolem(golemIndex++, dt)) SummonGolem(body);
                            break;
                    }
                }

                // Боевой клич бега: раз в 0.5 с бафф на 1 с себе и союзникам в радиусе. Бег сервер знает — его шлёт клиент.
                if (warcry && auraTick && body.isSprinting) WarcryTick(body);

                if (momentum)
                {
                    if (!state) state = body.gameObject.AddComponent<PerkBodyState>();
                    TickMomentum(body, state, dt);
                }
                else if (MomentumBuff && body.GetBuffCount(MomentumBuff) > 0) body.SetBuffCount(MomentumBuff.buffIndex, 0);
                if (solusIndex == 0 && CastingBuff && body.GetBuffCount(CastingBuff) > 0) body.SetBuffCount(CastingBuff.buffIndex, 0);

                // Реген от недостающего здоровья: лечение как регенерация (nonRegen = false), каждый физический кадр.
                if (regenPerks > 0)
                {
                    var health = body.healthComponent;
                    float missing = health.fullHealth - health.health;
                    float amount = missing * Perks.MissingHpRegenPercent / 100f * regenPerks * dt;
                    if (amount > 0.001f) health.Heal(amount, default(ProcChainMask), false);
                }
            }
        }

        private void WarcryTick(CharacterBody source)
        {
            var team = source.teamComponent ? source.teamComponent.teamIndex : TeamIndex.Player;
            float radiusSqr = Perks.WarcryRadius * Perks.WarcryRadius;
            foreach (var ally in bodies)
            {
                if (!ally || !ally.healthComponent || !ally.healthComponent.alive) continue;
                if (!ally.teamComponent || ally.teamComponent.teamIndex != team) continue;
                if ((ally.corePosition - source.corePosition).sqrMagnitude > radiusSqr) continue;
                ally.AddTimedBuff(WarcryBuff, AuraTickInterval * 2f);
            }
        }

        /// <summary>Мини-голем рядом с хозяином: обычный союзный голем + бафф-метка (скорость, урон, размер).</summary>
        private static void SummonGolem(CharacterBody owner)
        {
            var offset = UnityEngine.Random.insideUnitCircle.normalized * 4f;
            var position = GroundNear(owner.footPosition + new Vector3(offset.x, 0f, offset.y));
            var master = Summon("GolemMaster", position, owner, Perks.GolemLifetime);
            if (!master) return;

            // Тело может появиться не сразу после Perform — тогда метку вешаем, когда оно стартует.
            // (В 0.8.x метка не успевала: голем был обычного размера и с полным уроном.)
            var golem = master.GetBody();
            if (golem) golem.AddBuff(MiniGolemBuff);
            else master.onBodyStart += MarkMiniGolem;
        }

        private static void MarkMiniGolem(CharacterBody body)
        {
            if (body && !body.HasBuff(MiniGolemBuff)) body.AddBuff(MiniGolemBuff);
            if (body && body.master) body.master.onBodyStart -= MarkMiniGolem;
        }

        /// <summary>Ближайшая к точке нода наземной навигации (под размер голема) — чтобы не появиться в стене или под картой.</summary>
        private static Vector3 GroundNear(Vector3 position)
        {
            var nodes = SceneInfo.instance ? SceneInfo.instance.groundNodes : null;
            if (!nodes) return position + Vector3.up * 0.5f;
            var index = nodes.FindClosestNode(position, HullClassification.Golem);
            return nodes.GetNodePosition(index, out var nodePosition) ? nodePosition : position + Vector3.up * 0.5f;
        }

        /// <summary>Аура: урон всем врагам в радиусе раз в 0.5 с, proc 0.1 — предметы "при попадании" срабатывают.</summary>
        private void AuraTick(CharacterBody source, Perk perk)
        {
            float damage = source.damage * perk.AuraDamagePercent / 100f * AuraTickInterval;
            if (damage <= 0f) return;
            float radiusSqr = perk.AuraRadius * perk.AuraRadius;
            var team = source.teamComponent ? source.teamComponent.teamIndex : TeamIndex.Player;

            foreach (var victim in bodies)
            {
                if (!IsEnemy(team, victim)) continue;
                if ((victim.corePosition - source.corePosition).sqrMagnitude > radiusSqr) continue;

                var info = new DamageInfo
                {
                    attacker = source.gameObject,
                    inflictor = source.gameObject,
                    damage = damage,
                    position = victim.corePosition,
                    crit = source.RollCrit(),
                    procCoefficient = Perks.AuraProcCoefficient,
                    procChainMask = default(ProcChainMask),
                    damageType = DamageType.Generic,
                    damageColorIndex = DamageColorIndex.Item,
                    force = Vector3.zero,
                };
                victim.healthComponent.TakeDamage(info);
                // Как BlastAttack у игры: после урона — "попадание", чтобы сработали предметы.
                GlobalEventManager.instance.OnHitEnemy(info, victim.gameObject);
                GlobalEventManager.instance.OnHitAll(info, victim.gameObject);
            }
        }

        private float scalerCheckTimer;
        private float bookTickTimer;

        /// <summary>Все машины: берсерку пересчитать статы, когда сменилось число "недостающих 70", и сферы ауры.</summary>
        private void Update()
        {
            if (!Run.instance) return;

            // Подстраховка размера мини-голема: если событие баффа пропустили (у клиента, или бафф пришёл раньше тела) —
            // раз в 0.25 с ищем големов с меткой без уменьшалки.
            // Боссы из книги призыва: размер и статы от героя (раз в секунду).
            bookTickTimer -= Time.deltaTime;
            if (bookTickTimer <= 0f)
            {
                bookTickTimer = 1f;
                Diabloid.Books.GearBooks.Tick();
            }

            scalerCheckTimer -= Time.deltaTime;
            if (scalerCheckTimer <= 0f && MiniGolemBuff)
            {
                scalerCheckTimer = 0.25f;
                foreach (var other in CharacterBody.readOnlyInstancesList)
                    if (other && other.HasBuff(MiniGolemBuff) && !other.GetComponent<ModelScaler>())
                        other.gameObject.AddComponent<ModelScaler>().Multiplier = Perks.GolemScale;
            }

            foreach (var (master, body, _) in GearRegistry.Wearers())
            {
                var gear = GearRegistry.FindWorn(master);
                if (gear == null) continue;

                if (gear.HasPerk(PerkType.Berserk))
                {
                    var state = body.GetComponent<PerkBodyState>();
                    if (!state) state = body.gameObject.AddComponent<PerkBodyState>();
                    int steps = BerserkSteps(body);
                    if (steps != state.BerserkSteps)
                    {
                        state.BerserkSteps = steps;
                        body.MarkAllStatsDirty();
                    }
                }

                // Броня от здоровья считается от maxHealth прошлого пересчёта — сменился он, пересчитать ещё раз.
                if (gear.HasPerk(PerkType.HealthArmor))
                {
                    var state = body.GetComponent<PerkBodyState>();
                    if (!state) state = body.gameObject.AddComponent<PerkBodyState>();
                    if (Mathf.Abs(body.maxHealth - state.HealthArmorBase) > 0.5f)
                    {
                        state.HealthArmorBase = body.maxHealth;
                        body.MarkAllStatsDirty();
                    }
                }

                if (gear.HasPerk(PerkType.DamageAura) && !body.GetComponent<PerkAuraVisual>())
                    body.gameObject.AddComponent<PerkAuraVisual>();
            }
        }
    }

    /// <summary>Когда призван союзник перка (для диагностики времени жизни). Висит на мастере, только сервер.</summary>
    internal sealed class PerkSummonInfo : MonoBehaviour
    {
        public float SpawnTime;
    }

    /// <summary>Состояние перков на теле игрока. Живёт вместе с телом: новый этап — всё с нуля.</summary>
    internal sealed class PerkBodyState : MonoBehaviour
    {
        public int BerserkSteps = -1;
        public float HealthArmorBase = -1f;

        // 0.10.0
        public float MomentumTimer;

        // Стоит ли персонаж: позицию игрока-клиента сервер получает рывками (не каждый физический кадр),
        // поэтому сравниваем не соседние кадры, а точки раз в 0.25 с: сдвинулся меньше чем на 20 см — стоит.
        public bool IsStill;
        private Vector3 samplePosition;
        private float sampleTimer;
        private bool hasSample;

        public void UpdateStill(Vector3 position, float dt)
        {
            if (!hasSample) { samplePosition = position; hasSample = true; IsStill = false; return; }
            sampleTimer += dt;
            if (sampleTimer < 0.25f) return;
            sampleTimer = 0f;
            IsStill = (position - samplePosition).sqrMagnitude < 0.04f;
            samplePosition = position;
        }
        private readonly List<float> swarmReadyAt = new List<float>();
        private readonly List<float> solusReadyAt = new List<float>();
        private readonly List<float> solusCast = new List<float>();

        private static void Grow(List<float> list, int index) { while (list.Count <= index) list.Add(0f); }

        public bool SwarmReady(int index, float now) { Grow(swarmReadyAt, index); return now >= swarmReadyAt[index]; }
        public void SwarmUsed(int index, float until) { Grow(swarmReadyAt, index); swarmReadyAt[index] = until; }

        public bool SolusReady(int index, float now) { Grow(solusReadyAt, index); return now >= solusReadyAt[index]; }
        public void SolusUsed(int index, float until) { Grow(solusReadyAt, index); solusReadyAt[index] = until; }
        public float GetSolusCast(int index) { Grow(solusCast, index); return solusCast[index]; }
        public float SetSolusCast(int index, float value) { Grow(solusCast, index); solusCast[index] = value; return value; }
        public float MaxSolusCast() { float max = 0f; foreach (var c in solusCast) max = Math.Max(max, c); return max; }

        private readonly List<float> golemTimers = new List<float>();

        /// <summary>Таймер голема для перка №index. true — пора призывать (первый — через 3 с после появления тела).</summary>
        public bool TickGolem(int index, float dt)
        {
            while (golemTimers.Count <= index) golemTimers.Add(3f);
            golemTimers[index] -= dt;
            if (golemTimers[index] > 0f) return false;
            golemTimers[index] = Perks.GolemInterval;
            return true;
        }

        private readonly List<float> hurt = new List<float>();
        private readonly List<(float armor, float until)> armor = new List<(float, float)>();

        public float AddHurt(int index, float damage)
        {
            while (hurt.Count <= index) hurt.Add(0f);
            hurt[index] += damage;
            return hurt[index];
        }

        public void ResetHurt(int index)
        {
            if (index < hurt.Count) hurt[index] = 0f;
        }

        public void AddArmor(float amount, float until) => armor.Add((amount, until));

        public float ActiveArmor()
        {
            float sum = 0f;
            foreach (var (amount, _) in armor) sum += amount;
            return sum;
        }

        /// <summary>Убрать истёкшую броню. true — что-то убрали, статы надо пересчитать.</summary>
        public bool ExpireArmor(float now)
        {
            int removed = armor.RemoveAll(a => a.until <= now);
            return removed > 0;
        }
    }

    /// <summary>Сферы ауры урона вокруг игрока — по одной на перк. Косметика, у каждого своя.</summary>
    internal sealed class PerkAuraVisual : MonoBehaviour
    {
        private CharacterBody body;
        private readonly List<GameObject> spheres = new List<GameObject>();
        private readonly List<(Perk perk, int level)> perks = new List<(Perk, int)>();

        private void Awake() => body = GetComponent<CharacterBody>();

        private void Update()
        {
            perks.Clear();
            var gear = body && body.master ? GearRegistry.FindWorn(body.master) : null;
            gear?.CollectPerks(perks);

            int used = 0;
            bool show = DiabloidConfig.ShowPerkAura.Value && PerkManager.AuraMaterial &&
                        body && body.healthComponent && body.healthComponent.alive;
            if (show)
            {
                foreach (var (perk, _) in perks)
                {
                    if (perk.Type != PerkType.DamageAura) continue;
                    if (used >= spheres.Count) spheres.Add(CreateSphere());
                    var sphere = spheres[used++];
                    sphere.SetActive(true);
                    sphere.transform.position = body.corePosition;
                    float d = perk.AuraRadius * 2f;
                    sphere.transform.localScale = new Vector3(d, d, d);
                }
            }

            for (int i = used; i < spheres.Count; i++)
                if (spheres[i]) spheres[i].SetActive(false);
        }

        private static GameObject CreateSphere()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "DiabloidPerkAura";
            Destroy(go.GetComponent<Collider>()); // иначе сфера толкала бы всех и ловила пули
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = PerkManager.AuraMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        private void OnDestroy()
        {
            foreach (var sphere in spheres)
                if (sphere) Destroy(sphere);
        }
    }

    /// <summary>Уменьшает (или увеличивает) модель тела. Каждая машина сама; хитбоксы внутри модели масштабируются с ней.</summary>
    internal sealed class ModelScaler : MonoBehaviour
    {
        public float Multiplier = 1f;
        private CharacterBody body;
        private Transform model;
        private Vector3 originalScale;

        private void Awake() => body = GetComponent<CharacterBody>();

        // Каждый кадр, а не один раз: анимация появления или другие моды могут вернуть масштаб модели обратно.
        private void LateUpdate()
        {
            var current = body && body.modelLocator ? body.modelLocator.modelTransform : null;
            if (!current) return;
            if (current != model)
            {
                model = current;
                originalScale = model.localScale;
            }
            var wanted = originalScale * Multiplier;
            if (model.localScale != wanted) model.localScale = wanted;
        }
    }
}
