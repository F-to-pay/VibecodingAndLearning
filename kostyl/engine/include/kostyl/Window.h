#pragma once

#include "kostyl/Input.h"

#include <memory>
#include <string>

struct GLFWwindow;

namespace kostyl {

struct WindowConfig {
    std::string title = "Kostyl Engine";
    int width = 1280;
    int height = 720;
    bool vsync = true;
    bool resizable = true;
    int msaaSamples = 4;  // 0 — без сглаживания
};

// Окно + OpenGL 3.3 core контекст. Создаёт контекст и грузит функции GL через glad.
// Один экземпляр на программу (GLFW инициализируется/гасится здесь же).
class Window {
public:
    explicit Window(const WindowConfig& config);
    ~Window();

    Window(const Window&) = delete;
    Window& operator=(const Window&) = delete;

    bool shouldClose() const;
    void requestClose();

    void pollEvents();
    void swapBuffers();

    void setTitle(const std::string& title);
    void setVSync(bool enabled);
    bool vsync() const { return m_vsync; }

    // Размер framebuffer'а в пикселях (на HiDPI может отличаться от размера окна)
    int width() const { return m_fbWidth; }
    int height() const { return m_fbHeight; }
    float aspect() const { return m_fbHeight > 0 ? float(m_fbWidth) / float(m_fbHeight) : 1.0f; }
    bool isMinimized() const { return m_fbWidth == 0 || m_fbHeight == 0; }

    Input& input() { return *m_input; }
    GLFWwindow* handle() const { return m_window; }

private:
    static void onFramebufferSize(GLFWwindow* w, int width, int height);
    static void onKey(GLFWwindow* w, int key, int scancode, int action, int mods);
    static void onMouseButton(GLFWwindow* w, int button, int action, int mods);
    static void onCursorPos(GLFWwindow* w, double x, double y);
    static void onScroll(GLFWwindow* w, double dx, double dy);

    GLFWwindow* m_window = nullptr;
    std::unique_ptr<Input> m_input;
    int m_fbWidth = 0;
    int m_fbHeight = 0;
    bool m_vsync = true;
};

} // namespace kostyl
