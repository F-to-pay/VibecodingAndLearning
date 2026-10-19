// Демо-сцена Kostyl Engine: ящики по кругу, сферы с разными материалами, три цветные лампы и солнце.
//
// Запуск:   sandbox                          — обычный режим
//           sandbox --frames 120 --screenshot shot.png  — отрисовать 120 кадров, снять кадр и выйти
//           sandbox --novsync                — без вертикальной синхронизации (смотреть реальный fps)

#include <kostyl/Kostyl.h>

#include <glm/gtc/constants.hpp>

#include <cmath>
#include <cstdlib>
#include <cstring>
#include <exception>
#include <iterator>
#include <string>
#include <vector>

using namespace kostyl;

class DemoApp : public Application {
public:
    using Application::Application;

protected:
    void onInit() override
    {
        KLOG_INFO("Управление: ПКМ/Tab — мышь, WASD+E/Q — полёт, Shift — быстрее, колесо — зум");
        KLOG_INFO("            P — пауза анимации, L — солнце, F1 — каркас, F2 — vsync, F5 — шейдеры, F12 — скриншот");

        camera().position = {0.0f, 4.0f, 10.0f};
        camera().lookAt({0.0f, 1.0f, 0.0f});

        // --- Ресурсы ---
        auto cubeMesh = primitives::cube();
        auto sphereMesh = primitives::sphere(0.5f);
        auto planeMesh = primitives::plane(80.0f);

        auto crateTex = Texture::fromFile("textures/crate.png");
        auto floorTex = Texture::checkerboard(256, 2, {0.42f, 0.43f, 0.46f}, {0.22f, 0.23f, 0.26f});

        Scene& s = scene();

        // --- Пол ---
        Material floorMat;
        floorMat.albedoMap = floorTex;
        floorMat.uvScale = glm::vec2(40.0f);  // 80 м / 40 = текстура на каждые 2 м -> клетка 1 м
        floorMat.specular = 0.08f;
        floorMat.shininess = 16.0f;
        s.create("floor", planeMesh, floorMat);

        // --- Постамент со сферами разных материалов ---
        Material stoneMat;
        stoneMat.albedo = {0.20f, 0.20f, 0.22f};
        stoneMat.specular = 0.1f;
        Entity& pedestal = s.create("pedestal", cubeMesh, stoneMat);
        pedestal.transform.position = {0.0f, 0.2f, 0.0f};
        pedestal.transform.scale = {5.4f, 0.4f, 1.8f};

        struct SphereDesc { glm::vec3 albedo; float specular; float shininess; };
        const SphereDesc spheres[] = {
            {{0.80f, 0.08f, 0.06f}, 0.25f, 8.0f},    // матовый пластик
            {{0.90f, 0.90f, 0.92f}, 0.60f, 64.0f},   // глянцевая керамика
            {{1.00f, 0.66f, 0.22f}, 1.00f, 512.0f},  // "золото" (насколько позволяет Phong)
        };
        for (int i = 0; i < 3; ++i) {
            Material m;
            m.albedo = spheres[i].albedo;
            m.specular = spheres[i].specular;
            m.shininess = spheres[i].shininess;
            Entity& e = s.create("sphere" + std::to_string(i), sphereMesh, m);
            e.transform.position = {-1.8f + 1.8f * float(i), 1.0f, 0.0f};
            e.transform.scale = glm::vec3(1.2f);
        }

        // --- Кольцо ящиков ---
        Material crateMat;
        crateMat.albedoMap = crateTex;
        crateMat.specular = 0.2f;
        crateMat.shininess = 24.0f;

        constexpr int kCrates = 10;
        for (int i = 0; i < kCrates; ++i) {
            Entity& e = s.create("crate" + std::to_string(i), cubeMesh, crateMat);
            const float angle = glm::two_pi<float>() * float(i) / float(kCrates);
            m_orbiters.push_back({&e, angle, 6.0f, 0.3f + 0.15f * float(i % 3)});
        }

        // --- Штабель ящиков в стороне (статичный) ---
        const glm::vec3 stack[] = {{-9.0f, 0.5f, -6.0f}, {-7.9f, 0.5f, -6.2f}, {-8.5f, 1.5f, -6.1f}, {-8.4f, 0.5f, -4.9f}};
        for (size_t i = 0; i < std::size(stack); ++i) {
            Entity& e = s.create("stack" + std::to_string(i), cubeMesh, crateMat);
            e.transform.position = stack[i];
            e.transform.setEulerDegrees({0.0f, 12.0f * float(i) - 10.0f, 0.0f});
        }

        // --- Свет ---
        s.sun.direction = glm::normalize(glm::vec3(-0.5f, -1.0f, -0.35f));
        s.sun.color = {1.0f, 0.78f, 0.55f};  // вечернее, тёплое
        s.sun.intensity = 0.55f;

        s.skyColor = {0.10f, 0.13f, 0.20f};
        s.ambientSky = {0.07f, 0.085f, 0.13f};
        s.ambientGround = {0.025f, 0.02f, 0.018f};
        s.fogDensity = 0.035f;

        const glm::vec3 lightColors[] = {{1.0f, 0.45f, 0.12f}, {0.15f, 0.65f, 1.0f}, {1.0f, 0.2f, 0.65f}};
        for (const auto& c : lightColors) {
            PointLight l;
            l.color = c;
            l.intensity = 16.0f;
            l.range = 10.0f;
            s.pointLights.push_back(l);

            // Видимый "шарик" на месте лампы — unlit, чтобы светился сам
            Material gizmo;
            gizmo.unlit = true;
            gizmo.albedo = glm::vec3(0.0f);
            gizmo.emissive = c * 4.0f;
            Entity& g = s.create("light_gizmo", sphereMesh, gizmo);
            g.transform.scale = glm::vec3(0.25f);
            m_lightGizmos.push_back(&g);
        }

        applyAnimation();
    }

