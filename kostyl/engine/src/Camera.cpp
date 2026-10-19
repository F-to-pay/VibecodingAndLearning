#include "kostyl/Camera.h"
#include "kostyl/Input.h"

#include <glm/gtc/matrix_transform.hpp>

#include <algorithm>
#include <cmath>

namespace kostyl {

namespace {
constexpr glm::vec3 kWorldUp(0.0f, 1.0f, 0.0f);
}

glm::vec3 Camera::forward() const
{
    const float y = glm::radians(yaw);
    const float p = glm::radians(pitch);
    return glm::normalize(glm::vec3(std::cos(y) * std::cos(p), std::sin(p), std::sin(y) * std::cos(p)));
}

glm::vec3 Camera::right() const { return glm::normalize(glm::cross(forward(), kWorldUp)); }
glm::vec3 Camera::up() const { return glm::cross(right(), forward()); }

glm::mat4 Camera::view() const
{
    return glm::lookAt(position, position + forward(), kWorldUp);
}

glm::mat4 Camera::projection(float aspect) const
{
    return glm::perspective(glm::radians(fov), aspect, nearPlane, farPlane);
}

void Camera::lookAt(const glm::vec3& target)
{
    const glm::vec3 dir = target - position;
    if (glm::dot(dir, dir) < 1e-8f)
        return;
    const glm::vec3 d = glm::normalize(dir);
    pitch = glm::degrees(std::asin(std::clamp(d.y, -1.0f, 1.0f)));
    yaw = glm::degrees(std::atan2(d.z, d.x));
}

void FlyCameraController::update(Camera& camera, Input& input, float dt)
{
    // --- Захват мыши ---
    if (input.wasKeyPressed(GLFW_KEY_TAB))
        m_toggledCapture = !m_toggledCapture;
    input.setCursorCaptured(m_toggledCapture || input.isMouseDown(GLFW_MOUSE_BUTTON_RIGHT));

    // --- Вращение ---
    if (input.isCursorCaptured()) {
        const glm::vec2 d = input.mouseDelta() * sensitivity;
        camera.yaw += d.x;
        camera.pitch = std::clamp(camera.pitch - d.y, -89.0f, 89.0f);  // в зенит не даём — lookAt сломается
        camera.yaw = std::fmod(camera.yaw, 360.0f);
    }

    // --- Зум колесом ---
    camera.fov = std::clamp(camera.fov - input.scrollDelta() * 3.0f, 15.0f, 100.0f);

    // --- Движение ---
    glm::vec3 move(0.0f);
    const glm::vec3 fwd = camera.forward();
    const glm::vec3 right = camera.right();
    if (input.isKeyDown(GLFW_KEY_W)) move += fwd;
    if (input.isKeyDown(GLFW_KEY_S)) move -= fwd;
    if (input.isKeyDown(GLFW_KEY_D)) move += right;
    if (input.isKeyDown(GLFW_KEY_A)) move -= right;
    if (input.isKeyDown(GLFW_KEY_E) || input.isKeyDown(GLFW_KEY_SPACE))        move += kWorldUp;
    if (input.isKeyDown(GLFW_KEY_Q) || input.isKeyDown(GLFW_KEY_LEFT_CONTROL)) move -= kWorldUp;

    if (glm::dot(move, move) > 0.0f) {
        float speed = moveSpeed;
        if (input.isKeyDown(GLFW_KEY_LEFT_SHIFT))
            speed *= boostMultiplier;
        camera.position += glm::normalize(move) * speed * dt;  // normalize: по диагонали не быстрее
    }
}

} // namespace kostyl
