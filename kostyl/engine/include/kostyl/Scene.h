#pragma once

#include "kostyl/Light.h"
#include "kostyl/Material.h"
#include "kostyl/Transform.h"

#include <memory>
#include <string>
#include <string_view>
#include <vector>

namespace kostyl {

class Mesh;

// Объект сцены: что рисовать (mesh), как (material) и где (transform).
struct Entity {
    std::string name;
    Transform transform;
    std::shared_ptr<Mesh> mesh;
    Material material;
    bool visible = true;
};

// Плоский список объектов + освещение. Без иерархии и ECS — для начала хватит.
class Scene {
public:
    // Ссылка остаётся валидной, пока объект в сцене (объекты хранятся по unique_ptr).
    Entity& create(std::string name, std::shared_ptr<Mesh> mesh, Material material = {});
    Entity* find(std::string_view name);
    void remove(const Entity& entity);
    void clear();

    const std::vector<std::unique_ptr<Entity>>& entities() const { return m_entities; }

    // --- Освещение и атмосфера ---
    DirectionalLight sun;
    std::vector<PointLight> pointLights;  // больше kMaxPointLights — лишние игнорируются

    glm::vec3 ambientSky{0.10f, 0.12f, 0.16f};    // фоновый свет сверху
    glm::vec3 ambientGround{0.04f, 0.035f, 0.03f}; // и снизу (полусферическое освещение)
    glm::vec3 skyColor{0.30f, 0.38f, 0.50f};       // цвет фона и тумана (линейный)
    float fogDensity = 0.02f;                       // 0 — без тумана
    float exposure = 1.0f;

private:
    std::vector<std::unique_ptr<Entity>> m_entities;
};

} // namespace kostyl