    void onUpdate(float dt) override
    {
        m_controller.update(camera(), input(), dt);

        if (input().wasKeyPressed(GLFW_KEY_P))
            m_paused = !m_paused;
        if (input().wasKeyPressed(GLFW_KEY_L))
            scene().sun.enabled = !scene().sun.enabled;

        if (!m_paused) {
            m_animTime += dt;
            applyAnimation();
        }
    }

private:
    void applyAnimation()
    {
        const float t = m_animTime;

        for (size_t i = 0; i < m_orbiters.size(); ++i) {
            Orbiter& o = m_orbiters[i];
            const float a = o.baseAngle + t * 0.2f;
            Transform& tr = o.entity->transform;
            tr.position = {std::cos(a) * o.radius, 0.9f + 0.35f * std::sin(t * 1.4f + float(i)), std::sin(a) * o.radius};
            tr.rotation = glm::quat(glm::vec3(t * o.spin * 0.7f + float(i), t * o.spin, 0.0f));
        }

        auto& lights = scene().pointLights;
        for (size_t i = 0; i < lights.size(); ++i) {
            const float a = t * 0.7f + glm::two_pi<float>() * float(i) / float(lights.size());
            const glm::vec3 pos(std::cos(a) * 3.4f, 1.7f + 0.6f * std::sin(t * 1.3f + float(i) * 2.0f), std::sin(a) * 3.4f);
            lights[i].position = pos;
            m_lightGizmos[i]->transform.position = pos;
        }
    }

    struct Orbiter {
        Entity* entity;
        float baseAngle;
        float radius;
        float spin;  // рад/с
    };

    FlyCameraController m_controller;
    std::vector<Orbiter> m_orbiters;
    std::vector<Entity*> m_lightGizmos;
    float m_animTime = 0.0f;
    bool m_paused = false;
};

int main(int argc, char** argv)
{
    AppConfig config;
    config.window.title = "Kostyl Engine";

    for (int i = 1; i < argc; ++i) {
        const char* arg = argv[i];
        if (std::strcmp(arg, "--frames") == 0 && i + 1 < argc) {
            config.exitAfterFrames = std::atoi(argv[++i]);
        } else if (std::strcmp(arg, "--screenshot") == 0 && i + 1 < argc) {
            config.screenshotPath = argv[++i];
            if (config.exitAfterFrames <= 0)
                config.exitAfterFrames = 60;
        } else if (std::strcmp(arg, "--novsync") == 0) {
            config.window.vsync = false;
        } else {
            KLOG_WARN("Неизвестный аргумент: %s", arg);
        }
    }

    try {
        DemoApp app(config);
        return app.run();
    } catch (const std::exception& e) {
        KLOG_ERROR("Фатальная ошибка: %s", e.what());
        return EXIT_FAILURE;
    }
}
