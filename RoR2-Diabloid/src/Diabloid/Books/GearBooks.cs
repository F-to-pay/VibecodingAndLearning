using System;
using System.Collections.Generic;
using Diabloid.Config;
using Diabloid.Content;
using Diabloid.Gameplay;
using Diabloid.Loot;
using R2API;
using RoR2;
using RoR2.Navigation;
using UnityEngine;
using UnityEngine.Networking;

namespace Diabloid.Books
{
    /// <summary>
    /// Книги — активные перки. Это обычное ванильное снаряжение (слот снаряжения, клавиша Q, кулдаун игры),
    /// только эффекты свои. Книги лежат в общем пуле снаряжения (бочки, магазины), иногда падают вместо
    /// шмотки с мобов, а с артефактом снаряжения оранжевые источники дают книги вместо шмоток.
    /// </summary>
    /// <remarks>Порядок в enum — порядок регистрации; новые книги — в конец.</remarks>
    internal enum BookType
    {
        Summoning,   // случайный союзный босс: урон 300% от урона героя, здоровье 500% от здоровья героя, размер ×0.5, 45 с
        // --- 0.11.0 (эффекты — BookEffects.cs) ---
        Meteor,          // метеор в точку прицела через 1 с: 380% урона в 5 м, отталкивает, proc 1.0, КД 6 с
        Stealth,         // невидимость и +30% скорости на 4 с, КД 25 с
        Shapeshifter,    // захват формы цели (не босса телепорта) → превращение на 30 с (или обратно вручную), КД 10 с
        KnifeStorm,      // 50 ножей за 5 с по цели, 20% урона каждый, proc 0.1, КД 80 с
        PhilosopherStone,// 25 золота со скейлом сложности, КД 30 с
        Adventurer,      // метка в 40–200 м, дошёл — предмет как из малого сундука, КД 60 с
        // --- 0.15.0 ---
        // Свитки: на Q ничего, пока в слоте — 3 своих перка. (0.15.0 был один свиток со случайными — это его место.)
        ScrollRuin,      // взрыв, добивка, заражение
        // --- 0.15.2 ---
        ScrollRetinue,   // мини-голем, огоньки, Солус
        ScrollWard,      // регенерация, броня от здоровья, отражение (0.17.1: броня от урона убрана)
        ScrollFortune,   // удача, души, опыт
    }

    internal static class GearBooks
    {
        public const float SummonCooldown = 90f;
        public const float SummonLifetime = 45f;
        public const float SummonDamageShare = 3f;   // ×урон героя (итоговый, со всеми предметами)
        public const float SummonHealthShare = 5f;   // ×макс. здоровье героя
        public const float SummonScale = 0.5f;

        /// <summary>Из кого выбирается босс. Черви не берутся: они живут под землёй и союзником бесполезны.</summary>
        private static readonly string[] BossMasters =
        {
            "BeetleQueenMaster", "TitanMaster", "VagrantMaster", "ImpBossMaster", "GravekeeperMaster",
            "ClayBossMaster", "RoboBallBossMaster", "MegaConstructMaster",
        };

        private sealed class Book
        {
            public BookType Type;
            public Words Name;
            public Words Description;
            public float Cooldown;
            public EquipmentDef Def;
            /// <summary>Перки, которые даёт снаряжение, пока лежит в слоте (книга — 1, свиток — 3). Броски — при подборе.</summary>
            public PerkType[] Perks = new PerkType[0];
            public bool IsScroll;
        }

