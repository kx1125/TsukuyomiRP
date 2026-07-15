#ifndef TSUKUYOMI_PBR_INPUT_INCLUDED
#define TSUKUYOMI_PBR_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/SurfaceType.hlsl"

// Keep the material constant buffer stable across variants for SRP Batcher.
CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
float4 _BaseMap_TexelSize;
half4 _BaseColor;
half4 _EmissionColor;
half _Cutoff;
half _Roughness;
half _Metallic;
half _BumpScale;
half _OcclusionStrength;
half _ClearCoatMask;
half _ClearCoatSmoothness;
half _MicroShadowOpacity;
half _RoughDiffuseStrength;
half _IndirectSpecularFGDStrength;
half _IndirectDiffuseIntensity;
half _IndirectSpecularIntensity;
half _HorizonOcclusionPower;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END

#ifdef UNITY_DOTS_INSTANCING_ENABLED

UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
    UNITY_DOTS_INSTANCED_PROP(float4, _BaseColor)
    UNITY_DOTS_INSTANCED_PROP(float4, _EmissionColor)
    UNITY_DOTS_INSTANCED_PROP(float, _Cutoff)
    UNITY_DOTS_INSTANCED_PROP(float, _Roughness)
    UNITY_DOTS_INSTANCED_PROP(float, _Metallic)
    UNITY_DOTS_INSTANCED_PROP(float, _BumpScale)
    UNITY_DOTS_INSTANCED_PROP(float, _OcclusionStrength)
    UNITY_DOTS_INSTANCED_PROP(float, _ClearCoatMask)
    UNITY_DOTS_INSTANCED_PROP(float, _ClearCoatSmoothness)
    UNITY_DOTS_INSTANCED_PROP(float, _MicroShadowOpacity)
    UNITY_DOTS_INSTANCED_PROP(float, _RoughDiffuseStrength)
    UNITY_DOTS_INSTANCED_PROP(float, _IndirectSpecularFGDStrength)
    UNITY_DOTS_INSTANCED_PROP(float, _IndirectDiffuseIntensity)
    UNITY_DOTS_INSTANCED_PROP(float, _IndirectSpecularIntensity)
    UNITY_DOTS_INSTANCED_PROP(float, _HorizonOcclusionPower)
UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)

static float4 unity_DOTS_Sampled_BaseColor;
static float4 unity_DOTS_Sampled_EmissionColor;
static float unity_DOTS_Sampled_Cutoff;
static float unity_DOTS_Sampled_Roughness;
static float unity_DOTS_Sampled_Metallic;
static float unity_DOTS_Sampled_BumpScale;
static float unity_DOTS_Sampled_OcclusionStrength;
static float unity_DOTS_Sampled_ClearCoatMask;
static float unity_DOTS_Sampled_ClearCoatSmoothness;
static float unity_DOTS_Sampled_MicroShadowOpacity;
static float unity_DOTS_Sampled_RoughDiffuseStrength;
static float unity_DOTS_Sampled_IndirectSpecularFGDStrength;
static float unity_DOTS_Sampled_IndirectDiffuseIntensity;
static float unity_DOTS_Sampled_IndirectSpecularIntensity;
static float unity_DOTS_Sampled_HorizonOcclusionPower;

void SetupDOTSTsukuyomiPBRMaterialPropertyCaches()
{
    unity_DOTS_Sampled_BaseColor = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _BaseColor);
    unity_DOTS_Sampled_EmissionColor = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _EmissionColor);
    unity_DOTS_Sampled_Cutoff = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _Cutoff);
    unity_DOTS_Sampled_Roughness = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _Roughness);
    unity_DOTS_Sampled_Metallic = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _Metallic);
    unity_DOTS_Sampled_BumpScale = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _BumpScale);
    unity_DOTS_Sampled_OcclusionStrength = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _OcclusionStrength);
    unity_DOTS_Sampled_ClearCoatMask = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _ClearCoatMask);
    unity_DOTS_Sampled_ClearCoatSmoothness = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _ClearCoatSmoothness);
    unity_DOTS_Sampled_MicroShadowOpacity = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _MicroShadowOpacity);
    unity_DOTS_Sampled_RoughDiffuseStrength = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _RoughDiffuseStrength);
    unity_DOTS_Sampled_IndirectSpecularFGDStrength = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _IndirectSpecularFGDStrength);
    unity_DOTS_Sampled_IndirectDiffuseIntensity = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _IndirectDiffuseIntensity);
    unity_DOTS_Sampled_IndirectSpecularIntensity = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _IndirectSpecularIntensity);
    unity_DOTS_Sampled_HorizonOcclusionPower = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _HorizonOcclusionPower);
}

