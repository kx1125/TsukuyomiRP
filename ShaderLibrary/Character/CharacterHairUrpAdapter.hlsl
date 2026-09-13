#ifndef TSUKUYOMI_CHARACTER_HAIR_URP_ADAPTER_INCLUDED
#define TSUKUYOMI_CHARACTER_HAIR_URP_ADAPTER_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

// This file is the only intentional HGRP -> URP boundary. The Hair equation
// consumes these values; it does not call URP lighting functions directly.
struct TsukuyomiHairLightInput
{
    float3 direction;
    float3 color;
    float directionalIntensityScale;
    float distanceAttenuation;
    float directionalShadow;
    float characterShadow;
    float punctualDiffuseBias;
    float punctualShadowColorScale;
    float punctualSpecularScale;
};

struct TsukuyomiHairIndirectInput
{
    float4 dominantDirection;
    float3 irradiance;
    float3 dominantIrradiance;
};

TsukuyomiHairLightInput TsukuyomiHairAdaptLight(Light light)
{
    TsukuyomiHairLightInput result;
    result.direction = light.direction;
    result.color = light.color;
    // URP Light.color is already intensity-weighted; HGRP exposes that scalar
    // separately as DirectionalLightCustomData1.w.
    result.directionalIntensityScale = 1.0;
    result.distanceAttenuation = light.distanceAttenuation;
    // HGRP keeps the directional shadow and CharacterShadow mask separate.
    // URP exposes one resolved attenuation, so both source inputs map to it.
    result.directionalShadow = light.shadowAttenuation;
    result.characterShadow = light.shadowAttenuation;

    // HGRP punctual customData.x/y/z are not represented by URP Light.
    // Neutral adapter values preserve the source type-1 punctual equation.
    result.punctualDiffuseBias = 0.0;
    result.punctualShadowColorScale = 1.0;
    result.punctualSpecularScale = 1.0;
    return result;
}

TsukuyomiHairIndirectInput TsukuyomiHairAdaptIndirect(float3 bakedGI)
{
    TsukuyomiHairIndirectInput result = (TsukuyomiHairIndirectInput)0;
    result.irradiance = max(bakedGI, 0.0.xxx);
    // URP's common GI result has no HGRP SH coefficient tuple. Its sampled GI
    // maps to both source irradiance observations; the material equation still
    // performs HGRP's exposure and color reduction in the original order.
    result.dominantIrradiance = result.irradiance;
    result.dominantDirection = 0.0.xxxx;
    return result;
}

float TsukuyomiHairSampleSceneEyeDepth(float2 uv)
{
    return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
}

float3 TsukuyomiHairSampleSceneNormal(float2 uv)
{
    return normalize(SampleSceneNormals(uv));
}

#endif