        private static readonly Book[] books =
        {
            new Book
            {
                Type = BookType.Summoning,
                Name = new Words("Tome of Summoning", "Книга призыва"),
                Description = new Words(
                    $"Summon a random allied boss for {SummonLifetime}s: half size, {SummonDamageShare * 100}% of your damage, {SummonHealthShare * 100}% of your max health.",
                    $"Призывает случайного союзного босса на {SummonLifetime} с: вдвое меньше, {SummonDamageShare * 100}% твоего урона, {SummonHealthShare * 100}% твоего здоровья."),
                Cooldown = SummonCooldown,
                Perks = new[] { PerkType.GolemSummon },
            },
            new Book
            {
                Type = BookType.Meteor,
                Name = new Words("Tome of the Meteor", "Книга метеора"),
                Description = new Words(
                    $"A meteor falls where you aim after {BookEffects.MeteorDelay}s: {BookEffects.MeteorDamage * 100}% damage within {BookEffects.MeteorRadius}m and knockback.",
                    $"Через {BookEffects.MeteorDelay} с в точку прицела падает метеор: {BookEffects.MeteorDamage * 100}% урона в {BookEffects.MeteorRadius} м и отталкивание."),
                Cooldown = BookEffects.MeteorCooldown,
                Perks = new[] { PerkType.ExplodeOnHit },
            },
            new Book
            {
                Type = BookType.Stealth,
                Name = new Words("Tome of Stealth", "Книга скрытности"),
                Description = new Words(
                    $"Become invisible and gain +{BookEffects.StealthSpeed * 100}% movement speed for {BookEffects.StealthDuration}s.",
                    $"Невидимость и +{BookEffects.StealthSpeed * 100}% к скорости на {BookEffects.StealthDuration} с."),
                Cooldown = BookEffects.StealthCooldown,
                Perks = new[] { PerkType.SprintMomentum },
            },
            new Book
            {
                Type = BookType.Shapeshifter,
                Name = new Words("Tome of the Shapeshifter", "Книга перевёртыша"),
                Description = new Words(
                    $"First use: erase the aimed enemy (not a teleporter boss) and store its form in this book. Then: turn into that form for {BookEffects.ShapeDuration}s with +{BookEffects.ShapeMoveSpeed * 100}% speed and +{BookEffects.ShapeDamage * 100}% damage (use again to return early).",
                    $"Первое применение: стирает цель под прицелом (не босса телепорта) и запоминает её форму в этой книге. Дальше: превращает в эту форму на {BookEffects.ShapeDuration} с, +{BookEffects.ShapeMoveSpeed * 100}% скорости и +{BookEffects.ShapeDamage * 100}% урона (повторно — вернуться раньше)."),
                Cooldown = BookEffects.ShapeCooldown,
                Perks = new[] { PerkType.KillDamage },
            },
            new Book
            {
                Type = BookType.KnifeStorm,
                Name = new Words("Tome of Blades", "Книга шквала ножей"),
                Description = new Words(
                    $"{BookEffects.KnivesCount} knives hit the aimed enemy over {BookEffects.KnivesDuration}s, {BookEffects.KnivesDamage * 100}% damage each.",
                    $"{BookEffects.KnivesCount} ножей за {BookEffects.KnivesDuration} с по цели под прицелом, {BookEffects.KnivesDamage * 100}% урона каждый."),
                Cooldown = BookEffects.KnivesCooldown,
                Perks = new[] { PerkType.ExtraHit },
            },
            new Book
            {
                Type = BookType.PhilosopherStone,
                Name = new Words("Tome of the Philosopher's Stone", "Книга философского камня"),
                Description = new Words(
                    $"Gain {BookEffects.StoneGold} gold (scales over time like chest prices).",
                    $"Даёт {BookEffects.StoneGold} золота (растёт со временем, как цены сундуков)."),
                Cooldown = BookEffects.StoneCooldown,
                Perks = new[] { PerkType.LootLuck },
            },
            new Book
            {
                Type = BookType.Adventurer,
                Name = new Words("Tome of the Adventurer", "Книга авантюриста"),
                Description = new Words(
                    $"Marks a random spot on the map. Reach it to get an item (like a small chest).",
                    $"Отмечает случайную точку на карте. Дойди до неё — получишь предмет (как из малого сундука)."),
                Cooldown = BookEffects.AdventureCooldown,
                Perks = new[] { PerkType.XpBonus },
            },
            new Book
            {
                Type = BookType.ScrollRuin,
                Name = new Words("Scroll of Ruin", "Свиток разрушения"),
                IsScroll = true,
                Perks = new[] { PerkType.ExplodeOnHit, PerkType.ExtraHit, PerkType.Infection },
            },
            new Book
            {
                Type = BookType.ScrollRetinue,
                Name = new Words("Scroll of the Retinue", "Свиток свиты"),
                IsScroll = true,
                Perks = new[] { PerkType.GolemSummon, PerkType.WispOnKill, PerkType.StandStillSolus },
            },
            new Book
            {
                Type = BookType.ScrollWard,
                Name = new Words("Scroll of Warding", "Свиток защиты"),
                IsScroll = true,
                Perks = new[] { PerkType.MissingHpRegen, PerkType.HealthArmor, PerkType.ReflectDamage },
            },
            new Book
            {
                Type = BookType.ScrollFortune,
                Name = new Words("Scroll of Fortune", "Свиток фортуны"),
                IsScroll = true,
                Perks = new[] { PerkType.LootLuck, PerkType.KillDamage, PerkType.XpBonus },
            },
        };

