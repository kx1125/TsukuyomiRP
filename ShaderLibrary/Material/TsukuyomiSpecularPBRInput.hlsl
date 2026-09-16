#ifndef TSUKUYOMI_SPECULAR_PBR_INPUT_INCLUDED
#define TSUKUYOMI_SPECULAR_PBR_INPUT_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/SurfaceType.hlsl"
#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Material/TsukuyomiSpecularPBRMath.hlsl"

// Same layout for every pass and local keyword variant.
CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
float4 _BaseMap_TexelSize;
float4 _BaseColor;
float4 _EmissionColor;
float4 _OcclusionColor;
float4 _TransmissionColor;
float _MaterialType;
float _Cutoff;
float _Roughness;
float _Metallic;
float _BumpScale;
float _BumpTile;
float _DetailNormalMapScale;
float _DetailNormalMapTile;
float TransmissionOcc;
float TransmissionShadows;
float TransmissionRange;
float DynamicPassTransmission;
float _MicroShadowOpacity;
float _IndirectDiffuseIntensity;
float _IndirectSpecularIntensity;
float _HorizonOcclusionPower;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END

#ifdef UNITY_DOTS_INSTANCING_ENABLED
UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
    UNITY_DOTS_INSTANCED_PROP(float4, _BaseColor)
    UNITY_DOTS_INSTANCED_PROP(float4, _EmissionColor)
    UNITY_DOTS_INSTANCED_PROP(float4, _OcclusionColor)
    UNITY_DOTS_INSTANCED_PROP(float4, _TransmissionColor)
    UNITY_DOTS_INSTANCED_PROP(float, _MaterialType)
    UNITY_DOTS_INSTANCED_PROP(float, _Cutoff)
    UNITY_DOTS_INSTANCED_PROP(float, _Roughness)
    UNITY_DOTS_INSTANCED_PROP(float, _Metallic)
    UNITY_DOTS_INSTANCED_PROP(float, _BumpScale)
    UNITY_DOTS_INSTANCED_PROP(float, _BumpTile)
    UNITY_DOTS_INSTANCED_PROP(float, _DetailNormalMapScale)
    UNITY_DOTS_INSTANCED_PROP(float, _DetailNormalMapTile)
    UNITY_DOTS_INSTANCED_PROP(float, TransmissionOcc)
    UNITY_DOTS_INSTANCED_PROP(float, TransmissionShadows)
    UNITY_DOTS_INSTANCED_PROP(float, TransmissionRange)
    UNITY_DOTS_INSTANCED_PROP(float, DynamicPassTransmission)
    UNITY_DOTS_INSTANCED_PROP(float, _MicroShadowOpacity)
    UNITY_DOTS_INSTANCED_PROP(float, _IndirectDiffuseIntensity)
    UNITY_DOTS_INSTANCED_PROP(float, _IndirectSpecularIntensity)
    UNITY_DOTS_INSTANCED_PROP(float, _HorizonOcclusionPower)
UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)
static float4 specularPBR_BaseColor;
static float4 specularPBR_EmissionColor;
static float4 specularPBR_OcclusionColor;
static float4 specularPBR_TransmissionColor;
static float specularPBR_MaterialType;
static float specularPBR_Cutoff;
static float specularPBR_Roughness;
static float specularPBR_Metallic;
static float specularPBR_BumpScale;
static float specularPBR_BumpTile;
static float specularPBR_DetailNormalMapScale;
static float specularPBR_DetailNormalMapTile;
static float specularPBRTransmissionOcc;
static float specularPBRTransmissionShadows;
static float specularPBRTransmissionRange;
static float specularPBRDynamicPassTransmission;
static float specularPBR_MicroShadowOpacity;
static float specularPBR_IndirectDiffuseIntensity;
static float specularPBR_IndirectSpecularIntensity;
static float specularPBR_HorizonOcclusionPower;
void SetupSpecularPBRMaterialPropertyCaches()
{
    specularPBR_BaseColor = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _BaseColor);
    specularPBR_EmissionColor = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _EmissionColor);
    specularPBR_OcclusionColor = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _OcclusionColor);
    specularPBR_TransmissionColor = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _TransmissionColor);
    specularPBR_MaterialType = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _MaterialType);
    specularPBR_Cutoff = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _Cutoff);
    specularPBR_Roughness = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _Roughness);
    specularPBR_Metallic = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _Metallic);
    specularPBR_BumpScale = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _BumpScale);
    specularPBR_BumpTile = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _BumpTile);
    specularPBR_DetailNormalMapScale = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _DetailNormalMapScale);
    specularPBR_DetailNormalMapTile = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _DetailNormalMapTile);
    specularPBRTransmissionOcc = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, TransmissionOcc);
    specularPBRTransmissionShadows = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, TransmissionShadows);
    specularPBRTransmissionRange = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, TransmissionRange);
    specularPBRDynamicPassTransmission = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, DynamicPassTransmission);
    specularPBR_MicroShadowOpacity = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _MicroShadowOpacity);
    specularPBR_IndirectDiffuseIntensity = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _IndirectDiffuseIntensity);
    specularPBR_IndirectSpecularIntensity = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _IndirectSpecularIntensity);
    specularPBR_HorizonOcclusionPower = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _HorizonOcclusionPower);
}
#undef UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES
#define UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES() SetupSpecularPBRMaterialPropertyCaches()
#define _BaseColor specularPBR_BaseColor
#define _EmissionColor specularPBR_EmissionColor
#define _OcclusionColor specularPBR_OcclusionColor
#define _TransmissionColor specularPBR_TransmissionColor
#define _MaterialType specularPBR_MaterialType
#define _Cutoff specularPBR_Cutoff
#define _Roughness specularPBR_Roughness
#define _Metallic specularPBR_Metallic
#define _BumpScale specularPBR_BumpScale
#define _BumpTile specularPBR_BumpTile
#define _DetailNormalMapScale specularPBR_DetailNormalMapScale
#define _DetailNormalMapTile specularPBR_DetailNormalMapTile
#define TransmissionOcc specularPBRTransmissionOcc
#define TransmissionShadows specularPBRTransmissionShadows
#define TransmissionRange specularPBRTransmissionRange
#define DynamicPassTransmission specularPBRDynamicPassTransmission
#define _MicroShadowOpacity specularPBR_MicroShadowOpacity
#define _IndirectDiffuseIntensity specularPBR_IndirectDiffuseIntensity
#define _IndirectSpecularIntensity specularPBR_IndirectSpecularIntensity
#define _HorizonOcclusionPower specularPBR_HorizonOcclusionPower
#endif

TEXTURE2D(_PBRMask); SAMPLER(sampler_PBRMask);
TEXTURE2D(_OcclusionMap); SAMPLER(sampler_OcclusionMap);
TEXTURE2D(_DetailNormalMap); SAMPLER(sampler_DetailNormalMap);
TEXTURE2D(_TransmissionMap); SAMPLER(sampler_TransmissionMap);

half4 SampleSpecularPBRMask(float2 uv) { return SAMPLE_TEXTURE2D(_PBRMask, sampler_PBRMask, uv); }
half3 SampleSpecularPBRNormal(float2 uv)
{
    half3 normalTS = half3(0,0,1);
#if defined(_NORMALMAP)
    normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv * max(_BumpTile, 0.0001)), _BumpScale);
#endif
#if defined(ENABLE_DETAIL_NORMALMAP)
    half3 detail = UnpackNormalScale(SAMPLE_TEXTURE2D(_DetailNormalMap, sampler_DetailNormalMap,
        uv * max(_DetailNormalMapTile, 0.0001)), _DetailNormalMapScale);
    normalTS = BlendNormalRNM(normalTS, detail);
#endif
    return normalTS;
}

void InitializeTsukuyomiSpecularPBRSurfaceData(float2 uv, out SurfaceData s)
{
    s = (SurfaceData)0;
    half4 base = SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
    half4 mask = SampleSpecularPBRMask(uv);
    s.alpha = Alpha(base.a, _BaseColor, _Cutoff);
    s.albedo = base.rgb * _BaseColor.rgb;
    s.metallic = saturate(mask.b * _Metallic);
    s.specular = saturate(mask.r).xxx;
    s.smoothness = 1.0 - SpecularPBRRoughness(mask.g, _Roughness);
    s.normalTS = SampleSpecularPBRNormal(uv);
    s.occlusion = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv).r;
#if defined(_EMISSION)
    s.emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb * _EmissionColor.rgb;
#endif
}
#endif
