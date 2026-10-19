#pragma once

#include <glm/glm.hpp>

namespace kostyl {

// Должно совпадать с MAX_POINT_LIGHTS в assets/shaders/common.glsl
constexpr int kMaxPointLights = 8;

// "Солнце": бесконечно далёкий источник, у всех лучей одно направление
struct DirectionalLight {
    glm::vec3 direction = glm::normalize(glm::vec3(-0.4f, -1.0f, -0.3f));  // куда светит
    glm::vec3 color{1.0f};
    float intensity = 1.0f;
    bool enabled = true;
};

// Лампочка: светит во все стороны, гаснет с расстоянием и плавно обнуляется к range
struct PointLight {
    glm::vec3 position{0.0f};
    glm::vec3 color{1.0f};
    float intensity = 5.0f;
    float range = 10.0f;
};

} // namespace kostyl
