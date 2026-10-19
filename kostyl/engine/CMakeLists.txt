add_library(kostyl STATIC
    src/Application.cpp
    src/Assets.cpp
    src/Camera.cpp
    src/Input.cpp
    src/Log.cpp
    src/Mesh.cpp
    src/Primitives.cpp
    src/Renderer.cpp
    src/Scene.cpp
    src/Shader.cpp
    src/StbImpl.cpp
    src/Texture.cpp
    src/Window.cpp
)

target_include_directories(kostyl PUBLIC include)
target_link_libraries(kostyl
    PUBLIC  glfw glad glm::glm
    PRIVATE stb
)

target_compile_definitions(kostyl
    PUBLIC
        GLFW_INCLUDE_NONE                       # GL-заголовки даёт glad, а не GLFW
        GLM_ENABLE_EXPERIMENTAL
        $<$<CONFIG:Debug>:KOSTYL_DEBUG>
    PRIVATE
        KOSTYL_ASSETS_DIR="${PROJECT_SOURCE_DIR}/assets"
)

kostyl_setup_target(kostyl)

# stb — чужой код, его варнинги нам не интересны
set_source_files_properties(src/StbImpl.cpp PROPERTIES COMPILE_OPTIONS "-w")
if(MSVC)
    set_source_files_properties(src/StbImpl.cpp PROPERTIES COMPILE_OPTIONS "/w")
endif()
