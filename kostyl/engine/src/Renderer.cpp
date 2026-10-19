#include "kostyl/Renderer.h"
#include "kostyl/Camera.h"
#include "kostyl/Log.h"
#include "kostyl/Mesh.h"
#include "kostyl/Scene.h"
#include "kostyl/Shader.h"
#include "kostyl/Texture.h"

#include <glad/glad.h>
#include <glm/gtc/type_ptr.hpp>
#include <stb_image_write.h>

#include <algorithm>
#include <cmath>

namespace kostyl {

namespace {

constexpr unsigned kFrameDataBinding = 0;

// Раскладка std140 — ДОЛЖНА совпадать с блоком FrameData в assets/shaders/common.glsl.
// В std140 vec3 выравнивается как vec4, поэтому всё упаковано в vec4: так проще не ошибиться.
struct FrameData {
    glm::mat4 view;
    glm::mat4 projection;
    glm::mat4 viewProjection;
    glm::vec4 cameraPos;      // xyz
    glm::vec4 sunDirection;   // xyz — куда светит, w — 1 если включено
    glm::vec4 sunColor;       // rgb * intensity
    glm::vec4 ambientSky;     // rgb
    glm::vec4 ambientGround;  // rgb
    glm::vec4 fog;            // rgb — цвет, w — плотность
    glm::vec4 params;         // x — экспозиция, y — время
    glm::vec4 pointPos[kMaxPointLights];    // xyz, w — радиус действия
    glm::vec4 pointColor[kMaxPointLights];  // rgb * intensity
    glm::ivec4 counts;        // x — число точечных источников
};
static_assert(sizeof(FrameData) == 3 * 64 + 7 * 16 + 2 * kMaxPointLights * 16 + 16,
              "FrameData разъехался с std140 — проверь выравнивание");

// То же, что делает конец фрагментного шейдера: ACES-тонмаппинг + гамма.
// Нужно, чтобы цвет очистки совпадал с туманом на горизонте.
glm::vec3 toDisplay(glm::vec3 c, float exposure)
{
    c *= exposure;
    const glm::vec3 mapped = glm::clamp((c * (2.51f * c + 0.03f)) / (c * (2.43f * c + 0.59f) + 0.14f), 0.0f, 1.0f);
    return glm::pow(mapped, glm::vec3(1.0f / 2.2f));
}

} // namespace

Renderer::Renderer()
{
    Shader::registerBlockBinding("FrameData", kFrameDataBinding);

    glGenBuffers(1, &m_frameUbo);
    glBindBuffer(GL_UNIFORM_BUFFER, m_frameUbo);
    glBufferData(GL_UNIFORM_BUFFER, sizeof(FrameData), nullptr, GL_DYNAMIC_DRAW);
    glBindBufferBase(GL_UNIFORM_BUFFER, kFrameDataBinding, m_frameUbo);
    glBindBuffer(GL_UNIFORM_BUFFER, 0);

    m_litShader = loadShader("shaders/lit.vert", "shaders/lit.frag");
    m_whiteTexture = Texture::solid(glm::vec4(1.0f));

    glEnable(GL_DEPTH_TEST);
    glDepthFunc(GL_LESS);
    glEnable(GL_CULL_FACE);  // задние грани не рисуем — экономия ~половины фрагментов
    glCullFace(GL_BACK);
    glFrontFace(GL_CCW);
    glEnable(GL_MULTISAMPLE);
}

Renderer::~Renderer()
{
    glDeleteBuffers(1, &m_frameUbo);
}

std::shared_ptr<Shader> Renderer::loadShader(const std::string& vertexPath, const std::string& fragmentPath)
{
    auto shader = std::make_shared<Shader>(vertexPath, fragmentPath);
    m_shaders.push_back(shader);
    return shader;
}

bool Renderer::reloadShaders()
{
    bool allOk = true;
    int count = 0;
    for (auto& weak : m_shaders) {
        if (auto s = weak.lock()) {
            allOk = s->reload() && allOk;
            ++count;
        }
    }
    // Выкидываем шейдеры, которые уже никто не использует
    m_shaders.erase(std::remove_if(m_shaders.begin(), m_shaders.end(),
                                   [](const auto& w) { return w.expired(); }),
                    m_shaders.end());
    if (allOk)
        KLOG_INFO("Шейдеры перезагружены (%d шт.)", count);
    return allOk;
}

void Renderer::render(const Scene& scene, const Camera& camera, int width, int height, float time)
{
    m_stats = {};
    if (width <= 0 || height <= 0)
        return;

    glViewport(0, 0, width, height);

    const glm::vec3 clear = toDisplay(scene.skyColor, scene.exposure);
    glClearColor(clear.r, clear.g, clear.b, 1.0f);
    glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);

