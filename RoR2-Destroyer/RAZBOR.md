# Разбор: как устроен RoR2-Destroyer

Документ для того, кто полезет в код (то есть для тебя). Что видит игрок — в `README.md`.
0.5.0 (двенадцать предметов) собрана против настоящих DLL игры 1.5.0 и R2API из профиля Diabloid; в игре ещё не запускалась.

## 1. Общая картина

| Часть | Папка | Суть |
|---|---|---|
| **Чистое ядро** | `Core/` | языки (`Loc`, `Words`), формулы стаков (`Stacking`), разметка текстов (`Tx`) |
| **Рисование** | `Art/` | холст `IconCanvas`, фигуры `Shapes`, заливки `Paints`, все иконки — `IconBook` (+ `Art/Icons/`: предметы и баффы) |
| **Предметы в игре** | `Content/` | `ItemBase` (основа предмета), `ItemRegistry`, `ItemSettings`, иконка → спрайт, тексты, `HitEvents` (при попадании), `BuffFactory` (свои баффы), `GameAssets` (ванильные эффекты) |
| **Сами предметы** | `Items/` | по классу на предмет + `ItemList.cs`; шаблон `ItemTemplate.cs.txt` |
| **Настройки** | `Config/`, `Integration/` | конфиг BepInEx + меню Risk of Options на двух языках |
| **Отладка** | `UI/DebugMenu.cs` | F3: выдать / забрать / бросить предмет |
| **Верстак** | `tools/Workbench/` | самотест `Core`+`Art` и рендер иконок в PNG без игры |

`Core/` и `Art/` — **чистый C#**: ни Unity, ни RoR2. Их же компилирует `tools/Workbench`, поэтому формулы
и иконки проверяются без запуска игры. Всё, что знает про игру, лежит в остальных папках.

## 2. Жизненный цикл предмета

```
Awake плагина
  └─ ItemRegistry.Register → для каждого из ItemList.Create():
       ├─ проверка Id (A-Z, 0-9, _; без повторов)
       ├─ ItemSettings: секция "Item: ID", настройка Enabled, потом BindConfig предмета
       ├─ выключен → всё, предмета в игре нет
       ├─ ItemDef: name = "DESTROYER_" + Id, токены _NAME/_PICKUP/_DESC/_LORE, теги, тир (рефлексией),
       │   иконка: IconBook.Get(Id) → IconCanvas 256×256 → Texture2D с мипмапами → Sprite
       ├─ тексты на двух языках (overlay R2API) + подписка: сменилась настройка секции → тексты заново
       ├─ ItemAPI.Add(CustomItem)
       └─ Hook() предмета — его подписки на события игры
     Ошибка в одном предмете пишется в лог и не роняет остальные.

RoR2Application.onLoad (каталоги собраны)
  └─ ItemRegistry.OnCatalogsReady → для каждого:
       ├─ PickupIndex (для дропа)
       ├─ моделька: ModelFrom → копия pickupModelPrefab/Reference ванильного предмета в ItemDef и PickupDef
       └─ OnCatalogsReady() предмета
```

## 3. Как добавить предмет

1. Скопировать `Items/ItemTemplate.cs.txt` → `Items/МойПредмет.cs`, переименовать класс, поменять `Id`.
2. Вписать `new МойПредмет(),` в `Items/ItemList.cs`.
3. Заполнить: `Tier`, `Name`, `Pickup`, `Description`, `Tags`, `ModelFrom`, настройки в `BindConfig`, эффект в `Hook`.
4. Иконка: метод `static void МойПредмет(IconCanvas c)` в `IconBook` (лучше отдельным файлом
   `Art/Icons/МойПредмет.cs` с `public static partial class IconBook`) и строчка `{ "MY_ID", МойПредмет }` в `IconBook.All`.
5. `build.cmd` → глянуть `logs\icons\_sheet.png` → в игре F3 → `+1`.

Шаблон компилируется в облачной проверке (переименованный в .cs и вписанный в список), так что API в нём рабочее.

### Три типовых способа сделать эффект (все в шаблоне)

| Способ | Где работает | Для чего |
|---|---|---|
| `RecalculateStatsAPI.GetStatCoefficients += (body, args) => ...` | **каждая машина**, при пересчёте статов | урон, скорость, броня, здоровье, крит, реген, прыжки |
| `HitEvents.OnHit += (info, attacker, victim) => ...` | **сервер**, после ванильных проков | "при попадании": шансы, дебаффы, доп. удары |
| `UseBehavior<T>()` → `CharacterBody.ItemBehavior` | по умолчанию **сервер** | таймеры, ауры, всё, что тикает; `stack` = копий |

"При попадании" — через `HitEvents.OnHit` (см. §3а), а `onServerDamageDealt` — когда нужен любой полученный урон,
включая DoT (так шампиньон узнаёт, что его ударили).

Ещё полезное (всё публичное в 1.5.0, проверено по сборке):
`GlobalEventManager.onCharacterDeathGlobal` (убийства), `onServerCharacterExecuted`,
`CharacterBody.onBodyInventoryChangedGlobal`, `CharacterBody.onBodyStartGlobal`,
`Inventory.onServerItemGiven`, On-хук `GlobalEventManager.OnHitEnemy` (публичный; внутренний `ProcessHitEnemy` — private).

### Подсчёт копий

`Count(body)` / `Count(master)` / `Count(inventory)` = `Inventory.GetItemCountEffective`. В 1.5 у инвентаря
несколько видов копий: постоянные, временные (Дрифтер), "канальные", "арендованные". Ванильный `GetItemCount`
зовёт именно `Effective` (проверено по IL). Выдавать в отладке — `GiveItemPermanent`, забирать — `RemoveItemPermanent`.

### Формулы стаков (`Core/Stacking.cs`)

