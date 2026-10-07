#ifndef TSUKUYOMI_RECT_LIGHT_INCLUDED
#define TSUKUYOMI_RECT_LIGHT_INCLUDED
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"
#include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Ray Traced Shadow/RTShadowSampling.hlsl"

struct TsukuyomiRectLightData
{
    float4 positionRange;
    float4 rightWidth;
    float4 upHeight;
    float4 forwardInvArea;
    float4 colorIntensity;
    uint quality, layerMask, shadowIndex, padding1;
};
StructuredBuffer<TsukuyomiRectLightData> _TsukuyomiRectLights;
int _TsukuyomiRectLightCount, _TsukuyomiRectLightHasHigh;
TEXTURE2D(_TsukuyomiRectGGXMatrix);
TEXTURE2D(_TsukuyomiRectGGXAmplitude);
TEXTURE2D(_TsukuyomiRectDisneyMatrix);
TEXTURE2D(_TsukuyomiRectDisneyFGD);
// LTC lookup filtering is independent of any one texture. A texture-associated
// sampler becomes invalid when that texture is stripped (for example specular-off).

float3 RectSafeNormalize(float3 v) { return v * rsqrt(max(dot(v, v), 1e-20)); }
float2 RectLutUV(float2 uv) { return saturate(uv) * (63.0 / 64.0) + .5 / 64.0; }

struct RectMaterialData
{
    float3x3 worldToLocal, specularMatrix, diffuseMatrix;
    float3 diffuseColor, specularAmplitude;
    float diffuseFGD;
};

RectMaterialData PrepareRectMaterial(float3 N, float3 V, float3 baseColor, float metallic,
    float perceptualRoughness, bool multiScatter, bool hasHigh)
{
    RectMaterialData p = (RectMaterialData)0;
    float nv = saturate(dot(N, V));
    float3 t = V - N * dot(N, V);
    if (dot(t, t) < 1e-8) t = cross(abs(N.z) < .999 ? float3(0, 0, 1) : float3(0, 1, 0), N);
    t = RectSafeNormalize(t);
    p.worldToLocal = float3x3(t, cross(N, t), N);
    float r = clamp(perceptualRoughness, .02, 1);
    float2 uv = RectLutUV(float2(r, sqrt(1 - nv)));
    float4 m = SAMPLE_TEXTURE2D_LOD(_TsukuyomiRectGGXMatrix, sampler_LinearClamp, uv, 0);
    float2 amp = SAMPLE_TEXTURE2D_LOD(_TsukuyomiRectGGXAmplitude, sampler_LinearClamp, uv, 0).xy;
    // selfshadow exports GLM columns, normalized by inverseM[1][1].
    p.specularMatrix = float3x3(m.x, 0, m.z, 0, 1, 0, m.y, 0, m.w);
    float3 f0 = lerp(.04.xxx, baseColor, metallic);
    p.specularAmplitude = f0 * amp.x + (1 - f0) * amp.y;
    if (multiScatter) p.specularAmplitude *= 1 + f0 * (rcp(max(amp.x, 1e-4)) - 1);
    p.diffuseColor = baseColor * (1 - metallic);
    if (hasHigh)
    {
        m = SAMPLE_TEXTURE2D_LOD(_TsukuyomiRectDisneyMatrix, sampler_LinearClamp, uv, 0);
        // HDRP packing differs: m00, m02, m11, m20; m22 = 1.
        p.diffuseMatrix = float3x3(m.x, 0, m.y, 0, m.z, 0, m.w, 0, 1);
        p.diffuseFGD = SAMPLE_TEXTURE2D_LOD(_TsukuyomiRectDisneyFGD, sampler_LinearClamp,
            RectLutUV(float2(sqrt(nv), r)), 0).r + .5;
    }
    return p;
}

float RectMobileRadial(float distanceSquared, float rangeSquared)
{
    float a = saturate(1 - distanceSquared / max(rangeSquared, 1e-8));
    a *= a; a *= a; return a * a;
}
float RectMobileCone(float cosAngle)
{
    const float outer = 0.0174349558003; // cos(89.001 degrees), inner = cos(70 degrees)
    float a = saturate((cosAngle - outer) / (0.3420201433 - outer));
    return a * a;
}
float3 EvaluateRectDiffuseLow(TsukuyomiRectLightData light, float3 toLight, float3 N, float3 diffuseColor)
{
    float d2 = dot(toLight, toLight);
    float3 L = RectSafeNormalize(toLight);
    return diffuseColor * INV_PI * light.colorIntensity.rgb * saturate(dot(N, L))
        * RectMobileRadial(d2, light.positionRange.w * light.positionRange.w)
        * RectMobileCone(dot(light.forwardInvArea.xyz, -L));
}

