#ifndef TSUKUYOMI_CHARACTER_FACE_INPUT_INCLUDED
#define TSUKUYOMI_CHARACTER_FACE_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

// One layout for Forward, depth, shadows, Meta, motion vectors and FSR3.
CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
float4 _BaseMap_TexelSize;
half4 _BaseColor;
half4 _SDFRimColor;
half4 _EmissionColor;
float4 _HighlightMapVector;
float4x4 _AnchorTRS; // URP adapter: head world-to-local; zero means object fallback.
float _EmotionIndex;
half _EmotionBlend;
half _Smoothness;
half _Metallic;
half _Specular;
half _BumpScale;
half _ShadowColorBrightness;
half _ShadowColorSaturation;
half _FaceRimOffScale;
half _SkinRimOffScale;
half _BackFaceNormalFlip;
half _EmissionBrightness;
half _AlphaClipThreshold;
half _DiffuseOffset; // URP adapter for the source's global diffuse bias.
half _Fsr3ReactiveScale;
half _Fsr3CompositionScale;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END

// Internal URP pass integration, not legacy ShaderLab property aliases.
#define _Cutoff _AlphaClipThreshold
#define sampler_BaseMap sampler_LinearRepeat

TEXTURE2D(_SDFMask);
TEXTURE2D(_SDFLightmap);
TEXTURE2D(_DiffRampMap);
TEXTURE2D(_EmotionMap);
TEXTURE2D(_HighlightMap);
SAMPLER(sampler_LinearMirror);

struct TsukuyomiFaceSurface
{
    float3 albedo;
    float3 shadowAlbedo;
    float3 normalTS;
    float3 emission;
    float4 mask;
    float alpha;
    float alphaMask;
};

float TsukuyomiFaceLuminance(float3 color)
{
    return dot(color, float3(0.2126729041, 0.7151522040, 0.0721750036));
}

float3 TsukuyomiFaceSaturateColor(float3 color, float saturation)
{
    return lerp(TsukuyomiFaceLuminance(color).xxx, color, saturation);
}

float3 TsukuyomiFaceSampleNormalTS(float2 uv)
{
#if defined(_NORMALMAP)
    // Skin b103 L431-L440: RG/AG decode, then scale XY without recomputing Z.
    // UnpackNormalScale handles the platform's Unity NormalMap import encoding.
    return UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_LinearRepeat, uv), _BumpScale);
#else
    return float3(0.0, 0.0, 1.0);
#endif
}

TsukuyomiFaceSurface TsukuyomiFaceSampleSurface(float2 uv)
{
    TsukuyomiFaceSurface surface = (TsukuyomiFaceSurface)0;
    float4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_LinearRepeat, uv);
    surface.alpha = Alpha(baseSample.a, _BaseColor, _AlphaClipThreshold);
    surface.alphaMask = baseSample.a;
    surface.albedo = baseSample.rgb * _BaseColor.rgb;
#if defined(_EMOTION_MAP)
    // Skin b131 L424-L430. The expression replaces the already tinted base.
    float2 tile = float2(fmod(_EmotionIndex, 2.0), floor(_EmotionIndex * 0.5)) * 0.5;
    float4 emotion = SAMPLE_TEXTURE2D(_EmotionMap, sampler_LinearRepeat, tile + uv * 0.5);
    surface.albedo = lerp(surface.albedo, emotion.rgb, emotion.a * _EmotionBlend);
#endif
    surface.shadowAlbedo = TsukuyomiFaceSaturateColor(
        surface.albedo * _ShadowColorBrightness, _ShadowColorSaturation);
    surface.normalTS = TsukuyomiFaceSampleNormalTS(uv);
#if defined(_SDFLIGHTMAP)
    // b131 L431-L434: R rim mask, G ordinary-skin weight, B rim-scale blend,
    // A global fake-rim mask (retained in the data; that global effect is omitted).
    surface.mask = SAMPLE_TEXTURE2D(_SDFMask, sampler_LinearMirror, uv);
#else
    surface.mask = float4(1.0, 1.0, 1.0, 0.0);
#endif
#if defined(_EMISSION)
    // Skin b127 L434, L805: independent of HighlightMap and light count.
    surface.emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_LinearRepeat, uv).rgb
        * _EmissionColor.rgb * _EmissionBrightness;
#endif
    return surface;
}

float TsukuyomiFaceBackFaceSign(bool frontFace)
{
    return frontFace ? 1.0 : lerp(-1.0, 1.0, _BackFaceNormalFlip);
}

#endif
