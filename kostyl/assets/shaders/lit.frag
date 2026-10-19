#version 330 core
#include "common.glsl"

in vec3 vWorldPos;
in vec3 vNormal;
in vec2 vUV;

uniform sampler2D uAlbedoMap;
uniform vec3  uAlbedo;
uniform vec3  uEmissive;
uniform float uSpecular;
uniform float uShininess;
uniform bool  uUnlit;

out vec4 FragColor;

// Blinn-Phong с энергетической нормировкой блика: (n + 8) / 8.
// Без неё узкий блик (большой shininess) получается тусклым, а широкий — пересвеченным.
vec3 shade(vec3 N, vec3 V, vec3 L, vec3 radiance, vec3 albedo)
{
    float NdotL = max(dot(N, L), 0.0);
    if (NdotL <= 0.0)
        return vec3(0.0);

    vec3  H    = normalize(L + V);
    float spec = pow(max(dot(N, H), 0.0), uShininess) * (uShininess + 8.0) / 8.0 * uSpecular;
    return radiance * NdotL * (albedo + vec3(spec));
}

// Затухание: физичное 1/d^2, но плавно обнуляется к радиусу действия (чтобы свет не тянулся бесконечно)
float attenuation(float dist, float range)
{
    float x = dist / range;
    float window = clamp(1.0 - x * x * x * x, 0.0, 1.0);
    return window * window / (dist * dist + 1.0);
}

// ACES (аппроксимация Нарковича): сжимает HDR в 0..1 без грязного клиппинга ярких мест
vec3 tonemapACES(vec3 x)
{
    return clamp((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14), 0.0, 1.0);
}

void main()
{
    vec3 albedo = uAlbedo * texture(uAlbedoMap, vUV).rgb;
    vec3 color;

    if (uUnlit) {
        color = albedo + uEmissive;
    } else {
        vec3 N = normalize(vNormal);
        if (!gl_FrontFacing)
            N = -N;  // двусторонние поверхности освещаем с обеих сторон
        vec3 V = normalize(uCameraPos.xyz - vWorldPos);

        // Полусферический эмбиент: сверху "небо", снизу "земля"
        vec3 ambient = mix(uAmbientGround.rgb, uAmbientSky.rgb, N.y * 0.5 + 0.5);
        color = ambient * albedo + uEmissive;

        if (uSunDirection.w > 0.5)
            color += shade(N, V, normalize(-uSunDirection.xyz), uSunColor.rgb, albedo);

        for (int i = 0; i < uCounts.x; ++i) {
            vec3  toLight = uPointPos[i].xyz - vWorldPos;
            float dist    = length(toLight);
            vec3  L       = toLight / max(dist, 1e-4);
            color += shade(N, V, L, uPointColor[i].rgb * attenuation(dist, uPointPos[i].w), albedo);
        }
    }

    // Экспоненциальный туман: дальние объекты растворяются в цвете неба
    float f = length(uCameraPos.xyz - vWorldPos) * uFog.w;
    float fog = 1.0 - exp(-f * f);
    color = mix(color, uFog.rgb, fog);

    color = tonemapACES(color * uParams.x);
    color = pow(color, vec3(1.0 / 2.2));  // линейное -> sRGB (монитор ждёт гамму)
    FragColor = vec4(color, 1.0);
}
