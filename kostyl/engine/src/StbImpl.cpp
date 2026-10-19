// Единственное место, где живёт реализация stb. Варнинги для этого файла выключены в CMake.

// На Windows пути передаются в stb как UTF-8 (иначе кириллица в пути ломает загрузку)
#define STBI_WINDOWS_UTF8
#define STBIW_WINDOWS_UTF8

#define STB_IMAGE_IMPLEMENTATION
#include <stb_image.h>

#define STB_IMAGE_WRITE_IMPLEMENTATION
#include <stb_image_write.h>
