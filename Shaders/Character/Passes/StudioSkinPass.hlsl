#ifndef TSUKUYOMI_STUDIO_SKIN_PASS_INCLUDED
#define TSUKUYOMI_STUDIO_SKIN_PASS_INCLUDED

#include TSUKUYOMI_MATERIAL_INPUT
#include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Lit/Passes/TsukuyomiSpecularPBRPassCommon.hlsl"

void StudioInitializeSkinSurface(TsukuyomiSpecularPBRVaryings input, out SurfaceData surfaceData)
{
    StudioOverlayUV=input.studioOverlay.xy;
    StudioVertexColor=float4(input.studioVertexColor,1);
    InitializeTsukuyomiSpecularPBRSurfaceData(input.uv, surfaceData);
    if (_SourceMode>.5 && _SourceMode<1.5)
    {
        half brow=StudioBrowWeight()*_FaceBrowColor.a;
        half blush=SAMPLE_TEXTURE2D(_RegionMask,sampler_LinearRepeat,input.uv).b*_FaceBlushStrength*_FaceBlushColor.a;
        surfaceData.albedo=lerp(surfaceData.albedo,_FaceBrowColor.rgb*_BaseColor.rgb,brow);
        surfaceData.albedo=lerp(surfaceData.albedo,_FaceBlushColor.rgb*_BaseColor.rgb,saturate(blush));
    }
}

#define TSUKUYOMI_SETUP_SURFACE(input, surface) StudioInitializeSkinSurface(input, surface)
#define TSUKUYOMI_MODIFY_SURFACE(surface) surface.albedo *= _StudioAlbedoGain
#define TSUKUYOMI_SKIN_BASE_TEXTURE(uv, surface) (surface.albedo / max(_BaseColor.rgb, float3(0.0001,0.0001,0.0001)))
#define TSUKUYOMI_DIFFUSION_TINT(uv, surface) StudioDiffusionTint(uv)
#include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Lit/Passes/TsukuyomiSpecularPBRPass.hlsl"

#endif