        /// <summary>Снаряжение книги данного типа (None, если ещё не зарегистрирована).</summary>
        public static EquipmentIndex EquipmentOf(BookType type)
        {
            foreach (var book in books)
                if (book.Type == type && book.Def) return book.Def.equipmentIndex;
            return EquipmentIndex.None;
        }

        /// <summary>Книга (или свиток) по снаряжению: тип и её постоянный перк.</summary>
        public static bool TryGetBook(EquipmentIndex index, out BookType type, out PerkType[] perks)
        {
            type = default;
            perks = null;
            if (index == EquipmentIndex.None) return false;
            foreach (var book in books)
            {
                if (!book.Def || book.Def.equipmentIndex != index) continue;
                type = book.Type;
                perks = book.Perks;
                return true;
            }
            return false;
        }

        /// <summary>Метка призванного книгой босса: по ней все машины считают статы и уменьшают модель.</summary>
        public static BuffDef SummonedBossBuff { get; private set; }

        private static readonly Dictionary<EquipmentDef, Book> byDef = new Dictionary<EquipmentDef, Book>();
        private static readonly List<PickupIndex> pickups = new List<PickupIndex>();

        // ---------------------------------------------------------------- регистрация (Awake)

        public static void Register()
        {
            foreach (var book in books)
            {
                var def = ScriptableObject.CreateInstance<EquipmentDef>();
                string id = "DiabloidBook" + book.Type;
                def.name = id;
                def.nameToken = $"DIABLOID_BOOK_{book.Type}_NAME".ToUpperInvariant();
                def.pickupToken = $"DIABLOID_BOOK_{book.Type}_PICKUP".ToUpperInvariant();
                def.descriptionToken = $"DIABLOID_BOOK_{book.Type}_DESC".ToUpperInvariant();
                def.loreToken = "";
                def.cooldown = book.Cooldown;
                def.canDrop = true;                 // в ванильный пул снаряжения
                def.appearsInSinglePlayer = true;
                def.appearsInMultiPlayer = true;
                def.canBeRandomlyTriggered = false; // Бутылочный хаос не должен сыпать боссами
                def.enigmaCompatible = false;
                def.isLunar = false;
                def.isBoss = false;
                def.colorIndex = ColorCatalog.ColorIndex.Equipment;

                // Перки снаряжения — в описание (0.15.0). Свиток: на Q ничего, описание — только перки.
                string perksEn = string.Join(", ", Array.ConvertAll(book.Perks, p => $"\"{Perks.Title(p).En}\""));
                string perksRu = string.Join(", ", Array.ConvertAll(book.Perks, p => $"«{Perks.Title(p).Ru}»"));
                var description = book.IsScroll
                    ? new Words($"Does nothing when used. While equipped: perks {perksEn}.",
                                $"На Q ничего не делает. Пока в слоте: перки {perksRu}.")
                    : book.Perks.Length > 0
                        ? new Words($"{book.Description.En} While equipped: perk {perksEn}.",
                                    $"{book.Description.Ru} Пока в слоте: перк {perksRu}.")
                        : book.Description;

                GameLanguage.AddToken(def.nameToken, book.Name);
                GameLanguage.AddToken(def.pickupToken, description);
                GameLanguage.AddToken(def.descriptionToken, description);

                book.Def = def;
                byDef[def] = book;
                ContentAddition.AddEquipmentDef(def);
            }

            SummonedBossBuff = ScriptableObject.CreateInstance<BuffDef>();
            SummonedBossBuff.name = "bdDiabloidBookBoss";
            SummonedBossBuff.buffColor = new Color32(255, 170, 60, 255);
            SummonedBossBuff.canStack = false;
            SummonedBossBuff.isDebuff = false;
            ContentAddition.AddBuffDef(SummonedBossBuff);

            BookEffects.Register();
            On.RoR2.EquipmentSlot.PerformEquipmentAction += OnPerformEquipmentAction;
            RecalculateStatsAPI.GetStatCoefficients += OnGetStatCoefficients;
            CharacterBody.onBuffFirstStackGained += OnBuffFirstStackGained;
        }

