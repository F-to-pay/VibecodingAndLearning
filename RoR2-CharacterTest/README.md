# RoR2-CharacterTest

Мод выживших для Risk of Rain 2 (1.5.x). BepInEx + R2API, C#. Без Unity и AssetBundle: тело, моделька,
аниматор и витрина — клоны ванильного выжившего, навыки и всё поведение — свой код.

Версия **0.6.0** — каркас + один тестовый выживший. Как всё устроено и как добавить своего — [`RAZBOR.md`](RAZBOR.md).

## Выжившие

| Выживший | Донор | Навыки |
|---|---|---|
| **Подопытный** `TEST_SUBJECT` | Коммандо | По умолчанию — **копии кнопок Коммандо**: Double Tap · Phase Round · Tactical Dive · Suppressive Fire (как в ваниле, без замков). Альтернативы в снаряжении: **Парные пистолеты-пулемёты** (47%, 16 выстр./с, proc 0.4, вид и звук как у Double Tap; обойма 40, полная перезарядка через 2 с без стрельбы; скорость атаки множит урон, а не темп) · **Подствольные гранатомёты** (2 гранаты по дуге, по 340% в 6 м, 7 с) · **Перекрёстный огонь** (R: стоишь на месте, не падаешь и неуязвим; 12 × скорость атаки выстрелов по 100% за 1 с × √скорости атаки по видимым врагам в 40 м, поровну между ними; без целей не нажимается; 14 с) |

Подопытный — проверка каркаса: 130 ОЗ (+39 за уровень), остальные статы как у Коммандо, цвет — салатовый.
Стоит в конце списка выбора. Выключается в настройках: `Survivor: TEST_SUBJECT → Enabled`.

## Настройки

Все цифры навыков — в секции `Survivor: TEST_SUBJECT`, а с **Risk of Options** (в профиле стоит) — в игре:
Settings → Mod Options → RoR2-CharacterTest. Почти всё действует сразу, описания навыков перестраиваются сами.
После перезапуска — только перезарядки (`*.Cooldown`, `SMG.ReloadDelay`) и `SMG.Magazine`.

| Навык | Настройки (по умолчанию) |
|---|---|
| ПП | `SMG.Damage` 0.47 · `SMG.Proc` 0.4 · `SMG.FireRate` 16 · `SMG.Magazine` 40 · `SMG.ReloadDelay` 2 с |
| Гранаты | `Grenade.Damage` 3.4 · `Grenade.Proc` 1 · `Grenade.Radius` 6 м · `Grenade.Shots` 2 · `Grenade.Interval` 0.15 с · `Grenade.Cooldown` 7 с · `Grenade.Speed` 70 м/с · `Grenade.UpAngle` 6° · `Grenade.Gravity` ×1 |
| Перекрёстный огонь | `Crossfire.Damage` 1 · `Crossfire.Proc` 1 · `Crossfire.Shots` 12 · `Crossfire.Duration` 1 с · `Crossfire.AttackSpeedToDuration` 0.5 · `Crossfire.Range` 40 м · `Crossfire.Cooldown` 14 с |

`Crossfire.AttackSpeedToDuration` — куда идут лишние выстрелы от скорости атаки (СА): длительность × СА^k, темп × СА^(1−k).
0 — всё в темп, 1 — всё в длительность, 0.5 — поровну (при СА ×2: 24 выстрела за 1.41 с).

## Профиль r2modman

Профиль r2modman — **CharecterTest** (именно так, с опечаткой). В нём должны стоять:

- R2API: `R2API_Core`, `R2API_Language`, `R2API_ContentManagement` (или весь пакет `R2API`).

`Directory.Build.props.user` смотрит в `...\profiles\CharecterTest\BepInEx\plugins` — после сборки DLL сама
окажется в профиле. Другой профиль — поправь путь там (образец: `Directory.Build.props.user.example`).

## Сборка

`build.cmd` → `logs\build.log` (`0 Error(s)` — хорошо). DLL: `src\CharacterTest\bin\Release\netstandard2.1\CharacterTest.dll`.

Готовая DLL, собранная Claude в облаке против настоящих DLL игры, лежит в `out\CharacterTest.dll`
(в git не попадает).

В игре искать в `BepInEx\LogOutput.log` строки `RoR2-CharacterTest` и `Survivor TEST_SUBJECT`.
