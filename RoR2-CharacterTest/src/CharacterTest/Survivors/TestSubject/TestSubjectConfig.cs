using BepInEx.Configuration;
using CharacterTest.Config;
using CharacterTest.Core;

namespace CharacterTest.Survivors.TestSubject
{
    /// <summary>
    /// Все цифры навыков Подопытного — секция "Survivor: TEST_SUBJECT" (и меню Risk of Options).
    /// Состояния читают .Value в момент выстрела, поэтому почти всё действует сразу.
    /// Помечены "restart" только то, что зашито в SkillDef при запуске: перезарядки и размер обоймы.
    /// Ключи после выпуска не переименовываем (конфиг пользователь чистит сам).
    /// </summary>
    public static class TestSubjectConfig
    {
        // --- M1: Парные пистолеты-пулемёты ---
        public static ConfigEntry<float> SmgDamage, SmgProc, SmgFireRate, SmgReloadDelay;
        public static ConfigEntry<int> SmgMagazine;

        // --- M2: Подствольные гранатомёты ---
        public static ConfigEntry<float> GrenadeDamage, GrenadeProc, GrenadeRadius, GrenadeCooldown, GrenadeInterval;
        public static ConfigEntry<int> GrenadeShots;
        public static ConfigEntry<float> GrenadeSpeed, GrenadeUpAngle, GrenadeGravity;

        // --- R: Перекрёстный огонь ---
        public static ConfigEntry<float> CrossDamage, CrossProc, CrossDuration, CrossRange, CrossCooldown, CrossDurationShare;
        public static ConfigEntry<int> CrossShots;

