#ifndef CHARACTER_INPUT_INCLUDED
#define CHARACTER_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/SurfaceType.hlsl"

CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
float4 _BaseMap_TexelSize;
half4 _BaseColor;
half4 _EmissionColor;

half _EmotionID;
half _EmotionInt;
half _Cutoff;
half _Roughness;
half _Metallic;
half _BumpScale;
half _OcclusionStrength;
half _MicroShadowOpacity;
half _RoughDiffuseStrength;
half _IndirectSpecularFGDStrength;
half _IndirectDiffuseIntensity;
half _IndirectSpecularIntensity;
half _HorizonOcclusionPower;
half _Fsr3ReactiveScale;
half _Fsr3CompositionScale;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;

half _FresnelStrengthMax;
half _FresnelStrengthMin;
half _DiffuseOffset;
half _EmissiveParaInt;
half4 _FresnelColor;
half4 _ShadowColor;
half4 _RimColor;
float4 _FakeRimLight;
half4 _RimSmoothness;

float4x4 _AnchorTRS;
CBUFFER_END

// #ifdef UNITY_DOTS_INSTANCING_ENABLED
//
// UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
//     UNITY_DOTS_INSTANCED_PROP(float4, _BaseColor)
//     UNITY_DOTS_INSTANCED_PROP(float4, _EmissionColor)
//     UNITY_DOTS_INSTANCED_PROP(float, _Cutoff)
//     UNITY_DOTS_INSTANCED_PROP(float, _Roughness)
//     UNITY_DOTS_INSTANCED_PROP(float, _Metallic)
//     UNITY_DOTS_INSTANCED_PROP(float, _BumpScale)
//     UNITY_DOTS_INSTANCED_PROP(float, _OcclusionStrength)
//     UNITY_DOTS_INSTANCED_PROP(float, _ClearCoatMask)
//     UNITY_DOTS_INSTANCED_PROP(float, _ClearCoatSmoothness)
//     UNITY_DOTS_INSTANCED_PROP(float, _MicroShadowOpacity)
//     UNITY_DOTS_INSTANCED_PROP(float, _RoughDiffuseStrength)
//     UNITY_DOTS_INSTANCED_PROP(float, _IndirectSpecularFGDStrength)
//     UNITY_DOTS_INSTANCED_PROP(float, _IndirectDiffuseIntensity)
//     UNITY_DOTS_INSTANCED_PROP(float, _IndirectSpecularIntensity)
//     UNITY_DOTS_INSTANCED_PROP(float, _HorizonOcclusionPower)
//     UNITY_DOTS_INSTANCED_PROP(float, _Fsr3ReactiveScale)
//     UNITY_DOTS_INSTANCED_PROP(float, _Fsr3CompositionScale)
// UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)
//
// static float4 unity_DOTS_Sampled_BaseColor;
// static float4 unity_DOTS_Sampled_EmissionColor;
// static float unity_DOTS_Sampled_Cutoff;
// static float unity_DOTS_Sampled_Roughness;
// static float unity_DOTS_Sampled_Metallic;
// static float unity_DOTS_Sampled_BumpScale;
// static float unity_DOTS_Sampled_OcclusionStrength;
// static float unity_DOTS_Sampled_ClearCoatMask;
// static float unity_DOTS_Sampled_ClearCoatSmoothness;
// static float unity_DOTS_Sampled_MicroShadowOpacity;
// static float unity_DOTS_Sampled_RoughDiffuseStrength;
// static float unity_DOTS_Sampled_IndirectSpecularFGDStrength;
// static float unity_DOTS_Sampled_IndirectDiffuseIntensity;
// static float unity_DOTS_Sampled_IndirectSpecularIntensity;
// static float unity_DOTS_Sampled_HorizonOcclusionPower;
// static float unity_DOTS_Sampled_Fsr3ReactiveScale;
// static float unity_DOTS_Sampled_Fsr3CompositionScale;
//
// void SetupDOTSTsukuyomiPBRMaterialPropertyCaches()
// {
//     unity_DOTS_Sampled_BaseColor = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _BaseColor);
//     unity_DOTS_Sampled_EmissionColor = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _EmissionColor);
//     unity_DOTS_Sampled_Cutoff = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _Cutoff);
//     unity_DOTS_Sampled_Roughness = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _Roughness);
//     unity_DOTS_Sampled_Metallic = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _Metallic);
//     unity_DOTS_Sampled_BumpScale = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _BumpScale);
//     unity_DOTS_Sampled_OcclusionStrength = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _OcclusionStrength);
//     unity_DOTS_Sampled_ClearCoatMask = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _ClearCoatMask);
//     unity_DOTS_Sampled_ClearCoatSmoothness = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _ClearCoatSmoothness);
//     unity_DOTS_Sampled_MicroShadowOpacity = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _MicroShadowOpacity);
//     unity_DOTS_Sampled_RoughDiffuseStrength = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _RoughDiffuseStrength);
//     unity_DOTS_Sampled_IndirectSpecularFGDStrength = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _IndirectSpecularFGDStrength);
//     unity_DOTS_Sampled_IndirectDiffuseIntensity = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _IndirectDiffuseIntensity);
//     unity_DOTS_Sampled_IndirectSpecularIntensity = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _IndirectSpecularIntensity);
//     unity_DOTS_Sampled_HorizonOcclusionPower = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _HorizonOcclusionPower);
//     unity_DOTS_Sampled_Fsr3ReactiveScale = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _Fsr3ReactiveScale);
//     unity_DOTS_Sampled_Fsr3CompositionScale = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _Fsr3CompositionScale);
// }
//
// #undef UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES
// #define UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES() SetupDOTSTsukuyomiPBRMaterialPropertyCaches()
//
// #define _BaseColor unity_DOTS_Sampled_BaseColor
// #define _EmissionColor unity_DOTS_Sampled_EmissionColor
// #define _Cutoff unity_DOTS_Sampled_Cutoff
// #define _Roughness unity_DOTS_Sampled_Roughness
// #define _Metallic unity_DOTS_Sampled_Metallic
// #define _BumpScale unity_DOTS_Sampled_BumpScale
// #define _OcclusionStrength unity_DOTS_Sampled_OcclusionStrength
// #define _ClearCoatMask unity_DOTS_Sampled_ClearCoatMask
// #define _ClearCoatSmoothness unity_DOTS_Sampled_ClearCoatSmoothness
// #define _MicroShadowOpacity unity_DOTS_Sampled_MicroShadowOpacity
// #define _RoughDiffuseStrength unity_DOTS_Sampled_RoughDiffuseStrength
// #define _IndirectSpecularFGDStrength unity_DOTS_Sampled_IndirectSpecularFGDStrength
// #define _IndirectDiffuseIntensity unity_DOTS_Sampled_IndirectDiffuseIntensity
// #define _IndirectSpecularIntensity unity_DOTS_Sampled_IndirectSpecularIntensity
// #define _HorizonOcclusionPower unity_DOTS_Sampled_HorizonOcclusionPower
// #define _Fsr3ReactiveScale unity_DOTS_Sampled_Fsr3ReactiveScale
// #define _Fsr3CompositionScale unity_DOTS_Sampled_Fsr3CompositionScale
//
// #endif

