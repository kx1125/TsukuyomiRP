#ifndef TSUKUYOMI_PBR_META_PASS_INCLUDED
#define TSUKUYOMI_PBR_META_PASS_INCLUDED

#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Material/TsukuyomiPBRInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/BRDF.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UniversalMetaPass.hlsl"

half4 TsukuyomiPBRUniversalFragmentMeta(Varyings input) : SV_Target
{
    SurfaceData surfaceData;
    InitializeTsukuyomiPBRSurfaceData(input.uv, surfaceData);

    BRDFData brdfData;
    InitializeBRDFData(surfaceData.albedo, surfaceData.metallic, surfaceData.specular, surfaceData.smoothness, surfaceData.alpha, brdfData);

    MetaInput metaInput;
    metaInput.Albedo = brdfData.diffuse + brdfData.specular * brdfData.roughness * 0.5h;
    metaInput.Emission = surfaceData.emission;
    return UniversalFragmentMeta(input, metaInput);
}

#endif