| Функция | Формула | Пример |
|---|---|---|
| `Linear(n, first, per)` | first + per·(n−1) | 15% + 10% за копию |
| `Hyperbolic(n, k)` | 1 − 1/(1 + k·n) | как Tougher Times, не доходит до 100% |
| `Exponential(n, p)` | 1 − (1 − p)^n | каждая копия срезает p от остатка |
| `Geometric(n, r)` | (1 + r)^n | множитель ×1.1 за копию |

### Тексты (`Core/Tx.cs`)

`Tx.Damage("+15%")`, `Tx.Healing`, `Tx.Utility`, `Tx.Health`, `Tx.Void`, `Tx.Lunar`, `Tx.Death`, `Tx.Stack("+10% per stack")`
дают ванильную окраску `<style=cIsDamage>` и т.д. `Tx.Pct(0.15f)` → `15%`, всегда с точкой.

### 3а. Общие помощники

- `HitEvents.OnHit += (info, attacker, victim) => ...` — "при попадании" как у ванили: один On-хук на
  `GlobalEventManager.OnHitEnemy` (публичный; он зовёт приватный `ProcessHitEnemy`). Фильтр тот же, что у ванили:
  сервер, proc больше 0, урон не отклонён; плюс есть оба тела. `info.damage` — "TOTAL damage" (до брони, без крита; крит — `info.crit`).
  Падение одного обработчика пишется в лог и не глушит остальные.
- `Roll(шанс, info.procCoefficient, attacker.master)` в `ItemBase` — шанс в долях × proc, с удачей.
- `BuffFactory.Create("ID", цвет, isDebuff, canStack)` — BuffDef с иконкой из `IconBook.Buffs` (белый силуэт,
  игра красит в цвет баффа) через `ContentAddition.AddBuffDef`. Звать в `Hook` (Awake).
- `GameAssets.Load(путь)` / `SpawnEffect(путь, позиция, масштаб)` — ванильные префабы по старым путям. Только пути,
  которые грузит сама игра (найдены по строкам в RoR2.dll): взрыв Бегемота, кровавый всплеск Shatterspleen, метка Радара.
- `DestroyerPlugin.Instance.gameObject.AddComponent<T>()` — свой MonoBehaviour (трекер смартфона).

## 4. Иконки

**2D-модельки на земле (0.14.0, `Content/FlatPickupModel.cs`).** `ItemBase.BorrowModel`: если `Visual → FlatPickupModels` (по умолчанию да) — вместо ванильной модели свой префаб: квадрат `MeshRenderer` с иконкой предмета, компонент `FaceCamera` в `LateUpdate` разворачивает его к `CameraRigController.sceneCam`. Тонкости (IL `PickupDisplay` 1.5): размер игра подгоняет по ОБЪЁМУ bounds первого Mesh/SkinnedMesh-рендерера — у плоского квадрата объём 0, поэтому в меше две лишние вершины без треугольников (z = ±0.5) → bounds 1×1×1; подсветка подбора берёт только Mesh/SkinnedMesh — поэтому не `SpriteRenderer`; материал — копия встроенного Sprites-Default (его отдаёт новый `SpriteRenderer`), без освещения; `PickupDef.displayPrefabReference = null` (null игра проверяет и берёт `displayPrefab`). Префабы — под выключенным держателем с `DontDestroyOnLoad`. Не получилось — лог и одолженная 3D-модель.

`IconCanvas` — квадрат, координаты 0..1, (0,0) слева сверху, y вниз. Фигура — функция расстояния до края (SDF),
поэтому края сглажены на любом размере, а фигуры складываются, вычитаются, поворачиваются, раздуваются.

- Фигуры: `Circle`, `Ellipse`, `Box` (со скруглением), `Capsule` (палка), `Arc` (дуга с круглыми концами — колбаса),
  `Ring`, `Polygon`, `RegularPolygon`, `Star`, `Pie`.
- Операции: `Union`, `Intersect`, `Subtract`, `Grow`, `Outline`, `SmoothUnion`, `Move`, `Rotate` (по часовой), `MirrorX`, `SymmetricX`.
- Заливки: `Solid`, `Linear`, `Vertical`, `Radial`, `Lit` (объём со светом сверху-слева), `Stripes`.
- Холст: `Fill`, `Stroke`, `Outline` (обводка ПОД рисунком), `Glow`.
- Ванильный стиль: объём через `Paints.Lit`, в конце `c.Outline(IconBook.OutlineWidth, IconBook.Ink)`. Фон прозрачный —
  цветную подложку тира рисует игра.
- `SymmetricX`: половинка должна заходить за ось, иначе по оси шов (см. щит в `_demo.png`).
- В игре иконка 256×256 с мипмапами; рисуется при загрузке, заглушка — ~65 мс в облаке.
- Нет иконки в `IconBook.All` → бомба-заглушка.
- Баффы — `IconBook.Buffs`: чисто белые силуэты без обводки (самотест проверяет, что белые). Нет иконки → белое кольцо.

Класс называется `IconCanvas`, а не `Canvas`: иначе он сталкивается с `UnityEngine.Canvas` в каждом файле с `using UnityEngine`.

## 5. Настройки

- Общие — `Config/DestroyerConfig.cs` (сейчас только отладка). Предмета — через `ItemSettings` в `BindConfig`.
- Всё регистрируется через `DestroyerConfig.Bind` → попадает в Risk of Options с названием и описанием на двух языках,
  категория — имя предмета.
- `Enabled` требует перезапуска и **должен совпадать у всех в лобби**: выключенный предмет не регистрируется,
  и индексы предметов у хоста и клиента разъедутся.
- Статы (RecalculateStats) считаются на каждой машине по СВОИМ настройкам; серверные эффекты — по настройкам хоста.

## 5а. Предметы: как сделаны

