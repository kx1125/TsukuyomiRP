#ifndef TSUKUYOMI_OPENPBR_INCLUDED
#define TSUKUYOMI_OPENPBR_INCLUDED
#include "TsukuyomiOpenPBRDiffuse.hlsl"
#include "TsukuyomiOpenPBRSpecular.hlsl"

// Prepare once per pixel, reuse for all lights and both quality entry points.
TsukuyomiOpenPBRPrepared TsukuyomiOpenPBRPrepare(TsukuyomiOpenPBRMaterial m, TsukuyomiOpenPBRGeometry g)
{
    TsukuyomiOpenPBRPrepared p = (TsukuyomiOpenPBRPrepared)0;
    m.baseColor = saturate(m.baseColor); m.metalness = saturate(m.metalness);
    m.diffuseRoughness = saturate(m.diffuseRoughness); m.specularRoughness = saturate(m.specularRoughness);
    m.anisotropy = saturate(m.anisotropy); m.specularIOR = clamp(m.specularIOR, 1.0, 3.0);
    m.coatWeight = saturate(m.coatWeight); m.coatColor = saturate(m.coatColor);
    m.coatIOR = clamp(m.coatIOR, 1.0, 3.0); m.coatRoughness = saturate(m.coatRoughness);
    m.coatDarkening = saturate(m.coatDarkening);
    m.fuzzWeight = saturate(m.fuzzWeight); m.fuzzColor = saturate(m.fuzzColor); m.fuzzRoughness = saturate(m.fuzzRoughness);
    g.geometricNormalWS = TsuOPBRNormalize(g.geometricNormalWS, float3(0, 0, 1));
    g.normalWS = TsuOPBRNormalize(g.normalWS, g.geometricNormalWS);
    g.coatNormalWS = TsuOPBRNormalize(g.coatNormalWS, g.normalWS);
    g.viewDirectionWS = TsuOPBRNormalize(g.viewDirectionWS, g.normalWS);
    p.material = m; p.geometry = g;
    p.baseSpecular = TsuOPBRPrepareSpecular(g.normalWS, g.tangentWS, g.tangentSign, g.viewDirectionWS,
        m.specularRoughness, m.anisotropy, m.specularIOR, m.metalness, m.baseColor);
    if (m.coatWeight > 0.0)
        p.coatSpecular = TsuOPBRPrepareSpecular(g.coatNormalWS, g.tangentWS, g.tangentSign,
            g.viewDirectionWS, m.coatRoughness, 0.0, m.coatIOR, 0.0, 1.0);
    p.fuzzN = TsuOPBRNormalize(lerp(g.normalWS, g.coatNormalWS, m.coatWeight), g.normalWS);
    // LTC coordinates align +X with the projected view direction.
    TsuOPBRBasis(p.fuzzN, g.viewDirectionWS, 1.0, p.fuzzT, p.fuzzB);
    if (m.fuzzWeight > 0.0)
        p.fuzzLtc = TsuOPBRFuzzCoefficients(saturate(dot(p.fuzzN, g.viewDirectionWS)), m.fuzzRoughness);
    p.fuzzEnergy = m.fuzzWeight * saturate(p.fuzzLtc.z);
    // OpenPBR 1.1.1 interfaced-Lambertian darkening approximation.
    float Fav = TsuOPBRF0(m.coatIOR) + (1.0 - TsuOPBRF0(m.coatIOR)) / 21.0;
    if (m.coatIOR <= 1.00001) Fav = 0.0;
    float Kr = 1.0 - (1.0 - Fav) / (m.coatIOR * m.coatIOR);
    float K = lerp(Fav, Kr, lerp(1.0, m.specularRoughness, m.metalness));
    float3 Eb = lerp(m.baseColor + (1.0 - m.baseColor) * TsuOPBRF0(m.specularIOR), m.baseColor, m.metalness);
    p.coatDarkening = lerp(1.0, (1.0 - K) / max(1.0 - Eb * K, 1e-5), m.coatDarkening);
    p.diffuseViewAlbedoBalanced = TsuOPBRFONAlbedoBalanced(p.baseSpecular.NoV, m.diffuseRoughness);
    p.diffuseViewAlbedoHigh = TsuOPBRFONAlbedoHigh(p.baseSpecular.NoV, m.diffuseRoughness);
    return p;
}

