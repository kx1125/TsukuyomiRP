#ifndef CHARACTER_HAIR_INPUT_INCLUDED
#define CHARACTER_HAIR_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
float4 _StrokeMap_ST;
float4 _LineMap_ST;
half4 _BaseColor;
half4 _HairBaseTintColor;
half4 _HairAddTintColor;
half4 _EmissionColor;
half _EmissionBrightness;
half _BumpScale;
half _SpecBumpScale;
half _OcclusionStrength;

half _DiffuseOffset;
half _ShadowColorBrightness;
half _ShadowColorSaturation;
half _SpecularScale;
half _AnisotropyValue;
half _AnisotropyDirX;
half _AnisotropyIntensity;
half _AnisotropyEdgeFade;
half _AnisotropyValue2;
half _AnisotropyRange2;
half4 _AnisotropyColor2;
half _StrokeScale;
half _LineAmount;
half _LineValue;
half _LineRange;
half _LineIntensity;
half _LineSaturation;
half _UseLineMap;

UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END

TEXTURE2D(_StrokeMap);
SAMPLER(sampler_StrokeMap);
TEXTURE2D(_LineMap);
SAMPLER(sampler_LineMap);
TEXTURE2D(_SplitNormalMap);
SAMPLER(sampler_SplitNormalMap);
TEXTURE2D(_MetallicGlossMap);
SAMPLER(sampler_MetallicGlossMap);
TEXTURE2D(_DiffuseRampMap);
SAMPLER(sampler_DiffuseRampMap);
TEXTURE2D(_SpecRampMap);
SAMPLER(sampler_SpecRampMap);

struct CharacterHairSurfaceData
{
    SurfaceData surface;
    half4 metallicGloss;
    float3 specNormalTS;
    half strokeOffset;
    half lineMapMask;
};

struct CharacterHairInputData
{
    InputData inputData;
    float3 specNormalWS;
};

void InitializeCharacterHairSurfaceData(float2 uv, out CharacterHairSurfaceData outData)
{
    half4 albedoAlpha = SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
    half4 metallicGloss = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, uv);
    half stroke = SAMPLE_TEXTURE2D(
        _StrokeMap,
        sampler_StrokeMap,
        uv * _StrokeMap_ST.xy + _StrokeMap_ST.zw).r;
    half lineSample = SAMPLE_TEXTURE2D(
        _LineMap,
        sampler_LineMap,
        uv * _LineMap_ST.xy + _LineMap_ST.zw).r;

    float4 splitNormal = SAMPLE_TEXTURE2D(_SplitNormalMap, sampler_SplitNormalMap, uv);
    float2 diffuseNormalXY = splitNormal.xy * 2.0 - 1.0;
    float diffuseNormalZ = sqrt(1.0 - saturate(dot(diffuseNormalXY, diffuseNormalXY)));
    float3 diffuseNormalTS = float3(diffuseNormalXY * _BumpScale, diffuseNormalZ);

    float2 specularNormalXY = splitNormal.zw * 2.0 - 1.0;
    float specularNormalZ = sqrt(1.0 - saturate(dot(specularNormalXY, specularNormalXY)));
    float3 specularNormalTS = float3(specularNormalXY * _SpecBumpScale, specularNormalZ);

    outData.specNormalTS = specularNormalTS;
    outData.metallicGloss = metallicGloss;
    outData.strokeOffset = (stroke * 2.0h - 1.0h) * _StrokeScale;
    outData.lineMapMask = 1.0h - lineSample;
    outData.surface = (SurfaceData)0;
    outData.surface.alpha = albedoAlpha.a * _BaseColor.a;
    outData.surface.albedo = albedoAlpha.rgb * _BaseColor.rgb * lerp(_HairAddTintColor.rgb, _HairBaseTintColor.rgb, outData.surface.alpha);
    outData.surface.metallic = saturate(metallicGloss.r);
    outData.surface.specular = half3(0.0h, 0.0h, 0.0h);
    outData.surface.smoothness = saturate(metallicGloss.a);
    outData.surface.normalTS = diffuseNormalTS;
    outData.surface.occlusion = lerp(1.0h, metallicGloss.b, _OcclusionStrength);
    outData.surface.emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb * _EmissionColor.rgb * _EmissionBrightness;
    outData.surface.clearCoatMask = 0.0h;
    outData.surface.clearCoatSmoothness = 0.0h;
}

#endif
