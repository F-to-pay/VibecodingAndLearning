#include "kostyl/Texture.h"
#include "kostyl/Assets.h"
#include "kostyl/Log.h"

#include <glad/glad.h>
#include <stb_image.h>

#include <algorithm>
#include <stdexcept>
#include <vector>

namespace kostyl {

namespace {

unsigned char toByte(float v)
{
    return static_cast<unsigned char>(std::clamp(v, 0.0f, 1.0f) * 255.0f + 0.5f);
}

} // namespace

std::shared_ptr<Texture> Texture::fromPixels(int width, int height, int channels,
                                             const unsigned char* pixels, const TextureParams& params)
{
    if (width <= 0 || height <= 0 || channels < 1 || channels > 4 || !pixels)
        throw std::invalid_argument("Texture::fromPixels: кривые аргументы");

    GLenum format = GL_RGBA;
    GLint internal = params.srgb ? GL_SRGB8_ALPHA8 : GL_RGBA8;
    switch (channels) {
    case 1: format = GL_RED; internal = GL_R8;  break;  // одноканальные — всегда линейные
    case 2: format = GL_RG;  internal = GL_RG8; break;
    case 3: format = GL_RGB; internal = params.srgb ? GL_SRGB8 : GL_RGB8; break;
    default: break;
    }

    std::shared_ptr<Texture> tex(new Texture());
    tex->m_width = width;
    tex->m_height = height;

    glGenTextures(1, &tex->m_id);
    glBindTexture(GL_TEXTURE_2D, tex->m_id);

    // По умолчанию GL ждёт, что каждая строка выровнена на 4 байта. У RGB-картинки
    // шириной 3 пикселя это не так — получим перекошенную текстуру. Отключаем.
    glPixelStorei(GL_UNPACK_ALIGNMENT, 1);
    glTexImage2D(GL_TEXTURE_2D, 0, internal, width, height, 0, format, GL_UNSIGNED_BYTE, pixels);
    glPixelStorei(GL_UNPACK_ALIGNMENT, 4);

    if (channels == 1) {
        // Чтобы одноканальная текстура читалась в шейдере как серая, а не красная
        const GLint swizzle[] = {GL_RED, GL_RED, GL_RED, GL_ONE};
        glTexParameteriv(GL_TEXTURE_2D, GL_TEXTURE_SWIZZLE_RGBA, swizzle);
    }

    const GLint wrap = params.repeat ? GL_REPEAT : GL_CLAMP_TO_EDGE;
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, wrap);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, wrap);

    GLint minFilter = params.nearest ? GL_NEAREST : GL_LINEAR;
    if (params.mipmaps) {
        glGenerateMipmap(GL_TEXTURE_2D);
        minFilter = params.nearest ? GL_NEAREST_MIPMAP_NEAREST : GL_LINEAR_MIPMAP_LINEAR;
    }
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, minFilter);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, params.nearest ? GL_NEAREST : GL_LINEAR);

    // Анизотропная фильтрация — пол под острым углом не превращается в мыло.
    // В 3.3 core её нет, но расширение есть практически везде.
    if (params.mipmaps && !params.nearest &&
        (GLAD_GL_EXT_texture_filter_anisotropic || GLAD_GL_ARB_texture_filter_anisotropic)) {
        GLfloat maxAniso = 1.0f;
        glGetFloatv(GL_MAX_TEXTURE_MAX_ANISOTROPY_EXT, &maxAniso);
        glTexParameterf(GL_TEXTURE_2D, GL_TEXTURE_MAX_ANISOTROPY_EXT, std::min(8.0f, maxAniso));
    }

    glBindTexture(GL_TEXTURE_2D, 0);
    return tex;
}

std::shared_ptr<Texture> Texture::fromFile(const std::string& path, const TextureParams& params)
{
    const std::string full = assets::path(path);

    // OpenGL считает, что первая строка текстуры — нижняя, а в файлах картинок она верхняя.
    stbi_set_flip_vertically_on_load(1);

    int w = 0, h = 0, n = 0;
    unsigned char* data = stbi_load(full.c_str(), &w, &h, &n, 0);
    if (!data)
        throw std::runtime_error("Не загрузилась текстура " + full + ": " + stbi_failure_reason());

    std::shared_ptr<Texture> tex;
    try {
        tex = fromPixels(w, h, n, data, params);
    } catch (...) {
        stbi_image_free(data);
        throw;
    }
    stbi_image_free(data);

    KLOG_INFO("Текстура %s: %dx%d, каналов %d", full.c_str(), w, h, n);
    return tex;
}

std::shared_ptr<Texture> Texture::solid(const glm::vec4& color)
{
    const unsigned char px[4] = {toByte(color.r), toByte(color.g), toByte(color.b), toByte(color.a)};
    TextureParams p;
    p.mipmaps = false;
    return fromPixels(1, 1, 4, px, p);
}

std::shared_ptr<Texture> Texture::checkerboard(int size, int cells, const glm::vec3& a, const glm::vec3& b)
{
    cells = std::max(cells, 1);
    std::vector<unsigned char> px(static_cast<size_t>(size) * size * 3);
    const int cell = std::max(size / cells, 1);

    for (int y = 0; y < size; ++y) {
        for (int x = 0; x < size; ++x) {
            const bool odd = ((x / cell) + (y / cell)) % 2 != 0;
            const glm::vec3& c = odd ? b : a;
            unsigned char* p = &px[(static_cast<size_t>(y) * size + x) * 3];
            p[0] = toByte(c.r);
            p[1] = toByte(c.g);
            p[2] = toByte(c.b);
        }
    }
    return fromPixels(size, size, 3, px.data());
}

Texture::~Texture()
{
    if (m_id)
        glDeleteTextures(1, &m_id);
}

void Texture::bind(unsigned unit) const
{
    glActiveTexture(GL_TEXTURE0 + unit);
    glBindTexture(GL_TEXTURE_2D, m_id);
}

} // namespace kostyl
