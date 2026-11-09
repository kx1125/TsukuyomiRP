#ifndef TOON_BRDF_DATA_INCLUDED
#define TOON_BRDF_DATA_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/BRDF.hlsl"
#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Toon/ToonSurfaceData.hlsl"

struct ToonBRDFData
{
    half3 albedo;
    half3 diffuse;
    half3 specular;
    half reflectivity;
    half perceptualRoughness;
    half roughness;
    half roughness2;
    half grazingTerm;
    half normalizationTerm;
    half roughness2MinusOne;
    float4 ToonData;
};

void InitializeToonBRDFData(inout ToonSurfaceData surfaceData, out ToonBRDFData brdfData)
{
    brdfData = (ToonBRDFData)0;
    brdfData.albedo = surfaceData.albedo;

#ifdef _SPECULAR_SETUP
    half reflectivity = ReflectivitySpecular(surfaceData.specular);
    half oneMinusReflectivity = half(1.0) - reflectivity;
    brdfData.diffuse = surfaceData.albedo * oneMinusReflectivity;
    brdfData.specular = surfaceData.specular;
#else
    half oneMinusReflectivity = OneMinusReflectivityMetallic(surfaceData.metallic);
    half reflectivity = half(1.0) - oneMinusReflectivity;
    brdfData.diffuse = surfaceData.albedo * oneMinusReflectivity;
    brdfData.specular = lerp(kDielectricSpec.rgb, surfaceData.albedo, surfaceData.metallic);
#endif

    brdfData.reflectivity = reflectivity;
    brdfData.perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(surfaceData.smoothness);
    brdfData.roughness = max(PerceptualRoughnessToRoughness(brdfData.perceptualRoughness), HALF_MIN_SQRT);
    brdfData.roughness2 = max(brdfData.roughness * brdfData.roughness, HALF_MIN);
    brdfData.grazingTerm = saturate(surfaceData.smoothness + reflectivity);
    brdfData.normalizationTerm = brdfData.roughness * half(4.0) + half(2.0);
    brdfData.roughness2MinusOne = brdfData.roughness2 - half(1.0);
    brdfData.ToonData = surfaceData.ToonData;

#if defined(_ALPHAPREMULTIPLY_ON)
    brdfData.diffuse *= surfaceData.alpha;
#endif
}

ToonBRDFData CreateToonClearCoatBRDFData(ToonSurfaceData surfaceData, inout ToonBRDFData brdfData)
{
    ToonBRDFData clearCoatBRDFData = (ToonBRDFData)0;
    clearCoatBRDFData.ToonData = surfaceData.ToonData;

#if defined(_CLEARCOAT) || defined(_CLEARCOATMAP)
    clearCoatBRDFData.albedo = half(1.0);
    clearCoatBRDFData.diffuse = kDielectricSpec.aaa;
    clearCoatBRDFData.specular = kDielectricSpec.rgb;
    clearCoatBRDFData.reflectivity = kDielectricSpec.r;
    clearCoatBRDFData.perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(surfaceData.clearCoatSmoothness);
    clearCoatBRDFData.roughness = max(PerceptualRoughnessToRoughness(clearCoatBRDFData.perceptualRoughness), HALF_MIN_SQRT);
    clearCoatBRDFData.roughness2 = max(clearCoatBRDFData.roughness * clearCoatBRDFData.roughness, HALF_MIN);
    clearCoatBRDFData.normalizationTerm = clearCoatBRDFData.roughness * half(4.0) + half(2.0);
    clearCoatBRDFData.roughness2MinusOne = clearCoatBRDFData.roughness2 - half(1.0);
    clearCoatBRDFData.grazingTerm = saturate(surfaceData.clearCoatSmoothness + kDielectricSpec.x);

    half ieta = lerp(1.0h, CLEAR_COAT_IETA, surfaceData.clearCoatMask);
    half coatRoughnessScale = Sq(ieta);
    half sigma = RoughnessToVariance(PerceptualRoughnessToRoughness(brdfData.perceptualRoughness));
    brdfData.perceptualRoughness = RoughnessToPerceptualRoughness(VarianceToRoughness(sigma * coatRoughnessScale));
    brdfData.roughness = max(PerceptualRoughnessToRoughness(brdfData.perceptualRoughness), HALF_MIN_SQRT);
    brdfData.roughness2 = max(brdfData.roughness * brdfData.roughness, HALF_MIN);
    brdfData.normalizationTerm = brdfData.roughness * 4.0h + 2.0h;
    brdfData.roughness2MinusOne = brdfData.roughness2 - 1.0h;
    brdfData.specular = lerp(brdfData.specular, ConvertF0ForClearCoat15(brdfData.specular), surfaceData.clearCoatMask);
#endif

    return clearCoatBRDFData;
}

#endif
