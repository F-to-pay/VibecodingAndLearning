#include "kostyl/Primitives.h"

#include <glm/gtc/constants.hpp>

#include <algorithm>
#include <cmath>

namespace kostyl::primitives {

namespace {

// Квадрат с центром center, нормалью n и осями u, v (u x v = n => обход против часовой).
void addQuad(std::vector<Vertex>& verts, std::vector<std::uint32_t>& idx,
             glm::vec3 center, glm::vec3 n, glm::vec3 u, glm::vec3 v, float half)
{
    const auto base = static_cast<std::uint32_t>(verts.size());
    const glm::vec2 corners[4] = {{-1, -1}, {1, -1}, {1, 1}, {-1, 1}};
    for (const auto& c : corners) {
        verts.push_back({center + (u * c.x + v * c.y) * half, n, (c + 1.0f) * 0.5f});
    }
    idx.insert(idx.end(), {base, base + 1, base + 2, base, base + 2, base + 3});
}

} // namespace

std::shared_ptr<Mesh> cube(float size)
{
    const float h = size * 0.5f;
    std::vector<Vertex> verts;
    std::vector<std::uint32_t> idx;
    verts.reserve(24);
    idx.reserve(36);

    struct Face { glm::vec3 n, u, v; };
    const Face faces[6] = {
        {{ 1, 0, 0}, { 0, 0, -1}, {0, 1,  0}},
        {{-1, 0, 0}, { 0, 0,  1}, {0, 1,  0}},
        {{ 0, 1, 0}, { 1, 0,  0}, {0, 0, -1}},
        {{ 0,-1, 0}, { 1, 0,  0}, {0, 0,  1}},
        {{ 0, 0, 1}, { 1, 0,  0}, {0, 1,  0}},
        {{ 0, 0,-1}, {-1, 0,  0}, {0, 1,  0}},
    };
    for (const auto& f : faces)
        addQuad(verts, idx, f.n * h, f.n, f.u, f.v, h);

    return std::make_shared<Mesh>(verts, idx);
}

std::shared_ptr<Mesh> plane(float size)
{
    std::vector<Vertex> verts;
    std::vector<std::uint32_t> idx;
    addQuad(verts, idx, glm::vec3(0.0f), {0, 1, 0}, {1, 0, 0}, {0, 0, -1}, size * 0.5f);
    return std::make_shared<Mesh>(verts, idx);
}

std::shared_ptr<Mesh> sphere(float radius, int segments, int rings)
{
    segments = std::max(segments, 3);
    rings = std::max(rings, 2);

    std::vector<Vertex> verts;
    std::vector<std::uint32_t> idx;
    verts.reserve(static_cast<size_t>((segments + 1) * (rings + 1)));

    // UV-сфера: phi — от северного полюса (0) к южному (pi), theta — вокруг оси Y.
    // Шов по theta = 0/2pi дублируем, чтобы UV не перескакивал с 1 на 0.
    for (int r = 0; r <= rings; ++r) {
        const float phi = glm::pi<float>() * float(r) / float(rings);
        for (int s = 0; s <= segments; ++s) {
            const float theta = glm::two_pi<float>() * float(s) / float(segments);
            const glm::vec3 n(std::sin(phi) * std::cos(theta),
                              std::cos(phi),
                              -std::sin(phi) * std::sin(theta));
            verts.push_back({n * radius, n, {float(s) / float(segments), 1.0f - float(r) / float(rings)}});
        }
    }

    const auto row = static_cast<std::uint32_t>(segments + 1);
    for (int r = 0; r < rings; ++r) {
        for (int s = 0; s < segments; ++s) {
            const std::uint32_t a = static_cast<std::uint32_t>(r) * row + static_cast<std::uint32_t>(s);
            const std::uint32_t b = a + row;  // кольцо ниже
            const std::uint32_t c = b + 1;
            const std::uint32_t d = a + 1;
            if (r != rings - 1) idx.insert(idx.end(), {a, b, c});  // у южного полюса этот вырожден
            if (r != 0)         idx.insert(idx.end(), {a, c, d});  // у северного — этот
        }
    }

    return std::make_shared<Mesh>(verts, idx);
}

} // namespace kostyl::primitives
