#ifndef TSUKUYOMI_STUDIO_MATERIAL_DATA_INCLUDED
#define TSUKUYOMI_STUDIO_MATERIAL_DATA_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Material/TsukuyomiSpecularPBRMath.hlsl"

// Family defines are fixed by ShaderLab, never by material keywords.
// Every pass of a shader has the same buffer layout, including DOTS variants.
#if defined(_STUDIO_SKIN)
#define STUDIO_MATERIAL_PROPERTIES(M, I) \
    M(float4, _BaseMap_ST) \
    M(float4, _BaseMap_TexelSize) \
    I(float4, _BaseColor) \
    I(float4, _EmissionColor) \
    I(float4, _TransmissionColor) \
    I(float, _Cutoff) \
    I(float, _BumpScale) \
    I(float, _BumpTile) \
    I(float, _DetailNormalMapScale) \
    I(float, _DetailNormalMapTile) \
    I(float, TransmissionOcc) \
    I(float, TransmissionShadows) \
    I(float, TransmissionRange) \
    I(float, DynamicPassTransmission) \
    I(float, _MicroShadowOpacity) \
    I(float, _IndirectDiffuseIntensity) \
    I(float, _IndirectSpecularIntensity) \
    I(float, _HorizonOcclusionPower) \
    M(float, _Cull) \
    M(float, _SourceMode) \
    M(float, _SourceGloss) \
    M(float, _SourceGlossWeight) \
    M(float, _PoreStrength) \
    M(float4, _BodyRegionColor) \
    M(float4, _BodyRegionSubColor) \
    M(float4, _NailColor) \
    M(float, _BodyRegionScale) \
    M(float, _BodyRegionGloss) \
    M(float, _BodyRegionBump) \
    M(float, _NailGloss) \
    M(float, _SubsurfaceWeight) \
    M(float, _VeinOpacity) \
    M(float, _WetEffect) \
    M(float, _WetSkinGloss) \
    M(float, _WetStreakGloss) \
    M(float, _WetStreakBump) \
    M(float, _WetRegion) \
    M(float, _WetGlossLevel) \
    M(float, _WetSplatOpacity) \
    M(float, _WetDropsAmount) \
    M(float, _WetDropsScale) \
    M(float, _WetDropOpacity) \
    M(float, _WetDropBump) \
    M(float4, _WetVoronoi) \
    M(float, _FaceBlushStrength) \
    M(float4, _FaceBrowColor) \
    M(float4, _FaceBlushColor) \
    M(float4, _FaceBrowUV) \
    M(float, _FaceBrowRotation) \
    M(float, _FaceBrowGloss) \
    M(float4, _PoreUV) \
    M(float4, _VeinColor) \
    M(float4, _StudioSubColor) \
    M(float4, _StudioSubAdjust) \
    M(float, _StudioAlbedoGain)
TEXTURE2D(_PBRMask);
SAMPLER(sampler_PBRMask);
TEXTURE2D(_DetailNormalMap);
TEXTURE2D(_TransmissionMap);
SAMPLER(sampler_TransmissionMap);
TEXTURE2D(_SourceDetailMap);
TEXTURE2D(_SourceGlossMap);
TEXTURE2D(_RegionMask);
TEXTURE2D(_BodyRegionMap);
TEXTURE2D(_PoreMap);
TEXTURE2D(_SubsurfaceAlbedo);
TEXTURE2D(_WetMap);
SAMPLER(sampler_PoreMap);
SAMPLER(sampler_BodyRegionMap);
SAMPLER(sampler_FaceBrowMap);
TEXTURE2D(_FaceBrowMap);

#elif defined(STUDIO_EYE_SHADOW)
#define STUDIO_MATERIAL_PROPERTIES(M, I) \
    M(float4, _BaseMap_ST) \
    M(float4, _BaseMap_TexelSize) \
    I(float4, _BaseColor) \
    I(float, _Cutoff) \
    M(float, _Cull) \
    M(float, _StudioEyeShadowScale)

#endif

// Declare each property once; generate the buffer and DOTS caches from that list.
#define STUDIO_FIELD(type, name) type name;
#define STUDIO_SKIP(type, name)
CBUFFER_START(UnityPerMaterial)
STUDIO_MATERIAL_PROPERTIES(STUDIO_FIELD, STUDIO_FIELD)
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END
#undef STUDIO_FIELD

#ifdef UNITY_DOTS_INSTANCING_ENABLED
#define STUDIO_DOTS_FIELD(type, name) UNITY_DOTS_INSTANCED_PROP(type, name)
UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
STUDIO_MATERIAL_PROPERTIES(STUDIO_SKIP, STUDIO_DOTS_FIELD)
UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)
#undef STUDIO_DOTS_FIELD
#define STUDIO_CACHE(type, name) static type studio##name;
STUDIO_MATERIAL_PROPERTIES(STUDIO_SKIP, STUDIO_CACHE)
#undef STUDIO_CACHE
void SetupStudioMaterialPropertyCaches()
{
#define STUDIO_CACHE(type, name) studio##name = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(type, name);
    STUDIO_MATERIAL_PROPERTIES(STUDIO_SKIP, STUDIO_CACHE)
#undef STUDIO_CACHE
}
#undef UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES
#define UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES() SetupStudioMaterialPropertyCaches()
#if defined(_STUDIO_SKIN)
#define _BaseColor studio_BaseColor
#define _EmissionColor studio_EmissionColor
#define _TransmissionColor studio_TransmissionColor
#define _Cutoff studio_Cutoff
#define _BumpScale studio_BumpScale
#define _BumpTile studio_BumpTile
#define _DetailNormalMapScale studio_DetailNormalMapScale
#define _DetailNormalMapTile studio_DetailNormalMapTile
#define TransmissionOcc studioTransmissionOcc
#define TransmissionShadows studioTransmissionShadows
#define TransmissionRange studioTransmissionRange
#define DynamicPassTransmission studioDynamicPassTransmission
#define _MicroShadowOpacity studio_MicroShadowOpacity
#define _IndirectDiffuseIntensity studio_IndirectDiffuseIntensity
#define _IndirectSpecularIntensity studio_IndirectSpecularIntensity
#define _HorizonOcclusionPower studio_HorizonOcclusionPower
#elif defined(STUDIO_EYE_SHADOW)
#define _BaseColor studio_BaseColor
#define _Cutoff studio_Cutoff
#endif
#endif
#undef STUDIO_SKIP
#undef STUDIO_MATERIAL_PROPERTIES

#endif
