#ifndef TSUKUYOMI_STUDIO_SURFACE_PASS_INCLUDED
#define TSUKUYOMI_STUDIO_SURFACE_PASS_INCLUDED

#include TSUKUYOMI_MATERIAL_INPUT
#include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Lit/Passes/TsukuyomiSpecularPBRPassCommon.hlsl"

// No Studio-specific light loops, BRDF or shadow-receiver overrides.
void TsukuyomiSpecularPBRForwardFragment(TsukuyomiSpecularPBRVaryings input,
    FRONT_FACE_TYPE frontFace : FRONT_FACE_SEMANTIC, out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    if(IsStudioHair()) input.normalWS=normalize(input.normalWS)*IS_FRONT_VFACE(frontFace,1,-1);
    if(IsStudioHair()) StudioHairCoverage(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).a,input.positionCS.xy);
    SurfaceData surface;
    InitializeTsukuyomiSpecularPBRSurfaceData(input.uv,surface);
    if(IsStudioAITCloth()) StudioAITCoverage(input.uv,input.positionCS.xy);
    else if(_SourceMode>4.5) StudioClothCoverage(input.uv,input.positionCS.xy);
    if(_StudioLashes>.5) {
        float mask=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).a;
        surface.alpha=StudioLashAlpha(mask,input.positionCS.xy);
        surface.albedo=mask*_BaseColor.rgb;
    }
    if(IsStudioEye()) {
        float3 viewWS=GetWorldSpaceNormalizeViewDir(input.positionWS);
        float3 viewTS=float3(dot(input.tangentWS.xyz,viewWS),dot(input.studioBitangentWS,viewWS),dot(input.normalWS,viewWS));
        StudioApplyEyeSurface(input.uv,viewTS,surface);
    }
#if defined(LOD_FADE_CROSSFADE)
    LODFadeCrossFade(input.positionCS);
#endif
    InputData inputData;
    TsukuyomiSpecularPBRInitializeInputData(input,surface.normalTS,inputData);
    SETUP_DEBUG_TEXTURE_DATA(inputData,UNDO_TRANSFORM_TEX(input.uv,_BaseMap));
#if defined(_DBUFFER)
    ApplyDecalToSurfaceData(input.positionCS,surface,inputData);
#endif
    inputData.normalWS=SpecularPBRRasterNormal(inputData.normalWS,inputData.viewDirectionWS);
    TsukuyomiSpecularPBRInitializeBakedGIData(input,inputData);
    TsukuyomiOpenPBRPrepared prepared=PrepareSpecularPBR(surface,inputData,input,IsStudioHair()?_HairAnisotropy:0);
    TsukuyomiOpenPBRLightingOptions options=TsukuyomiOpenPBRDefaultLightingOptions();
    options.occlusion=surface.occlusion;
    options.microShadowOpacity=_MicroShadowOpacity;
    options.indirectDiffuseIntensity=_IndirectDiffuseIntensity;
    options.indirectSpecularIntensity=_IndirectSpecularIntensity;
    options.horizonOcclusionPower=_HorizonOcclusionPower;
    float3 color=TsukuyomiOpenPBRFragmentBalanced(inputData,prepared,options)+surface.emission;
    outColor=half4(MixFog(color,inputData.fogCoord),surface.alpha);
#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers=EncodeMeshRenderingLayer();
#endif
}

#endif