void RectVertices(float3 c, float3 a, float3 b, out float3 v[4])
{
    v[0] = c - a - b; v[1] = c - a + b;
    v[2] = c + a + b; v[3] = c + a - b;
}

bool RectTinyProjection(float3 c, float3 a, float3 b)
{
    return max(dot(a, a), dot(b, b)) < dot(c, c) * 1e-6;
}
// First-order solid-angle moment. Below a 0.001-radian half-extent, omitted terms
// are O(angle^2); this avoids subtracting nearly identical FP32 edge vectors.
float3 RectSmallFormFactor(float3 c, float3 a, float3 b)
{
    float d2 = max(dot(c, c), 1e-20);
    return c * (4 * abs(dot(c, cross(a, b))) * INV_PI / (d2 * d2));
}

float RectLambertIrradiance(float3 c, float3 a, float3 b)
{
    float3 vectorIrradiance = 0;
    if (RectTinyProjection(c, a, b))
    {
        // Preserve the UE edge approximation's small-angle weight, rather than changing its energy.
        vectorIrradiance = RectSmallFormFactor(c, a, b) * (TWO_PI * (1.5708 - .175) * rsqrt(2.0));
    }
    else
    {
        float3 v[4]; RectVertices(c, a, b, v);
        [unroll] for (int i = 0; i < 4; i++) v[i] = RectSafeNormalize(v[i]);
        [unroll] for (int j = 0; j < 4; j++)
        {
            float cosine = clamp(dot(v[j], v[(j + 1) % 4]), -1, 1);
            float angleWeight = (1.5708 - .175 * cosine) * rsqrt(max(1 + cosine, 1e-10));
            vectorIrradiance += cross(v[j], v[(j + 1) % 4]) * angleWeight;
        }
    }
    float magnitude = length(vectorIrradiance);
    float irradiance = .5 * magnitude;
    float noL = vectorIrradiance.z / max(magnitude, 1e-30);
    float sinAlpha = sqrt(saturate(irradiance * INV_PI));
    // SphereHorizonCosWrap: approximate clipping to the receiving hemisphere.
    if (noL < sinAlpha)
    {
        float wrapped = sinAlpha + max(noL, -sinAlpha);
        noL = wrapped * wrapped / max(4 * sinAlpha, 1e-10);
    }
    return max(0, irradiance * noL) * INV_PI;
}

float3 RectEdge(float3 a, float3 b)
{
    float3 edge = cross(a, b);
    float sine = length(edge);
    return edge * (atan2(sine, clamp(dot(a, b), -1, 1)) / max(sine, 1e-10));
}

// HDRP default horizon approximation. Output includes the cosine's 1/pi normalization.
float RectDisneyPolygon(float3x3 inverseM, float3 c, float3 a, float3 b)
{
    if (c.z + abs(a.z) + abs(b.z) <= 0) return 0;
    c = mul(inverseM, c); a = mul(inverseM, a); b = mul(inverseM, b);
    if (c.z + abs(a.z) + abs(b.z) <= 0) return 0;
    if (RectTinyProjection(c, a, b))
    {
        float3 f = RectSmallFormFactor(c, a, b);
        float len = length(f);
        return max(0, (len * len + f.z) / (len + 1));
    }
    float3 v[4]; RectVertices(c, a, b, v);
    [unroll] for (int i = 0; i < 4; i++) v[i] = RectSafeNormalize(v[i]);
    float3 f = (RectEdge(v[0], v[1]) + RectEdge(v[1], v[2])
        + RectEdge(v[2], v[3]) + RectEdge(v[3], v[0])) * INV_TWO_PI;
    float len = length(f);
    return max(0, (len * len + f.z) / (len + 1));
}

