using System;
using System.Collections.Generic;

namespace Destroyer.Art
{
    /// <summary>
    /// Все иконки мода: Id предмета → функция, которая его рисует.
    ///
    /// Зачем отдельно от предметов: эта папка не знает про Unity и RoR2, поэтому верстак (tools/Workbench)
    /// рисует те же иконки в PNG без запуска игры — правишь рисунок, жмёшь build.cmd, смотришь logs\icons.
    ///
    /// Как добавить иконку: написать метод `static void Имя(IconCanvas c)` (можно в отдельном файле
    /// `partial class IconBook`, например Art/Icons/Bomb.cs) и вписать его в All под Id предмета.
    /// Предмет без иконки получает Placeholder.
    /// </summary>
    public static partial class IconBook
    {
        /// <summary>Id предмета (как ItemBase.Id, без префикса DESTROYER_) → рисунок.</summary>
        public static readonly Dictionary<string, Action<IconCanvas>> All = new Dictionary<string, Action<IconCanvas>>
        {
            { "CHORIZO", Chorizo },
            { "PAINT_CAN", PaintCan },
            { "NITRO_COTTON", NitroCotton },
            { "SMARTPHONE", Smartphone },
            { "CHAMPIGNON", Champignon },
            { "DOLL", Doll },
            { "FOX_TAIL", FoxTail },
            { "BALLOONS", Balloons },
            { "CHAIR", Chair },
            { "BATTERIES", Batteries },
            { "KETTLEBELL", Kettlebell },
            { "VACUUM", Vacuum },
            { "SMOKE_SCREEN", SmokeScreen },
            { "PAIN_CONVERTER", PainConverter },
            { "SONIC_CANNON", SonicCannon },
            { "WAFFLE_IRON", WaffleIron },
            { "ARTILLERY_FUSE", ArtilleryFuse },
            { "DEUS_EX_MACHINA", DeusExMachina },
            { "PRIMROSE", Primrose },
            { "CHAINSAW", Chainsaw },
            { "LION_HEAD", LionHead },
            { "CROWN_OF_ELITISM", CrownOfElitism },
            { "COLD_EMBRACE", ColdEmbrace },
            { "HATRED", Hatred },
            { "MULTIVITAMIN", Multivitamin },
            { "PAWN", Pawn },
            { "LIFE_DRAIN_BOOK", LifeDrainBook },
            { "COMPOSURE", Composure },
            { "WHITE_PAINT", WhitePaint },
            { "WHITE_PAINT_EMPTY", WhitePaintEmpty },
            { "GREEN_PAINT", GreenPaint },
            { "RED_PAINT", RedPaint },
        };

        /// <summary>
        /// Иконки баффов: Id баффа → БЕЛЫЙ силуэт на прозрачном фоне (игра красит его в цвет баффа).
        /// Без обводки — у ванильных баффов её нет.
        /// </summary>
        public static readonly Dictionary<string, Action<IconCanvas>> Buffs = new Dictionary<string, Action<IconCanvas>>
        {
            { "TASTY", BuffTasty },
            { "CHAMPIGNON_ARMOR", BuffChampignonArmor },
            { "FOX_TAIL_COOLDOWN", BuffFoxTailCooldown },
            { "CHAIR_SEATED", BuffChairSeated },
            { "BALLOON", BuffBalloon },
            { "SMOKE_CLEAR", BuffSmokeClear },
            { "PAIN_RESTORE", BuffPainRestore },
            { "PAIN_ARMOR", BuffPainArmor },
            { "SCORCH", BuffScorch },
            { "CHAINSAW_ACTIVE", BuffChainsawActive },
            { "CHAINSAW_COOLDOWN", BuffChainsawCooldown },
            { "LION_FEAR", BuffLionFear },
            { "CROWN_REIGN", BuffCrownReign },
            { "CROWN_OPPRESSION", BuffCrownOppression },
            { "PAWN_GUARD", BuffPawnGuard },
            { "LIFE_DRAIN", BuffLifeDrain },
            { "COLD_EMBRACE_STACKS", BuffColdEmbraceStacks },
            { "COMPOSURE_ACTIVE", BuffComposureActive },
            { "RED_PAINT_DECAY", BuffRedPaintDecay },
        };

        /// <summary>Рисунок для предмета; нет своего — заглушка.</summary>
        public static Action<IconCanvas> Get(string itemId) =>
            itemId != null && All.TryGetValue(itemId, out var paint) ? paint : Placeholder;

        /// <summary>Рисунок для баффа; нет своего — белый круг.</summary>
        public static Action<IconCanvas> GetBuff(string buffId) =>
            buffId != null && Buffs.TryGetValue(buffId, out var paint) ? paint : BuffPlaceholder;

        public static void BuffPlaceholder(IconCanvas c) => c.Fill(Shapes.Ring(0.5f, 0.5f, 0.3f, 0.12f), Rgba.White);

        /// <summary>Нарисовать иконку на новом холсте.</summary>
        public static IconCanvas Render(Action<IconCanvas> painter, int size)
        {
            var canvas = new IconCanvas(size);
            painter(canvas);
            return canvas;
        }

        // ---------- Общая палитра ----------

        public static readonly Rgba Ink = Rgba.Hex("#14110f");       // обводка, как у ванильных иконок
        public static readonly Rgba Steel = Rgba.Hex("#8c96a0");
        public static readonly Rgba DarkSteel = Rgba.Hex("#3a4048");
        public static readonly Rgba Fire = Rgba.Hex("#ff7a1a");
        public static readonly Rgba Ember = Rgba.Hex("#ffd34d");
        public static readonly Rgba Blood = Rgba.Hex("#b3202a");
        public static readonly Rgba VoidPurple = Rgba.Hex("#a64dff");
        public static readonly Rgba LunarBlue = Rgba.Hex("#4d9dff");

        /// <summary>Толщина стандартной обводки (в долях холста).</summary>
        public const float OutlineWidth = 0.028f;

        /// <summary>
        /// Заглушка — бомба с фитилём. Видишь её в игре — у предмета нет своей иконки.
        /// Заодно пример: фигуры, градиенты, блик, обводка.
        /// </summary>
        public static void Placeholder(IconCanvas c)
        {
            // Корпус: тёмный шар, светлее сверху-слева.
            var body = Shapes.Circle(0.46f, 0.58f, 0.30f);
            c.Fill(body, Paints.Lit(Rgba.Hex("#4c5664"), 0.6f));

            // Горловина под фитиль.
            var neck = Shapes.Rotate(Shapes.Box(0.46f, 0.27f, 0.08f, 0.06f, 0.015f), 0.46f, 0.27f, 35f);
            c.Fill(Shapes.Move(neck, 0.07f, 0f), Paints.Vertical(Steel, DarkSteel, 0.2f, 0.34f));

            // Фитиль и искра.
            c.Fill(Shapes.Capsule(0.57f, 0.24f, 0.70f, 0.13f, 0.018f), Rgba.Hex("#c9a46a"));
            c.Fill(Shapes.Star(0.73f, 0.11f, 0.10f, 0.035f, 8, 10f), Paints.Radial(0.73f, 0.11f, 0.10f, Rgba.White, Fire));

            // Блик на корпусе.
            c.Fill(Shapes.Ellipse(0.36f, 0.47f, 0.07f, 0.045f), Rgba.White.WithAlpha(0.35f));

            c.Outline(OutlineWidth, Ink);
        }
    }
}
