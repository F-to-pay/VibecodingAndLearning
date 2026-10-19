#pragma once

#include <glm/glm.hpp>

#include <string>
#include <unordered_map>

namespace kostyl {

// GLSL-программа из вершинного и фрагментного шейдера.
//
// Фишки:
//  * #include "file.glsl" — подставляет файл (путь относительно включающего файла);
//  * reload() — горячая перезагрузка: если новый код не собрался, остаётся старая программа;
//  * uniform-блоки с именами из registerBlockBinding() привязываются к слотам автоматически.
//
// Сеттеры uniform'ов работают с ТЕКУЩЕЙ привязанной программой — сначала bind().
class Shader {
public:
    // Пути — как для assets::path(): "shaders/lit.vert". Бросает std::runtime_error, если не собралось.
    Shader(std::string vertexPath, std::string fragmentPath);
    ~Shader();

    Shader(const Shader&) = delete;
    Shader& operator=(const Shader&) = delete;

    void bind() const;
    unsigned id() const { return m_program; }

    // Перечитать файлы и пересобрать. false — ошибка (лог в консоли), старая программа жива.
    bool reload();

    void setInt(const char* name, int v);
    void setBool(const char* name, bool v) { setInt(name, v ? 1 : 0); }
    void setFloat(const char* name, float v);
    void setVec2(const char* name, const glm::vec2& v);
    void setVec3(const char* name, const glm::vec3& v);
    void setVec4(const char* name, const glm::vec4& v);
    void setMat3(const char* name, const glm::mat3& m);
    void setMat4(const char* name, const glm::mat4& m);

    // Глобальная таблица "имя uniform-блока -> binding point". Renderer регистрирует "FrameData" -> 0.
    static void registerBlockBinding(const std::string& blockName, unsigned binding);

private:
    int location(const char* name);
    unsigned build();  // 0 при ошибке

    std::string m_vertexPath;
    std::string m_fragmentPath;
    unsigned m_program = 0;
    std::unordered_map<std::string, int> m_locations;
};

} // namespace kostyl