// LTC PDF divided by NdotL algebraically, avoiding a grazing 0/0.
// Derived from Zeltner/Burley/Chiang via Adobe (Apache-2.0; see notices).
float3 TsuOPBREvaluateFuzz(TsukuyomiOpenPBRPrepared p, float3 L)
{
    if (dot(p.fuzzN, L) <= 0.0 || dot(p.fuzzN, p.geometry.viewDirectionWS) <= 0.0) return 0.0;
    float3 w = float3(dot(p.fuzzT, L), dot(p.fuzzB, L), dot(p.fuzzN, L));
    float a = p.fuzzLtc.x;
    float3 q = float3(a * w.x + p.fuzzLtc.y * w.z, a * w.y, w.z);
    float l2 = dot(q, q);
    if (a <= 0.0 || l2 <= 1e-12) return 0.0;
    return p.material.fuzzColor * p.fuzzEnergy * TSU_OPBR_INV_PI * (a / l2) * (a / l2);
}

float3 TsuOPBRCoatTransmission(TsukuyomiOpenPBRPrepared p, float NoL)
{
    float eta = p.material.coatIOR;
    float cv = sqrt(max(1e-5, 1.0 - (1.0 - p.coatSpecular.NoV * p.coatSpecular.NoV) / (eta * eta)));
    float cl = sqrt(max(1e-5, 1.0 - (1.0 - NoL * NoL) / (eta * eta)));
    return pow(max(p.material.coatColor, 1e-8), 0.5 * (rcp(cv) + rcp(cl))) * p.coatDarkening;
}

// All aggregate direct outputs are BRDF values relative to base NdotL.
// Coat/fuzz normals are converted to this measure here; callers multiply the
// base cosine exactly once. Backfacing geometry never receives opaque light.
TsukuyomiOpenPBRResponse TsuOPBREvaluateDirect(TsukuyomiOpenPBRPrepared p, float3 L, bool high, float baseSpecularStrength)
{
    TsukuyomiOpenPBRResponse o = (TsukuyomiOpenPBRResponse)0;
    L = TsuOPBRNormalize(L, p.geometry.normalWS);
    float NoL = dot(p.geometry.normalWS, L);
    if (NoL <= 0.0 || dot(p.geometry.normalWS, p.geometry.viewDirectionWS) <= 0.0 ||
        dot(p.geometry.geometricNormalWS, L) <= 0.0 || dot(p.geometry.geometricNormalWS, p.geometry.viewDirectionWS) <= 0.0) return o;
    baseSpecularStrength = saturate(baseSpecularStrength);
    float3 baseEnergy = (high ? p.baseSpecular.energyHigh : p.baseSpecular.energyBalanced) * baseSpecularStrength;
    float El = high ? TsuOPBRFONAlbedoHigh(NoL, p.material.diffuseRoughness) : TsuOPBRFONAlbedoBalanced(NoL, p.material.diffuseRoughness);
    float Ev = high ? p.diffuseViewAlbedoHigh : p.diffuseViewAlbedoBalanced;
    o.diffuse = TsuOPBREON(p.material.baseColor, p.material.diffuseRoughness, p.baseSpecular.NoV,
        NoL, dot(p.geometry.viewDirectionWS, L), Ev, El) * (1.0 - p.material.metalness) * (1.0 - baseEnergy);
    o.specular = high ? TsuOPBREvaluateSpecularHigh(p.baseSpecular, L) : TsuOPBREvaluateSpecularBalanced(p.baseSpecular, L);
    o.specular *= baseSpecularStrength;
    if (p.material.coatWeight > 0.0)
    {
        float3 coatEnergy = high ? p.coatSpecular.energyHigh : p.coatSpecular.energyBalanced;
        float coatNoL = saturate(dot(p.geometry.coatNormalWS, L));
        float3 transmission = high ? TsuOPBRCoatTransmission(p, coatNoL) : p.material.coatColor;
        float3 baseScale = lerp(1.0, (1.0 - coatEnergy) * transmission, p.material.coatWeight);
        o.diffuse *= baseScale; o.specular *= baseScale;
        float3 coat = high ? TsuOPBREvaluateSpecularHigh(p.coatSpecular, L) : TsuOPBREvaluateSpecularBalanced(p.coatSpecular, L);
        o.specular += p.material.coatWeight * coat * (coatNoL / max(NoL, 1e-6));
    }
    o.diffuse *= 1.0 - p.fuzzEnergy;
    o.specular *= 1.0 - p.fuzzEnergy;
    if (p.material.fuzzWeight > 0.0)
        o.specular += TsuOPBREvaluateFuzz(p, L) * saturate(dot(p.fuzzN, L)) / max(NoL, 1e-6);
    return o;
}