        /// <summary>После загрузки: модель книги одалживаем у Фолианта Гора (тоже книга), собираем список пикапов.</summary>
        public static void OnCatalogsReady()
        {
            var tomeIndex = ItemCatalog.FindItemIndex("BonusGoldPackOnKill");
            var tomeDef = tomeIndex != ItemIndex.None ? ItemCatalog.GetItemDef(tomeIndex) : null;
            var tomePickup = tomeIndex != ItemIndex.None ? PickupCatalog.GetPickupDef(PickupCatalog.FindPickupIndex(tomeIndex)) : null;

            // Свиток — моделька Военных облигаций (War Bonds, Seekers of the Storm); не нашлась — как книга.
            var bondsIndex = ItemCatalog.FindItemIndex("BarrageOnBoss");
            var bondsDef = bondsIndex != ItemIndex.None ? ItemCatalog.GetItemDef(bondsIndex) : null;
            var bondsPickup = bondsDef ? PickupCatalog.GetPickupDef(PickupCatalog.FindPickupIndex(bondsIndex)) : null;

            pickups.Clear();
            foreach (var book in books)
            {
                if (!book.Def || book.Def.equipmentIndex == EquipmentIndex.None) continue;
                var pickupIndex = PickupCatalog.FindPickupIndex(book.Def.equipmentIndex);
                if (!pickupIndex.isValid) continue;
                pickups.Add(pickupIndex);

                bool scrollLook = book.IsScroll && bondsDef && bondsPickup != null;
                var sourceDef = scrollLook ? bondsDef : tomeDef;
                var sourcePickup = scrollLook ? bondsPickup : tomePickup;
                if (!sourceDef) continue;
                // В 1.5 иконка у ванильных предметов часто лежит только ссылкой (pickupIconSprite = null до загрузки),
                // а значок снаряжения в HUD читает сам спрайт — без загрузки он белый квадрат (0.9.1). Грузим сразу.
                var icon = LoadIcon(sourceDef);
                book.Def.pickupModelPrefab = sourceDef.pickupModelPrefab;
                book.Def.pickupModelReference = sourceDef.pickupModelReference;
                book.Def.pickupIconSprite = icon;
                book.Def.pickupIconSpriteReference = sourceDef.pickupIconSpriteReference;
                var pickupDef = PickupCatalog.GetPickupDef(pickupIndex);
                if (pickupDef != null && sourcePickup != null)
                {
                    pickupDef.displayPrefab = sourcePickup.displayPrefab;
                    pickupDef.displayPrefabReference = sourcePickup.displayPrefabReference;
                    pickupDef.iconSprite = icon ? icon : sourcePickup.iconSprite;
                    pickupDef.iconSpriteReference = sourcePickup.iconSpriteReference;
                    pickupDef.iconTexture = icon ? icon.texture : sourcePickup.iconTexture;
                }
            }

            if (RoR2Content.Buffs.ArmorBoost) SummonedBossBuff.iconSprite = RoR2Content.Buffs.ArmorBoost.iconSprite;
            BookEffects.OnCatalogsReady();
            DiabloidPlugin.Log.LogInfo($"Registered {pickups.Count} book(s).");
        }

