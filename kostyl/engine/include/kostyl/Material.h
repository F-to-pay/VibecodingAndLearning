#pragma once

#include <glm/glm.hpp>

#include <memory>

namespace kostyl {

class Shader;
class Texture;

// Параметры поверхности для стандартного шейдера (Blinn-Phong).
// Все цвета — ЛИНЕЙНЫЕ (не sRGB). Тонмаппинг и гамма делаются в шейдере.
struct Material {
    std::shared_ptr<Shader> shader;      // nullptr — стандартный lit-шейдер рендерера
    std::shared_ptr<Texture> albedoMap;  // nullptr — белая текстура 1x1

    glm::vec3 albedo{1.0f};    // умножается на albedoMap
    glm::vec3 emissive{0.0f};  // собственное свечение (не зависит от света)
    float specular = 0.5f;     // сила блика 0..1
    float shininess = 32.0f;   // резкость блика: 8 — пластик, 256 — полированный металл
    glm::vec2 uvScale{1.0f};   // тайлинг текстуры
    bool unlit = false;        // true — без освещения: albedo * текстура + emissive
};

} // namespace kostyl
