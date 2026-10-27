#ifndef TSUKUYOMI_SPECULAR_PBR_PASS_INCLUDED
#define TSUKUYOMI_SPECULAR_PBR_PASS_INCLUDED

#ifndef TSUKUYOMI_MATERIAL_INPUT
#define TSUKUYOMI_MATERIAL_INPUT "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Material/TsukuyomiSpecularPBRInput.hlsl"
#endif
#include TSUKUYOMI_MATERIAL_INPUT
#include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Lit/Passes/TsukuyomiSpecularPBRPassCommon.hlsl"

#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Lighting/TsukuyomiSpecularPBRSSS.hlsl"

// Material adapters override only their inputs; all lighting passes stay shared.
#ifndef TSUKUYOMI_SETUP_SURFACE
#define TSUKUYOMI_SETUP_SURFACE(input, surface) InitializeTsukuyomiSpecularPBRSurfaceData(input.uv, surface)
#endif
#ifndef TSUKUYOMI_MODIFY_SURFACE
#define TSUKUYOMI_MODIFY_SURFACE(surface)
#endif
#ifndef TSUKUYOMI_SKIN_BASE_TEXTURE
#define TSUKUYOMI_SKIN_BASE_TEXTURE(uv, surface) SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb
#endif
#ifndef TSUKUYOMI_DIFFUSION_TINT
#define TSUKUYOMI_DIFFUSION_TINT(uv, surface) lerp(_OcclusionColor.rgb, 1.0, surface.occlusion)
#endif

void SetupSpecularPBR(TsukuyomiSpecularPBRVaryings input, out SurfaceData surfaceData, out InputData inputData)
{
#if defined(TSUKUYOMI_SPECULAR_PBR_SSS_LIGHTING_PASS)
    // SSS quality changes the lighting viewport, while URP screen-space inputs
    // (AO, clustered lights, APV, decals and SSGI) still use camera pixel coordinates.
    input.positionCS.xy *= _TsukuyomiSpecularPBRSSSLightingScale.xy;
#endif
    TSUKUYOMI_SETUP_SURFACE(input, surfaceData);
#if defined(LOD_FADE_CROSSFADE)
    LODFadeCrossFade(input.positionCS);
#endif
    TSUKUYOMI_MODIFY_SURFACE(surfaceData);
    TsukuyomiSpecularPBRInitializeInputData(input, surfaceData.normalTS, inputData);
    SETUP_DEBUG_TEXTURE_DATA(inputData, UNDO_TRANSFORM_TEX(input.uv, _BaseMap));
#if defined(_DBUFFER)
    ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
#endif
    inputData.normalWS = SpecularPBRRasterNormal(inputData.normalWS, inputData.viewDirectionWS);
    TsukuyomiSpecularPBRInitializeBakedGIData(input, inputData);
}

void TsukuyomiSpecularPBRForwardFragment(TsukuyomiSpecularPBRVaryings input,
    out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    SurfaceData surfaceData; InputData inputData;
    SetupSpecularPBR(input, surfaceData, inputData);
    TsukuyomiOpenPBRPrepared p = PrepareSpecularPBR(surfaceData, inputData, input);
    float strength = saturate(SampleSpecularPBRMask(input.uv).r);
    TsukuyomiOpenPBRResponse lit = SpecularPBRLightingHigh(inputData, p, strength, surfaceData.occlusion, 0.0);
    float3 baseTexture = TSUKUYOMI_SKIN_BASE_TEXTURE(input.uv, surfaceData);
    float3 color = SpecularPBRCompositeSkin(lit, baseTexture, saturate(SampleSpecularPBRMask(input.uv).a),
        inputData.positionWS, inputData.normalizedScreenSpaceUV, surfaceData.emission);
    outColor = half4(MixFog(color, inputData.fogCoord), 1.0);
#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

half4 TsukuyomiSpecularPBRMaskFragment(TsukuyomiSpecularPBRVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
#if !defined(_SSS_ON)
    clip(-1);
    return 0;
#else
    Alpha(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a, _BaseColor, _Cutoff);
#if defined(LOD_FADE_CROSSFADE)
    LODFadeCrossFade(input.positionCS);
#endif
    return saturate(SampleSpecularPBRMask(input.uv).a);
#endif
}

half4 TsukuyomiSpecularPBRSSSLightingFragment(TsukuyomiSpecularPBRVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
#if !defined(_SSS_ON)
    clip(-1);
    return 0;
#else
    SurfaceData surfaceData; InputData inputData;
    SetupSpecularPBR(input, surfaceData, inputData);
    float4 mask = SampleSpecularPBRMask(input.uv);
    if (mask.a <= 0.0) return 0;
    SpecularPBRPrepareDiffusionSurface(surfaceData, TSUKUYOMI_DIFFUSION_TINT(input.uv, surfaceData));
    TsukuyomiOpenPBRPrepared p = PrepareSpecularPBR(surfaceData, inputData, input);
    float3 transmission = SpecularPBRSampleTransmission(input.uv, surfaceData.occlusion);
    TsukuyomiOpenPBRResponse lit = SpecularPBRLightingHigh(inputData, p, saturate(mask.r), surfaceData.occlusion, transmission);
    // Mask participates in blur rejection and final blending, never twice in radiance.
    return half4(lit.diffuse, 1.0);
#endif
}

#endif
