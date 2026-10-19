#pragma once

#include <glm/glm.hpp>
#include <glm/gtc/matrix_transform.hpp>
#include <glm/gtc/quaternion.hpp>

namespace kostyl {

// Положение / поворот / масштаб объекта. Поворот — кватернион (никакого gimbal lock).
struct Transform {
    glm::vec3 position{0.0f};
    glm::quat rotation{1.0f, 0.0f, 0.0f, 0.0f};  // (w, x, y, z) — без поворота
    glm::vec3 scale{1.0f};

    // Модельная матрица: сначала масштаб, потом поворот, потом перенос (T * R * S)
    glm::mat4 matrix() const
    {
        glm::mat4 m = glm::translate(glm::mat4(1.0f), position);
        m *= glm::mat4_cast(rotation);
        return glm::scale(m, scale);
    }

    // Повернуть на angle радиан вокруг оси в мировых координатах
    void rotate(float angle, const glm::vec3& axis)
    {
        rotation = glm::normalize(glm::angleAxis(angle, glm::normalize(axis)) * rotation);
    }

    // Углы Эйлера в градусах (pitch X, yaw Y, roll Z)
    void setEulerDegrees(const glm::vec3& degrees)
    {
        rotation = glm::quat(glm::radians(degrees));
    }
};

} // namespace kostyl
