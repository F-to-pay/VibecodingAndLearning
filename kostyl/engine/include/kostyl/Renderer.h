#pragma once

#include <cstddef>
#include <memory>
#include <string>
#include <vector>

namespace kostyl {

class Camera;
class Scene;
class Shader;
class Texture;

struct RenderStats {
    int drawCalls = 0;
    int shaderSwitches = 0;
    std::size_t triangles = 0;
};

// Рисует Scene глазами Camera.
//
// Данные кадра (матрицы камеры, свет, туман) один раз за кадр заливаются в uniform-буфер
// FrameData (binding 0) — все шейдеры видят их без единого glUniform на объект.
// Поштучно на объект ставятся только модельная матрица и параметры материала.
class Renderer {
public:
    Renderer();
    ~Renderer();

    Renderer(const Renderer&) = delete;
    Renderer& operator=(const Renderer&) = delete;

    void render(const Scene& scene, const Camera& camera, int width, int height, float time);

    // Шейдер, который будет перезагружаться вместе со всеми по reloadShaders() (F5)
    std::shared_ptr<Shader> loadShader(const std::string& vertexPath, const std::string& fragmentPath);
    bool reloadShaders();

    void setWireframe(bool enabled) { m_wireframe = enabled; }
    bool wireframe() const { return m_wireframe; }

    const RenderStats& stats() const { return m_stats; }

    // Сохранить back buffer в PNG. Звать после render(), до swapBuffers().
    bool saveScreenshot(const std::string& path, int width, int height) const;

private:
    unsigned m_frameUbo = 0;
    std::shared_ptr<Shader> m_litShader;
    std::shared_ptr<Texture> m_whiteTexture;
    std::vector<std::weak_ptr<Shader>> m_shaders;
    bool m_wireframe = false;
    RenderStats m_stats;
};

} // namespace kostyl