// GGX uses exact polygon clipping in the transformed cosine frame (at most five vertices).
float RectGGXPolygon(float3x3 inverseM, float3 c, float3 a, float3 b)
{
    if (c.z + abs(a.z) + abs(b.z) <= 0) return 0;
    c = mul(inverseM, c); a = mul(inverseM, a); b = mul(inverseM, b);
    if (RectTinyProjection(c, a, b) && abs(c.z) > abs(a.z) + abs(b.z))
        return max(0, RectSmallFormFactor(c, a, b).z);
    float3 v[4]; RectVertices(c, a, b, v);
    float3 clipped[5]; uint count = 0;
    [unroll] for (uint i = 0; i < 4; i++)
    {
        float3 start = v[i], end = v[(i + 1) % 4];
        bool inside = start.z > 0, nextInside = end.z > 0;
        if (inside) clipped[count++] = start;
        if (inside != nextInside)
        {
            float t = start.z / (start.z - end.z);
            clipped[count++] = lerp(start, end, t);
        }
    }
    if (count < 3) return 0;
    float3 sum = 0;
    [loop] for (uint j = 0; j < count; j++)
        sum += RectEdge(RectSafeNormalize(clipped[j]), RectSafeNormalize(clipped[(j + 1) % count]));
    return max(0, sum.z) * INV_TWO_PI;
}

// Finite-range window only; geometric falloff already comes from the polygon integral.
float RectRangeFade(float d2, float range)
{
    float x = d2 / max(range * range, 1e-8);
    float a = saturate(1 - x * x);
    return a * a;
}

void EvaluateRectLight(TsukuyomiRectLightData light, RectMaterialData material, float3 positionWS,
    float3 normalWS, out float3 diffuse, out float3 specular)
{
    diffuse = 0; specular = 0;
    float3 toLight = light.positionRange.xyz - positionWS;
    float d2 = dot(toLight, toLight);
    // One-sided emitter; a coplanar receiver has zero projected emitting area.
    if (dot(light.forwardInvArea.xyz, -toLight) <= 1e-7 || d2 >= light.positionRange.w * light.positionRange.w) return;
    float3 c = mul(material.worldToLocal, toLight);
    float3 a = mul(material.worldToLocal, light.rightWidth.xyz * light.rightWidth.w);
    float3 b = mul(material.worldToLocal, light.upHeight.xyz * light.upHeight.w);
    float3 radiance = light.colorIntensity.rgb * light.forwardInvArea.w * RectRangeFade(d2, light.positionRange.w);
    specular = RectGGXPolygon(material.specularMatrix, c, a, b) * material.specularAmplitude * radiance;
    if (light.quality == 0)
        diffuse = EvaluateRectDiffuseLow(light, toLight, normalWS, material.diffuseColor);
    else if (light.quality == 1)
        diffuse = RectLambertIrradiance(c, a, b) * material.diffuseColor * radiance;
    else
        diffuse = RectDisneyPolygon(material.diffuseMatrix, c, a, b) * material.diffuseFGD * material.diffuseColor * radiance;
}

void AccumulateRectLights(float3 positionWS, float3 normalWS, float3 viewWS, uint renderingLayer,
    float3 baseColor, float metallic, float roughness, bool multiScatter, out float3 diffuse, out float3 specular)
{
    diffuse = 0; specular = 0;
    if (_TsukuyomiRectLightCount <= 0) return;
    RectMaterialData material = PrepareRectMaterial(normalWS, viewWS, baseColor, saturate(metallic),
        roughness, multiScatter, _TsukuyomiRectLightHasHigh != 0);
    [loop] for (int i = 0; i < min(_TsukuyomiRectLightCount, 64); i++)
    {
        TsukuyomiRectLightData light = _TsukuyomiRectLights[i];
        if ((renderingLayer & light.layerMask) == 0) continue;
        float3 d, s;
        EvaluateRectLight(light, material, positionWS, normalWS, d, s);
        diffuse += d; specular += s;
    }
}
// Screen-space shadows are explicit so the original entry point remains usable in other passes.
void AccumulateRectLights(float3 positionWS, float3 normalWS, float3 viewWS, uint renderingLayer,
    float3 baseColor, float metallic, float roughness, bool multiScatter, float2 screenUV,
    out float3 diffuse, out float3 specular)
{
    diffuse = 0; specular = 0;
    if (_TsukuyomiRectLightCount <= 0) return;
    RectMaterialData material = PrepareRectMaterial(normalWS, viewWS, baseColor, saturate(metallic),
        roughness, multiScatter, _TsukuyomiRectLightHasHigh != 0);
    [loop] for (int i = 0; i < min(_TsukuyomiRectLightCount, 64); i++)
    {
        TsukuyomiRectLightData light = _TsukuyomiRectLights[i];
        if ((renderingLayer & light.layerMask) == 0) continue;
        float3 d, s;
        EvaluateRectLight(light, material, positionWS, normalWS, d, s);
        float visibility;
        TrySampleRTShadowSlice((int)light.shadowIndex, screenUV, visibility);
        diffuse += d * visibility;
        specular += s * visibility;
    }
}
#endif
