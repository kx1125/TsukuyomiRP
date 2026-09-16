// EON formulas adapted from Adobe OpenPBR (Copyright 2026 Adobe), Apache-2.0.
// See Third Party Notices~/OpenPBR. Modifications: HLSL, cached view albedo,
// explicit fixed-quality functions and BRDF-only (no cosine) return convention.
#ifndef TSUKUYOMI_OPENPBR_DIFFUSE_INCLUDED
#define TSUKUYOMI_OPENPBR_DIFFUSE_INCLUDED
#include "TsukuyomiOpenPBRTypes.hlsl"

static const float TsuOPBRFONA = 0.5 - 2.0 / (3.0 * TSU_OPBR_PI);
static const float TsuOPBRFONB = 2.0 / 3.0 - 28.0 / (15.0 * TSU_OPBR_PI);

float TsuOPBRFONAlbedoBalanced(float mu, float r)
{
    float x = 1.0 - saturate(mu);
    float g = x * (0.0571085289 + x * (0.491881867 + x * (-0.332181442 + x * 0.0714429953)));
    return (1.0 + r * g) / (1.0 + TsuOPBRFONA * r);
}

float TsuOPBRFONAlbedoHigh(float mu, float r)
{
    mu = saturate(mu);
    float s = sqrt(max(0.0, 1.0 - mu * mu));
    float g = s * (acos(mu) - s * mu) + (2.0 / 3.0) *
        (s * mu * (1.0 + s + s * s) / (1.0 + s) - s);
    return (1.0 + r * g * TSU_OPBR_INV_PI) / (1.0 + TsuOPBRFONA * r);
}

float3 TsuOPBREON(float3 rho, float r, float NoV, float NoL, float VoL, float Ev, float El)
{
    if (NoV <= 0.0 || NoL <= 0.0) return 0.0;
    float AF = rcp(1.0 + TsuOPBRFONA * r);
    float s = VoL - NoV * NoL;
    float st = s > 0.0 ? s / max(max(NoV, NoL), 1e-6) : s;
    float3 ss = rho * TSU_OPBR_INV_PI * AF * (1.0 + r * st);
    float Eavg = AF * (1.0 + TsuOPBRFONB * r);
    float3 rhoMS = rho * rho * Eavg / max(1.0 - rho * (1.0 - Eavg), 1e-6);
    return ss + rhoMS * TSU_OPBR_INV_PI * max(0.0, 1.0 - Ev) * max(0.0, 1.0 - El) / max(1.0 - Eavg, 1e-6);
}

float3 TsuOPBREONAlbedo(float3 rho, float r, float Ev)
{
    float Eavg = (1.0 + TsuOPBRFONB * r) / (1.0 + TsuOPBRFONA * r);
    float3 rhoMS = rho * rho * Eavg / max(1.0 - rho * (1.0 - Eavg), 1e-6);
    return rho * Ev + rhoMS * (1.0 - Ev);
}

#endif