| Предмет | Где живёт логика | Суть реализации |
|---|---|---|
| Чоризо | сервер: `HitEvents` + `RoR2Application.onFixedUpdate` (10 раз/с) | дебафф `bdDESTROYER_TASTY` (не стакается); тик: тела с меткой × тела с предметом из вражеской команды; расстояние — от `corePosition` едока до ближайшей точки хитбоксов жертвы (`Collider.ClosestPoint`, для невыпуклых мешей — габаритная коробка; без хитбоксов — центр минус `bestFitRadius`). Метка снимается ДО урона. Укус: `TakeDamage` + `OnHitEnemy`/`OnHitAll` с proc 1.2, `HealFraction`, кровавый всплеск |
| Баллончик | сервер: `HitEvents` | шанс от копий не растёт (0.3.0), копии добавляют длительность; `DLC1Content.Buffs.Blinded` — ванильный: в `RecalculateStats` режет `visionDistance` до 15 + 0.75·радиус; по нему ИИ ищет цели и проверяет LoS (`BaseAI.HasLOS`, `BullseyeSearch`). Сразу `RecalculateStats()`, потом каждому `BaseAI` мастера `currentEnemy.Reset()` и `enemyAttention = 0`. Игрока, ослеплённого монстром с баллончиком, накроет ванильный туман `VisionLimitEffect` |
| Вата | сервер: `HitEvents` | `SphereSearch` (entityPrecise, вражеские команды, по одному хитбоксу на сущность), цель пропускается; урон `info.damage × 0.3` (0.3.0, ключ `ExplosionDamage`), крит и procChainMask от попадания, `DamageType.AOE`, proc 0 по умолчанию; флаг `exploding` от взрыва из взрыва; эффект — взрыв Бегемота, scale = радиус |
| Смартфон | деньги — сервер (On-хук `CharacterMaster.GiveMoney(uint, bool)`), метки — каждая машина для своего игрока | остаток дроби копится у мастера, сброс на старте забега. Метки (0.3.0): кандидаты — как у Радара: `InterfaceInstanceTracker<IInteractable>` + `ShouldShowOnScanner()`; категория — повтор `PingIndicator.GetInteractableIcon` (`Smartphone.Classify`): бочка → «Shrine» в имени (мимо) → `IChestBehavior` (сундук) → лут (мимо) → `SummonMasterBehavior` (дрон) → телепорт (мимо) → своя иконка `PingInfoProvider` (мимо) → статуя Базара (?) → покупка: за лунные монеты (?), иначе мимо → `TimedChestController` (сундук) → остальное (?). 1 копия — ближайший сундук, `DroneFromCopies` (2) — ближайший дрон, `BarrelAndMysteryFromCopies` (3) — ближайшая бочка и все «?». Метка — `PoiPositionIndicator` + иконка из `GetInteractableIcon` (как приватный `ChestRevealer.RevealedObject`) |
| Шампиньон | сервер: `ItemBehavior` + `onServerDamageDealt`; броня — все машины | `GetNotMoving()` (только сервер, как у Bustling Fungus: сначала ~секунда без движения); накопленное → стаки баффа `bdDESTROYER_CHAMPIGNON_ARMOR` раз в 0.25 с; `RecalculateStatsAPI`: `armorAdd += стаки`. Движение — сразу 0, урон — ×0.1. Опция `IgnoreDamageOverTime` |
| Лисий хвост | сервер: `RoR2Application.onFixedUpdate` (10 раз/с) | владельцы без баффа-кулдауна × враги: грубый отсев по центру, точно — `Geometry.DistanceToSurface` (общий с чоризо). Ванильный `RoR2Content.Buffs.Cloak` на 3 с + свой `bdDESTROYER_FOX_TAIL_COOLDOWN` (`isCooldown`) на 30·0.8^(n−1) с |
| Стул | сервер: `ItemBehavior` по `GetNotMoving()`; статы — все машины | бафф `bdDESTROYER_CHAIR_SEATED` вкл/выкл; `RecalculateStatsAPI`: `critAdd += 100·15%·n` (в процентах), `critDamageMultAdd += 15%·n` |
| Гиря | сервер: `HitEvents` | `HealthComponent.TakeDamageForce(направление × 250·n·proc, alwaysApply, disableAirControl)`; направление — от атакующего по горизонтали + 0.3 вверх; `BodyFlags.Unmovable` пропускаем. Импульс, а не скорость: масса решает |
| Пылесос | дальность — каждая машина, магнит — сервер | `Interactor.maxInteractionDistance` = исходное (запоминается компонентом) + 3·n при смене инвентаря; клиентский `InteractionDriver` ищет по нему, сервер (`PerformInteraction`) дистанцию не проверяет. Магнит: раз в 0.25 с `FindObjectsOfType<GravitatePickup>()` (в `InstanceTracker` их нет), свободным в радиусе — `ForceGravitate(коллайдер тела)`; команду, флаги и "сферу при полном ХП не тянуть" проверяет ваниль |
| Шарики | сервер: призыв/возврат в `onFixedUpdate`; визуал — каждая машина | `MasterSummon` `RoboBallMiniMaster` (как у Diabloid), +1 `MinionLeash`, телу флаг `Mechanical`. Скрытый бафф-метка `bdDESTROYER_BALLOON`, стаки = копии хозяина → `damageTotalMult × (0.2 + 0.1·(n−1))`, `healthTotalMult × (0.8 + 0.2·(n−1))`. Первый раз — сразу 5, дальше +1 раз в 10 с; хозяин потерял предмет — `TrueKill`. Предметы дронов: `DroneWeaponsBoost` = копии `DroneWeapons` хозяина, `DroneDynamiteDisplay` = копии `DronesDropDynamite` (как ванильные `UpdateMinionInventory`, они пропускают тело, которого ещё нет при `MasterSummon`). Визуал: модель ×0.25 каждый кадр, через 0.5 с — копии материалов `CharacterModel.baseRendererInfos` с `_Color`/`_TintColor` из палитры по `netId` (одинаково на всех машинах) |
| Дымовая завеса | слепота — сервер (`ItemBehavior.FixedUpdate`); статы — все машины; убийства — сервер (`onCharacterDeathGlobal`) | Заражение: `ItemBase.Corrupts` → `ItemRegistry` хукает `On.RoR2.ItemCatalog.SetItemRelationships` и дописывает свой `ItemRelationshipProvider` (`ContagiousItem`, пары `itemDef1` = что перерабатывается, `itemDef2` = пустотный). Слепота: пока нет `bdDESTROYER_SMOKE_CLEAR` — держим ванильный `DLC1Content.Buffs.Blinded` обычным `AddBuff` (флаг «повесили мы», снимаем только своё; Blast Shower снимет — вернём на следующем тике). Убийство (есть `victimMaster`, разные команды) → `AddTimedBuff(SMOKE_CLEAR, 5)`. `RecalculateStatsAPI` без SMOKE_CLEAR: `b = 0.15 + 0.05·(n−1)`; `moveSpeedMultAdd += b + 0.05·n`; `MultAdd += b` для health/shield/regen/jumpPower/damage/attackSpeed/critDamage; `armorTotalMult`, `critTotalMult` ×(1+b); перезарядка ÷(1+b) через `SkillCooldowns` (0.14.4; через R2API не работало). Слепота режет и `visionDistance` игрока → `BullseyeSearch` (автонаводка Охотницы и т.п.) видит ~15 м |
| Преобразователь боли | сервер: `onServerDamageDealt`, `ItemBehavior`; броня — все машины | Удар по владельцу → возврат `{осталось = damageDealt·min(0.999, 1 − 0.5·0.9^(n−1)), скорость = это / 20}` (0.7.3: каждая копия закрывает 10% остатка до 100%, асимптота) в список поведения. `FixedUpdate`: каждому возврату `min(осталось, скорость·dt)`, сумма раз в 0.1 с — `Heal(…, nonRegen: false)` (как реген: без цифр, мимо Corpsebloom). Броня (0.7.1): раз в 0.1 с стаки баффа `PAIN_ARMOR` = `round(10·n·(1 − combinedHealthFraction))` (0.7.2; в 0.7.1 было 70% и 100 (+50) — неубиваемо), `RecalculateStatsAPI`: `armorAdd += стаки` (как шампиньон: смена стаков сама грязнит статы и синкается). Число возвратов — `SetBuffCount(PAIN_RESTORE)`. Потерял предмет — недолеченное пропадает. (0.7.0 было 40% за 10·0.9^(n−1) с + разгон от лечения — выкинуто) |
| Звуковая пушка | сервер: `ItemBehavior` | Таймер `3·0.95^(n−1)` (0.16.1, было 0.7); на нуле — `BullseyeSearch` (вражеские команды, 50 м, `filterByLoS`, по дистанции), нет цели — повтор через 0.2 с. Выстрел — `BulletAttack` из `corePosition` в цель: урон `body.damage·3.2`, `RollCrit`, proc 1, `FalloffModel.None`, радиус 1.2, трассер `TracerSmokeChase`, попадание `KnockBackHitEnemiesImpact`, у владельца — `BootShockwave` (пути в `GameAssets`) |
| Вафельница | сервер: `HitEvents` | Каждое попадание (proc > 0, `info.dotIndex == None`) → `AddBuff(SCORCH)` (дебафф без таймера). ≥ 6 → `SetBuffCount(0)` и ванильный Burn через `InflictDotInfo`: `totalDamage = damage·(1 + 0.5·(n−1))`, `damageMultiplier = коэф·interval / (2·def.damageCoefficient)` — при `totalDamage` игра сама считает длительность `totalDamage·interval / урон за тик` (IL `DotController.AddDot`), выходит ровно 2 с. Перед этим `StrengthenBurnUtils.CheckDotForUpgrade` (Ignition Tank множит и `damageMultiplier`, и `totalDamage` — длительность та же) |
| Артиллерийский запал | сервер: `HitEvents` + `onFixedUpdate` | Повтор `FireworkLauncher` (IL 1.5): из ванильного `Prefabs/FireworkLauncher` берём `projectilePrefab`, `launchInterval`, `randomCircleRange`; точка — 2 м над `corePosition` + круг, направление `up + insideUnitSphere·0.1`, сила 200, крит — один бросок на залп. Свой пускатель, потому что снаряд правим после появления: `ProjectileManager.FireProjectile` может отложить выстрел (очередь `CacheProjectileForNextFrameServer`), а `FireProjectileImmediateServer` отдаёт объект сразу → `ProjectileController.procCoefficient = 0.1`, `ProjectileExplosion.blastRadius = 2 + 1·(n−1)`, `blastProcCoefficient = 1` (итог BlastAttack = их произведение, IL `PerformDamage`). Ракеты с компонентом-меткой — их попадания запал не запускают. ICBM: только `GetMoreMissileDamageMultiplier` |
| Deus Ex Machina | сервер: `onFixedUpdate` (4/с), `onBodyInventoryChangedGlobal`; статы — все машины | Как призрак элиты (`NetworkedAffixDeathBehaviour.TrySpawnGhostBodyViaMasterSummon`): `MasterCatalog.FindAiMasterIndexForBody` → `MasterSummon` (summonerBodyObject = хозяин → миньон, `AIOwnership` ставит leader), `loadout` — копия хозяина, `preSpawnSetupCallback` — предметы до тела. Предметы: `CopyItemsFrom(хозяин, defaultItemCopyFilterDelegate && не Deus)` (= `CleanInventory` + `AddItemsFrom`, стирает всё — поэтому +1 `MinionLeash` заново) при каждой смене инвентаря хозяина. Следование: свой `AISkillDriver` первым через `BaseAI.ReplaceSkillDrivers`: `moveTargetType = CurrentLeader`, `minDistance 30`, `ChaseMoveTarget`, спринт, без навыка. Статы (0.9.0, лунный): у хозяина — по числу предметов, у копии — скрытый бафф `DEUS_CLONE` (стаки = копии хозяина, держится каждый тик — после Dio новое тело); обоим `damageTotalMult`/`healthTotalMult`/`regenTotalMult × 0.93^n` (0.9.1; в 0.9.0 было 0.9 — слишком сильно), копия иначе полная (в 0.8.x был красный, копии 70% (+5%)). Смерть — `IsDeadAndOutOfLivesServer` → таймер 10 с. Сам предмет `CannotCopy` + `AIBlacklist` |
| Первоцвет | статы — все машины; лечение — сервер (On-хук `HealthComponent.Heal`) | Переработка всех лунных: новое `ItemBase.CorruptsAll` (условие по `ItemDef`, здесь `tier == Lunar`) — `ItemRegistry` в хуке `SetItemRelationships` проходит `ItemCatalog.allItemDefs` (каталог уже собран: IL `ItemCatalog.Init` — `SetItemDefs`, потом `SetItemRelationships`), скрытые и сам предмет пропускает. Статы (0.10.1, перевёрнуто: стаки усиливают и бьют по лечению): `k = 1.2 + 0.2·(n−1)`; `TotalMult × k` для health/shield/regen/moveSpeed/jumpPower/damage/attackSpeed/crit/critDamage, перезарядка ÷k через `SkillCooldowns` (0.14.4; через R2API не работало); `armorAdd += 50 + 25·(n−1)` (не умножается; 0.10.3, было 200 плоско). Лечение: `f = 1 − 0.15·n` без порога (0.10.2); `f ≥ 0` — `amount × f`; `f < 0` — ванилу не зовём, снимаем `amount·|f|` прямо с `health` (`Networkhealth`, до нуля — `Suicide`), как ванильный отрицательный реген в `ServerFixedUpdate`: без события урона, щит не трогает. Реген идёт через тот же `Heal` и тоже становится уроном (в 0.10.0 было ×3 (−0.1) и `clamp01(0.05·(n−1))`), ноль — сразу `return 0` без ванили; касается и регена (он идёт через тот же `Heal`) |
| Бензопила | сервер: `onFixedUpdate`; состояние — таймер-баффы | 0.11.1: запуск, когда нет `CHAINSAW_ACTIVE`/`CHAINSAW_COOLDOWN` и есть враг в контакте → `AddTimedBuff(ACTIVE, 3)` и `AddTimedBuff(COOLDOWN (isCooldown), 3 + 10)` (0.11.2, было 20). Пока ACTIVE: шаг `0.3 / частота`, частота `1 + 2·(a − 1)` при a ≥ 1 и просто `a` при a < 1 (0.14.3: иначе при a = 0.5 частота 0 — пила раз в 30 с), `a = attackSpeed / baseAttackSpeed`, без потолка — короче кадра бьёт несколько раз за кадр (предохранитель 50/кадр). Каждый тик — заново ближайший враг в контакте (`DistanceToSurface ≤ 1.5 + owner.radius`), урон `damage·(1 + 0.5·(n−1)) / a`, свой `RollCrit`, как укус чоризо: `TakeDamage` + `OnHitEnemy`/`OnHitAll` с proc 0.3. (0.11.0: все в контакте, 50% (+30%) / 0.2 с / proc 0.4, без перезарядки, потолок 20 тиков/с.) |
| Голова льва | сервер 4/с: `EnemyAura`; броня — все машины | Новый общий помощник `Content/EnemyAura`: источники (носитель, радиус, стаки) → врагу в радиусе (до поверхности: центр − `bestFitRadius`) дебафф со стаками = максимум среди накрывших (не складываются); ушёл — `SetBuffCount(0)`, снимает только своё. Дебафф `LION_FEAR`, стаки = копии; `armorAdd −= 15·стаки` |
| Корона Элитаризма | сервер 4/с + `onServerDamageDealt`; статы — все машины | Ходьба: `!isSprinting` → `moveSpeedReductionMultAdd += p/(1−p)` (игра делит скорость на 1 + сумма замедлений; `set_isSprinting` сам зовёт `RecalculateStats`, IL 1.5). Таймер спокойствия на носителя: +0.25 за тик, 0 при `isSprinting` и когда носитель — атакующий в `onServerDamageDealt` (урона > 0, не по себе) и это прямая атака (0.12.1): `damageInfo.damageType.damageSource & (Primary | Secondary | Special) != 0`, без `DOT` и `dotIndex`; Utility (мобильность), предметы (`NoneSpecified`), снаряжение и Hazard не гасят. ≥ 5 с → бафф `CROWN_REIGN` и источник `EnemyAura` с радиусом 50 + 20·(n−1) (0.12.2, было +5) → дебафф `CROWN_OPPRESSION` (стаки = копии): `attackSpeedTotalMult` и `damageTotalMult × (1 − min(0.95, 0.15 + 0.02·(n−1)))`, `armorAdd −= 30 + 5·(n−1)`. Лечение (0.12.2): `onCharacterDeathGlobal` — враг носителя умер в радиусе (центр − `bestFitRadius`) при активном `CROWN_REIGN`, кто бы ни убил → `HealFraction(0 + 0.05·(n−1))` |
| Его холодные объятья | замена навыка — каждая машина; навык — владелец (authority) | Свой `SkillDef` + `EntityState` `ColdEmbraceState` (машина "Weapon"), регистрация `ContentAddition.AddEntityState(Type, out)` / `AddSkillDef`; тексты навыка — оверлеи `DESTROYER_COLD_EMBRACE_SKILL_*`. Замена — `primary.SetSkillOverride(предмет, skill, Replacement)` в `onBodyInventoryChangedGlobal`/`onBodyStartGlobal` (оверрайды не сетевые, как у Heresy). Состояние: пока кнопка зажата — цель `BullseyeSearch` (8°, 80 м, LoS, по углу), заряды `(4 + 1·(n−1))·dt`, цель невалидна (смерть, дальность, `Physics.Raycast` по world) → заряды в 0 и поиск заново. Отпустил или прервали (OnExit) — `BulletAttack` в цель: `damageStat·0.5·floor(заряды)·(attackSpeed/baseAttackSpeed)`, proc 1, `damageSource = Primary`, эффект `AffixWhiteImpactEffect`. Счётчик — бафф `COLD_EMBRACE_STACKS` через `AddBuffAuthority`/`RemoveBuffAuthority`. Штраф `attackSpeedTotalMult × 0.9^n`. `CannotCopy` + `AIBlacklist` |
| Ненависть | статы — все машины | 0.14.3: `k = 2 + 1·(n−1)`; `damageTotalMult × k`, `attackSpeedTotalMult ÷ k`, перезарядка ×k через `SkillCooldowns` (0.14.4; `cooldownMultAdd` в игре не сработал). (0.14.2: ×2^n урона / ×0.5^n скорости без штрафа перезарядок; 0.13.0–0.14.1 — наоборот, плюс хук обнуления толчка) |
| Мультивитамин | статы — все машины | `TotalMult × (1 + 0.02·n)` (0.15.1, было 0.01) для health/shield/regen/moveSpeed/jumpPower/damage/attackSpeed/crit/critDamage/armor, перезарядка ÷k через `SkillCooldowns` (0.14.4; через R2API не работало) |
| Пешка | сервер 2/с; статы — все машины | По команде — максимум копий у носителей; всем `!isPlayerControlled` этой команды — стаки `PAWN_GUARD`. `healthMultAdd += 0.07·n`; при `outOfDanger` (игра сама пересчитывает статы, `OnOutOfDangerChanged`) `baseRegenAdd += 3·n`, `levelRegenAdd += 0.6·n` |
| Книга высасывания жизни | сервер 2/с; статы — все машины | 0.14.1 — именно реген (в 0.14.0 был урон-тик). Враги: дебафф `LIFE_DRAIN` (`EnemyAura`, стаки = копии сильнейшего) → `baseRegenAdd −= d`, `levelRegenAdd −= 0.2·d`, `d = 10 + 10·(n−1)`. Реген уходит в минус — ваниль сама снимает health (`ServerFixedUpdate`) и на нуле зовёт `Suicide` без убийцы; On-хук `HealthComponent.Suicide`: нет убийцы + наш дебафф → `killerOverride` = последний пивший носитель (деньги и опыт). Носитель: сумма `d·(1 + 0.2·(уровень врага − 1))` по врагам в радиусе ÷5 → скрытый бафф `LIFE_DRAIN_GAIN` в единицах 0.1 ОЗ/с → `baseRegenAdd += стаки/10` |
| Хладнокровие | переключение — владелец (authority); статы — все машины; лечение и таяние — сервер | 0.15.0, переключатель переделан в 0.15.1. Навык: свой подкласс `ComposureSkillDef : SkillDef` (`DestroyerComposureSkill`, перезарядка 0.5 с, `mustKeyPress`, бег не сбивает) БЕЗ машины состояний: `CanExecute` = только `IsReady` (заряд/задержка, машина не проверяется), `OnExecute` у владельца `AddBuffAuthority`/`RemoveBuffAuthority(COMPOSURE_ACTIVE)`, `stock −= stockToConsume`, `OnSkillActivated` — как ванильный `SkillDef.OnExecute`, только без `SetInterruptState`, поэтому основная атака не прерывается (в 0.15.0 состояние на машине `Weapon` её сбивало). `SkillCooldowns.Exempt`: On-хук `GenericSkill.CalculateFinalRechargeInterval` отдаёт базу SkillDef, `RunRecharge` не замедляется — ни Ненависть, ни ускорители, ни ванильные Alien Head и т.п. Бафф живёт на теле — новый этап начинается выключенным. Замена Shift — `SetSkillOverride(Replacement)` при смене инвентаря / старте тела (как объятья). С баффом: `armorAdd += 80·n`, `moveSpeedMultAdd += 0.4`; On-хук `HealthComponent.Heal` → 0. Таяние: `RoR2Application.onFixedUpdate`, копилка `fullHealth·0.005·n·dt`, целые единицы снимаются с `Networkhealth` напрямую (без события урона — щит работает, предметы на получение урона молчат), на нуле `Suicide`. Потерял предмет — бафф снимается |
| Белые краски / Пустые банки | сервер: `Stage.onServerStageBegin` | 0.16.0. По всем `CharacterMaster`: `n = GetItemCountPermanent` (временные и аренда не обналичиваются) → `RemoveItemPermanent(n)`, `GiveItemPermanent(WHITE_PAINT_EMPTY, n)`, `2n` раз `Run.treasureRng.NextElementUniform(availableTier1DropList)` → `GiveItemPermanent`. Уведомления `CharacterMasterNotificationQueue.SendTransformNotification` (сетевое): краски → банки, краски → каждый предмет. Банки — `ItemTier.NoTier`, `canRemove = false`. Теги `AIBlacklist`, `CannotCopy`. Могут выпасть сами краски — сработают на следующем этапе |
| Зелёные краски | статы — все машины; урон — сервер `HitEvents` | `healthMultAdd += 0.01·n`. На попадание (не DoT): отдельный `DamageInfo` `maxHealth · 0.01·n · proc`, proc 0, крит как у атаки, `OnHitEnemy` не зовём — цепочек нет |
| Красные краски | статы — все машины; разложение — сервер | `critAdd += 10` (плоско). Крит (`HitEvents`, флаг `crit`, не DoT) → компонент `DecayTracker` на жертве: `урон/с[атакующий] += damage · critMultiplier · 0.04·n`, бафф `RED_PAINT_DECAY` +1 стак (только видимость). Тик раз в 0.5 с: `DamageInfo` `DoT`/`DamageSource.DOT`, proc 0, без крита, цвет Poison — не прокает и не плодит новое разложение. Пропал атакующий — его доля тикает без автора. Таймера нет — до смерти цели |
| Батарейки | статы — где есть мастер (сервер); разряд — сервер: `HitEvents` | "механический миньон" = `BodyFlags.Mechanical` + `minionOwnership.ownerMaster` (как Spare Drone Parts). `moveSpeedMultAdd += 0.15`; смена инвентаря хозяина → `MarkAllStatsDirty` его миньонам. Разряд — `LightningOrb` Tesla, урон `body.damage` дрона × (1 + 1·(n−1)), proc 0 (без цепочек), удача хозяина |

