#pragma once

#include "kostyl/Camera.h"
#include "kostyl/Renderer.h"
#include "kostyl/Scene.h"
#include "kostyl/Window.h"

#include <memory>
#include <string>

namespace kostyl {

struct AppConfig {
    WindowConfig window;

    // Для автотестов/CI: отрисовать exitAfterFrames кадров, сохранить скриншот (если путь задан) и выйти.
    int exitAfterFrames = 0;  // 0 — работать, пока не закроют
    std::string screenshotPath;
};

// Каркас приложения: окно, главный цикл, рендерер, сцена и камера.
// Наследуйся и переопредели onInit/onUpdate. По умолчанию рисуется scene() из camera().
//
// Встроенные клавиши: Esc — выход, F1 — каркас, F2 — vsync, F5 — перезагрузка шейдеров, F12 — скриншот.
class Application {
public:
    explicit Application(AppConfig config = {});
    virtual ~Application();

    Application(const Application&) = delete;
    Application& operator=(const Application&) = delete;

    int run();

protected:
    virtual void onInit() {}
    virtual void onUpdate(float /*dt*/) {}
    virtual void onRender();  // по умолчанию: renderer().render(scene(), camera(), ...)
    virtual void onShutdown() {}

    Window& window() { return *m_window; }
    Input& input() { return m_window->input(); }
    Renderer& renderer() { return *m_renderer; }
    Scene& scene() { return m_scene; }
    Camera& camera() { return m_camera; }
    float time() const { return m_time; }

private:
    void handleBuiltinKeys();
    void updateStats(float dt);

    AppConfig m_config;
    std::unique_ptr<Window> m_window;      // порядок важен: окно (GL-контекст) создаётся первым
    std::unique_ptr<Renderer> m_renderer;  // а уничтожается последним
    Scene m_scene;
    Camera m_camera;

    float m_time = 0.0f;
    long long m_frame = 0;
    bool m_screenshotRequested = false;

    float m_statsTimer = 0.0f;
    int m_statsFrames = 0;
};

} // namespace kostyl
