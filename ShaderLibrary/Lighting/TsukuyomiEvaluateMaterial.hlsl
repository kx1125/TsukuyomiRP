#ifndef TSUKUYOMI_EVALUATE_MATERIAL_INCLUDED
#define TSUKUYOMI_EVALUATE_MATERIAL_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonLighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/AmbientOcclusion.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Debug/Debugging3D.hlsl"

#ifndef TSUKUYOMI_SPECULAR_OCCLUSION_BLEND
    #define TSUKUYOMI_SPECULAR_OCCLUSION_BLEND half(1.0)
#endif

#ifndef TSUKUYOMI_AMBIENT_OCCLUSION_INTENSITY
    #define TSUKUYOMI_AMBIENT_OCCLUSION_INTENSITY half(1.0)
#endif

#ifndef TSUKUYOMI_NOT_RECEIVE_OCCLUSION
    #define TSUKUYOMI_NOT_RECEIVE_OCCLUSION 0
#endif

#if defined(_SURFACE_TYPE_TRANSPARENT) && defined(_TRANSPARENT_WRITE_DEPTH)
    #define TSUKUYOMI_TRANSPARENT_RECEIVE_OCCLUSION 1
#else
    #define TSUKUYOMI_TRANSPARENT_RECEIVE_OCCLUSION 0
#endif

#if !defined(_SURFACE_TYPE_TRANSPARENT) || TSUKUYOMI_TRANSPARENT_RECEIVE_OCCLUSION
    #define TSUKUYOMI_SURFACE_TYPE_RECEIVE_OCCLUSION 1
#else
    #define TSUKUYOMI_SURFACE_TYPE_RECEIVE_OCCLUSION 0
#endif

struct TsukuyomiBRDFOcclusionFactor
{
    half3 indirectAmbientOcclusion;
    half3 directAmbientOcclusion;
    half3 indirectSpecularOcclusion;
    half3 directSpecularOcclusion;
};

void TsukuyomiSpecularOcclusionMultiBounce(inout TsukuyomiBRDFOcclusionFactor aoFactor, float NdotV,
    float perceptualRoughness, float specularOcclusionFromData, float3 fresnel0)
{
    half roughness = PerceptualRoughnessToRoughness(perceptualRoughness);
    half indirectSpecularOcclusion = lerp(half(1.0), GetSpecularOcclusionFromAmbientOcclusion(ClampNdotV(NdotV), aoFactor.indirectAmbientOcclusion.x, roughness), TSUKUYOMI_SPECULAR_OCCLUSION_BLEND);
    half directSpecularOcclusion = lerp(half(1.0), indirectSpecularOcclusion, _AmbientOcclusionParam.w);

    aoFactor.indirectSpecularOcclusion = GTAOMultiBounce(min(specularOcclusionFromData, indirectSpecularOcclusion), fresnel0);
    aoFactor.directSpecularOcclusion = directSpecularOcclusion.xxx;
}

void TsukuyomiDiffuseOcclusionMultiBounce(inout TsukuyomiBRDFOcclusionFactor aoFactor, float ambientOcclusionFromData, float3 diffuseColor)
{
    aoFactor.indirectAmbientOcclusion = GTAOMultiBounce(min(ambientOcclusionFromData, aoFactor.indirectAmbientOcclusion.x), diffuseColor);
    aoFactor.directAmbientOcclusion = aoFactor.directAmbientOcclusion.xxx;
}

TsukuyomiBRDFOcclusionFactor TsukuyomiCreateBRDFOcclusionFactor(AmbientOcclusionFactor aoFactor)
{
    TsukuyomiBRDFOcclusionFactor brdfOcclusionFactor = (TsukuyomiBRDFOcclusionFactor)0;
    brdfOcclusionFactor.directAmbientOcclusion = aoFactor.directAmbientOcclusion.xxx;
    brdfOcclusionFactor.indirectAmbientOcclusion = aoFactor.indirectAmbientOcclusion.xxx;
    brdfOcclusionFactor.directSpecularOcclusion = aoFactor.directAmbientOcclusion.xxx;
    brdfOcclusionFactor.indirectSpecularOcclusion = aoFactor.indirectAmbientOcclusion.xxx;

    if (!IsLightingFeatureEnabled(DEBUGLIGHTINGFEATUREFLAGS_AMBIENT_OCCLUSION))
    {
        brdfOcclusionFactor.directAmbientOcclusion = half3(1.0h, 1.0h, 1.0h);
        brdfOcclusionFactor.indirectAmbientOcclusion = half3(1.0h, 1.0h, 1.0h);
        brdfOcclusionFactor.directSpecularOcclusion = half3(1.0h, 1.0h, 1.0h);
        brdfOcclusionFactor.indirectSpecularOcclusion = half3(1.0h, 1.0h, 1.0h);
    }

    return brdfOcclusionFactor;
}