Кукла `DOLL` (лунная, 0.4.0) — отдельно, потому что лезет в смерть:

- **Триггер:** `GlobalEventManager.onCharacterDeathGlobal` (сервер), только настоящие смерти: у жертвы есть мастер и тело,
  команды разные, у убийцы есть предмет. Убийства копятся в очередь и обрабатываются в `RoR2Application.onFixedUpdate`
  следующего кадра — без рекурсии (взрыв из куклы убил врага → снова кукла), цепочка идёт волнами.
- **Самоурон (0.6.1):** `healthComponent.fullCombinedHealth × (0.01 + 0.01·(n−1))` — от здоровья+щита, чтобы Transcendence (здоровье = 1) не обнулял; `TakeDamage` без атакующего, `BypassArmor | BypassBlock | BypassOneShotProtection`, proc 0, может убить. Добил — проки не делаем.
- **Блок лечения (0.4.1):** перед проками — ванильный `RoR2Content.Buffs.HealingDisabled` на `HealBlockDuration` (0.3 с):
  `HealthComponent.Heal` при нём возвращает 0 (IL 1.5). Без этого Monster Tooth (`Tooth`) роняет сферу лечения в позицию
  жертвы — в тебя — и она тут же гасит самоурон. Сфера, которая отскочит и будет подобрана позже 0.3 с, вылечит — это честно.
