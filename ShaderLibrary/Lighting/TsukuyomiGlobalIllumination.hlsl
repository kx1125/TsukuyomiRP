#ifndef TSUKUYOMI_GLOBAL_ILLUMINATION_INCLUDED
#define TSUKUYOMI_GLOBAL_ILLUMINATION_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/BRDF.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GlobalIllumination.hlsl"
#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Lighting/TsukuyomiEvaluateMaterial.hlsl"

half3 TsukuyomiEnvironmentBRDFSpecularDFG(BRDFData brdfData, half NoV, half fresnelTerm)
{
    // Analytic split-sum DFG approximation used as a lightweight replacement for a preintegrated LUT.
    float4 c0 = float4(-1.0, -0.0275, -0.572, 0.022);
    float4 c1 = float4(1.0, 0.0425, 1.04, -0.04);
    float4 r = brdfData.perceptualRoughness * c0 + c1;
    float a004 = min(r.x * r.x, exp2(-9.28 * NoV)) * r.x + r.y;
    float2 ab = float2(-1.04, 1.04) * a004 + r.zw;

    half3 dfgSpecular = max(half3(0.0h, 0.0h, 0.0h), brdfData.specular * ab.x + ab.y);
    half3 urpSpecular = EnvironmentBRDFSpecular(brdfData, fresnelTerm);
    return lerp(urpSpecular, dfgSpecular, saturate(_IndirectSpecularFGDStrength));
}

half3 TsukuyomiGlobalIllumination(BRDFData brdfData, BRDFData brdfDataClearCoat, float clearCoatMask,
    half3 bakedGI, TsukuyomiBRDFOcclusionFactor aoFactor, float3 positionWS,
    half3 normalWS, half3 viewDirectionWS, float2 normalizedScreenSpaceUV,
    uint renderingLayers)
{
    half3 reflectVector = reflect(-viewDirectionWS, normalWS);
    half NoV = saturate(dot(normalWS, viewDirectionWS));
    half fresnelTerm = Pow4(1.0h - NoV);

    half3 indirectDiffuse = bakedGI;
    half3 indirectSpecular = GlossyEnvironmentReflection(reflectVector, positionWS, brdfData.perceptualRoughness, 1.0h, normalizedScreenSpaceUV);

    // Filament-style horizon falloff reduces reflection leaks near grazing normal-map angles.
    half horizon = saturate(1.0h + dot(reflectVector, normalWS));
    indirectSpecular *= pow(horizon, _HorizonOcclusionPower);

    indirectDiffuse = indirectDiffuse * brdfData.diffuse * aoFactor.indirectAmbientOcclusion * _IndirectDiffuseIntensity;
    indirectSpecular = indirectSpecular * TsukuyomiEnvironmentBRDFSpecularDFG(brdfData, NoV, fresnelTerm) * aoFactor.indirectSpecularOcclusion * _IndirectSpecularIntensity;

    half3 color = indirectDiffuse + indirectSpecular;

    if (IsOnlyAOLightingFeatureEnabled())
    {
        color = aoFactor.indirectAmbientOcclusion + aoFactor.indirectSpecularOcclusion;
    }

#if defined(_CLEARCOAT) || defined(_CLEARCOATMAP)
    half3 coatIndirectSpecular = GlossyEnvironmentReflection(reflectVector, positionWS, brdfDataClearCoat.perceptualRoughness, 1.0h, normalizedScreenSpaceUV);
    half3 coatColor = EnvironmentBRDFClearCoat(brdfDataClearCoat, clearCoatMask, coatIndirectSpecular, fresnelTerm);
    half coatFresnel = kDielectricSpec.x + kDielectricSpec.a * fresnelTerm;
    color = color * (1.0h - coatFresnel * clearCoatMask) + coatColor * aoFactor.indirectSpecularOcclusion;
#endif

    return color;
}

#endif
