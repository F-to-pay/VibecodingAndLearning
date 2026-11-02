using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using Destroyer.Content;
using Destroyer.Core;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace Destroyer.Items
{
    /// <summary>
    /// "Я не прощу тебя" (кукла, лунный). Убил врага — теряешь 1% (+1%) полного запаса (здоровье + щит) сквозь всё:
    /// броню, блок и защиту от ваншота; может убить (0.6.1, было 20% базового урона) и твои on-kill эффекты срабатывают ещё 1 (+1) раз, как будто убили ТЕБЯ.
    ///
    /// Это вывернутая наизнанку Forgive Me Please. Ванильная кукла (Projectile.DeathProjectile.FixedUpdate, IL 1.5)
    /// собирает DamageReport (атакующий — хозяин, жертва — HealthComponent куклы) и зовёт публичный
    /// GlobalEventManager.TriggerCharacterDeathProc → приватный OnCharacterDeath. Мы делаем то же, но жертва — ты сам.
    ///
    /// Почему это безопасно (IL OnCharacterDeath):
    ///  * весь блок "умерла жертва" (OnPlayerCharacterDeath, таймер game over, смертельные эффекты элиток жертвы,
    ///    душа артефакта Soul и т.п.) стоит за проверкой victimMaster != null — мы обнуляем victimMaster в отчёте
    ///    (readonly-поле, рефлексией), ровно как у ванильной куклы, у которой мастера нет;
    ///  * дальше — только эффекты атакующего: CharacterBody.HandleOnKillEffectsServer и предметы по инвентарю
    ///    атакующего; позиции и радиусы берутся с жертвы, то есть с тебя;
    ///  * в конце — событие onDeathProcServer (= onCharacterDeathGlobal). Его слушатели видят такую же "фальшивую смерть
    ///    без мастера", как от ванильной куклы. Свои фальшивки мы отличаем так же: victimMaster == null.
    ///
    /// Срабатывание вешает ванильный дебафф HealingDisabled на 0.3 с (HealthComponent.Heal при нём возвращает 0, IL 1.5):
    /// иначе Monster Tooth роняет сферу лечения прямо в тебя и она сразу отменяет самоурон.
    ///
    /// Убийства копятся в очередь и обрабатываются в следующем физическом кадре: так нет рекурсии
    /// (взрыв Виспа из куклы убивает врага → снова кукла → ...), цепочка идёт волнами по кадрам.
    /// </summary>
    internal sealed class Doll : ItemBase
    {
        public override string Id => "DOLL";
        public override ItemTier Tier => ItemTier.Lunar;
        public override string ModelFrom => "DeathProjectile"; // кукла Forgive Me Please (снаряжение)
        public override ItemTag[] Tags => new[] { ItemTag.Damage, ItemTag.OnKillEffect, ItemTag.AIBlacklist };

        public override Words Name => new Words("I Won't Forgive You", "Я не прощу тебя");

        public override Words Pickup => new Words(
            "Your on-kill effects also trigger on you... <style=cDeath>but every kill hurts you.</style>",
            "Эффекты убийства срабатывают и на тебе... <style=cDeath>но каждое убийство ранит тебя.</style>");

        public override Words Description => new Words(
            $"Killing an enemy triggers your {Tx.Damage("on-kill effects")} {Tx.Damage(Times(procsPerStack.Value).En)} {Tx.Stack("+" + procsPerStack.Value + " per stack")} " +
            $"{Tx.Utility("as if you were the one killed")}. " +
            $"{Tx.Health("Every kill deals " + Tx.Pct(selfDamage.Value) + " of your maximum health and shield")} {Tx.Stack("+" + Tx.Pct(selfDamagePerStack.Value) + " per stack")} " +
            $"{Tx.Health("to you, ignoring armor, blocks and one-shot protection. It can kill you.")} " +
            $"{Tx.Health("While it triggers, your healing is disabled for " + Tx.Seconds(healBlock.Value).En + ".")}",
            $"Убийство врага запускает твои {Tx.Damage("эффекты убийства")} ещё {Tx.Damage(Times(procsPerStack.Value).Ru)} {Tx.Stack("+" + procsPerStack.Value + " за копию")} " +
            $"{Tx.Utility("так, будто убили тебя")}. " +
            $"{Tx.Health("Каждое убийство наносит тебе " + Tx.Pct(selfDamage.Value) + " от макс. здоровья и щита")} {Tx.Stack("+" + Tx.Pct(selfDamagePerStack.Value) + " за копию")} " +
            $"{Tx.Health("сквозь броню, блок и защиту от ваншота. Может убить.")} " +
            $"{Tx.Health("При срабатывании лечение отключается на " + Tx.Seconds(healBlock.Value).Ru + ".")}");

        public override Words Lore => new Words(
            "You asked for forgiveness. I kept the pins.",
            "Ты просил прощения. Я оставила себе иголки.");

        private static Words Times(int n) => new Words(n == 1 ? "once" : n + " times", n + (n % 10 >= 2 && n % 10 <= 4 && (n % 100 < 12 || n % 100 > 14) ? " раза" : " раз"));

        private ConfigEntry<float> selfDamage, selfDamagePerStack, healBlock;
        private ConfigEntry<int> procsPerStack;

        protected override void BindConfig(ItemSettings s)
        {
            selfDamage = s.Float("SelfDamage", 0.01f, 0f, 1f, new Words("Self-damage", "Урон себе"),
                new Words("Damage to yourself per kill with the first copy, as a fraction of maximum health + shield (0.01 = 1%).",
                          "Урон себе за убийство с первой копией, доля макс. здоровья + щита (0.01 = 1%)."), "{0:0.#%}");
            selfDamagePerStack = s.Float("SelfDamagePerStack", 0.01f, 0f, 1f, new Words("Self-damage per stack", "Урон себе за копию"),
                new Words("Extra self-damage per additional copy (0.01 = +1%).", "Прибавка урона себе за каждую копию сверх первой (0.01 = +1%)."), "{0:0.#%}");
            healBlock = s.Float("HealBlockDuration", 0.3f, 0f, 5f, new Words("Healing block", "Блок лечения"),
                new Words("Healing is disabled for this long when the doll triggers, seconds (Monster Tooth orbs land right on you).",
                          "На сколько секунд отключается лечение при срабатывании куклы (сферы Monster Tooth падают прямо в тебя)."), "{0:0.##} s");
            procsPerStack = s.Int("ProcsPerStack", 1, 0, 10, new Words("On-kill triggers per copy", "Срабатываний за копию"),
                new Words("How many times your on-kill effects trigger on you per copy.", "Сколько раз эффекты убийства срабатывают на тебе за каждую копию."));
        }

        /// <summary>DamageReport.victimMaster — readonly; Mono позволяет писать в него рефлексией.</summary>
        private static readonly FieldInfo victimMasterField =
            typeof(DamageReport).GetField("victimMaster", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private readonly List<CharacterBody> pending = new List<CharacterBody>();
        private readonly List<CharacterBody> processing = new List<CharacterBody>();

        protected override void Hook()
        {
            if (victimMasterField == null)
                DestroyerPlugin.Log.LogError("Doll: DamageReport.victimMaster not found (game update?), the doll will only hurt.");

            GlobalEventManager.onCharacterDeathGlobal += OnDeath;
            RoR2Application.onFixedUpdate += Process;
            Run.onRunStartGlobal += run => pending.Clear();
        }

        private void OnDeath(DamageReport report)
        {
            if (!NetworkServer.active || report == null) return;
            // Настоящая смерть врага. Фальшивые (наша и ванильной куклы) — без мастера жертвы.
            if (!report.victimMaster || !report.victimBody) return;
            var killer = report.attackerBody;
            if (!killer || report.victimTeamIndex == report.attackerTeamIndex) return;
            if (Count(killer) <= 0) return;
            pending.Add(killer);
        }

        private void Process()
        {
            if (!NetworkServer.active || pending.Count == 0) return;
            // Пока обрабатываем, кукла может породить новые убийства — они встанут в pending на следующий кадр.
            processing.Clear();
            processing.AddRange(pending);
            pending.Clear();

            foreach (var body in processing)
            {
                if (!body || !body.healthComponent || !body.healthComponent.alive) continue;
                int n = Count(body);
                if (n <= 0) continue;

                HurtSelf(body, n);
                if (!body.healthComponent.alive) continue; // кукла добила — on-kill уже не для кого

                int procs = procsPerStack.Value * n;
                // До проков: лечение от них (сфера Monster Tooth и т.п.) должно упереться в дебафф.
                if (procs > 0 && healBlock.Value > 0f && RoR2Content.Buffs.HealingDisabled)
                    body.AddTimedBuff(RoR2Content.Buffs.HealingDisabled, healBlock.Value);
                for (int i = 0; i < procs && body && body.healthComponent.alive; i++)
                    ProcOnKillOnSelf(body);
            }
            processing.Clear();
        }

        private void HurtSelf(CharacterBody body, int n)
        {
            // От полного запаса (здоровье + щит): с Transcendence здоровье = 1, иначе кукла бы почти не кусала.
            float amount = body.healthComponent.fullCombinedHealth * Stacking.Linear(n, selfDamage.Value, selfDamagePerStack.Value);
            if (amount <= 0f) return;
            body.healthComponent.TakeDamage(new DamageInfo
            {
                attacker = null,           // "окружение": не прокает шипы и прочее "в ответ атакующему"
                inflictor = null,
                damage = amount,
                position = body.corePosition,
                procCoefficient = 0f,
                crit = false,
                damageColorIndex = DamageColorIndex.Void,
                damageType = DamageType.BypassArmor | DamageType.BypassBlock | DamageType.BypassOneShotProtection,
            });
        }

        private static void ProcOnKillOnSelf(CharacterBody body)
        {
            var manager = GlobalEventManager.instance;
            if (!manager || victimMasterField == null) return;

            var info = new DamageInfo
            {
                attacker = body.gameObject,
                inflictor = body.gameObject,
                damage = body.damage,
                position = body.corePosition,
                procCoefficient = 1f,
                crit = false,
                damageColorIndex = DamageColorIndex.Item,
                damageType = DamageType.Generic,
            };
            var health = body.healthComponent;
            var report = new DamageReport(info, health, info.damage, health.combinedHealth);
            try
            {
                victimMasterField.SetValue(report, null); // без мастера жертвы — как у ванильной куклы
            }
            catch (System.Exception e)
            {
                if (!warnedReadonly) DestroyerPlugin.Log.LogError($"Doll: cannot clear DamageReport.victimMaster: {e.Message}");
                warnedReadonly = true;
            }
            // С мастером игра решила бы, что ТЫ умер (OnPlayerCharacterDeath, таймер game over). Не вышло обнулить — не зовём.
            if (report.victimMaster) return;
            manager.TriggerCharacterDeathProc(report);
        }

        private static bool warnedReadonly;
    }
}