- **On-kill "в себе":** как ванильная Forgive Me Please (`Projectile.DeathProjectile.FixedUpdate`): собрать `DamageReport`
  (атакующий — ты, жертва — твой `HealthComponent`) и позвать публичный `GlobalEventManager.TriggerCharacterDeathProc` →
  приватный `OnCharacterDeath`. Перед вызовом `victimMaster` обнуляется рефлексией (readonly-поле). Не обнулилось — вызов
  пропускается: с мастером игра решила бы, что умер игрок.
- **Почему так можно** (IL `OnCharacterDeath`): блок "умерла жертва" (`OnPlayerCharacterDeath`, `Stage.ResetGameOverTimer`,
  смертельные эффекты элиток, душа артефакта Soul, плоды, ядовитые ежи и т.п.) стоит за `victimMaster != null`. Дальше —
  только эффекты атакующего: `CharacterBody.HandleOnKillEffectsServer` и предметы по его инвентарю (`ProcIgniteOnKill`,
  `ExplodeOnDeath`, `Dagger`, `Tooth`, `Infusion`, `Bandolier`, `BarrierOnKill`, `GhostOnKill`...) с позицией/радиусом жертвы,
  то есть тебя. Happiest Mask при этом может призвать призрака тебя самого. В конце — `onDeathProcServer`
  (`onCharacterDeathGlobal` подписывает туда же): слушатели видят "смерть без мастера", как от ванильной куклы
  (артефакт Sacrifice, например, без мастера жертвы ничего не роняет). Кто из слушателей считает такие "смерти" убийствами —
  считает их и с ванильной куклы.