#undef UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES
#define UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES() SetupDOTSTsukuyomiPBRMaterialPropertyCaches()

#define _BaseColor unity_DOTS_Sampled_BaseColor
#define _EmissionColor unity_DOTS_Sampled_EmissionColor
#define _Cutoff unity_DOTS_Sampled_Cutoff
#define _Roughness unity_DOTS_Sampled_Roughness
#define _Metallic unity_DOTS_Sampled_Metallic
#define _BumpScale unity_DOTS_Sampled_BumpScale
#define _OcclusionStrength unity_DOTS_Sampled_OcclusionStrength
#define _ClearCoatMask unity_DOTS_Sampled_ClearCoatMask
#define _ClearCoatSmoothness unity_DOTS_Sampled_ClearCoatSmoothness
#define _MicroShadowOpacity unity_DOTS_Sampled_MicroShadowOpacity
#define _RoughDiffuseStrength unity_DOTS_Sampled_RoughDiffuseStrength
#define _IndirectSpecularFGDStrength unity_DOTS_Sampled_IndirectSpecularFGDStrength
#define _IndirectDiffuseIntensity unity_DOTS_Sampled_IndirectDiffuseIntensity
#define _IndirectSpecularIntensity unity_DOTS_Sampled_IndirectSpecularIntensity
#define _HorizonOcclusionPower unity_DOTS_Sampled_HorizonOcclusionPower

#endif

TEXTURE2D(_RMOE);
SAMPLER(sampler_RMOE);

struct TsukuyomiPBRMaskData
{
    half roughness;
    half metallic;
    half occlusion;
    half emissionMask;
};

TsukuyomiPBRMaskData SampleTsukuyomiPBRMask(float2 uv)
{
    half4 rmoe = SAMPLE_TEXTURE2D(_RMOE, sampler_RMOE, uv);

    TsukuyomiPBRMaskData maskData;
    maskData.roughness = saturate(_Roughness * rmoe.r);
    maskData.metallic = saturate(_Metallic * rmoe.g);
    maskData.occlusion = LerpWhiteTo(rmoe.b, _OcclusionStrength);
    maskData.emissionMask = rmoe.a;
    return maskData;
}

inline void InitializeTsukuyomiPBRSurfaceData(float2 uv, out SurfaceData outSurfaceData)
{
    half4 albedoAlpha = SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
    TsukuyomiPBRMaskData maskData = SampleTsukuyomiPBRMask(uv);

    outSurfaceData.alpha = Alpha(albedoAlpha.a, _BaseColor, _Cutoff);
    outSurfaceData.albedo = albedoAlpha.rgb * _BaseColor.rgb;
    outSurfaceData.albedo = AlphaModulate(outSurfaceData.albedo, outSurfaceData.alpha);
    outSurfaceData.metallic = maskData.metallic;
    outSurfaceData.specular = half3(0.0h, 0.0h, 0.0h);
    outSurfaceData.smoothness = saturate(1.0h - maskData.roughness);
    outSurfaceData.normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
    outSurfaceData.occlusion = maskData.occlusion;

    outSurfaceData.emission = _EmissionColor.rgb * maskData.emissionMask;
    outSurfaceData.clearCoatMask = _ClearCoatMask;
    outSurfaceData.clearCoatSmoothness = _ClearCoatSmoothness;
}

// Shared alias for future Tsukuyomi materials that pack roughness/metallic/AO/emission differently.
inline void InitializeTsukuyomiSurfaceData(float2 uv, out SurfaceData outSurfaceData)
{
    InitializeTsukuyomiPBRSurfaceData(uv, outSurfaceData);
}

#endif
