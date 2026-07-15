#ifndef TSUKUYOMI_BRDF_INCLUDED
#define TSUKUYOMI_BRDF_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/BRDF.hlsl"

#ifndef TSUKUYOMI_USE_DIFFUSE_LAMBERT_BRDF
    #define TSUKUYOMI_USE_DIFFUSE_LAMBERT_BRDF 0
#endif

#ifndef TSUKUYOMI_GGX_BRDF
    #define TSUKUYOMI_GGX_BRDF 1
#endif

half TsukuyomiPow5(half x)
{
    return x * x * x * x * x;
}

half3 TsukuyomiDiffuseGGXRoughNoPI(half3 diffuseColor, half perceptualRoughness, half NoV, half NoL, half VoH, half NoH)
{
    NoV = saturate(NoV);
    NoL = saturate(NoL);
    VoH = saturate(VoH);
    NoH = saturate(NoH);

    half roughness = perceptualRoughness;
    half alpha = roughness * roughness;
    half scale = max(0.55h - 0.2h * roughness, 1.25h - 1.6h * roughness);
    half bias = saturate(4.0h * alpha);
    half diffuseSingleScatter = lerp(1.0h, scale * (NoH + bias) * rcp(NoH + 0.025h) * VoH * VoH, roughness);
    half diffuseMultiScatter = alpha * 0.38h;

    return diffuseColor * (diffuseSingleScatter + diffuseMultiScatter);
}

half TsukuyomiDirectBRDFDiffuseTermNoPI(float NdotL, float clampNdotV, float LdotV, half perceptualRoughness)
{
#if TSUKUYOMI_USE_DIFFUSE_LAMBERT_BRDF
    return half(1.0);
#else
    return half(DisneyDiffuseNoPI(clampNdotV, abs(NdotL), LdotV, perceptualRoughness));
#endif
}

half3 TsukuyomiGGXBRDFSpecular(BRDFData brdfData, half3 normalWS, half3 lightDirectionWS, half3 viewDirectionWS)
{
    float3 halfDir = SafeNormalize(float3(lightDirectionWS) + float3(viewDirectionWS));
    float NoH = saturate(dot(float3(normalWS), halfDir));
    float NdotL = dot(float3(normalWS), float3(lightDirectionWS));
    float NdotV = dot(float3(normalWS), float3(viewDirectionWS));
    float clampNdotV = ClampNdotV(NdotV);
    float LoH = saturate(dot(float3(lightDirectionWS), halfDir));
    float partLambdaV = GetSmithJointGGXPartLambdaV(clampNdotV, brdfData.roughness);
    float3 F = F_Schlick(brdfData.specular, LoH);
    float DV = DV_SmithJointGGX(NoH, abs(NdotL), clampNdotV, brdfData.roughness, partLambdaV);
    half3 specularTerm = half3(DV * F);

    // URP diffuse is not multiplied by INV_PI, so the specular lobe is scaled to the same convention.
    specularTerm *= PI;

#if REAL_IS_HALF
    specularTerm = specularTerm - HALF_MIN;
    specularTerm = clamp(specularTerm, 0.0h, 1000.0h);
#endif

    return specularTerm;
}

half3 TsukuyomiLitSpecular(BRDFData brdfData, half3 normalWS, half3 lightDirectionWS, half3 viewDirectionWS)
{
#if TSUKUYOMI_GGX_BRDF
    return TsukuyomiGGXBRDFSpecular(brdfData, normalWS, lightDirectionWS, viewDirectionWS);
#else
    return DirectBRDFSpecular(brdfData, normalWS, lightDirectionWS, viewDirectionWS) * brdfData.specular;
#endif
}

#endif
