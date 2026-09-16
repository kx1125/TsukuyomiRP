#ifndef TSUKUYOMI_SPECULAR_PBR_META_INCLUDED
#define TSUKUYOMI_SPECULAR_PBR_META_INCLUDED
#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Material/TsukuyomiSpecularPBRInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UniversalMetaPass.hlsl"
half4 TsukuyomiSpecularPBRMetaFragment(Varyings input) : SV_Target
{
    SurfaceData s;
    InitializeTsukuyomiSpecularPBRSurfaceData(input.uv, s);
    float strength = saturate(SampleSpecularPBRMask(input.uv).r);
    float3 f0 = lerp(0.04.xxx, s.albedo, s.metallic) * strength;
    float roughness = 1.0 - s.smoothness;
    MetaInput metaInput = (MetaInput)0;
    // URP's diffuse-plus-rough-specular baking approximation; no SSS or fog in Meta.
    metaInput.Albedo = s.albedo * (1.0 - s.metallic) * (1.0 - 0.04 * strength)
        + f0 * roughness * roughness * 0.5;
    metaInput.Emission = s.emission;
    return UniversalFragmentMeta(input, metaInput);
}
#endif
