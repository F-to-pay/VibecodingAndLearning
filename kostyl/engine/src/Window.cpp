#include "kostyl/Window.h"
#include "kostyl/Log.h"

#include <glad/glad.h>
#include <GLFW/glfw3.h>

#include <stdexcept>

// Ноуты с двумя видеокартами (Intel + Nvidia/AMD): просим драйвер запускать нас на дискретке.
// Драйверы ищут эти символы в экспорте exe. Работает только на Windows.
#ifdef _WIN32
extern "C" {
__declspec(dllexport) unsigned long NvOptimusEnablement = 1;
__declspec(dllexport) int AmdPowerXpressRequestHighPerformance = 1;
}
#endif

namespace kostyl {

namespace {

void glfwErrorCallback(int code, const char* description)
{
    KLOG_ERROR("GLFW %d: %s", code, description);
}

#ifdef KOSTYL_DEBUG
void APIENTRY glDebugCallback(GLenum source, GLenum type, GLuint id, GLenum severity,
                              GLsizei /*length*/, const GLchar* message, const void* /*user*/)
{
    if (severity == GL_DEBUG_SEVERITY_NOTIFICATION)
        return;  // спам вида "buffer will use VIDEO memory" — не интересно
    if (source == GL_DEBUG_SOURCE_SHADER_COMPILER)
        return;  // ошибки компиляции шейдеров Shader печатает сам, с именами файлов
    // Известный шум драйвера NVIDIA: 131185 — "буфер в видеопамяти", 131218 — перекомпиляция шейдера,
    // 131169 — выделение памяти под framebuffer, 131204 — "текстура не подходит под сэмплер"
    if (id == 131185 || id == 131218 || id == 131169 || id == 131204)
        return;

    const char* typeStr = "other";
    switch (type) {
    case GL_DEBUG_TYPE_ERROR:               typeStr = "error"; break;
    case GL_DEBUG_TYPE_DEPRECATED_BEHAVIOR: typeStr = "deprecated"; break;
    case GL_DEBUG_TYPE_UNDEFINED_BEHAVIOR:  typeStr = "undefined behavior"; break;
    case GL_DEBUG_TYPE_PERFORMANCE:         typeStr = "performance"; break;
    case GL_DEBUG_TYPE_PORTABILITY:         typeStr = "portability"; break;
    default: break;
    }

    if (severity == GL_DEBUG_SEVERITY_HIGH || type == GL_DEBUG_TYPE_ERROR)
        KLOG_ERROR("GL [%s] #%u: %s", typeStr, id, message);
    else
        KLOG_WARN("GL [%s] #%u: %s", typeStr, id, message);
}
#endif

Window* self(GLFWwindow* w) { return static_cast<Window*>(glfwGetWindowUserPointer(w)); }

} // namespace

Window::Window(const WindowConfig& config)
    : m_vsync(config.vsync)
{
    glfwSetErrorCallback(glfwErrorCallback);
    if (!glfwInit())
        throw std::runtime_error("glfwInit() не взлетел");

    glfwWindowHint(GLFW_CONTEXT_VERSION_MAJOR, 3);
    glfwWindowHint(GLFW_CONTEXT_VERSION_MINOR, 3);
    glfwWindowHint(GLFW_OPENGL_PROFILE, GLFW_OPENGL_CORE_PROFILE);
    glfwWindowHint(GLFW_OPENGL_FORWARD_COMPAT, GLFW_TRUE);  // нужно для macOS, остальным не мешает
    glfwWindowHint(GLFW_RESIZABLE, config.resizable ? GLFW_TRUE : GLFW_FALSE);
    glfwWindowHint(GLFW_SAMPLES, config.msaaSamples);
#ifdef KOSTYL_DEBUG
    glfwWindowHint(GLFW_OPENGL_DEBUG_CONTEXT, GLFW_TRUE);
#endif

    m_window = glfwCreateWindow(config.width, config.height, config.title.c_str(), nullptr, nullptr);
    if (!m_window) {
        glfwTerminate();
        throw std::runtime_error("Не удалось создать окно с OpenGL 3.3 core. Драйвер видеокарты живой?");
    }

    glfwMakeContextCurrent(m_window);

    if (!gladLoadGLLoader(reinterpret_cast<GLADloadproc>(glfwGetProcAddress))) {
        glfwDestroyWindow(m_window);
        glfwTerminate();
        throw std::runtime_error("glad не смог загрузить функции OpenGL");
    }

    KLOG_INFO("OpenGL %s", reinterpret_cast<const char*>(glGetString(GL_VERSION)));
    KLOG_INFO("GPU    %s (%s)", reinterpret_cast<const char*>(glGetString(GL_RENDERER)),
              reinterpret_cast<const char*>(glGetString(GL_VENDOR)));
    KLOG_INFO("GLSL   %s", reinterpret_cast<const char*>(glGetString(GL_SHADING_LANGUAGE_VERSION)));

#ifdef KOSTYL_DEBUG
    GLint flags = 0;
    glGetIntegerv(GL_CONTEXT_FLAGS, &flags);
    if (GLAD_GL_KHR_debug && (flags & GL_CONTEXT_FLAG_DEBUG_BIT)) {
        glEnable(GL_DEBUG_OUTPUT);
        glEnable(GL_DEBUG_OUTPUT_SYNCHRONOUS);  // ошибка прилетит прямо из виноватого вызова — удобно в отладчике
        glDebugMessageCallback(glDebugCallback, nullptr);
        KLOG_INFO("GL debug output включён");
    } else {
        KLOG_WARN("GL debug output недоступен (нет KHR_debug или debug-контекста)");
    }
#endif

    glfwSwapInterval(m_vsync ? 1 : 0);
    glfwGetFramebufferSize(m_window, &m_fbWidth, &m_fbHeight);

    m_input = std::make_unique<Input>(m_window);

    glfwSetWindowUserPointer(m_window, this);
    glfwSetFramebufferSizeCallback(m_window, onFramebufferSize);
    glfwSetKeyCallback(m_window, onKey);
    glfwSetMouseButtonCallback(m_window, onMouseButton);
    glfwSetCursorPosCallback(m_window, onCursorPos);
    glfwSetScrollCallback(m_window, onScroll);
}

Window::~Window()
{
    if (m_window)
        glfwDestroyWindow(m_window);
    glfwTerminate();
}

bool Window::shouldClose() const { return glfwWindowShouldClose(m_window); }
void Window::requestClose() { glfwSetWindowShouldClose(m_window, GLFW_TRUE); }
void Window::pollEvents() { glfwPollEvents(); }
void Window::swapBuffers() { glfwSwapBuffers(m_window); }
void Window::setTitle(const std::string& title) { glfwSetWindowTitle(m_window, title.c_str()); }

void Window::setVSync(bool enabled)
{
    m_vsync = enabled;
    glfwSwapInterval(enabled ? 1 : 0);
}

void Window::onFramebufferSize(GLFWwindow* w, int width, int height)
{
    Window* win = self(w);
    win->m_fbWidth = width;
    win->m_fbHeight = height;
}

void Window::onKey(GLFWwindow* w, int key, int /*scancode*/, int action, int /*mods*/)
{
    self(w)->m_input->onKey(key, action);
}

void Window::onMouseButton(GLFWwindow* w, int button, int action, int /*mods*/)
{
    self(w)->m_input->onMouseButton(button, action);
}

void Window::onCursorPos(GLFWwindow* w, double x, double y)
{
    self(w)->m_input->onCursorPos(x, y);
}

void Window::onScroll(GLFWwindow* w, double /*dx*/, double dy)
{
    self(w)->m_input->onScroll(dy);
}

} // namespace kostyl