TsukuyomiBRDFOcclusionFactor TsukuyomiCreateBRDFOcclusionFactorMultiBounce(AmbientOcclusionFactor aoFactor, float NdotV,
    float perceptualRoughness, float ambientOcclusionFromData, float3 diffuseColor,
    float specularOcclusionFromData, float3 fresnel0)
{
    TsukuyomiBRDFOcclusionFactor brdfOcclusionFactor = TsukuyomiCreateBRDFOcclusionFactor(aoFactor);
    TsukuyomiSpecularOcclusionMultiBounce(brdfOcclusionFactor, NdotV, perceptualRoughness, specularOcclusionFromData, fresnel0);
    TsukuyomiDiffuseOcclusionMultiBounce(brdfOcclusionFactor, ambientOcclusionFromData, diffuseColor);

    if (!IsLightingFeatureEnabled(DEBUGLIGHTINGFEATUREFLAGS_AMBIENT_OCCLUSION))
    {
        brdfOcclusionFactor.directAmbientOcclusion = half3(1.0h, 1.0h, 1.0h);
        brdfOcclusionFactor.indirectAmbientOcclusion = half3(1.0h, 1.0h, 1.0h);
        brdfOcclusionFactor.directSpecularOcclusion = half3(1.0h, 1.0h, 1.0h);
        brdfOcclusionFactor.indirectSpecularOcclusion = half3(1.0h, 1.0h, 1.0h);
    }

    return brdfOcclusionFactor;
}

half TsukuyomiSampleAmbientOcclusion(float2 normalizedScreenSpaceUV)
{
    float2 uv = UnityStereoTransformScreenSpaceTex(normalizedScreenSpaceUV);
    return half(SAMPLE_TEXTURE2D_X(_ScreenSpaceOcclusionTexture, sampler_LinearClamp, uv).x);
}

AmbientOcclusionFactor TsukuyomiGetScreenSpaceAmbientOcclusion(float2 normalizedScreenSpaceUV)
{
    AmbientOcclusionFactor aoFactor;

#if defined(_SCREEN_SPACE_OCCLUSION) && TSUKUYOMI_SURFACE_TYPE_RECEIVE_OCCLUSION && !TSUKUYOMI_NOT_RECEIVE_OCCLUSION
    float ssao = saturate(TsukuyomiSampleAmbientOcclusion(normalizedScreenSpaceUV) + (1.0 - _AmbientOcclusionParam.x));
    aoFactor.indirectAmbientOcclusion = ssao;
    aoFactor.directAmbientOcclusion = lerp(half(1.0), ssao, _AmbientOcclusionParam.w * TSUKUYOMI_AMBIENT_OCCLUSION_INTENSITY);
#else
    aoFactor.directAmbientOcclusion = half(1.0);
    aoFactor.indirectAmbientOcclusion = half(1.0);
#endif

#if defined(DEBUG_DISPLAY)
    switch (_DebugLightingMode)
    {
        case DEBUGLIGHTINGMODE_LIGHTING_WITHOUT_NORMAL_MAPS:
            aoFactor.directAmbientOcclusion = 0.5h;
            aoFactor.indirectAmbientOcclusion = 0.5h;
            break;

        case DEBUGLIGHTINGMODE_LIGHTING_WITH_NORMAL_MAPS:
            aoFactor.directAmbientOcclusion *= 0.5h;
            aoFactor.indirectAmbientOcclusion *= 0.5h;
            break;
    }
#endif

    return aoFactor;
}

AmbientOcclusionFactor TsukuyomiCreateAmbientOcclusionFactor(InputData inputData, SurfaceData surfaceData)
{
    AmbientOcclusionFactor aoFactor = TsukuyomiGetScreenSpaceAmbientOcclusion(inputData.normalizedScreenSpaceUV);
    aoFactor.indirectAmbientOcclusion = min(aoFactor.indirectAmbientOcclusion, surfaceData.occlusion);
    return aoFactor;
}

#endif