TEXTURE2D(_RMOE);
SAMPLER(sampler_RMOE);
TEXTURE2D(_EmotionMap);
TEXTURE2D(_FaceMask);
TEXTURE2D(_FaceSDFMap);
TEXTURE2D(_DiffuseRampMap);
TEXTURE2D(_EmissiveMap);

struct TsukuyomiPBRMaskData
{
    half roughness;
    half metallic;
    half occlusion;
    half emissionMask;
};

struct CharacterFaceData
{
    half4 faceMask;
    float3 normalBlend;
    float2 uv;
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

inline void InitializeCharacterFaceSurfaceData(float2 uv, out SurfaceData outSurfaceData)
{
    half4 albedoAlpha = SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
    half2 expressionOffset = half2((_EmotionID - 2.0 * trunc(_EmotionID / 2.0)) * 0.5, floor(_EmotionID * 0.5) * 0.5);
    half4 expressionSample = SAMPLE_TEXTURE2D(_EmotionMap, sampler_LinearClamp, expressionOffset + uv * 0.5);
    half3 albedoLinear = lerp(albedoAlpha.rgb, expressionSample.xyz, expressionSample.a * _EmotionInt);

    outSurfaceData.alpha = Alpha(albedoAlpha.a, _BaseColor, _Cutoff);
    outSurfaceData.albedo = albedoLinear * _BaseColor.rgb;
    outSurfaceData.albedo = AlphaModulate(outSurfaceData.albedo, outSurfaceData.alpha);
    outSurfaceData.metallic = _Metallic;
    outSurfaceData.specular = half3(0.0h, 0.0h, 0.0h);
    outSurfaceData.smoothness = saturate(1.0h - _Roughness);
    outSurfaceData.normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
    outSurfaceData.occlusion = 1;

    outSurfaceData.emission = _EmissionColor.rgb;
    outSurfaceData.clearCoatMask = 0;
    outSurfaceData.clearCoatSmoothness = 0;
}

inline void InitializeTsukuyomiSurfaceData(float2 uv, out SurfaceData outSurfaceData)
{
    InitializeCharacterFaceSurfaceData(uv, outSurfaceData);
}

#endif
