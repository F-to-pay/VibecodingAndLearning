using System.Collections.Generic;
using BepInEx.Configuration;
using CharacterTest.Config;
using EntityStates;
using C = CharacterTest.Survivors.TestSubject.TestSubjectConfig;
using CharacterTest.Content;
using CharacterTest.Core;
using CharacterTest.Survivors.TestSubject.States;
using RoR2;
using UnityEngine;

namespace CharacterTest.Survivors.TestSubject
{
    /// <summary>
    /// Подопытный: проверка каркаса. Тело, витрина и ИИ — Коммандо, все четыре навыка свои
    /// (простейшие, чтобы проверить цепочку SkillDef → SkillFamily → EntityState → сеть).
    /// Когда появится настоящий персонаж — этот можно выключить в настройках или удалить из SurvivorList.
    /// </summary>
    public sealed class TestSubjectSurvivor : SurvivorBase
    {
        public override string Id => "TEST_SUBJECT";
        public override string BodyName => "TestSubjectBody";

        public override AssetRef DonorBody => GameAssets.CommandoBody;
        public override AssetRef DonorDisplay => GameAssets.CommandoDisplay;
        public override AssetRef? DonorMaster => GameAssets.CommandoMonsterMaster;

        public override Words Name => new Words("Test Subject", "Подопытный");
        public override Words Subtitle => new Words("Framework Check", "Проверка каркаса");
        public override Words Description => new Words(
            "The Test Subject exists to prove that the mod skeleton works. Starts with copies of Commando's skills; custom ones are alternatives in the loadout.<color=#CCD3E0>\n\n" +
            "< ! > Twin SMGs: 40-round magazine, 16 shots per second; attack speed boosts damage.\n\n" +
            "< ! > Underbarrel launchers lob two grenades in an arc.\n\n" +
            "< ! > Crossfire roots you in place, invulnerable, and spreads 12 shots between all visible enemies nearby.</color>\n",
            "Подопытный нужен, чтобы доказать: скелет мода работает. По умолчанию у него копии навыков Коммандо, свои — альтернативы в снаряжении.<color=#CCD3E0>\n\n" +
            "< ! > Парные ПП: обойма на 40, 16 выстрелов в секунду, скорость атаки идёт в урон.\n\n" +
            "< ! > Подствольники кидают две гранаты по дуге.\n\n" +
            "< ! > Перекрёстный огонь: стоишь неуязвимый и делишь 12 выстрелов между всеми видимыми врагами рядом.</color>\n");
        public override Words Lore => new Words(
            "Subject #0. No name, no memories, no complaints. Fires when told to fire.",
            "Образец №0. Ни имени, ни воспоминаний, ни жалоб. Стреляет, когда велят.");

        public override Color Color => new Color(0.55f, 0.85f, 0.45f);
        public override float SortPosition => 100f;

        public override void BindConfig(ConfigFile config, string section) => TestSubjectConfig.Bind(config, section);

        protected internal override void CreateAssets()
        {
            UnderbarrelGrenades.CreateProjectile();
        }

        protected internal override void SetupBody(CharacterBody body)
        {
            // Чуть живучее Коммандо — видно, что статы наши (в игре: 130 ОЗ на 1 уровне).
            body.baseMaxHealth = 130f;
            body.levelMaxHealth = 39f;
            body.baseRegen = 1f;
            body.levelRegen = 0.2f;
            body.baseDamage = 12f;
            body.levelDamage = 2.4f;
            body.baseArmor = 0f;
            body.baseMoveSpeed = 7f;
            body.baseJumpCount = 1;
        }

