#ifndef TOON_DBUFFER_INCLUDED
#define TOON_DBUFFER_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Toon/ToonSurfaceData.hlsl"

#if defined(_DBUFFER)
void ApplyDecalToSurfaceData(float4 positionCS, inout ToonSurfaceData surfaceData, inout InputData inputData)
{
#ifdef _SPECULAR_SETUP
    half metallic = 0;
    ApplyDecal(positionCS,
        surfaceData.albedo,
        surfaceData.specular,
        inputData.normalWS,
        metallic,
        surfaceData.occlusion,
        surfaceData.smoothness);
#else
    half3 specular = 0;
    ApplyDecal(positionCS,
        surfaceData.albedo,
        specular,
        inputData.normalWS,
        surfaceData.metallic,
        surfaceData.occlusion,
        surfaceData.smoothness);
#endif
}
#endif

#endif
