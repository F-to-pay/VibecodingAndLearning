#pragma once

#include <glm/glm.hpp>

#include <memory>
#include <string>

namespace kostyl {

struct TextureParams {
    bool srgb = true;      // true для цветовых карт (альбедо). Для нормалей/масок — false.
    bool mipmaps = true;
    bool repeat = true;    // false — CLAMP_TO_EDGE
    bool nearest = false;  // true — пиксельная фильтрация (для пиксель-арта)
};

// 2D-текстура. Создаётся только через фабрики, живёт в shared_ptr.
class Texture {
public:
    // PNG/JPG/TGA/BMP/... через stb_image. Бросает std::runtime_error, если файл не читается.
    static std::shared_ptr<Texture> fromFile(const std::string& path, const TextureParams& params = {});

    // Сырые пиксели: channels = 1..4, по байту на канал, строки сверху вниз.
    static std::shared_ptr<Texture> fromPixels(int width, int height, int channels,
                                               const unsigned char* pixels, const TextureParams& params = {});

    // 1x1 сплошной цвет (sRGB, 0..1)
    static std::shared_ptr<Texture> solid(const glm::vec4& color);

    // Шахматка: size x size пикселей, cells клеток по стороне, цвета a/b в sRGB 0..1
    static std::shared_ptr<Texture> checkerboard(int size, int cells, const glm::vec3& a, const glm::vec3& b);

    ~Texture();
    Texture(const Texture&) = delete;
    Texture& operator=(const Texture&) = delete;

    void bind(unsigned unit) const;

    unsigned id() const { return m_id; }
    int width() const { return m_width; }
    int height() const { return m_height; }

private:
    Texture() = default;

    unsigned m_id = 0;
    int m_width = 0;
    int m_height = 0;
};

} // namespace kostyl
