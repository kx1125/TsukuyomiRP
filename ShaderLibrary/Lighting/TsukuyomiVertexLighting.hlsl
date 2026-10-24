#ifndef TSUKUYOMI_VERTEX_LIGHTING_INCLUDED
#define TSUKUYOMI_VERTEX_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"

float3 SpecularPBRVertexLighting(float3 positionWS, float3 normalWS)
{
    float3 color = 0;
#if defined(_ADDITIONAL_LIGHTS_VERTEX)
    uint pixelLightCount = GetAdditionalLightsCount();
    uint layers = GetMeshRenderingLayer();
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, positionWS);
#if defined(_LIGHT_LAYERS)
        if (IsMatchingLightLayer(light.layerMask, layers))
#endif
            color += light.color * light.distanceAttenuation * saturate(dot(normalWS, light.direction));
    LIGHT_LOOP_END
#endif
    return color;
}

#endif