- `ModelFrom` у куклы — снаряжение `DeathProjectile`: `ItemBase.BorrowModel` ищет сначала предмет, потом снаряжение.

## 6. Проверено по IL / сборке (RoR2 1.5.0, R2API Items 1.0.8, Language 1.1.0)

- `ItemDef.deprecatedTier` private; геттер `tier` берёт `_itemTierDef`, а если его нет — `deprecatedTier`. Сеттер `tier` ищет
  в `ItemTierCatalog` (в Awake пуст). Поэтому тир пишется рефлексией в `deprecatedTier` — как в Diabloid, проверено там в игре.
- `ItemDef.CreatePickupDef` берёт иконку из `pickupIconSprite`, модельку — из `pickupModelPrefab`/`pickupModelReference`,
  каплю — из `ItemTierDef` тира.
- `LanguageAPI.Add` молча игнорирует уже существующий ключ. Поиск токена: overlay[язык] → overlay[generic] → Add[язык] →
  Add[generic]. Поэтому все тексты мода — overlay (`GameLanguage.SetText`), их можно менять на лету.
- `CharacterBody.AddItemBehavior<T>(0)` уничтожает компонент, `>0` — создаёт/обновляет `stack`.
- `GlobalEventManager.ProcessHitEnemy`: выход, если proc = 0 или `rejected`, или не сервер, или у жертвы `HiddenRejectAllDamage`.
- Бегемот: без шанса, взрыв `1.5 + 2.5·копий` м × proc, урон `Util.OnHitProcDamage(info.damage, …, 0.6)`, эффект
  `Prefabs/Effects/OmniEffect/OmniExplosionVFXQuick` со scale = радиус.
