#include "kostyl/Assets.h"

#include <filesystem>
#include <fstream>
#include <sstream>
#include <stdexcept>

namespace fs = std::filesystem;

// Все строки-пути в движке — UTF-8. На Windows узкие строки по умолчанию
// трактуются в ANSI-кодировке, и путь вида C:\Users\Дима\... превращается в кашу.
// Поэтому конвертируем явно через u8path / u8string.

namespace kostyl::assets {

std::string path(const std::string& relative)
{
    std::error_code ec;
    const fs::path rel = fs::u8path(relative);

    if (rel.is_absolute() || fs::exists(rel, ec))
        return relative;

    const fs::path candidates[] = {
        fs::path("assets") / rel,
        fs::u8path(KOSTYL_ASSETS_DIR) / rel,
    };
    for (const auto& c : candidates) {
        if (fs::exists(c, ec))
            return c.generic_u8string();
    }
    // Не нашли — вернём вариант из папки проекта, ошибка всплывёт при чтении с понятным путём.
    return (fs::u8path(KOSTYL_ASSETS_DIR) / rel).generic_u8string();
}

std::string readText(const std::string& filePath)
{
    std::ifstream file(fs::u8path(filePath), std::ios::binary);
    if (!file)
        throw std::runtime_error("Не могу открыть файл: " + filePath);

    std::ostringstream ss;
    ss << file.rdbuf();
    return ss.str();
}

} // namespace kostyl::assets
