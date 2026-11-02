# Разбор: как устроен RoR2-CharacterTest

Для того, кто полезет в код (то есть для тебя). Что видит игрок — в `README.md`.
0.6.0 собрана против настоящих DLL игры 1.5.0 и R2API (`RoR2-Diabloid/logs/refs.tar.gz`); в игре ещё не запускалась.

## 1. Общая картина

| Часть | Папка | Суть |
|---|---|---|
| **Ядро** | `Core/` | языки (`Loc`, `Words`) — чистый C#, без игры |
| **Кирпичи** | `Content/` | `GameAssets` (ванильные префабы), `PrefabUtil` (клон + сетевой id), `SkillSpec`/`SkillFactory` (навыки), `ModSkillState` (основа состояний), `GameLanguage` (тексты) |
| **Выжившие** | `Survivors/` | `SurvivorBase`, `SurvivorRegistry`, `SurvivorList`; по папке на выжившего, в ней `States/` |
| **Настройки** | `Config/` | общие (`Debug → VerboseLog`); у выжившего своя секция `Survivor: ID` |

## 2. Жизненный цикл выжившего

```
Awake плагина
  └─ SurvivorRegistry.Register → для каждого из SurvivorList.Create():
       ├─ проверка Id (A-Z, 0-9, _), BodyName (PascalCase…Body), без повторов
       ├─ секция "Survivor: ID": Enabled, потом BindConfig выжившего; выключен → его нет в игре
       ├─ тело: клон донора (PrefabUtil, сетевой) → baseNameToken/subtitle, bodyColor → SetupBody (статы)
       ├─ навыки: SkillFactory.ApplyToBody — на каждый слот из Skills() новое SkillFamily
       │    из наших SkillDef (состояния регистрируются в EntityStateCatalog сами)
       ├─ витрина: клон донорской (не сетевая)
       ├─ ИИ-мастер (если DonorMaster задан): клон, bodyPrefab → наше тело
       ├─ SurvivorDef + тексты на двух языках (NAME, SUBTITLE, DESCRIPTION, LORE, OUTRO_*)
       ├─ ContentAddition: AddBody, AddMaster, AddSurvivorDef (+ AddSkillDef/AddSkillFamily/AddEntityState раньше)
       └─ Hook() выжившего
     Ошибка в одном выжившем пишется в лог и не роняет остальных.
```

Токены: `CHARTEST_<ID>_NAME` и т.д.; навыки — `CHARTEST_<ID>_<KEY>_NAME/_DESC`. Лор логбук берёт сам:
`baseNameToken` с `_NAME` → `_LORE`.

## 3. Как добавить выжившего

1. Папка `Survivors/Имя/`, класс `ИмяSurvivor : SurvivorBase` (образец — `TestSubject/TestSubjectSurvivor.cs`).
2. Строчка `new ИмяSurvivor(),` в `Survivors/SurvivorList.cs`.
3. Заполнить: `Id`, `BodyName`, донора (`DonorBody`, `DonorDisplay`, `DonorMaster` — пути в `GameAssets`),
   тексты, `Color`, `SetupBody` (статы).
4. `Skills()`: по `SkillSpec` на навык. Заготовки `SkillSpec.Primary/Secondary/Utility/Special` ставят
   типичные флаги; что не подходит — поля `SkillSpec` или `Tweak`. Два описания на один слот = два варианта в лоадауте.
   Слот без описаний остаётся донорским — удобно писать навыки по одному.
   Копия донорского навыка: `SkillSpec.FromDonor(SkillSlot.X, "KEY")` (по умолчанию — его стандартный вариант,
   третьим аргументом — номер варианта). `Object.Instantiate` донорского SkillDef: подкласс, состояние, цифры, иконка и
   тексты (токены) донорские, имя/skillName свои; замок-ачивка донора не переходит (он висит на Variant, а Variant наш).
   Правки цифр — через `tweak`. Порядок в `Skills()` = порядок в лоадауте, первый — по умолчанию.
5. Состояние навыка: класс от `ModSkillState` в `States/`. Шаблон — `TwinSmg` (автоогонь одним состоянием, обойма на зарядах навыка, вид Double Tap), `UnderbarrelGrenades`
   (очередь снарядов-клонов). Пробные Прострел/Рывок/Нова удалены в 0.4.1 — образцы смотри в git-истории 0.1.0–0.4.0.

### Где что выполняется (важно для мультиплеера)

| Проверка | Где | Что тут делать |
|---|---|---|
| без проверки | все машины, где есть тело | анимация, звук, вспышка у дула |
| `isAuthority` | машина-владелец тела | `BulletAttack.Fire`, `BlastAttack.Fire`, `characterMotor`, `outer.SetNextState…` |
| `NetworkServer.active` | сервер | баффы (`AddTimedBuff`), спавн, предметы |

`BulletAttack`/`BlastAttack` с клиента-владельца сами отправляют попадания серверу — так делают ванильные навыки.

