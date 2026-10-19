#include "kostyl/Input.h"

namespace kostyl {

namespace {
template <typename Array>
bool inRange(const Array& arr, int idx) { return idx >= 0 && idx < static_cast<int>(arr.size()); }
}

Input::Input(GLFWwindow* window)
    : m_window(window)
{
}

bool Input::isKeyDown(int key) const       { return inRange(m_keyDown, key) && m_keyDown[key]; }
bool Input::wasKeyPressed(int key) const   { return inRange(m_keyPressed, key) && m_keyPressed[key]; }
bool Input::wasKeyReleased(int key) const  { return inRange(m_keyReleased, key) && m_keyReleased[key]; }
bool Input::isMouseDown(int button) const  { return inRange(m_buttonDown, button) && m_buttonDown[button]; }
bool Input::wasMousePressed(int button) const { return inRange(m_buttonPressed, button) && m_buttonPressed[button]; }

void Input::setCursorCaptured(bool captured)
{
    if (captured == m_captured)
        return;
    m_captured = captured;
    glfwSetInputMode(m_window, GLFW_CURSOR, captured ? GLFW_CURSOR_DISABLED : GLFW_CURSOR_NORMAL);
    if (glfwRawMouseMotionSupported())
        glfwSetInputMode(m_window, GLFW_RAW_MOUSE_MOTION, captured ? GLFW_TRUE : GLFW_FALSE);
    // При смене режима GLFW телепортирует курсор — первый сдвиг после этого игнорируем,
    // иначе камера дёрнется.
    m_firstMouse = true;
}

void Input::beginFrame()
{
    m_keyPressed.fill(false);
    m_keyReleased.fill(false);
    m_buttonPressed.fill(false);
    m_mouseDelta = glm::vec2(0.0f);
    m_scroll = 0.0f;
}

void Input::onKey(int key, int action)
{
    if (!inRange(m_keyDown, key))  // GLFW_KEY_UNKNOWN = -1
        return;
    if (action == GLFW_PRESS) {
        m_keyDown[key] = true;
        m_keyPressed[key] = true;
    } else if (action == GLFW_RELEASE) {
        m_keyDown[key] = false;
        m_keyReleased[key] = true;
    }
    // GLFW_REPEAT игнорируем: для игр автоповтор не нужен
}

void Input::onMouseButton(int button, int action)
{
    if (!inRange(m_buttonDown, button))
        return;
    if (action == GLFW_PRESS) {
        m_buttonDown[button] = true;
        m_buttonPressed[button] = true;
    } else if (action == GLFW_RELEASE) {
        m_buttonDown[button] = false;
    }
}

void Input::onCursorPos(double x, double y)
{
    const glm::vec2 pos(static_cast<float>(x), static_cast<float>(y));
    if (m_firstMouse) {
        m_firstMouse = false;
    } else {
        m_mouseDelta += pos - m_mousePos;  // за кадр может прийти несколько событий — копим
    }
    m_mousePos = pos;
}

void Input::onScroll(double dy)
{
    m_scroll += static_cast<float>(dy);
}

} // namespace kostyl