- `CharacterMaster.GiveMoney(uint)` → `GiveMoney(uint, false)`; MMHOOK: `On.RoR2.CharacterMaster.GiveMoney_uint_bool`.
- `CharacterBody.UpdateNotMoving` работает только на сервере: смещение за кадр ≤ 0.1·dt → копится секундомер.
- Затенение в 1.5: приватное поле `transform` есть у `CharacterBody` и `PositionIndicator` — у них только `x.gameObject.transform`.
- `GlobalEventManager.TriggerCharacterDeathProc(DamageReport)` публичный, = `OnCharacterDeath`; зовут кукла FMP, `ProjectileGrantOnKillOnDestroy`,
  `DreamDemonProjectileController`. Внутри `OnCharacterDeath`: `victimTeamIndex` берётся заново из тела жертвы; без тела жертвы
  блок жертвы пропускается, но Desk Plant / Sonorous Whispers / TempestOnKill разыменуют тело без проверки — тело жертвы нужно.
- Все поля `DamageReport` — readonly. `onCharacterDeathGlobal` — это `onDeathProcServer` (add одного добавляет в другой).
- Новые теги 1.5: `CanBeTemporary`, `CanBeRental`, `AllowedForUseAsCraftingIngredient`, `CannotMagnify`, `CannotTransform`,
  `CannotDisable`, `SacrificeBlacklist`, `DeathAffixBlacklist` и др. Новые тиры: `FoodTier`, `Cursed`, `Page`, `Hex`.