## 4. Донор и почему без Unity

Своей модели без Unity не сделать: нужны меш, кости, аниматор, AssetBundle. Поэтому выживший = клон ванильного:
моделька, хитбоксы, аниматор, скины, витрина, капсула — донорские. Отсюда:

- Анимации в состояниях — **имена слоёв и состояний аниматора донора**. Настоящие слои mdlCommandoDualies (1.5, из лога):
  `Body | Impact | Gesture, Override | Gesture, Additive | AimPitch | AimYaw | Gesture Left, Additive | Gesture Right, Additive | Flinch | Idle, Additive`.
  Ванила (и 0.1.0) просит `"Gesture Additive, Left/Right"` — таких нет; правильно `"Gesture Left/Right, Additive"` +
  `"FirePistol, Left/Right"`, `"Body"` + `"DodgeForward"`, `"Gesture, Additive"` + `"ThrowGrenade"`. В состояниях — `PlayAnimationSafe`
  (из `ModSkillState`): ищет состояние на названном слое, а нет слоя — на любом (0.2.1: у Коммандо 1.5 **нет слоёв**
  `Gesture Additive, Left/Right`, ванильный FirePistol2 работает только потому, что Unity с индексом слоя −1 ищет по всем слоям).
  Что где нашлось и список настоящих слоёв — один раз в лог (`[anim] ...`).
  Ванильный `PlayAnimation` на промахе спамит `Animator.GotoState: State could not be found` / `Invalid Layer Index '-1'` (так было в 0.1.0).
- Дула (`muzzleName`) — из `ChildLocator` донора: у Коммандо `MuzzleLeft`, `MuzzleRight`.
- Эффекты и звуки удобно брать из статических полей ванильных состояний (`FirePistol2.tracerEffectPrefab`):
  игра заполняет их из EntityStateConfiguration при загрузке каталога, к моменту навыка они уже есть.
- Скины донора остаются (мастерский скин Коммандо открывается его же мастерством).
- Портрет (`portraitIcon`) и иконки навыков — донорские, пока не нарисуем свои (можно взять холст из Destroyer `Art/`).

## 5. Сеть: свой assetId вместо PrefabAPI

`R2API.Prefab` нет ни в профиле, ни в облачных refs, поэтому `PrefabAPI.InstantiateClone` заменён `PrefabUtil.Clone`:

- копия живёт под выключенным объектом-держателем (DontDestroyOnLoad, HideAndDontSave) — Awake не зовётся, это "префаб";
- сетевой копии ставится `NetworkIdentity.m_AssetId` = MD5(`FToPay.CharacterTest:` + имя) — **рефлексией**
  (поле приватное; прямой доступ через публицированные GameLibs в игре кинет `FieldAccessException`);
- регистрирует префаб в сети сама игра: всё, что в ContentPack (`AddBody`, `AddMaster`), идёт в
  `ContentManager.networkedObjectPrefabs`.

Без своего assetId клиент заспавнил бы Коммандо вместо нашего тела. **Имя клона после релиза не менять** — сменится assetId.

То же с `GenericSkill._skillFamily` — приватное поле, ставится рефлексией.

## 6. Обойма на зарядах навыка (TwinSmg)

- SkillDef: `baseMaxStock = rechargeStock = обойма`, `baseRechargeInterval = задержка перезарядки`, `stockToConsume = 1`.
  Игра сама копит секундомер, пока зарядов меньше максимума, и по истечении добавляет `rechargeStock` разом.
- Каждый выстрел ставит `activatorSkillSlot.rechargeStopwatch = 0` → перезарядка = N секунд тишины, а не N секунд после опустошения.
- Очередь — одно состояние с таймером выстрелов (остаток переносится), а не состояние на пулю: смена состояния ест тик (12/с → 10/с).
  Первую пулю оплачивает активация, остальные — `DeductStock(1)`; выход только на границе выстрела.
- Темп от скорости атаки не зависит; урон × `attackSpeed / baseAttackSpeed`, пересчёт на каждом выстреле.
- Уменьшение перезарядок (Alien Head и т.п.) ускоряет и перезарядку обоймы — как у Бандита.

## 6½. Снаряд-клон (UnderbarrelGrenades)

- `SurvivorBase.CreateAssets()` — в Awake, после BindConfig и до навыков: тут клонируются снаряды.
- Граната = `PrefabUtil.Clone(CommandoGrenadeProjectile, networked)` + `ContentAddition.AddProjectile`. На клоне:
  `ProjectileImpactExplosion` — взрыв от любого касания (`destroyOnWorld/destroyOnEnemy/detonateOnEnemy/impactOnWorld`),
  без фитиля и детей, `falloffModel = None`, `blastDamageCoefficient = 1` (урон — в `FireProjectileInfo.damage`),
  `preserveExplosionScale = false` (эффект под радиус); `ProjectileDamage.damageType.damageSource = Secondary`.
