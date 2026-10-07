#ifndef TSUKUYOMI_RT_SHADOW_COMMON_INCLUDED
#define TSUKUYOMI_RT_SHADOW_COMMON_INCLUDED

// Shared by ray generation and material hit groups. Keep this payload layout identical.
struct TsukuyomiRTShadowPayload
{
    float visibility;
    float hitDistance;
};

// Matches RayTracedShadowGpuLight (six float4 values, 96 bytes).
struct TsukuyomiRTShadowLightData
{
    float4 positionRange;
    float4 directionType;
    float4 rightSize;
    float4 upSize;
    float4 shapeParams;
    // Strength, stable random seed, history valid, previous history slice.
    float4 shadowParams;
};

#endif