## 7. Грабли (часть — из Diabloid)

- **Публицированные GameLibs врут.** Компилятор пускает к private, Mono в игре кидает `FieldAccessException`.
  Облачная сборка против настоящих DLL это ловит, КРОМЕ затенения: `CharacterBody.transform` и т.п. — всегда `x.gameObject.transform`.
- **On-хуки к приватным вложенным типам** компилируются, но в игре `MethodAccessException` в момент подписки (в Awake = весь мод мёртв).
- **Не призывать и не убивать, обходя `CharacterBody.readOnlyInstancesList`**: `MasterSummon.Perform` создаёт тело сразу,
  список меняется посреди `foreach` → `InvalidOperationException` (так упали шарики 0.5.0). Сначала снимок в свой список.
- Unity-объекты: не `?.` и не `??` — только `if (x)` / `x ? a : b`.
- BepInEx не обновляет сохранённое значение при смене дефолта. **Конфиг пользователь чистит сам** — ключи при смене дефолтов
  НЕ переименовываем и ничего под это не подстраиваем, просто меняем дефолт.
- `Id` предмета после релиза не менять (сейвы ProperSave, конфиги).
- Шрифт чата игры может не иметь «→», «−» — в игровых строках обычные знаки.
- `Util.CheckRoll` берёт проценты 0..100, не доли.
- Доп. урон из своего же обработчика попадания — флаг повторного входа и `procCoefficient = 0`, иначе бесконечная цепочка.
- `CharacterBody.GetNotMoving()` на клиенте всегда врёт (секундомер считает только сервер).
- `ChestRevealer.RevealedObject` приватный вложенный тип: ни вызвать, ни хукнуть — только повторить руками.
- Пустотный предмет: `Tier => ItemTier.VoidTier1..3/VoidBoss` + `Corrupts => "PAINT_CAN"` (наш Id) или `"Syringe"` (имя ванильного ItemDef).
  Пары отдаём в хуке `ItemCatalog.SetItemRelationships` — раньше каталог ещё пуст, позже игра уже разложила пары.
- **Перезарядку нельзя удлинить множителем (0.14.5, IL `GenericSkill.CalculateFinalRechargeInterval` 1.5):** итог = `Min(база, Max(0.5, база × cooldownScale − flat)) + temporaryCooldownPenalty` — масштаб больше 1 игра срезает до базы. Поэтому ни R2API `cooldownMultAdd`, ни `cooldownScale` Ненависть не замедляли (а отрицательный `cooldownMultAdd` R2API ещё и обнуляет). Свой помощник `Content/SkillCooldowns`: множитель < 1 — домножаем `cooldownScale` в On-хуке `CharacterBody.RecalculateStats` после ванили; > 1 — On-хук `GenericSkill.RunRecharge(dt, continuous)` делит `dt` (цифра на иконке — базовая, но тикает в k раз медленнее). Предмет регистрирует «тело → множитель». Используют Ненависть (×k), Завеса, Первоцвет, Мультивитамин (÷k). `SkillCooldowns.Exempt(SkillDef)` (0.15.1) — навык вне всех множителей, включая ванильные (переключатель Хладнокровия).
- Ослеплять СВОЕГО игрока — только `AddBuff` (без таймера): `AddTimedBuff` идёт через `ImmuneToDebuffBehavior`, и Ben's Raincoat
  тратил бы заряды каждый тик.

## 8. Сборка и проверка

- **Готовый DLL от Claude (с 0.12.1):** после каждого изменения Claude сам собирает `Destroyer.dll` в облаке (Release, pdb внутри — номера строк в логах BepInEx) и кладёт его прямо в профиль: `...\r2modmanPlus-local\RiskOfRain2\profiles\DestroyerMod\BepInEx\plugins\Destroyer\Destroyer.dll`. `build.cmd` запускать не обязательно; если запустишь — он соберёт то же самое из исходников и перезапишет файл (копирование в профиль — цель `CopyToProfile` в csproj). **Грабли (0.14.1):** игра запускается с профиля **DestroyerMod**; профиль `Destroyer` в r2modman — пустышка (там только папка, которую создало копирование), и до 0.14.1 и build.cmd, и Claude клали DLL туда — в игре ничего не менялось. Путь исправлен в `Directory.Build.props.user`.

- Дома: `build.cmd` (сборка, самотест, иконки → `logs\`). DLL сама копируется в профиль, если есть `Directory.Build.props.user`.
- В облаке (без NuGet): `logs\refs.tar.gz` из Diabloid (DLL игры, BepInEx, R2API, MMHOOK, Risk of Options) → плоская папка →
  `csc -noconfig -nostdlib -target:library -langversion:latest -nowarn:CS0618 -r:<все dll> $(find src/Destroyer -name '*.cs')`.
  Плюс второй прогон с шаблоном, переименованным в `.cs` и вписанным в `ItemList`.
- Workbench: `dotnet run --project tools/Workbench -c Release -- --test` и `-- --icons <папка>`.