- Траектория: скорость — `FireProjectileInfo.speedOverride` (читается на каждом выстреле); подъём — поворот направления
  вокруг оси "вправо" на −угол; тяжесть — `Rigidbody.useGravity` + `AntiGravityForce` с коэффициентом `1 − множитель`,
  ползунок меняет компонент ПРЕФАБА (новые гранаты берут значение из него).
- Звук/вспышка — с `new ThrowGrenade()`: конструктор EntityState заполняет поля из EntityStateConfiguration.
- В лог при запуске: список компонентов клона (`projectile TestSubjectUnderbarrelGrenade: ...`).

## 6¾. Наведение без камеры (Crossfire)

- Камеру не трогаем: `BulletAttack` летит из `inputBank.aimOrigin` прямо в `HurtBox` цели.
- Цели: `BullseyeSearch` (вражеские команды, LoS от aimOrigin, `filterByDistinctEntity`, по расстоянию), заново на каждом выстреле.
  Раздача: цель с наименьшим числом выстрелов, при равенстве — ближайшая (`Dictionary<HealthComponent,int>`).
- Модель: `characterDirection.moveVector/forward` к цели в FixedUpdate И в Update — машина Body (GenericCharacterMain.HandleMovements)
  каждый физтик ставит moveVector от движения/прицела. `SetAimTimer(0)` в начале — иначе "режим прицеливания" тянет к камере.
  Руки (AimAnimator) всё равно смотрят по прицелу камеры — трассеры идут из дул правильно.
- "Без целей ничего не происходит": свой `CrossfireSkillDef : SkillDef` с `CanExecute` (через `SkillSpec.Factory`) — навык
  не активируется, перезарядка не тратится.
- Число выстрелов: `floor(Shots × СА)`, длительность `Duration × СА^k`, интервал = длительность / N (0.6.0).
- Корень и неуязвимость (0.6.0): навык на машине **Body** — пока он идёт, GenericCharacterMain не крутится: нет ходьбы,
  прыжков и нажатий навыков. Машину Weapon в начале — в Idle (иначе зажатая M1 продолжила бы очередь ПП).
  Не падать: `characterMotor.gravityParameters.channeledAntiGravityGranterCount++` (и обратно в OnExit) + скорость в ноль
  каждый тик. Неуязвимость: `AddBuff(HiddenInvincibility)` на сервере, `RemoveBuff` в OnExit (не таймер — навык может кончиться раньше).
  Минимальный приоритет прерывания — Frozen.

## 6⅞. Настройки навыков

- `Survivors/TestSubject/TestSubjectConfig.cs` — все цифры; `CharacterTestConfig.Float/Int` регистрируют их и для меню.
- Состояния читают `.Value` в момент выстрела; то, что живёт в префабе снаряда (радиус, прок, тяжесть гранаты), меняется
  подпиской `SettingChanged` на префаб. Перезарядки и обойма зашиты в SkillDef при запуске — `restartRequired`.
- Описания: `SkillSpec.DynamicDescription` (лямбда от настроек); плагин на `Config.SettingChanged` зовёт
  `SkillFactory.RefreshTexts()`.

## 7. Грабли (часть — из Destroyer/Diabloid)

- **Публицированные GameLibs врут:** компилятор пускает к private, Mono в игре — нет. Облачная сборка против настоящих
  DLL это ловит. Затенение (`CharacterBody.transform`) — всегда `x.gameObject.transform`.
- **On-хуки к приватным вложенным типам** компилируются, но в игре `MethodAccessException` в момент подписки.
- Unity-объекты: не `?.` и не `??` — только `if (x)` / `x ? a : b`.
- Конфиг пользователь чистит сам — ключи при смене дефолтов не переименовываем.
- `Id`, `BodyName`, `Key` навыка после релиза не менять (лоадауты в сейве, конфиги, сетевой id).
- Состояние, которого нет в EntityStateCatalog, сеть передать не может — `SkillFactory.Build` регистрирует сам;
  состояние, в которое переходишь из своего же (`outer.SetNextState(new Другое())`), зарегистрируй руками:
  `SkillFactory.RegisterState(typeof(Другое))` в `Hook()`.
- Не проверено в игре (0.1.0): имена анимаций Коммандо в 1.5, что общие SkinDef у двух тел не путают SkinCatalog.

## 8. Сборка и проверка

- **Готовый DLL от Claude:** собирается в облаке и кладётся в `out\CharacterTest.dll`.
- Дома: `build.cmd` → `logs\build.log`. DLL сама копируется в профиль, если есть `Directory.Build.props.user`.
- В облаке (без NuGet): `RoR2-Diabloid\logs\refs.tar.gz` → плоская папка →
  `csc -noconfig -nostdlib -target:library -langversion:latest -nowarn:CS0618 -r:<все dll> $(find src/CharacterTest -name '*.cs')`.
