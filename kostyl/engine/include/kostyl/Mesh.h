#pragma once

#include <glm/glm.hpp>

#include <cstdint>
#include <vector>

namespace kostyl {

// Формат вершины. Атрибуты в шейдере: location 0 = position, 1 = normal, 2 = uv.
struct Vertex {
    glm::vec3 position;
    glm::vec3 normal;
    glm::vec2 uv;
};

// Геометрия на GPU: VAO + VBO + EBO. Треугольники, обход против часовой = лицевая сторона.
class Mesh {
public:
    Mesh(const std::vector<Vertex>& vertices, const std::vector<std::uint32_t>& indices);
    ~Mesh();

    Mesh(const Mesh&) = delete;
    Mesh& operator=(const Mesh&) = delete;

    void draw() const;

    std::size_t indexCount() const { return static_cast<std::size_t>(m_indexCount); }
    std::size_t triangleCount() const { return indexCount() / 3; }

private:
    unsigned m_vao = 0;
    unsigned m_vbo = 0;
    unsigned m_ebo = 0;
    int m_indexCount = 0;
};

} // namespace kostyl
