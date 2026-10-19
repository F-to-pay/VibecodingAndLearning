#include "kostyl/Shader.h"
#include "kostyl/Assets.h"
#include "kostyl/Log.h"

#include <glad/glad.h>
#include <glm/gtc/type_ptr.hpp>

#include <algorithm>
#include <filesystem>
#include <sstream>
#include <stdexcept>
#include <vector>

namespace fs = std::filesystem;

namespace kostyl {

namespace {

std::unordered_map<std::string, unsigned>& blockRegistry()
{
    static std::unordered_map<std::string, unsigned> registry;
    return registry;
}

// Разворачивает #include "..." рекурсивно. files — список всех файлов (индекс = номер в #line),
// повторное включение одного файла пропускается (как будто у всех #pragma once).
std::string preprocess(const std::string& path, std::vector<std::string>& files, int depth)
{
    if (depth > 16)
        throw std::runtime_error("Слишком глубокая вложенность #include (рекурсия?) в " + path);

    const int fileIndex = static_cast<int>(files.size());
    files.push_back(path);

    const std::string source = assets::readText(path);
    const fs::path dir = fs::u8path(path).parent_path();

    std::istringstream in(source);
    std::ostringstream out;
    std::string line;
    int lineNo = 0;

    while (std::getline(in, line)) {
        ++lineNo;
        if (!line.empty() && line.back() == '\r')
            line.pop_back();  // файлы с CRLF из Windows

        const auto start = line.find_first_not_of(" \t");
        if (start == std::string::npos || line.compare(start, 8, "#include") != 0) {
            out << line << '\n';
            continue;
        }

        const auto q1 = line.find('"', start + 8);
        const auto q2 = (q1 == std::string::npos) ? std::string::npos : line.find('"', q1 + 1);
        if (q2 == std::string::npos)
            throw std::runtime_error(path + ":" + std::to_string(lineNo) + ": кривой #include, нужны кавычки");

        const std::string incPath = (dir / fs::u8path(line.substr(q1 + 1, q2 - q1 - 1))).generic_u8string();

        if (std::find(files.begin(), files.end(), incPath) == files.end()) {
            out << "#line 1 " << files.size() << '\n';
            out << preprocess(incPath, files, depth + 1);
            out << "#line " << (lineNo + 1) << ' ' << fileIndex << '\n';
        } else {
            out << '\n';
        }
    }
    return out.str();
}

std::string fileTable(const std::vector<std::string>& files)
{
    std::string s;
    for (size_t i = 0; i < files.size(); ++i)
        s += "    " + std::to_string(i) + " = " + files[i] + "\n";
    return s;
}

GLuint compileStage(GLenum stage, const std::string& path)
{
    std::vector<std::string> files;
    std::string source;
    try {
        source = preprocess(path, files, 0);
    } catch (const std::exception& e) {
        KLOG_ERROR("%s", e.what());
        return 0;
    }

    const GLuint shader = glCreateShader(stage);
    const char* src = source.c_str();
    glShaderSource(shader, 1, &src, nullptr);
    glCompileShader(shader);

    GLint ok = GL_FALSE;
    glGetShaderiv(shader, GL_COMPILE_STATUS, &ok);
    if (!ok) {
        GLint len = 0;
        glGetShaderiv(shader, GL_INFO_LOG_LENGTH, &len);
        std::string log(static_cast<size_t>(std::max(len, 1)), '\0');
        glGetShaderInfoLog(shader, len, nullptr, log.data());
        KLOG_ERROR("Шейдер %s не скомпилился:\n%s\n  номера файлов в ошибках:\n%s",
                   path.c_str(), log.c_str(), fileTable(files).c_str());
        glDeleteShader(shader);
        return 0;
    }
    return shader;
}

} // namespace

void Shader::registerBlockBinding(const std::string& blockName, unsigned binding)
{
    blockRegistry()[blockName] = binding;
}

Shader::Shader(std::string vertexPath, std::string fragmentPath)
    : m_vertexPath(assets::path(vertexPath))
    , m_fragmentPath(assets::path(fragmentPath))
{
    m_program = build();
    if (!m_program)
        throw std::runtime_error("Шейдер не собрался: " + m_vertexPath + " + " + m_fragmentPath);
}

Shader::~Shader()
{
    if (m_program)
        glDeleteProgram(m_program);
}

unsigned Shader::build()
{
    const GLuint vert = compileStage(GL_VERTEX_SHADER, m_vertexPath);
    const GLuint frag = compileStage(GL_FRAGMENT_SHADER, m_fragmentPath);
    if (!vert || !frag) {
        if (vert) glDeleteShader(vert);
        if (frag) glDeleteShader(frag);
        return 0;
    }

    const GLuint program = glCreateProgram();
    glAttachShader(program, vert);
    glAttachShader(program, frag);
    glLinkProgram(program);
    glDetachShader(program, vert);
    glDetachShader(program, frag);
    glDeleteShader(vert);
    glDeleteShader(frag);

    GLint ok = GL_FALSE;
    glGetProgramiv(program, GL_LINK_STATUS, &ok);
    if (!ok) {
        GLint len = 0;
        glGetProgramiv(program, GL_INFO_LOG_LENGTH, &len);
        std::string log(static_cast<size_t>(std::max(len, 1)), '\0');
        glGetProgramInfoLog(program, len, nullptr, log.data());
        KLOG_ERROR("Линковка %s + %s провалилась:\n%s", m_vertexPath.c_str(), m_fragmentPath.c_str(), log.c_str());
        glDeleteProgram(program);
        return 0;
    }

    // Автопривязка uniform-блоков к слотам из реестра
    GLint blockCount = 0;
    glGetProgramiv(program, GL_ACTIVE_UNIFORM_BLOCKS, &blockCount);
    for (GLint i = 0; i < blockCount; ++i) {
        char name[128] = {};
        glGetActiveUniformBlockName(program, static_cast<GLuint>(i), sizeof(name), nullptr, name);
        const auto it = blockRegistry().find(name);
        if (it != blockRegistry().end())
            glUniformBlockBinding(program, static_cast<GLuint>(i), it->second);
        else
            KLOG_WARN("Uniform-блок '%s' в %s не зарегистрирован — останется на слоте 0", name, m_fragmentPath.c_str());
    }

    return program;
}

bool Shader::reload()
{
    const GLuint fresh = build();
    if (!fresh) {
        KLOG_WARN("Перезагрузка %s не удалась, оставляю старую версию", m_fragmentPath.c_str());
        return false;
    }
    glDeleteProgram(m_program);
    m_program = fresh;
    m_locations.clear();  // location'ы в новой программе другие
    return true;
}

void Shader::bind() const
{
    glUseProgram(m_program);
}

int Shader::location(const char* name)
{
    const auto it = m_locations.find(name);
    if (it != m_locations.end())
        return it->second;
    const int loc = glGetUniformLocation(m_program, name);  // -1, если uniform нет или его выкинул компилятор
    m_locations.emplace(name, loc);
    return loc;
}

void Shader::setInt(const char* name, int v)                { glUniform1i(location(name), v); }
void Shader::setFloat(const char* name, float v)            { glUniform1f(location(name), v); }
void Shader::setVec2(const char* name, const glm::vec2& v)  { glUniform2fv(location(name), 1, glm::value_ptr(v)); }
void Shader::setVec3(const char* name, const glm::vec3& v)  { glUniform3fv(location(name), 1, glm::value_ptr(v)); }
void Shader::setVec4(const char* name, const glm::vec4& v)  { glUniform4fv(location(name), 1, glm::value_ptr(v)); }
void Shader::setMat3(const char* name, const glm::mat3& m)  { glUniformMatrix3fv(location(name), 1, GL_FALSE, glm::value_ptr(m)); }
void Shader::setMat4(const char* name, const glm::mat4& m)  { glUniformMatrix4fv(location(name), 1, GL_FALSE, glm::value_ptr(m)); }

} // namespace kostyl
