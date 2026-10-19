#pragma once

#include <GLFW/glfw3.h>
#include <glm/vec2.hpp>

#include <array>

namespace kostyl {

// Состояние клавиатуры и мыши. Коды клавиш — родные GLFW (GLFW_KEY_W, GLFW_MOUSE_BUTTON_RIGHT, ...).
//
//   isKeyDown     — клавиша зажата прямо сейчас
//   wasKeyPressed — клавишу нажали в этом кадре (срабатывает один раз на нажатие)
class Input {
public:
    explicit Input(GLFWwindow* window);

    bool isKeyDown(int key) const;
    bool wasKeyPressed(int key) const;
    bool wasKeyReleased(int key) const;

    bool isMouseDown(int button) const;
    bool wasMousePressed(int button) const;

    glm::vec2 mousePosition() const { return m_mousePos; }
    glm::vec2 mouseDelta() const { return m_mouseDelta; }  // сдвиг за кадр, в пикселях
    float scrollDelta() const { return m_scroll; }         // прокрутка колеса за кадр

    // Захват курсора: прячет его и даёт бесконечное движение (для FPS-камеры).
    void setCursorCaptured(bool captured);
    bool isCursorCaptured() const { return m_captured; }

    // --- Внутренности, дёргает Window/Application ---
    void beginFrame();  // вызывать ДО glfwPollEvents
    void onKey(int key, int action);
    void onMouseButton(int button, int action);
    void onCursorPos(double x, double y);
    void onScroll(double dy);

private:
    static constexpr int kKeyCount = GLFW_KEY_LAST + 1;
    static constexpr int kButtonCount = GLFW_MOUSE_BUTTON_LAST + 1;

    GLFWwindow* m_window;

    std::array<bool, kKeyCount> m_keyDown{};
    std::array<bool, kKeyCount> m_keyPressed{};
    std::array<bool, kKeyCount> m_keyReleased{};
    std::array<bool, kButtonCount> m_buttonDown{};
    std::array<bool, kButtonCount> m_buttonPressed{};

    glm::vec2 m_mousePos{0.0f};
    glm::vec2 m_mouseDelta{0.0f};
    float m_scroll = 0.0f;
    bool m_firstMouse = true;
    bool m_captured = false;
};

} // namespace kostyl
