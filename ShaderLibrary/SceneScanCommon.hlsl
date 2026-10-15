#ifndef TSUKUYOMI_SCENE_SCAN_COMMON_INCLUDED
#define TSUKUYOMI_SCENE_SCAN_COMMON_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/ProjectionUtils.hlsl"
float _ScanCoverageMode;
float4 _ScanModuleFlags;
TEXTURE2D_X(_ScanTargetMask);
TEXTURE2D_X(_ScanExcludedMask);
float4 _ScanOriginProgress; // world origin, raw reversible time
float4 _ScanGridRange; // cell size, range, boost width, noise in metres
float4 _ScanDebugColor;
float4 _ScanTintColor;
float4 _ScanStyleParams; // desaturation, tint, outline, fresnel
float4 _ScanGridFlash; // grid intensity, flash intensity, debug view, pulse speed
float4 _ScanCurveParams; // overall blend, world progress, screen progress, flash
float4 _ScanGridSettings; // subdivisions, basic intensity, reveal, random
float4 _ScanTransitionParams; // pixelation/distortion strength, UE bFadedIn

float HashCell(float3 p) { return frac(sin(dot(p, float3(127.1, 311.7, 74.7))) * 43758.5453); }

bool IsSky(float depth)
{
    #if UNITY_REVERSED_Z
        return depth <= 0.000001;
    #else
        return depth >= 0.999999;
    #endif
}

float DeviceDepth(float rawDepth)
{
    #if UNITY_REVERSED_Z
        return rawDepth;
    #else
        return lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
    #endif
}

// UE CellnoiseALU's PCG3D16 R channel. The material uses a fixed 300 cm noise grid.
float VoxelCellNoise(float3 positionWS)
{
    uint3 v = (uint3)(int3)floor(positionWS / 3.0);
    v = v * 1664525u + 1013904223u;
    v.x += v.y * v.z; v.y += v.z * v.x; v.z += v.x * v.y;
    v.x += v.y * v.z; v.y += v.z * v.x; v.z += v.x * v.y;
    return (v.x >> 16u) / 65535.0;
}

float2 VoxelSceneUV(float2 uv, float rawDepth)
{
    if (_ScanTransitionParams.y > 0.5 || _ScanTransitionParams.x <= 0.0 || IsSky(rawDepth))
        return uv;
    float3 positionWS = ComputeWorldSpacePosition(uv, DeviceDepth(rawDepth), UNITY_MATRIX_I_VP);
    float noise = VoxelCellNoise(positionWS);
    float worldTime = saturate(_ScanCurveParams.y);
    float randomizedTime = worldTime < 0.5
        ? 2.0 * worldTime * noise
        : 1.0 - 2.0 * (1.0 - worldTime) * (1.0 - noise);
    // M_PP_ScanScene: GridCellSize / 2^floor(9 * randomizedWorldTime).
    // Whole world cells share one color sample, then halve in discrete steps.
    float cellSize = _ScanGridRange.x * exp2(-floor(9.0 * saturate(randomizedTime)));
    float3 snappedWS = floor(positionWS / cellSize) * cellSize;
    float4 clip = TransformWorldToHClip(snappedWS);
    if (clip.w <= 0.0001) return uv;
    float2 voxelUV = ComputeNormalizedDeviceCoordinates(snappedWS, UNITY_MATRIX_VP);
    return lerp(uv, voxelUV, _ScanTransitionParams.x);
}

float3 ScreenExpansion(float2 uv)
{
    // MF_RadialDistortion defaults: 10 UV cells, width .4, inner .5, outer .25.
    // The quantized direction and mask create the block-shaped expanding transition.
    float2 q = (round(uv * 10.0) / 10.0 - 0.5) * 2.0;
    float x = (length(q) + 1.6 - (sqrt(2.0) + 1.6) * _ScanCurveParams.z) / 0.8;
    float inner = 1.0 - saturate(x);
    float ring = saturate(x) * (1.0 - saturate(x - 1.0));
    float2 direction = q * rsqrt(max(dot(q, q), 0.000001));
    float2 offset = direction * lerp(0.5, 0.25, inner) * ring * ring;
    return float3(offset * _ScanTransitionParams.z, inner);
}

float2 ClampSceneUV(float2 uv)
{
    return clamp(uv, _BlitTexture_TexelSize.xy * 0.5, 1.0 - _BlitTexture_TexelSize.xy * 0.5);
}

float3 SafeNormal(float3 normalWS) { return normalWS * rsqrt(max(dot(normalWS, normalWS), 0.000001)); }

float RawDepthFromEye(float eyeDepth)
{
    float perspective = (rcp(max(eyeDepth, 0.0001)) - _ZBufferParams.w) / _ZBufferParams.z;
    float orthographic = (eyeDepth - _ProjectionParams.y) / max(_ProjectionParams.z - _ProjectionParams.y, 0.0001);
    #if UNITY_REVERSED_Z
        orthographic = 1.0 - orthographic;
    #endif
    return lerp(perspective, orthographic, unity_OrthoParams.w);
}

float2 ScanMasks(float3 positionWS)
{
    if (_ScanCoverageMode < 0.5) return float2(1.0, 0.0);
    float3 cell = floor(positionWS / _ScanGridRange.x + 0.5);
    float distanceWS = distance(cell * _ScanGridRange.x, _ScanOriginProgress.xyz);
    distanceWS += (HashCell(cell) * 2.0 - 1.0) * _ScanGridRange.w;
    float radius = _ScanGridRange.y * _ScanCurveParams.y;
    float width = max(_ScanGridRange.z, 0.01);
    float started = smoothstep(0.0, 0.012, _ScanCurveParams.y);
    float inside = (1.0 - smoothstep(radius - width * 0.35, radius + width * 0.35, distanceWS)) * started;
    // Scan boosts end at the held state; their inward replay follows the same curve on close.
    float boostFade = 1.0 - smoothstep(0.975, 1.0, _ScanOriginProgress.w);
    float ring = saturate(1.0 - abs(distanceWS - radius) / width) * started * boostFade;
    return float2(inside, ring);
}

float2 GridUV(float3 positionWS, float3 normalWS)
{
    float spacing = max(0.01, _ScanGridRange.x / _ScanGridSettings.x);
    float3 n = abs(normalWS);
    if (n.y >= n.x && n.y >= n.z) return positionWS.xz / spacing;
    if (n.x >= n.z) return positionWS.yz / spacing;
    return positionWS.xy / spacing;
}

float2 TargetSample(float2 uv)
{
    return SAMPLE_TEXTURE2D_X(_ScanTargetMask, sampler_PointClamp, saturate(uv)).rg;
}


#endif
