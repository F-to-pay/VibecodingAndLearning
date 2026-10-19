cmake_minimum_required(VERSION 3.25)
project(Kostyl VERSION 0.1.0 LANGUAGES C CXX)

# ---------------------------------------------------------------------------
#  Общие настройки
# ---------------------------------------------------------------------------
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
set(CMAKE_EXPORT_COMPILE_COMMANDS ON)  # для clangd / VS Code

if(NOT CMAKE_BUILD_TYPE AND NOT CMAKE_CONFIGURATION_TYPES)
    set(CMAKE_BUILD_TYPE Debug CACHE STRING "Тип сборки" FORCE)
endif()

option(KOSTYL_WARNINGS_AS_ERRORS "Считать варнинги ошибками" OFF)
option(KOSTYL_SANITIZE "Собрать с ASan + UBSan (только gcc/clang, не MinGW)" OFF)

# ---------------------------------------------------------------------------
#  Зависимости: GLFW и glm качаются сами, glad и stb лежат в external/
# ---------------------------------------------------------------------------
include(FetchContent)

set(GLFW_BUILD_DOCS     OFF CACHE BOOL "" FORCE)
set(GLFW_BUILD_TESTS    OFF CACHE BOOL "" FORCE)
set(GLFW_BUILD_EXAMPLES OFF CACHE BOOL "" FORCE)
set(GLFW_INSTALL        OFF CACHE BOOL "" FORCE)

FetchContent_Declare(glfw
    URL      https://github.com/glfw/glfw/releases/download/3.4/glfw-3.4.zip
    URL_HASH SHA256=b5ec004b2712fd08e8861dc271428f048775200a2df719ccf575143ba749a3e9
    DOWNLOAD_EXTRACT_TIMESTAMP TRUE
    SYSTEM
)

# glm — только заголовки. Кладём их в .../glm-src/glm, чтобы работал #include <glm/...>,
# а SOURCE_SUBDIR в никуда не даёт CMake собирать glm как библиотеку.
FetchContent_Declare(glm
    URL      https://github.com/g-truc/glm/releases/download/1.0.1/glm-1.0.1-light.zip
    URL_HASH SHA256=9a995de4da09723bd33ef194e6b79818950e5a8f2e154792f02e4615277cfb8d
    DOWNLOAD_EXTRACT_TIMESTAMP TRUE
    SOURCE_DIR    ${FETCHCONTENT_BASE_DIR}/glm-src/glm
    SOURCE_SUBDIR _no_cmake_please_
)

FetchContent_MakeAvailable(glfw glm)

add_library(glm_headers INTERFACE)
target_include_directories(glm_headers SYSTEM INTERFACE ${FETCHCONTENT_BASE_DIR}/glm-src)
target_compile_definitions(glm_headers INTERFACE GLM_FORCE_SILENT_WARNINGS)
add_library(glm::glm ALIAS glm_headers)

# glad: загрузчик функций OpenGL 3.3 core (+ KHR_debug, анизотропка)
add_library(glad STATIC external/glad/src/glad.c)
target_include_directories(glad SYSTEM PUBLIC external/glad/include)
target_link_libraries(glad PUBLIC ${CMAKE_DL_LIBS})

# stb_image / stb_image_write — header-only, реализация в engine/src/StbImpl.cpp
add_library(stb INTERFACE)
target_include_directories(stb SYSTEM INTERFACE external/stb)

# ---------------------------------------------------------------------------
#  Флаги предупреждений и санитайзеры (вешаем на наши таргеты, не на чужие)
# ---------------------------------------------------------------------------
function(kostyl_setup_target target)
    if(MSVC)
        target_compile_options(${target} PRIVATE /W4 /permissive- /utf-8)
        if(KOSTYL_WARNINGS_AS_ERRORS)
            target_compile_options(${target} PRIVATE /WX)
        endif()
    else()
        target_compile_options(${target} PRIVATE -Wall -Wextra -Wpedantic -Wshadow -Wconversion -Wno-sign-conversion)
        if(KOSTYL_WARNINGS_AS_ERRORS)
            target_compile_options(${target} PRIVATE -Werror)
        endif()
        if(KOSTYL_SANITIZE AND NOT MINGW)
            target_compile_options(${target} PRIVATE -fsanitize=address,undefined -fno-omit-frame-pointer)
            target_link_options(${target} PRIVATE -fsanitize=address,undefined)
        endif()
    endif()
endfunction()

add_subdirectory(engine)
add_subdirectory(sandbox)
