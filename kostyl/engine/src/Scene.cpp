#include "kostyl/Scene.h"
#include "kostyl/Mesh.h"

#include <algorithm>

namespace kostyl {

Entity& Scene::create(std::string name, std::shared_ptr<Mesh> mesh, Material material)
{
    auto e = std::make_unique<Entity>();
    e->name = std::move(name);
    e->mesh = std::move(mesh);
    e->material = std::move(material);
    m_entities.push_back(std::move(e));
    return *m_entities.back();
}

Entity* Scene::find(std::string_view name)
{
    for (auto& e : m_entities) {
        if (e->name == name)
            return e.get();
    }
    return nullptr;
}

void Scene::remove(const Entity& entity)
{
    m_entities.erase(std::remove_if(m_entities.begin(), m_entities.end(),
                                    [&](const auto& e) { return e.get() == &entity; }),
                     m_entities.end());
}

void Scene::clear()
{
    m_entities.clear();
    pointLights.clear();
}

} // namespace kostyl
