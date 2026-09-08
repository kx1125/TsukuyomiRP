#ifndef TSUKUYOMI_DENOISE_UTILS_INCLUDED
#define TSUKUYOMI_DENOISE_UTILS_INCLUDED

// Pure denoising math. No textures, samplers, constant buffers, camera globals,
// pipeline includes or signal packing. Callers load/decode samples and choose
// the depth space, filter footprint, thresholds and channel/history semantics.
// Inputs must be finite; variances/tolerances must be positive, and weights
// nonnegative with a positive total weight when resolving an average.

// The caller supplies distance / sigma (or depth delta * inverse sigma).
float DenoiseGaussianWeight(float normalizedDistance)
{
    return exp(-normalizedDistance * normalizedDistance);
}

float3 DenoiseGaussianWeight(float3 normalizedDistance)
{
    return exp(-normalizedDistance * normalizedDistance);
}

float DenoiseSpatialWeight(float2 offset, float variance)
{
    return exp2(-dot(offset, offset) / variance);
}

float DenoiseLinearDepthWeight(float centerDepth, float sampleDepth, float sensitivity, float bias)
{
    return saturate(1.0 - (sensitivity * abs(sampleDepth - centerDepth) + bias));
}

float DenoiseExponentialDepthWeight(float centerDepth, float sampleDepth, float tolerance)
{
    return exp2(-abs(sampleDepth - centerDepth) / tolerance);
}

// Normals must be in the same space. The power form expects unit normals.
float DenoiseNormalWeight(float3 centerNormal, float3 sampleNormal, float exponent)
{
    return pow(saturate(dot(centerNormal, sampleNormal)), exponent);
}

// Preserve the separable shadow filter's quartic normal-error response.
float DenoiseNormalErrorWeight(float3 centerNormal, float3 sampleNormal, float strength)
{
    float closeness = max(0.0, dot(sampleNormal, centerNormal));
    closeness *= closeness;
    closeness *= closeness;
    return max(0.0, 1.0 - (1.0 - closeness) * strength);
}

float DenoisePlaneWeight(float3 centerPosition, float3 samplePosition,
    float3 centerNormal, float3 sampleNormal, float strength)
{
    float3 delta = centerPosition - samplePosition;
    float distanceSquared = dot(delta, delta);
    float planeError = max(abs(dot(delta, sampleNormal)), abs(dot(delta, centerNormal)));
    return distanceSquared < 0.0001 ? 1.0 :
        pow(max(0.0, 1.0 - 2.0 * strength * planeError / sqrt(distanceSquared)), 2.0);
}

float DenoiseBilinearWeight(float2 delta)
{
    float2 weights = 1.0 - saturate(abs(delta));
    return weights.x * weights.y;
}

// Depths must use the same encoding; tolerance is expressed in that space.
float4 DenoiseInverseDepthWeights(float centerDepth, float4 sampleDepths,
    float4 spatialWeights, float tolerance)
{
    return spatialWeights / (abs(centerDepth - sampleDepths) + tolerance);
}

// Four samples in the same order as weights. The extra weighted fallback is
// explicit: e.g. AO uses visibility 1, while another signal may use 0 or a
// center sample. No alpha or neutral-color policy is imposed by these helpers.
float DenoiseResolve4(float4 samples, float4 weights, float fallback, float fallbackWeight)
{
    return (dot(samples, weights) + fallback * fallbackWeight) / (dot(weights, 1.0) + fallbackWeight);
}

float3 DenoiseResolve4(float3 sample0, float3 sample1, float3 sample2, float3 sample3,
    float4 weights, float3 fallback, float fallbackWeight)
{
    float3 sum = sample0 * weights.x + sample1 * weights.y
        + sample2 * weights.z + sample3 * weights.w + fallback * fallbackWeight;
    return sum / (dot(weights, 1.0) + fallbackWeight);
}

float4 DenoiseResolve4(float4 sample0, float4 sample1, float4 sample2, float4 sample3,
    float4 weights, float4 fallback, float fallbackWeight)
{
    float4 sum = sample0 * weights.x + sample1 * weights.y
        + sample2 * weights.z + sample3 * weights.w + fallback * fallbackWeight;
    return sum / (dot(weights, 1.0) + fallbackWeight);
}

// Positions and normals must be in a common space across both frames.
// UV bounds, sky rejection and texture/history validity remain caller policy.
bool DenoiseHistoryGeometryValid(float3 position, float3 previousPosition,
    float3 normal, float3 previousNormal, float positionTolerance, float normalThreshold)
{
    return dot(normal, previousNormal) >= normalThreshold
        && distance(position, previousPosition) <= positionTolerance;
}

// Call only for valid history, with previousSampleCount >= 1 and maxSampleCount
// >= 1. The caller owns reset behavior and where the sample count is stored.
float DenoiseTemporalHistoryWeight(float previousSampleCount, float maxSampleCount,
    float maxHistoryWeight, out float sampleCount)
{
    sampleCount = min(previousSampleCount + 1.0, maxSampleCount);
    return min(maxHistoryWeight, (sampleCount - 1.0) / sampleCount);
}

#endif
