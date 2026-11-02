using Destroyer.Content;

namespace Destroyer.Items
{
    /// <summary>
    /// Список предметов мода. Новый предмет: класс в этой папке (шаблон — ItemTemplate.cs.txt) + строчка сюда.
    ///
    /// Список явный, а не "найти всех наследников рефлексией": порядок предсказуемый, а GetTypes() на сборке
    /// с мягкой зависимостью (Risk of Options) может упасть, если зависимости нет.
    /// </summary>
    internal static class ItemList
    {
        public static ItemBase[] Create() => new ItemBase[]
        {
            new Chorizo(),
            new PaintCan(),
            new NitroCotton(),
            new Smartphone(),
            new Champignon(),
            new Doll(),
            new FoxTail(),
            new Balloons(),
            new Chair(),
            new Batteries(),
            new Kettlebell(),
            new Vacuum(),
            new SmokeScreen(),
            new PainConverter(),
            new SonicCannon(),
            new WaffleIron(),
            new ArtilleryFuse(),
            new DeusExMachina(),
            new Primrose(),
            new Chainsaw(),
            new LionHead(),
            new CrownOfElitism(),
            new ColdEmbrace(),
            new Hatred(),
            new Multivitamin(),
            new Pawn(),
            new LifeDrainBook(),
            new Composure(),
            new WhitePaint(),
            new WhitePaintEmpty(),
            new GreenPaint(),
            new RedPaint(),
        };
    }
}
