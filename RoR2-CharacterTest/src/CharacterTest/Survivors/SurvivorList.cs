using System.Collections.Generic;
using CharacterTest.Survivors.TestSubject;

namespace CharacterTest.Survivors
{
    /// <summary>Все выжившие мода. Новый выживший = класс-наследник SurvivorBase + строчка здесь.</summary>
    internal static class SurvivorList
    {
        public static IEnumerable<SurvivorBase> Create() => new SurvivorBase[]
        {
            new TestSubjectSurvivor(),
        };
    }
}
