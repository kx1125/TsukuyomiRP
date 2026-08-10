#ifndef GLASS_INPUT_INCLUDED
#define GLASS_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
float4 _BaseMap_TexelSize;
half4 _BaseColor;
half _Roughness;
half _BumpScale;
half _RefractionEnabled;
half _IndexOfRefraction;
half _Thickness;
half _Absorption;
half _MatCapIntensity;
half _ClearCoatMask;
half _ClearCoatSmoothness;
half _MicroShadowOpacity;
half _RoughDiffuseStrength;
half _IndirectSpecularFGDStrength;
half _IndirectDiffuseIntensity;
half _IndirectSpecularIntensity;
half _EnvironmentReflectionRange;
half _EnvironmentReflectionSharpness;
half _HorizonOcclusionPower;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END

inline void InitializeGlassSurfaceData(
    float2 uv,
    out SurfaceData surfaceData,
    out half3 transmissionTint,
    out half transmissionOpacity)
{
    half4 albedoAlpha = SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
    transmissionTint = albedoAlpha.rgb * _BaseColor.rgb;
    transmissionOpacity = saturate(albedoAlpha.a * _BaseColor.a);

    surfaceData = (SurfaceData)0;
    surfaceData.alpha = 1.0h;
    surfaceData.albedo = half3(0.0h, 0.0h, 0.0h);
    surfaceData.metallic = 0.0h;
    surfaceData.specular = half3(0.0h, 0.0h, 0.0h);
    surfaceData.smoothness = saturate(1.0h - _Roughness);
    surfaceData.normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
    surfaceData.occlusion = 1.0h;
    surfaceData.emission = half3(0.0h, 0.0h, 0.0h);
    surfaceData.clearCoatMask = _ClearCoatMask;
    surfaceData.clearCoatSmoothness = _ClearCoatSmoothness;
}

#endif
