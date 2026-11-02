using BepInEx.Configuration;
using Destroyer.Config;
using Destroyer.Core;

namespace Destroyer.Content
{
    /// <summary>
    /// Настройки одного предмета: секция "Item: ID" в .cfg и категория с именем предмета в Risk of Options.
    /// Предмет получает его в BindConfig и заводит свои числа:
    ///
    ///   damage = s.Float("Damage", 0.15f, 0f, 2f, new Words("Damage", "Урон"), new Words("...", "..."), "{0:P0}");
    ///
    /// Значение читается в момент использования (damage.Value), поэтому меняется прямо в забеге.
    /// Описание предмета пересобирается само, когда меняется любая настройка его секции.
    ///
    /// ВАЖНО: эффекты, которые считаются на каждой машине (статы через RecalculateStats), используют настройки
    /// КАЖДОГО игрока. Разные значения у хоста и клиента — разные цифры у них на экране. Серверные эффекты
    /// (урон по врагам, шансы, спавн) берут настройки хоста.
    /// </summary>
    internal sealed class ItemSettings
    {
        private readonly ConfigFile file;
        private readonly Words category;

        public readonly string Section;

        internal ItemSettings(ConfigFile file, string itemId, Words itemName)
        {
            this.file = file;
            Section = "Item: " + itemId;
            category = new Words("Item: " + itemName.En, "Предмет: " + itemName.Ru);
        }

        public ConfigEntry<float> Float(string key, float defaultValue, float min, float max, Words name, Words description,
                                        string format = "{0:0.##}", bool restartRequired = false) =>
            DestroyerConfig.Bind(file, Section, key, defaultValue, category, name, description, min, max, format, restartRequired);

        public ConfigEntry<int> Int(string key, int defaultValue, int min, int max, Words name, Words description,
                                    bool restartRequired = false) =>
            DestroyerConfig.Bind(file, Section, key, defaultValue, category, name, description, min, max, "{0}", restartRequired);

        public ConfigEntry<bool> Bool(string key, bool defaultValue, Words name, Words description, bool restartRequired = false) =>
            DestroyerConfig.Bind(file, Section, key, defaultValue, category, name, description, restartRequired: restartRequired);
    }
}
