#ifndef TSUKUYOMI_STUDIO_EYE_SHADOW_INPUT_INCLUDED
#define TSUKUYOMI_STUDIO_EYE_SHADOW_INPUT_INCLUDED

#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Material/StudioMaterialData.hlsl"
void StudioDepthAlpha(float2 uv,float2 screenPixel)
{
    Alpha(SampleAlbedoAlpha(uv,TEXTURE2D_ARGS(_BaseMap,sampler_BaseMap)).a,_BaseColor,_Cutoff);
}

#endif
