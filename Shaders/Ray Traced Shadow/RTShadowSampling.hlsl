#ifndef TSUKUYOMI_RT_SHADOW_SAMPLING_INCLUDED
#define TSUKUYOMI_RT_SHADOW_SAMPLING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"

Texture2DArray<float> _TsukuyomiRTShadowTexture;
StructuredBuffer<int> _TsukuyomiRTShadowAdditionalLightMap;
StructuredBuffer<int> _TsukuyomiRTShadowRectLightMap;
int _TsukuyomiRTShadowCount;
int _TsukuyomiRTShadowMainLightIndex;
int _TsukuyomiRTShadowAdditionalLightMapCount;
int _TsukuyomiRTShadowRectLightMapCount;

// Only sample for the opaque surface represented by this camera's depth prepass.
// A true result includes ShadowStrength; replace, rather than multiply, another realtime shadow.
bool TrySampleRTShadowSlice(int slice, float2 screenUV, out float visibility)
{
    visibility = 1.0;
    if (_TsukuyomiRTShadowCount <= 0 || slice < 0 || slice >= _TsukuyomiRTShadowCount
        || any(screenUV < 0) || any(screenUV >= 1)) return false;
    uint width, height, slices;
    _TsukuyomiRTShadowTexture.GetDimensions(width, height, slices);
    if ((uint)slice >= slices || width == 0 || height == 0) return false;
    uint2 pixel = min((uint2)(screenUV * float2(width, height)), uint2(width, height) - 1u);
    visibility = saturate(_TsukuyomiRTShadowTexture.Load(int4(pixel, slice, 0)));
    return true;
}

bool TrySampleMainLightRTShadow(float2 screenUV, out float visibility)
{
    return TrySampleRTShadowSlice(_TsukuyomiRTShadowMainLightIndex, screenUV, visibility);
}

bool TrySampleAdditionalLightRTShadow(uint lightLoopIndex, float2 screenUV, out float visibility)
{
    visibility = 1.0;
    if (_TsukuyomiRTShadowCount <= 0 || _TsukuyomiRTShadowAdditionalLightMapCount <= 0) return false;
#if USE_CLUSTER_LIGHT_LOOP
    int index = (int)lightLoopIndex;
#else
    // Validate the per-object index before reading unity_LightIndices / its structured equivalent.
    if (lightLoopIndex >= GetAdditionalLightsCount()) return false;
    int index = GetPerObjectLightIndex(lightLoopIndex);
#endif
    if (index < 0 || index >= _TsukuyomiRTShadowAdditionalLightMapCount) return false;
    return TrySampleRTShadowSlice(_TsukuyomiRTShadowAdditionalLightMap[index], screenUV, visibility);
}

bool TrySampleRectLightRTShadow(uint rectLightIndex, float2 screenUV, out float visibility)
{
    visibility = 1.0;
    if (_TsukuyomiRTShadowCount <= 0 || rectLightIndex >= (uint)_TsukuyomiRTShadowRectLightMapCount) return false;
    return TrySampleRTShadowSlice(_TsukuyomiRTShadowRectLightMap[rectLightIndex], screenUV, visibility);
}

#endif
