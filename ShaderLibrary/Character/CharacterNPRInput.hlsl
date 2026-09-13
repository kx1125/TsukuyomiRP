#ifndef TSUKUYOMI_CHARACTER_HAIR_INPUT_INCLUDED
#define TSUKUYOMI_CHARACTER_HAIR_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

// Raw globals retained from the generated HGRP CharacterNPR Hair shaders.
float4 _CharacterParams0;
float4 _CharacterParams1;
float4 _CharacterParams2;
float4 _CharacterParams5;
float4 _CharacterParams6;
float4 _CharacterParams7;
float4 _CharacterParams8;
float4 _CharacterParams9;
float4 _CharacterParams10;
float4 _CharacterParams11;
float4 _CharacterParams12;
float4 _CharacterParams13;
float4 _CharacterParams15;
float4 _ExposureWithMiscParams;
float4 _EnvironmentGlobalParams0;

// Property names and defaults match HGRP/CharacterNPR_Hair for the supported scope.
CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
float4 _BaseMap_TexelSize;
float4 _BaseColor;
float4 _EmissionColor;
float4 _AnisotropyColor2;
float4 _StrokeMap_ST;
float4 _LineMap_ST;
float4 _OutlineMask_ST;
float _SurfaceType;
float _AlphaPremultiply;
float _BackFaceNormalFlip;
float _AlphaClipThreshold;
float _Metallic;
float _Specular;
float _Smoothness;
float _EmissionBrightness;
float _BumpScale;
float _SpecBumpScale;
float _ShadowColorBrightness;
float _ShadowColorSaturation;
float _AnisotropyValue;
float _AnisotropyValue2;
float _AnisotropyIntensity;
float _AnisotropyEdgeFade;
float _AnisotropyRange2;
float _AnisotropyDirX;
float _StrokeScale;
float _LineAmount;
float _LineValue;
float _LineRange;
float _LineIntensity;
float _LineSaturation;
float _UseLineMap;
float _EnableOutline;
float _OutlineTransparent;
float _OutlineWidth;
float _OutlineOffsetZ;
float _OutlineColorBrightness;
float _OutlineColorSaturation;
float _OutlineAverageNormal;
float _Cull;
float _ZWrite;
float _ZTest;
float _AddPrecomputedVelocity;
float _XRMotionVectorsPass;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END

TEXTURE2D(_MetallicGlossMap);
SAMPLER(sampler_MetallicGlossMap);
TEXTURE2D(_SplitNormalMap);
SAMPLER(sampler_SplitNormalMap);
TEXTURE2D(_DiffRampMap);
SAMPLER(sampler_DiffRampMap);
TEXTURE2D(_SpecRampMap);
SAMPLER(sampler_SpecRampMap);
TEXTURE2D(_ShadowLutTex);
SAMPLER(sampler_ShadowLutTex);
TEXTURE2D(_StrokeMap);
SAMPLER(sampler_StrokeMap);
TEXTURE2D(_LineMap);
SAMPLER(sampler_LineMap);
TEXTURE2D(_OutlineMask);
SAMPLER(sampler_OutlineMask);
// Generated HGRP Hair uses LinearRepeat (provided by Core) for material maps
// and LinearMirror for ramps/LUTs.
SAMPLER(sampler_LinearMirror);

static const float3 TsukuyomiHairLuminanceWeights =
    float3(0.21267290413379669189453125,
           0.715152204036712646484375,
           0.072175003588199615478515625);

struct TsukuyomiHairSurface
{
    float3 baseColor;
    float3 shadowColor;
    float alpha;
    float metallic;
    float specular;
    float shadowMask;
    float smoothness;
    float3 diffuseNormalTS;
    float3 specularNormalTS;
    float strokeOffset;
    float lineMap;
    float3 emission;
};

float3 TsukuyomiHairDecodeDxt5Normal(float4 packedNormal, float scale)
{
    // HGRP b117 L447-L454: w *= x, then wy are remapped to [-1, 1].
    packedNormal.w *= packedNormal.x;
    float2 xy = (packedNormal.wy * 2.0) - 1.0.xx;
    float3 normalTS = float3(xy, 0.0);
    normalTS.z = max(1.000000016862383526387164645044e-16,
        sqrt(1.0 - clamp(dot(xy, xy), 0.0, 1.0)));
    normalTS.xy *= scale;
    return normalTS;
}

float3 TsukuyomiHairDecodeLinearNormal(float2 packedNormal, float scale)
{
    // HGRP b142 L450-L475: split map RG=diffuse and BA=specular.
    float2 xy = (packedNormal * 2.0) - 1.0.xx;
    float3 normalTS = float3(xy, 0.0);
    normalTS.z = max(1.000000016862383526387164645044e-16,
        sqrt(1.0 - clamp(dot(xy, xy), 0.0, 1.0)));
    normalTS.xy *= scale;
    return normalTS;
}

