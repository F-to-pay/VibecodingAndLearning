// Общие данные кадра. Раскладка ДОЛЖНА совпадать со struct FrameData в engine/src/Renderer.cpp.
// Подключать через:  #include "common.glsl"

#define MAX_POINT_LIGHTS 8

layout(std140) uniform FrameData {
    mat4 uView;
    mat4 uProjection;
    mat4 uViewProjection;
    vec4 uCameraPos;      // xyz
    vec4 uSunDirection;   // xyz — куда светит, w — 1 если включено
    vec4 uSunColor;       // rgb * intensity
    vec4 uAmbientSky;
    vec4 uAmbientGround;
    vec4 uFog;            // rgb — цвет, w — плотность
    vec4 uParams;         // x — экспозиция, y — время в секундах
    vec4 uPointPos[MAX_POINT_LIGHTS];    // xyz, w — радиус действия
    vec4 uPointColor[MAX_POINT_LIGHTS];  // rgb * intensity
    ivec4 uCounts;        // x — число точечных источников
};