        private static Sprite LoadIcon(ItemDef source) => LoadSprite(source.pickupIconSprite, source.pickupIconSpriteReference);

        private static Sprite LoadSprite(Sprite sprite, UnityEngine.AddressableAssets.AssetReference reference)
        {
            if (sprite) return sprite;
            if (reference == null || !reference.RuntimeKeyIsValid()) return null;
            try
            {
                return UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<Sprite>(reference.RuntimeKey).WaitForCompletion();
            }
            catch (Exception e)
            {
                DiabloidPlugin.Log.LogWarning($"Books: could not load the book icon: {e.Message}");
                return null;
            }
        }

        public static bool IsBook(EquipmentIndex index)
        {
            if (index == EquipmentIndex.None) return false;
            foreach (var book in books)
                if (book.Def && book.Def.equipmentIndex == index) return true;
            return false;
        }

        /// <summary>Все зарегистрированные книги: название + пикап (для отладочного меню).</summary>
        public static System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<Words, PickupIndex>> DebugList()
        {
            foreach (var book in books)
            {
                if (!book.Def || book.Def.equipmentIndex == EquipmentIndex.None) continue;
                var pickupIndex = PickupCatalog.FindPickupIndex(book.Def.equipmentIndex);
                if (pickupIndex.isValid) yield return new System.Collections.Generic.KeyValuePair<Words, PickupIndex>(book.Name, pickupIndex);
            }
        }

        /// <summary>Случайная книга как пикап (для дропа с мобов и артефакта снаряжения).</summary>
        public static PickupIndex RandomBookPickup(System.Random rng) =>
            pickups.Count == 0 ? PickupIndex.none : pickups[rng.Next(pickups.Count)];

        // ---------------------------------------------------------------- активация (сервер)

        private static bool OnPerformEquipmentAction(On.RoR2.EquipmentSlot.orig_PerformEquipmentAction orig, EquipmentSlot self, EquipmentDef equipmentDef)
        {
            if (equipmentDef && byDef.TryGetValue(equipmentDef, out var book))
            {
                var body = self ? self.characterBody : null;
                if (!body) return false;
                switch (book.Type)
                {
                    case BookType.Summoning: return SummonBoss(body);
                    case BookType.Meteor: return BookEffects.Meteor(body);
                    case BookType.Stealth: return BookEffects.Stealth(body);
                    case BookType.Shapeshifter: return BookEffects.Shapeshifter(body);
                    case BookType.KnifeStorm: return BookEffects.Knives(body);
                    case BookType.PhilosopherStone: return BookEffects.Stone(body);
                    case BookType.Adventurer: return BookEffects.Adventurer(body);
                    case BookType.ScrollRuin:
                    case BookType.ScrollRetinue:
                    case BookType.ScrollWard:
                    case BookType.ScrollFortune: return false; // свиток — только перки
                }
                return false;
            }
            return orig(self, equipmentDef);
        }

        private static bool SummonBoss(CharacterBody owner)
        {
            if (!NetworkServer.active) return false;

            var candidates = new List<GameObject>();
            foreach (var name in BossMasters)
            {
                var prefab = MasterCatalog.FindMasterPrefab(name);
                if (prefab) candidates.Add(prefab);
            }
            if (candidates.Count == 0)
            {
                DiabloidPlugin.Log.LogWarning("Book of Summoning: no boss masters found.");
                return false;
            }

            var prefabToSpawn = candidates[LootRoller.Rng.Next(candidates.Count)];
            // НЕ owner.transform: у CharacterBody приватное поле transform, публицированные библиотеки (сборка на ПК)
            // подставляют его вместо Component.transform → FieldAccessException, книга не срабатывала (0.9.0).
            var forward = owner.inputBank ? owner.inputBank.aimDirection : owner.gameObject.transform.forward;
            forward.y = 0f;
            var position = GroundNear(owner.footPosition + forward.normalized * 6f);

            var master = new MasterSummon
            {
                masterPrefab = prefabToSpawn,
                position = position,
                rotation = Quaternion.identity,
                summonerBodyObject = owner.gameObject,
                teamIndexOverride = owner.teamComponent ? owner.teamComponent.teamIndex : TeamIndex.Player,
                ignoreTeamMemberLimit = true,
            }.Perform();
            if (!master) return false;

            master.gameObject.AddComponent<MasterSuicideOnTimer>().lifeTimer = SummonLifetime;

            var body = master.GetBody();
            if (body) MarkSummonedBoss(body);
            else master.onBodyStart += MarkSummonedBoss;
            return true;
        }

