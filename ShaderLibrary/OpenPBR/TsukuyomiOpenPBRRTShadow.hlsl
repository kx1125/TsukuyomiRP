#ifndef TSUKUYOMI_OPENPBR_RT_SHADOW_INCLUDED
#define TSUKUYOMI_OPENPBR_RT_SHADOW_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Ray Traced Shadow/RTShadowSampling.hlsl"

// Published in RT slot order. URP's additional-shadow parameters can have strength=0
// for a light without a raster shadow slice, including additional directional lights.
float _TsukuyomiRTShadowStrengths[16];
// Independently published even when URP has no raster shadow pass this frame.
// xy use the same squared-distance scale and bias as URP's native shadow fade.
float4 _TsukuyomiRTShadowFallbackFade;

half TsuOPBRRTShadowFallbackFade(float3 positionWS)
{
    float3 cameraToPixel = positionWS - _WorldSpaceCameraPos;
    float distanceSquared = dot(cameraToPixel, cameraToPixel);
    return half(saturate(distanceSquared * _TsukuyomiRTShadowFallbackFade.x
        + _TsukuyomiRTShadowFallbackFade.y));
}

half TsuOPBRMixRTAndBakedShadow(float visibility, int slot, half4 shadowMask,
    half4 occlusionProbeChannels, half shadowFade)
{
    half baked = half(1.0);
#if defined(CALCULATE_BAKED_SHADOWS)
    baked = BakedShadow(shadowMask, occlusionProbeChannels,
        half4(saturate(_TsukuyomiRTShadowStrengths[slot]), 0, 0, 0));
#endif
    // RT visibility already includes light.shadowStrength. Apply strength only to
    // the independent baked term, then use URP's ShadowMask / Distance ShadowMask mix.
    return MixRealtimeAndBakedShadows(half(visibility), baked, shadowFade);
}

Light TsuOPBRGetMainLight(InputData inputData, half4 shadowMask)
{
#if !defined(_RECEIVE_SHADOWS_OFF) && !defined(_SURFACE_TYPE_TRANSPARENT)
    float visibility;
    if (TrySampleMainLightRTShadow(inputData.normalizedScreenSpaceUV, visibility))
    {
        // Start with the unshadowed light so PCSS/screen-space or atlas shadows are
        // neither sampled nor multiplied into the valid RT result.
        Light light = GetMainLight();
        half fade = TsuOPBRRTShadowFallbackFade(inputData.positionWS);
#if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
        fade = GetMainLightShadowFade(inputData.positionWS);
#endif
        light.shadowAttenuation = TsuOPBRMixRTAndBakedShadow(visibility,
            _TsukuyomiRTShadowMainLightIndex, shadowMask, _MainLightOcclusionProbes, fade);
#if defined(_LIGHT_COOKIES)
        light.color *= SampleMainLightCookie(inputData.positionWS);
#endif
        return light;
    }
#endif
    return GetMainLight(inputData.shadowCoord, inputData.positionWS, shadowMask);
}

Light TsuOPBRGetAdditionalLight(uint lightLoopIndex, InputData inputData, half4 shadowMask)
{
#if !defined(_RECEIVE_SHADOWS_OFF) && !defined(_SURFACE_TYPE_TRANSPARENT)
    float visibility;
    if (TrySampleAdditionalLightRTShadow(lightLoopIndex, inputData.normalizedScreenSpaceUV, visibility))
    {
#if USE_CLUSTER_LIGHT_LOOP
        int lightIndex = (int)lightLoopIndex;
#else
        int lightIndex = GetPerObjectLightIndex(lightLoopIndex);
#endif
        Light light = GetAdditionalPerObjectLight(lightIndex, inputData.positionWS);
#if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
        half4 occlusionProbeChannels = _AdditionalLightsBuffer[lightIndex].occlusionProbeChannels;
#else
        half4 occlusionProbeChannels = _AdditionalLightsOcclusionProbes[lightIndex];
#endif
        half fade = TsuOPBRRTShadowFallbackFade(inputData.positionWS);
#if defined(ADDITIONAL_LIGHT_CALCULATE_SHADOWS)
        fade = GetAdditionalLightShadowFade(inputData.positionWS);
#endif
        light.shadowAttenuation = TsuOPBRMixRTAndBakedShadow(visibility,
            _TsukuyomiRTShadowAdditionalLightMap[lightIndex], shadowMask, occlusionProbeChannels, fade);
#if defined(_LIGHT_COOKIES)
        light.color *= SampleAdditionalLightCookie(lightIndex, inputData.positionWS);
#endif
        return light;
    }
#endif
    return GetAdditionalLight(lightLoopIndex, inputData.positionWS, shadowMask);
}

#endif