float3 TsukuyomiHairSampleShadowColor(float3 baseColor)
{
#if defined(_SHADOW_LUT_TEX)
    // HGRP b117 L437-L446. The LUT is the original packed 32x32x32 layout.
    float3 low = baseColor * 12.9200000762939453125;
    float3 high = (pow(abs(baseColor), 0.4166666567325592041015625.xxx)
        * 1.05499994754791259765625) - 0.054999999701976776123046875.xxx;
    float3 srgb = clamp(float3(
        baseColor.x <= 0.003130800090730190277099609375 ? low.x : high.x,
        baseColor.y <= 0.003130800090730190277099609375 ? low.y : high.y,
        baseColor.z <= 0.003130800090730190277099609375 ? low.z : high.z), 0.0.xxx, 1.0.xxx);
    float slice = srgb.z * 31.0;
    float sliceFloor = floor(slice);
    float2 lutUV = ((srgb.xy * 31.0) * float2(0.0009765625, 0.03125))
        + float2(0.00048828125, 0.015625);
    lutUV.x += sliceFloor * 0.03125;
    float3 a = SAMPLE_TEXTURE2D_LOD(_ShadowLutTex, sampler_LinearMirror, lutUV, 0.0).xyz;
    float3 b = SAMPLE_TEXTURE2D_LOD(_ShadowLutTex, sampler_LinearMirror,
        lutUV + float2(0.03125, 0.0), 0.0).xyz;
    return lerp(a, b, (slice - sliceFloor).xxx);
#else
    // HGRP b90 L428-L429.
    float3 scaled = baseColor * _ShadowColorBrightness;
    return lerp(dot(scaled, TsukuyomiHairLuminanceWeights).xxx,
        scaled, _ShadowColorSaturation.xxx);
#endif
}

TsukuyomiHairSurface TsukuyomiHairSampleSurface(float2 uv)
{
    TsukuyomiHairSurface surface = (TsukuyomiHairSurface)0;
    float4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_LinearRepeat, uv);
    surface.baseColor = baseSample.xyz * _BaseColor.xyz;
    surface.alpha = baseSample.w * _BaseColor.w;

#if defined(_ALPHATEST_ON)
    clip(surface.alpha - _AlphaClipThreshold);
#endif

#if defined(_METALLICSPECGLOSSMAP)
    // HGRP b142 L443-L447: R metal, G spec, B shadow, A smooth/secondary lobe.
    float4 mask = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_LinearRepeat, uv);
    surface.metallic = mask.x;
    surface.specular = mask.y;
    surface.shadowMask = mask.z;
    surface.smoothness = mask.w;
#else
    surface.metallic = _Metallic;
    surface.specular = _Specular;
    surface.shadowMask = 1.0;
    surface.smoothness = _Smoothness;
#endif

    surface.shadowColor = TsukuyomiHairSampleShadowColor(surface.baseColor);

#if defined(_NORMALMAP)
    #if defined(_SPECULAR_NORMALMAP)
        float4 splitNormal = SAMPLE_TEXTURE2D(_SplitNormalMap, sampler_LinearRepeat, uv);
        surface.diffuseNormalTS = TsukuyomiHairDecodeLinearNormal(splitNormal.xy, _BumpScale);
        surface.specularNormalTS = TsukuyomiHairDecodeLinearNormal(splitNormal.zw, _SpecBumpScale);
    #else
        float4 normalSample = SAMPLE_TEXTURE2D(_BumpMap, sampler_LinearRepeat, uv);
        surface.diffuseNormalTS = TsukuyomiHairDecodeDxt5Normal(normalSample, _BumpScale);
        surface.specularNormalTS = surface.diffuseNormalTS;
    #endif
#else
    surface.diffuseNormalTS = float3(0.0, 0.0, 1.0);
    surface.specularNormalTS = surface.diffuseNormalTS;
#endif

#if defined(_STROKE_ON)
    float stroke = SAMPLE_TEXTURE2D(_StrokeMap, sampler_LinearRepeat,
        (uv * _StrokeMap_ST.xy) + _StrokeMap_ST.zw).x;
    surface.strokeOffset = ((stroke * 2.0) - 1.0) * _StrokeScale;
#else
    surface.strokeOffset = 0.0;
#endif

#if defined(_SPECULAR_LINE)
    surface.lineMap = SAMPLE_TEXTURE2D(_LineMap, sampler_LinearRepeat,
        (uv * _LineMap_ST.xy) + _LineMap_ST.zw).x;
#else
    surface.lineMap = 0.0;
#endif

#if defined(_EMISSION)
    surface.emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_LinearRepeat, uv).xyz
        * _EmissionColor.xyz * _EmissionBrightness;
#else
    surface.emission = 0.0.xxx;
#endif
    return surface;
}

void TsukuyomiHairAlphaClip(float2 uv)
{
#if defined(_ALPHATEST_ON)
    float alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_LinearRepeat, uv).w * _BaseColor.w;
    clip(alpha - _AlphaClipThreshold);
#endif
}

#endif
