#ifndef TSUKUYOMI_LIGHTING_DATA_INCLUDED
#define TSUKUYOMI_LIGHTING_DATA_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Debug/Debugging3D.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GlobalIllumination.hlsl"

#if defined(LIGHTMAP_ON)
    #define TSUKUYOMI_DECLARE_LIGHTMAP_OR_SH(lmName, shName, index) float2 lmName : TEXCOORD##index
    #define TSUKUYOMI_OUTPUT_LIGHTMAP_UV(lightmapUV, lightmapScaleOffset, OUT) OUT.xy = lightmapUV.xy * lightmapScaleOffset.xy + lightmapScaleOffset.zw;
    #define TSUKUYOMI_OUTPUT_SH4(absolutePositionWS, normalWS, viewDir, OUT, OUT_OCCLUSION)
#else
    #define TSUKUYOMI_DECLARE_LIGHTMAP_OR_SH(lmName, shName, index) half3 shName : TEXCOORD##index
    #define TSUKUYOMI_OUTPUT_LIGHTMAP_UV(lightmapUV, lightmapScaleOffset, OUT)
    #ifdef USE_APV_PROBE_OCCLUSION
        #define TSUKUYOMI_OUTPUT_SH4(absolutePositionWS, normalWS, viewDir, OUT, OUT_OCCLUSION) OUT.xyz = SampleProbeSHVertex(absolutePositionWS, normalWS, viewDir, OUT_OCCLUSION)
    #else
        #define TSUKUYOMI_OUTPUT_SH4(absolutePositionWS, normalWS, viewDir, OUT, OUT_OCCLUSION) OUT.xyz = SampleProbeSHVertex(absolutePositionWS, normalWS, viewDir)
    #endif
#endif

struct TsukuyomiLightingData
{
    half3 giColor;
    half3 mainLightColor;
    half3 additionalLightsColor;
    half3 vertexLightingColor;
    half3 emissionColor;
};

half3 TsukuyomiCalculateLightingColor(TsukuyomiLightingData lightingData, half3 albedo)
{
    half3 lightingColor = half3(0.0h, 0.0h, 0.0h);

    if (IsOnlyAOLightingFeatureEnabled())
    {
        return lightingData.giColor;
    }

    if (IsLightingFeatureEnabled(DEBUGLIGHTINGFEATUREFLAGS_GLOBAL_ILLUMINATION))
    {
        lightingColor += lightingData.giColor;
    }

    if (IsLightingFeatureEnabled(DEBUGLIGHTINGFEATUREFLAGS_MAIN_LIGHT))
    {
        lightingColor += lightingData.mainLightColor;
    }

    if (IsLightingFeatureEnabled(DEBUGLIGHTINGFEATUREFLAGS_ADDITIONAL_LIGHTS))
    {
        lightingColor += lightingData.additionalLightsColor;
    }

    if (IsLightingFeatureEnabled(DEBUGLIGHTINGFEATUREFLAGS_VERTEX_LIGHTING))
    {
        lightingColor += lightingData.vertexLightingColor;
    }

    lightingColor *= albedo;

    if (IsLightingFeatureEnabled(DEBUGLIGHTINGFEATUREFLAGS_EMISSION))
    {
        lightingColor += lightingData.emissionColor;
    }

    return lightingColor;
}

half4 TsukuyomiCalculateFinalColor(TsukuyomiLightingData lightingData, half alpha)
{
    return half4(TsukuyomiCalculateLightingColor(lightingData, 1.0h), alpha);
}

TsukuyomiLightingData TsukuyomiCreateLightingData(InputData inputData, SurfaceData surfaceData)
{
    TsukuyomiLightingData lightingData;
    lightingData.giColor = inputData.bakedGI;
    lightingData.emissionColor = surfaceData.emission;
    lightingData.vertexLightingColor = half3(0.0h, 0.0h, 0.0h);
    lightingData.mainLightColor = half3(0.0h, 0.0h, 0.0h);
    lightingData.additionalLightsColor = half3(0.0h, 0.0h, 0.0h);
    return lightingData;
}

#endif
