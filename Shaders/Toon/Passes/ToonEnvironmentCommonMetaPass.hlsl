#ifndef TOON_ENVIRONMENT_COMMON_META_PASS_INCLUDED
#define TOON_ENVIRONMENT_COMMON_META_PASS_INCLUDED

#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Toon/ToonEnvironmentCommonInput.hlsl"
#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Toon/ToonBRDFData.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UniversalMetaPass.hlsl"

half4 ToonEnvironmentCommonUniversalFragmentMeta(Varyings input) : SV_Target
{
    ToonSurfaceData surfaceData;
    InitializeToonEnvironmentCommonSurfaceData(input.uv, surfaceData);

    ToonBRDFData brdfData;
    InitializeToonBRDFData(surfaceData, brdfData);

    MetaInput metaInput;
    metaInput.Albedo = brdfData.diffuse + brdfData.specular * brdfData.roughness * 0.5h;
    metaInput.Emission = surfaceData.emission;
    return UniversalFragmentMeta(input, metaInput);
}

#endif
