#pragma once

#include "kostyl/Mesh.h"

#include <memory>

// Генераторы простой геометрии. Всё с нормалями и UV, центр в (0,0,0).
namespace kostyl::primitives {

std::shared_ptr<Mesh> cube(float size = 1.0f);
std::shared_ptr<Mesh> plane(float size = 1.0f);  // в плоскости XZ, нормаль +Y
std::shared_ptr<Mesh> sphere(float radius = 0.5f, int segments = 48, int rings = 24);

} // namespace kostyl::primitives
