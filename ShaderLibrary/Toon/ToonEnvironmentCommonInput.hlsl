#ifndef TOON_ENVIRONMENT_COMMON_INPUT_INCLUDED
#define TOON_ENVIRONMENT_COMMON_INPUT_INCLUDED

// Shared auxiliary passes reuse these material constants and samplers.
// Include this file first in every pass to keep UnityPerMaterial identical.
#define TSUKUYOMI_PBR_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/SurfaceType.hlsl"
#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Toon/ToonSurfaceData.hlsl"

// Keep the material constant buffer stable across variants for SRP Batcher.
CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
float4 _BaseMap_TexelSize;
float4 _EnvironmentMap_TexelSize;
float4 _NoiseMap_ST;
half4 _BaseColor;
half4 _EmissionColor;
half _Cutoff;
half _Roughness;
half _Metallic;
half _BumpScale;
half _OcclusionStrength;
half _ClearCoatMask;
half _ClearCoatSmoothness;

half _RoughDiffuseStrength;
half _IndirectSpecularFGDStrength;
half _IndirectDiffuseIntensity;
half _IndirectSpecularIntensity;
half _HorizonOcclusionPower;

half _Fsr3ReactiveScale;
half _Fsr3CompositionScale;

half4 _ShadowColor;
half _ShadowPosition;
half _ShadowSmoothness;
half4 _SpecularColor;
half _SpecularPosition;
half _SpecularSmoothness;
half4 _EdgeLightColor;
half _EdgeLightPosition;
half _EdgeLightSmoothness;
float4 _RimOriginBias;
half4 _RimColor;
half _RimWidth;
half _RimNoiseStrength;
float _RimReferenceDistance;
half _RimDistanceFade;
half _RimPosition;
half _RimSmoothness;
half _FresnelPosition;
half _FresnelSmoothness;
half4 _FresnelColor;
half4 _OutlineColor;
float _OutlineWidth;
float _OutlineNormalSource;
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
    UNITY_DOTS_INSTANCED_PROP(float, _RoughDiffuseStrength)
    UNITY_DOTS_INSTANCED_PROP(float, _IndirectSpecularFGDStrength)
    UNITY_DOTS_INSTANCED_PROP(float, _IndirectDiffuseIntensity)
    UNITY_DOTS_INSTANCED_PROP(float, _IndirectSpecularIntensity)
    UNITY_DOTS_INSTANCED_PROP(float, _HorizonOcclusionPower)
    UNITY_DOTS_INSTANCED_PROP(float, _Fsr3ReactiveScale)
    UNITY_DOTS_INSTANCED_PROP(float, _Fsr3CompositionScale)
    UNITY_DOTS_INSTANCED_PROP(float4, _ShadowColor)
    UNITY_DOTS_INSTANCED_PROP(float, _ShadowPosition)
    UNITY_DOTS_INSTANCED_PROP(float, _ShadowSmoothness)
    UNITY_DOTS_INSTANCED_PROP(float4, _SpecularColor)
    UNITY_DOTS_INSTANCED_PROP(float, _SpecularPosition)
    UNITY_DOTS_INSTANCED_PROP(float, _SpecularSmoothness)
    UNITY_DOTS_INSTANCED_PROP(float4, _OutlineColor)
    UNITY_DOTS_INSTANCED_PROP(float, _OutlineWidth)
    UNITY_DOTS_INSTANCED_PROP(float, _OutlineNormalSource)
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
static float unity_DOTS_Sampled_RoughDiffuseStrength;
static float unity_DOTS_Sampled_IndirectSpecularFGDStrength;
static float unity_DOTS_Sampled_IndirectDiffuseIntensity;
static float unity_DOTS_Sampled_IndirectSpecularIntensity;
static float unity_DOTS_Sampled_HorizonOcclusionPower;
static float unity_DOTS_Sampled_Fsr3ReactiveScale;
static float unity_DOTS_Sampled_Fsr3CompositionScale;
static float4 unity_DOTS_Sampled_ShadowColor;
static float unity_DOTS_Sampled_ShadowPosition;
static float unity_DOTS_Sampled_ShadowSmoothness;
static float4 unity_DOTS_Sampled_SpecularColor;
static float unity_DOTS_Sampled_SpecularPosition;
static float unity_DOTS_Sampled_SpecularSmoothness;
static float4 unity_DOTS_Sampled_OutlineColor;
static float unity_DOTS_Sampled_OutlineWidth;
static float unity_DOTS_Sampled_OutlineNormalSource;

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
    unity_DOTS_Sampled_RoughDiffuseStrength = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _RoughDiffuseStrength);
    unity_DOTS_Sampled_IndirectSpecularFGDStrength = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _IndirectSpecularFGDStrength);
    unity_DOTS_Sampled_IndirectDiffuseIntensity = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _IndirectDiffuseIntensity);
    unity_DOTS_Sampled_IndirectSpecularIntensity = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _IndirectSpecularIntensity);
    unity_DOTS_Sampled_HorizonOcclusionPower = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _HorizonOcclusionPower);
    unity_DOTS_Sampled_Fsr3ReactiveScale = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _Fsr3ReactiveScale);
    unity_DOTS_Sampled_Fsr3CompositionScale = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _Fsr3CompositionScale);
    unity_DOTS_Sampled_ShadowColor = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _ShadowColor);
    unity_DOTS_Sampled_ShadowPosition = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _ShadowPosition);
    unity_DOTS_Sampled_ShadowSmoothness = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _ShadowSmoothness);
    unity_DOTS_Sampled_SpecularColor = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _SpecularColor);
    unity_DOTS_Sampled_SpecularPosition = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _SpecularPosition);
    unity_DOTS_Sampled_SpecularSmoothness = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _SpecularSmoothness);
    unity_DOTS_Sampled_OutlineColor = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _OutlineColor);
    unity_DOTS_Sampled_OutlineWidth = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _OutlineWidth);
    unity_DOTS_Sampled_OutlineNormalSource = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _OutlineNormalSource);
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
#define _RoughDiffuseStrength unity_DOTS_Sampled_RoughDiffuseStrength
#define _IndirectSpecularFGDStrength unity_DOTS_Sampled_IndirectSpecularFGDStrength
#define _IndirectDiffuseIntensity unity_DOTS_Sampled_IndirectDiffuseIntensity
#define _IndirectSpecularIntensity unity_DOTS_Sampled_IndirectSpecularIntensity
#define _HorizonOcclusionPower unity_DOTS_Sampled_HorizonOcclusionPower
#define _Fsr3ReactiveScale unity_DOTS_Sampled_Fsr3ReactiveScale
#define _Fsr3CompositionScale unity_DOTS_Sampled_Fsr3CompositionScale
#define _ShadowColor unity_DOTS_Sampled_ShadowColor
#define _ShadowPosition unity_DOTS_Sampled_ShadowPosition
#define _ShadowSmoothness unity_DOTS_Sampled_ShadowSmoothness
#define _SpecularColor unity_DOTS_Sampled_SpecularColor
#define _SpecularPosition unity_DOTS_Sampled_SpecularPosition
#define _SpecularSmoothness unity_DOTS_Sampled_SpecularSmoothness
#define _OutlineColor unity_DOTS_Sampled_OutlineColor
#define _OutlineWidth unity_DOTS_Sampled_OutlineWidth
#define _OutlineNormalSource unity_DOTS_Sampled_OutlineNormalSource

#endif

TEXTURE2D(_RMOE);
SAMPLER(sampler_RMOE);
TEXTURE2D(_CurveMap);
SAMPLER(sampler_CurveMap);
TEXTURE2D(_NoiseMap);
SAMPLER(sampler_NoiseMap);
TEXTURE2D(_EnvironmentMap);
SAMPLER(sampler_Trilinear_RepeatU_ClampV);

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

inline void InitializeToonEnvironmentCommonSurfaceData(float2 uv, out ToonSurfaceData outSurfaceData)
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
    half edgeMask = SAMPLE_TEXTURE2D(_CurveMap, sampler_CurveMap, uv).r;
    edgeMask = saturate(edgeMask * 2 - 1);
    half noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, uv * _NoiseMap_ST.xy + _NoiseMap_ST.zw).r;
    outSurfaceData.ToonData = float4(edgeMask, noise, 0.0, 0.0);
}

#endif