TsukuyomiOpenPBRResponse TsuOPBREvaluateDirect(TsukuyomiOpenPBRPrepared p, float3 L, bool high)
{ return TsuOPBREvaluateDirect(p, L, high, 1.0); }

// Base-lobe strength applies to both metals and dielectrics. Coat/fuzz are unchanged.
TsukuyomiOpenPBRResponse TsukuyomiOpenPBREvaluateDirectHigh(TsukuyomiOpenPBRPrepared p, float3 L, float baseSpecularStrength)
{ return TsuOPBREvaluateDirect(p, L, true, baseSpecularStrength); }

TsukuyomiOpenPBRResponse TsukuyomiOpenPBREvaluateDirectBalanced(TsukuyomiOpenPBRPrepared p, float3 L)
{ return TsuOPBREvaluateDirect(p, L, false); }
TsukuyomiOpenPBRResponse TsukuyomiOpenPBREvaluateDirectHigh(TsukuyomiOpenPBRPrepared p, float3 L)
{ return TsuOPBREvaluateDirect(p, L, true); }

TsukuyomiOpenPBRResponse TsuOPBREvaluateIBL(TsukuyomiOpenPBRPrepared p, TsukuyomiOpenPBREnvironment e, bool high, float baseSpecularStrength)
{
    TsukuyomiOpenPBRResponse o = (TsukuyomiOpenPBRResponse)0;
    if (dot(p.geometry.geometricNormalWS, p.geometry.viewDirectionWS) <= 0.0 ||
        dot(p.geometry.normalWS, p.geometry.viewDirectionWS) <= 0.0) return o;
    float3 energy = (high ? p.baseSpecular.energyHigh : p.baseSpecular.energyBalanced) * saturate(baseSpecularStrength);
    float Ev = high ? p.diffuseViewAlbedoHigh : p.diffuseViewAlbedoBalanced;
    o.diffuse = e.diffuse * TsuOPBREONAlbedo(p.material.baseColor, p.material.diffuseRoughness, Ev)
        * (1.0 - p.material.metalness) * (1.0 - energy);
    o.specular = e.baseSpecular * energy;
    if (p.material.coatWeight > 0.0)
    {
        float3 coatEnergy = high ? p.coatSpecular.energyHigh : p.coatSpecular.energyBalanced;
        // Irradiance lacks incoming directions. Use the cosine-weighted mean
        // incoming cosine (2/3) for coat absorption; explicitly an IBL approximation.
        float3 transmission = high ? TsuOPBRCoatTransmission(p, 2.0 / 3.0) : p.material.coatColor;
        float3 scale = lerp(1.0, (1.0 - coatEnergy) * transmission, p.material.coatWeight);
        o.diffuse *= scale; o.specular *= scale;
        o.specular += e.coatSpecular * coatEnergy * p.material.coatWeight;
    }
    o.diffuse *= 1.0 - p.fuzzEnergy;
    o.specular = o.specular * (1.0 - p.fuzzEnergy) + e.fuzz * p.fuzzEnergy * p.material.fuzzColor;
    return o;
}

TsukuyomiOpenPBRResponse TsuOPBREvaluateIBL(TsukuyomiOpenPBRPrepared p, TsukuyomiOpenPBREnvironment e, bool high)
{ return TsuOPBREvaluateIBL(p, e, high, 1.0); }

TsukuyomiOpenPBRResponse TsukuyomiOpenPBREvaluateIBLHigh(TsukuyomiOpenPBRPrepared p, TsukuyomiOpenPBREnvironment e, float baseSpecularStrength)
{ return TsuOPBREvaluateIBL(p, e, true, baseSpecularStrength); }

TsukuyomiOpenPBRResponse TsukuyomiOpenPBREvaluateIBLBalanced(TsukuyomiOpenPBRPrepared p, TsukuyomiOpenPBREnvironment e)
{ return TsuOPBREvaluateIBL(p, e, false); }
TsukuyomiOpenPBRResponse TsukuyomiOpenPBREvaluateIBLHigh(TsukuyomiOpenPBRPrepared p, TsukuyomiOpenPBREnvironment e)
{ return TsuOPBREvaluateIBL(p, e, true); }

#endif
