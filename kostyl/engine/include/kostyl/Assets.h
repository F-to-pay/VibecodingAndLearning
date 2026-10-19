#pragma once

#include <string>

namespace kostyl::assets {

// Превращает относительный путь ассета ("shaders/lit.vert") в реальный.
// Ищет по очереди: ./assets/<rel>, затем <папка проекта>/assets/<rel> (зашита при сборке).
// Абсолютные пути и пути, которые уже существуют, возвращаются как есть.
std::string path(const std::string& relative);

// Читает текстовый файл целиком. Бросает std::runtime_error, если не вышло.
std::string readText(const std::string& filePath);

} // namespace kostyl::assets
