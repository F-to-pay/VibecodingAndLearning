#include "kostyl/Application.h"
#include "kostyl/Log.h"

#include <GLFW/glfw3.h>

#include <algorithm>
#include <chrono>
#include <cstdio>
#include <ctime>
#include <thread>

namespace kostyl {

namespace {

std::string timestampedScreenshotName()
{
    const std::time_t now = std::time(nullptr);
    char buf[64];
    std::strftime(buf, sizeof(buf), "screenshot_%Y%m%d_%H%M%S.png", std::localtime(&now));
    return buf;
}

} // namespace

Application::Application(AppConfig config)
    : m_config(std::move(config))
{
    m_window = std::make_unique<Window>(m_config.window);
    m_renderer = std::make_unique<Renderer>();
}

Application::~Application() = default;

int Application::run()
{
    onInit();

    double last = glfwGetTime();
    while (!m_window->shouldClose()) {
        const double now = glfwGetTime();
        // Ограничиваем dt: после брейкпоинта или перетаскивания окна мир не должен улететь
        const float dt = std::min(static_cast<float>(now - last), 0.1f);
        last = now;
        m_time += dt;

        input().beginFrame();
        m_window->pollEvents();
        handleBuiltinKeys();

        onUpdate(dt);

        if (m_window->isMinimized()) {
            // Окно свёрнуто — рисовать некуда, не жжём CPU впустую
            std::this_thread::sleep_for(std::chrono::milliseconds(16));
            continue;
        }

        onRender();
        ++m_frame;

        const bool autoExit = m_config.exitAfterFrames > 0 && m_frame >= m_config.exitAfterFrames;
        if (autoExit && !m_config.screenshotPath.empty()) {
            m_renderer->saveScreenshot(m_config.screenshotPath, m_window->width(), m_window->height());
        } else if (m_screenshotRequested) {
            m_renderer->saveScreenshot(timestampedScreenshotName(), m_window->width(), m_window->height());
        }
        m_screenshotRequested = false;

        m_window->swapBuffers();
        updateStats(dt);

        if (autoExit)
            m_window->requestClose();
    }

    onShutdown();
    return 0;
}

void Application::onRender()
{
    m_renderer->render(m_scene, m_camera, m_window->width(), m_window->height(), m_time);
}

void Application::handleBuiltinKeys()
{
    Input& in = input();
    if (in.wasKeyPressed(GLFW_KEY_ESCAPE))
        m_window->requestClose();
    if (in.wasKeyPressed(GLFW_KEY_F1))
        m_renderer->setWireframe(!m_renderer->wireframe());
    if (in.wasKeyPressed(GLFW_KEY_F2)) {
        m_window->setVSync(!m_window->vsync());
        KLOG_INFO("VSync: %s", m_window->vsync() ? "вкл" : "выкл");
    }
    if (in.wasKeyPressed(GLFW_KEY_F5))
        m_renderer->reloadShaders();
    if (in.wasKeyPressed(GLFW_KEY_F12))
        m_screenshotRequested = true;
}

void Application::updateStats(float dt)
{
    m_statsTimer += dt;
    ++m_statsFrames;
    if (m_statsTimer < 0.5f)
        return;

    const float fps = float(m_statsFrames) / m_statsTimer;
    const RenderStats& s = m_renderer->stats();
    char title[256];
    std::snprintf(title, sizeof(title), "%s | %.0f fps (%.2f ms) | draws %d | tris %llu%s",
                  m_config.window.title.c_str(), double(fps), double(1000.0f / fps),
                  s.drawCalls, static_cast<unsigned long long>(s.triangles), m_renderer->wireframe() ? " | wireframe" : "");
    m_window->setTitle(title);

    m_statsTimer = 0.0f;
    m_statsFrames = 0;
}

} // namespace kostyl
