#ifndef TSUKUYOMI_CHARACTER_HAIR_META_PASS_INCLUDED
#define TSUKUYOMI_CHARACTER_HAIR_META_PASS_INCLUDED

#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Character/CharacterNPRInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/BRDF.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UniversalMetaPass.hlsl"

float4 TsukuyomiHairFragmentMeta(Varyings input) : SV_Target
{
    TsukuyomiHairSurface surface = TsukuyomiHairSampleSurface(input.uv);
    BRDFData brdfData;
    InitializeBRDFData(surface.baseColor, surface.metallic,
        surface.specular.xxx, surface.smoothness, surface.alpha, brdfData);
    MetaInput metaInput;
    metaInput.Albedo = brdfData.diffuse
        + brdfData.specular * brdfData.roughness * 0.5;
    metaInput.Emission = surface.emission;
    return UniversalFragmentMeta(input, metaInput);
}

#endif