    // --- Данные кадра в UBO ---
    FrameData fd{};
    fd.view = camera.view();
    fd.projection = camera.projection(float(width) / float(height));
    fd.viewProjection = fd.projection * fd.view;
    fd.cameraPos = glm::vec4(camera.position, 1.0f);
    fd.sunDirection = glm::vec4(glm::normalize(scene.sun.direction), scene.sun.enabled ? 1.0f : 0.0f);
    fd.sunColor = glm::vec4(scene.sun.color * scene.sun.intensity, 0.0f);
    fd.ambientSky = glm::vec4(scene.ambientSky, 0.0f);
    fd.ambientGround = glm::vec4(scene.ambientGround, 0.0f);
    fd.fog = glm::vec4(scene.skyColor, scene.fogDensity);
    fd.params = glm::vec4(scene.exposure, time, 0.0f, 0.0f);

    const int lightCount = std::min(static_cast<int>(scene.pointLights.size()), kMaxPointLights);
    for (int i = 0; i < lightCount; ++i) {
        const PointLight& l = scene.pointLights[static_cast<size_t>(i)];
        fd.pointPos[i] = glm::vec4(l.position, l.range);
        fd.pointColor[i] = glm::vec4(l.color * l.intensity, 0.0f);
    }
    fd.counts = glm::ivec4(lightCount, 0, 0, 0);

    glBindBuffer(GL_UNIFORM_BUFFER, m_frameUbo);
    glBufferSubData(GL_UNIFORM_BUFFER, 0, sizeof(FrameData), &fd);
    glBindBuffer(GL_UNIFORM_BUFFER, 0);

    glPolygonMode(GL_FRONT_AND_BACK, m_wireframe ? GL_LINE : GL_FILL);

    // --- Объекты ---
    Shader* current = nullptr;
    for (const auto& ptr : scene.entities()) {
        const Entity& e = *ptr;
        if (!e.visible || !e.mesh)
            continue;

        const Material& mat = e.material;
        Shader* shader = mat.shader ? mat.shader.get() : m_litShader.get();
        if (shader != current) {
            shader->bind();
            shader->setInt("uAlbedoMap", 0);  // сэмплер на текстурный слот 0
            current = shader;
            ++m_stats.shaderSwitches;
        }

        const glm::mat4 model = e.transform.matrix();
        shader->setMat4("uModel", model);
        // Матрица нормалей: при неравномерном масштабе нормали нельзя крутить просто model'ью
        shader->setMat3("uNormalMatrix", glm::transpose(glm::inverse(glm::mat3(model))));

        shader->setVec3("uAlbedo", mat.albedo);
        shader->setVec3("uEmissive", mat.emissive);
        shader->setFloat("uSpecular", mat.specular);
        shader->setFloat("uShininess", std::max(mat.shininess, 1.0f));
        shader->setVec2("uUVScale", mat.uvScale);
        shader->setBool("uUnlit", mat.unlit);

        (mat.albedoMap ? *mat.albedoMap : *m_whiteTexture).bind(0);

        e.mesh->draw();
        ++m_stats.drawCalls;
        m_stats.triangles += e.mesh->triangleCount();
    }

    glBindVertexArray(0);
    glPolygonMode(GL_FRONT_AND_BACK, GL_FILL);
}

bool Renderer::saveScreenshot(const std::string& path, int width, int height) const
{
    if (width <= 0 || height <= 0)
        return false;

    std::vector<unsigned char> pixels(static_cast<size_t>(width) * static_cast<size_t>(height) * 3);
    glPixelStorei(GL_PACK_ALIGNMENT, 1);
    glReadBuffer(GL_BACK);
    glReadPixels(0, 0, width, height, GL_RGB, GL_UNSIGNED_BYTE, pixels.data());
    glPixelStorei(GL_PACK_ALIGNMENT, 4);

    stbi_flip_vertically_on_write(1);  // у GL строка 0 — нижняя
    const int ok = stbi_write_png(path.c_str(), width, height, 3, pixels.data(), width * 3);
    if (ok)
        KLOG_INFO("Скриншот: %s", path.c_str());
    else
        KLOG_ERROR("Не смог записать скриншот %s", path.c_str());
    return ok != 0;
}

} // namespace kostyl