        public static void Bind(ConfigFile c, string s)
        {
            SmgDamage = CharacterTestConfig.Float(c, s, "SMG.Damage", 0.47f, 0.05f, 5f,
                new Words("SMG: damage", "ПП: урон"), new Words("Damage per bullet (1 = 100%).", "Урон пули (1 = 100%)."), "{0:0.##}");
            SmgProc = CharacterTestConfig.Float(c, s, "SMG.Proc", 0.4f, 0f, 1f,
                new Words("SMG: proc coefficient", "ПП: коэффициент прока"), new Words("Proc coefficient per bullet.", "Коэффициент прока пули."), "{0:0.##}");
            SmgFireRate = CharacterTestConfig.Float(c, s, "SMG.FireRate", 16f, 1f, 60f,
                new Words("SMG: shots per second", "ПП: выстрелов в секунду"), new Words("Fixed fire rate; attack speed does not change it.", "Темп стрельбы; скорость атаки на него не влияет."), "{0:0.#}");
            SmgMagazine = CharacterTestConfig.Int(c, s, "SMG.Magazine", 40, 1, 300,
                new Words("SMG: magazine", "ПП: обойма"), new Words("Rounds per magazine.", "Патронов в обойме."), restartRequired: true);
            SmgReloadDelay = CharacterTestConfig.Float(c, s, "SMG.ReloadDelay", 2f, 0.5f, 10f,
                new Words("SMG: reload delay", "ПП: перезарядка"), new Words("Seconds without firing until a full reload.", "Секунд без стрельбы до полной перезарядки."), "{0:0.#} s", restartRequired: true);

            GrenadeDamage = CharacterTestConfig.Float(c, s, "Grenade.Damage", 3.4f, 0.1f, 20f,
                new Words("Grenades: damage", "Гранаты: урон"), new Words("Damage per grenade (1 = 100%).", "Урон гранаты (1 = 100%)."), "{0:0.##}");
            GrenadeProc = CharacterTestConfig.Float(c, s, "Grenade.Proc", 1f, 0f, 1f,
                new Words("Grenades: proc coefficient", "Гранаты: коэффициент прока"), new Words("Proc coefficient of the blast.", "Коэффициент прока взрыва."), "{0:0.##}");
            GrenadeRadius = CharacterTestConfig.Float(c, s, "Grenade.Radius", 6f, 1f, 30f,
                new Words("Grenades: blast radius", "Гранаты: радиус взрыва"), new Words("Blast radius, m.", "Радиус взрыва, м."), "{0:0.#} m");
            GrenadeShots = CharacterTestConfig.Int(c, s, "Grenade.Shots", 2, 1, 10,
                new Words("Grenades: count", "Гранаты: количество"), new Words("Grenades per use.", "Гранат за одно нажатие."));
            GrenadeInterval = CharacterTestConfig.Float(c, s, "Grenade.Interval", 0.15f, 0f, 1f,
                new Words("Grenades: delay between shots", "Гранаты: пауза между выстрелами"), new Words("Seconds between grenades (divided by attack speed).", "Секунд между гранатами (делится на скорость атаки)."), "{0:0.##} s");
            GrenadeCooldown = CharacterTestConfig.Float(c, s, "Grenade.Cooldown", 7f, 0.5f, 60f,
                new Words("Grenades: cooldown", "Гранаты: перезарядка"), new Words("Cooldown, s.", "Перезарядка, с."), "{0:0.#} s", restartRequired: true);
            // Ключи траектории — с 0.4.0, не переименовываем.
            GrenadeSpeed = CharacterTestConfig.Float(c, s, "Grenade.Speed", 70f, 10f, 200f,
                new Words("Grenades: speed", "Гранаты: скорость"), new Words("Launch speed, m/s.", "Скорость вылета, м/с."), "{0:0} m/s");
            GrenadeUpAngle = CharacterTestConfig.Float(c, s, "Grenade.UpAngle", 6f, -10f, 45f,
                new Words("Grenades: launch angle", "Гранаты: подъём ствола"), new Words("Degrees above the crosshair.", "На сколько градусов выше прицела."), "{0:0.#} deg");
            GrenadeGravity = CharacterTestConfig.Float(c, s, "Grenade.Gravity", 1f, 0f, 3f,
                new Words("Grenades: gravity", "Гранаты: тяжесть"), new Words("Gravity multiplier: 1 = normal, 0 = flies straight.", "Множитель силы тяжести: 1 — обычная, 0 — по прямой."), "x{0:0.##}");

            CrossDamage = CharacterTestConfig.Float(c, s, "Crossfire.Damage", 1f, 0.05f, 10f,
                new Words("Crossfire: damage", "Перекрёстный огонь: урон"), new Words("Damage per shot (1 = 100%).", "Урон выстрела (1 = 100%)."), "{0:0.##}");
            CrossProc = CharacterTestConfig.Float(c, s, "Crossfire.Proc", 1f, 0f, 1f,
                new Words("Crossfire: proc coefficient", "Перекрёстный огонь: коэффициент прока"), new Words("Proc coefficient per shot.", "Коэффициент прока выстрела."), "{0:0.##}");
            CrossShots = CharacterTestConfig.Int(c, s, "Crossfire.Shots", 12, 1, 100,
                new Words("Crossfire: shots", "Перекрёстный огонь: выстрелов"), new Words("Shots at base attack speed (× attack speed, rounded down).", "Выстрелов при базовой скорости атаки (× скорость атаки, вниз)."));
            CrossDuration = CharacterTestConfig.Float(c, s, "Crossfire.Duration", 1f, 0.1f, 10f,
                new Words("Crossfire: duration", "Перекрёстный огонь: длительность"), new Words("Seconds at base attack speed.", "Секунд при базовой скорости атаки."), "{0:0.##} s");
            CrossDurationShare = CharacterTestConfig.Float(c, s, "Crossfire.AttackSpeedToDuration", 0.5f, 0f, 1f,
                new Words("Crossfire: attack speed → duration", "Перекрёстный огонь: скорость атаки → длительность"),
                new Words("How extra shots from attack speed are split: 0 = all into fire rate, 1 = all into duration, 0.5 = evenly (duration × √AS, rate × √AS).",
                          "Как делятся лишние выстрелы от скорости атаки: 0 — всё в темп, 1 — всё в длительность, 0.5 — поровну (длительность × √СА, темп × √СА)."), "{0:0.##}");
            CrossRange = CharacterTestConfig.Float(c, s, "Crossfire.Range", 40f, 5f, 150f,
                new Words("Crossfire: range", "Перекрёстный огонь: дальность"), new Words("Target search radius, m.", "Радиус поиска целей, м."), "{0:0} m");
            CrossCooldown = CharacterTestConfig.Float(c, s, "Crossfire.Cooldown", 14f, 0.5f, 120f,
                new Words("Crossfire: cooldown", "Перекрёстный огонь: перезарядка"), new Words("Cooldown, s.", "Перезарядка, с."), "{0:0.#} s", restartRequired: true);
        }
    }
}
