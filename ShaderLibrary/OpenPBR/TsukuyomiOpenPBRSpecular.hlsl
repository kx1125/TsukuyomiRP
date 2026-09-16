#ifndef TSUKUYOMI_OPENPBR_SPECULAR_INCLUDED
#define TSUKUYOMI_OPENPBR_SPECULAR_INCLUDED
#include "TsukuyomiOpenPBRTypes.hlsl"
#include "TsukuyomiOpenPBRLuts.hlsl"

float TsuOPBRG1(float3 W, TsukuyomiOpenPBRSpecularState s)
{
    float mu = max(dot(s.N, W), 0.0);
    float tx = dot(s.T, W) * s.alpha.x;
    float by = dot(s.B, W) * s.alpha.y;
    return 2.0 * mu / max(mu + sqrt(mu * mu + tx * tx + by * by), 1e-7);
}

float TsuOPBRDistribution(float3 H, TsukuyomiOpenPBRSpecularState s)
{
    float3 q = float3(dot(s.T, H) / s.alpha.x, dot(s.B, H) / s.alpha.y, dot(s.N, H));
    float d = dot(q, q);
    return rcp(max(TSU_OPBR_PI * s.alpha.x * s.alpha.y * d * d, 1e-12));
}

TsukuyomiOpenPBRSpecularState TsuOPBRPrepareSpecular(float3 N, float3 T, float sign,
    float3 V, float r, float anisotropy, float ior, float metalness, float3 color)
{
    TsukuyomiOpenPBRSpecularState s = (TsukuyomiOpenPBRSpecularState)0;
    s.N = N; s.V = V;
    TsuOPBRBasis(N, T, sign, s.T, s.B);
    s.roughness = max(r, sqrt(TSU_OPBR_MIN_ALPHA));
    float a = s.roughness * s.roughness;
    float aspect = sqrt(1.0 - 0.9 * anisotropy);
    s.alpha = max(float2(a / aspect, a * aspect), TSU_OPBR_MIN_ALPHA);
    s.NoV = max(dot(N, V), 1e-5);
    s.ior = ior; s.metalness = metalness;
    s.f0 = lerp(TsuOPBRF0(ior).xxx, color, metalness);
    // Directional energy of anisotropic GGX is approximated by its geometric
    // mean roughness. Direct D and G remain fully anisotropic in both tiers.
    s.dfg = TsuOPBRDFG(s.NoV, s.roughness);
    float3 favg = lerp(ior <= 1.00001 ? 0.0 : TsuOPBRF0(ior) + (1.0 - TsuOPBRF0(ior)) / 21.0,
                      color + (1.0 - color) / 21.0, metalness);
    s.msColor = favg * favg * s.dfg.z / max(1.0 - favg * (1.0 - s.dfg.z), 1e-5);
    float3 single = s.f0 * s.dfg.x + s.dfg.y;
    if (ior <= 1.00001) single *= metalness;
    float3 dielectric = TsuOPBRDielectricEnergy(s.NoV, s.roughness, ior).xxx;
    float3 singleHigh = lerp(dielectric, color * s.dfg.x + s.dfg.y, metalness);
    float3 fastScale = 1.0 + s.f0 * (rcp(max(s.dfg.w, 1e-4)) - 1.0);
    s.energyBalanced = saturate(single * fastScale);
    s.energyHigh = saturate(singleHigh + s.msColor * (1.0 - s.dfg.w));
    return s;
}

float3 TsuOPBRSpecularSingle(TsukuyomiOpenPBRSpecularState s, float3 L, bool high)
{
    float NoL = dot(s.N, L);
    if (NoL <= 0.0 || dot(s.N, s.V) <= 0.0) return 0.0;
    float3 H = TsuOPBRNormalize(L + s.V, s.N);
    float VoH = saturate(dot(s.V, H));
    float3 F = s.f0 + (1.0 - s.f0) * TsuOPBRPow5(1.0 - VoH);
    if (high)
        F = lerp(TsuOPBRFresnel(VoH, s.ior).xxx, F, s.metalness);
    else if (s.ior <= 1.00001)
        F *= s.metalness;
    return F * TsuOPBRDistribution(H, s) * TsuOPBRG1(s.V, s) * TsuOPBRG1(L, s)
        / max(4.0 * s.NoV * NoL, 1e-8);
}

float3 TsuOPBREvaluateSpecularBalanced(TsukuyomiOpenPBRSpecularState s, float3 L)
{
    return TsuOPBRSpecularSingle(s, L, false) * (1.0 + s.f0 * (rcp(max(s.dfg.w, 1e-4)) - 1.0));
}

float3 TsuOPBREvaluateSpecularHigh(TsukuyomiOpenPBRSpecularState s, float3 L)
{
    float NoL = dot(s.N, L);
    if (NoL <= 0.0 || dot(s.N, s.V) <= 0.0) return 0.0;
    float El = TsuOPBRDFG(NoL, s.roughness).w;
    float ms = (1.0 - s.dfg.w) * (1.0 - El) / max(1.0 - s.dfg.z, 1e-5);
    return TsuOPBRSpecularSingle(s, L, true) + s.msColor * (TSU_OPBR_INV_PI * ms);
}

#endif
