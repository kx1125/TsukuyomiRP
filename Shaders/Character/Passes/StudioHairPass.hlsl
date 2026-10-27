#ifndef TSUKUYOMI_STUDIO_HAIR_PASS_INCLUDED
#define TSUKUYOMI_STUDIO_HAIR_PASS_INCLUDED

#include "Packages/tsukuyomi.render-pipelines.universal/Shaders/OpenPBR/OpenPBRForwardPass.hlsl"
#if defined(_STUDIO_HAIR_LIGHTING)
#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Material/TsukuyomiSpecularPBRMath.hlsl"
#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Lighting/StudioHairLighting.hlsl"
#endif

void StudioHairForwardFragment(TsukuyomiPBRVaryings input,
    FRONT_FACE_TYPE frontFace : FRONT_FACE_SEMANTIC, out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
#if !defined(_STUDIO_HAIR_LIGHTING)
    TsukuyomiPBRForwardFragment(input,outColor
#ifdef _WRITE_RENDERING_LAYERS
        ,outRenderingLayers
#endif
    );
#else
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    input.normalWS=normalize(input.normalWS)*IS_FRONT_VFACE(frontFace,1,-1);
    SurfaceData surface;
    InitializeTsukuyomiPBRSurfaceData(input.uv,surface);
#if defined(LOD_FADE_CROSSFADE)
    LODFadeCrossFade(input.positionCS);
#endif
    InputData inputData;
    TsukuyomiPBRInitializeInputData(input,surface.normalTS,inputData);
    SETUP_DEBUG_TEXTURE_DATA(inputData,UNDO_TRANSFORM_TEX(input.uv,_BaseMap));
#if defined(_DBUFFER)
    ApplyDecalToSurfaceData(input.positionCS,surface,inputData);
#endif
    inputData.normalWS=SpecularPBRRasterNormal(inputData.normalWS,inputData.viewDirectionWS);
    TsukuyomiPBRInitializeBakedGIData(input,inputData);
    float3 color=StudioHairLighting(inputData,input.uv)+surface.emission;
    outColor=half4(MixFog(color,inputData.fogCoord),OutputAlpha(surface.alpha,IsSurfaceTypeTransparent()));
#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers=EncodeMeshRenderingLayer();
#endif
#endif
}

#endif