        public override IEnumerable<SkillSpec> Skills()
        {
            // Сначала — копии стандартных кнопок Коммандо: они идут первыми, значит, они по умолчанию.
            // Свои навыки ниже — альтернативы в лоадауте.
            yield return SkillSpec.FromDonor(SkillSlot.Primary, "COMMANDO_PRIMARY");     // Double Tap
            yield return SkillSpec.FromDonor(SkillSlot.Secondary, "COMMANDO_SECONDARY"); // Phase Round
            yield return SkillSpec.FromDonor(SkillSlot.Utility, "COMMANDO_UTILITY");     // Tactical Dive
            yield return SkillSpec.FromDonor(SkillSlot.Special, "COMMANDO_SPECIAL");     // Suppressive Fire

            // Обойма = заряды навыка; перезарядка = кулдаун, который TwinSmg обнуляет каждым выстрелом.
            var smg = SkillSpec.Primary("PRIMARY_TWIN_SMG", typeof(TwinSmg),
                new Words("Twin SMGs", "Парные пистолеты-пулемёты"), default);
            smg.DynamicDescription = () => new Words(
                $"Fire twin SMGs for <style=cIsDamage>{Pct(C.SmgDamage.Value)} damage</style>, " +
                $"<style=cIsDamage>{C.SmgFireRate.Value:0.#}</style> shots per second. " +
                $"<style=cIsUtility>{C.SmgMagazine.Value} rounds</style>, full reload after " +
                $"<style=cIsUtility>{C.SmgReloadDelay.Value:0.#} s</style> without firing. " +
                "<style=cIsDamage>Attack speed</style> increases <style=cIsDamage>damage</style> instead of fire rate.",
                $"Очередь из двух пистолетов-пулемётов: <style=cIsDamage>{Pct(C.SmgDamage.Value)} урона</style>, " +
                $"<style=cIsDamage>{C.SmgFireRate.Value:0.#}</style> выстрелов в секунду. " +
                $"Обойма <style=cIsUtility>{C.SmgMagazine.Value} патронов</style>, полная перезарядка через " +
                $"<style=cIsUtility>{C.SmgReloadDelay.Value:0.#} с</style> без стрельбы. " +
                "<style=cIsDamage>Скорость атаки</style> повышает <style=cIsDamage>урон</style>, а не темп.");
            smg.Cooldown = C.SmgReloadDelay.Value;
            smg.MaxStock = C.SmgMagazine.Value;
            smg.RechargeStock = C.SmgMagazine.Value;
            smg.RequiredStock = 1;
            smg.StockToConsume = 1;
            smg.ResetCooldownOnUse = true;
            smg.Tweak = def => def.attackSpeedBuffsRestockSpeed = false;
            yield return smg;

            var grenades = SkillSpec.Secondary("SECONDARY_UNDERBARREL", typeof(UnderbarrelGrenades),
                new Words("Underbarrel Grenade Launchers", "Подствольные гранатомёты"), default, cooldown: C.GrenadeCooldown.Value);
            grenades.DynamicDescription = () => new Words(
                $"Lob <style=cIsDamage>{C.GrenadeShots.Value}</style> grenade(s), each exploding for " +
                $"<style=cIsDamage>{Pct(C.GrenadeDamage.Value)} damage</style> in a <style=cIsDamage>{C.GrenadeRadius.Value:0.#} m</style> radius.",
                $"<style=cIsDamage>{C.GrenadeShots.Value}</style> гранат(ы) по дуге, каждая взрывается на " +
                $"<style=cIsDamage>{Pct(C.GrenadeDamage.Value)} урона</style> в радиусе <style=cIsDamage>{C.GrenadeRadius.Value:0.#} м</style>.");
            yield return grenades;

            // Машина Body: пока идёт навык, GenericCharacterMain не работает — ни ходьбы, ни прыжков, ни других навыков.
            // Без видимых целей навык не нажимается (CrossfireSkillDef) — перезарядка не тратится.
            var crossfire = SkillSpec.Special("SPECIAL_CROSSFIRE", typeof(Crossfire),
                new Words("Crossfire", "Перекрёстный огонь"), default, cooldown: C.CrossCooldown.Value);
            crossfire.Machine = "Body";
            crossfire.Priority = InterruptPriority.PrioritySkill;
            crossfire.CancelSprinting = true;
            crossfire.Factory = () => ScriptableObject.CreateInstance<CrossfireSkillDef>();
            crossfire.DynamicDescription = () => new Words(
                $"Stand still and become <style=cIsUtility>invulnerable</style>. Over <style=cIsDamage>{C.CrossDuration.Value:0.##} s</style>, " +
                $"fire <style=cIsDamage>{C.CrossShots.Value}</style> shots for <style=cIsDamage>{Pct(C.CrossDamage.Value)} damage</style> " +
                $"at visible enemies within <style=cIsDamage>{C.CrossRange.Value:0} m</style>, spread evenly between them. " +
                "<style=cIsDamage>Attack speed</style> adds <style=cIsDamage>shots</style>: both fire rate and duration grow.",
                $"Замираешь на месте и становишься <style=cIsUtility>неуязвим</style>. За <style=cIsDamage>{C.CrossDuration.Value:0.##} с</style> — " +
                $"<style=cIsDamage>{C.CrossShots.Value}</style> выстрелов по <style=cIsDamage>{Pct(C.CrossDamage.Value)} урона</style> " +
                $"по видимым врагам в <style=cIsDamage>{C.CrossRange.Value:0} м</style>, поровну между ними. " +
                "<style=cIsDamage>Скорость атаки</style> добавляет <style=cIsDamage>выстрелы</style>: растут и темп, и длительность.");
            yield return crossfire;
        }

        private static string Pct(float coefficient) => (coefficient * 100f).ToString("0") + "%";
    }
}
