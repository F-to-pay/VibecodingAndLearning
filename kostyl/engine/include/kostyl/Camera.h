#pragma once

#include <glm/glm.hpp>

namespace kostyl {

class Input;

// Перспективная камера на углах Эйлера (yaw/pitch в градусах).
// yaw = -90, pitch = 0 — смотрит в -Z (стандарт OpenGL).
class Camera {
public:
    glm::vec3 position{0.0f, 2.0f, 6.0f};
    float yaw = -90.0f;
    float pitch = 0.0f;
    float fov = 60.0f;  // вертикальный, в градусах
    float nearPlane = 0.05f;
    float farPlane = 300.0f;

    glm::vec3 forward() const;
    glm::vec3 right() const;
    glm::vec3 up() const;

    glm::mat4 view() const;
    glm::mat4 projection(float aspect) const;

    void lookAt(const glm::vec3& target);
};

// Свободный полёт:
//   ПКМ (зажать) или Tab (переключить) — вращение мышью
//   WASD — движение, E/Space — вверх, Q/Ctrl — вниз, Shift — ускорение
//   колесо — зум (FOV)
class FlyCameraController {
public:
    float moveSpeed = 5.0f;     // м/с
    float boostMultiplier = 4.0f;
    float sensitivity = 0.12f;  // градусов на пиксель

    void update(Camera& camera, Input& input, float dt);

private:
    bool m_toggledCapture = false;
};

} // namespace kostyl