        private static void MarkSummonedBoss(CharacterBody body)
        {
            if (!body) return;
            if (body.master) body.master.onBodyStart -= MarkSummonedBoss;
            if (!body.HasBuff(SummonedBossBuff)) body.AddBuff(SummonedBossBuff);
            // Здоровье станет ×5 героя только после пересчёта — долечиваем, когда он прошёл.
            if (!body.GetComponent<SummonedBossHeal>()) body.gameObject.AddComponent<SummonedBossHeal>();
        }

        private static Vector3 GroundNear(Vector3 position)
        {
            var nodes = SceneInfo.instance ? SceneInfo.instance.groundNodes : null;
            if (!nodes) return position + Vector3.up;
            var index = nodes.FindClosestNode(position, HullClassification.Golem);
            return nodes.GetNodePosition(index, out var nodePosition) ? nodePosition + Vector3.up : position + Vector3.up;
        }

        // ---------------------------------------------------------------- статы и размер (все машины)

        private static CharacterBody OwnerBody(CharacterBody body)
        {
            var ownership = body.master ? body.master.minionOwnership : null;
            var owner = ownership ? ownership.ownerMaster : null;
            return owner ? owner.GetBody() : null;
        }

        /// <summary>
        /// Урон = 300% итогового урона героя, здоровье = 500% его макс. здоровья. R2API умеет только добавлять,
        /// поэтому считаем добавку к множителю от базы босса на его уровне: (цель / база) − 1.
        /// </summary>
        private static void OnGetStatCoefficients(CharacterBody body, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!body || !SummonedBossBuff || !body.HasBuff(SummonedBossBuff)) return;
            var owner = OwnerBody(body);
            if (!owner) return;

            float levelUps = Mathf.Max(0f, body.level - 1f);
            float baseHealth = body.baseMaxHealth + body.levelMaxHealth * levelUps;
            float baseDamage = body.baseDamage + body.levelDamage * levelUps;
            if (baseHealth > 0f) args.healthMultAdd += owner.maxHealth * SummonHealthShare / baseHealth - 1f;
            if (baseDamage > 0f) args.damageMultAdd += owner.damage * SummonDamageShare / baseDamage - 1f;
        }

        private static void OnBuffFirstStackGained(CharacterBody body, BuffDef buff)
        {
            if (buff == SummonedBossBuff && body && !body.GetComponent<ModelScaler>())
                body.gameObject.AddComponent<ModelScaler>().Multiplier = SummonScale;
        }

        /// <summary>Все машины: страховка размера + раз в секунду пересчёт статов (герой мог подобрать предмет).</summary>
        public static void Tick()
        {
            if (!SummonedBossBuff) return;
            foreach (var body in CharacterBody.readOnlyInstancesList)
            {
                if (!body || !body.HasBuff(SummonedBossBuff)) continue;
                if (!body.GetComponent<ModelScaler>()) body.gameObject.AddComponent<ModelScaler>().Multiplier = SummonScale;
                body.MarkAllStatsDirty();
            }
        }
    }

    /// <summary>Сервер: один раз долечить призванного босса до полного, когда его здоровье уже пересчитано.</summary>
    internal sealed class SummonedBossHeal : MonoBehaviour
    {
        private float timer = 0.3f;

        private void FixedUpdate()
        {
            if (!NetworkServer.active) { Destroy(this); return; }
            timer -= Time.fixedDeltaTime;
            if (timer > 0f) return;
            var health = GetComponent<HealthComponent>();
            if (health && health.alive) health.HealFraction(1f, default(ProcChainMask));
            Destroy(this);
        }
    }
}
