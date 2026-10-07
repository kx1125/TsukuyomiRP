#ifndef TSUKUYOMI_OPENPBR_RECT_LIGHT_INCLUDED
#define TSUKUYOMI_OPENPBR_RECT_LIGHT_INCLUDED
#include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Rect Light/RectLight.hlsl"

// The rectangle's diffuse quality remains a light setting. OpenPBR supplies
// layer colors and energies; its material tier independently selects the FGD.
// GGX shape uses the existing isotropic LTC fit, with an anisotropic stretch.
struct TsuOPBRRectSpecular
{
    float3x3 frame, inverseM, cosineFrame;
    float3 singleEnergy, multipleEnergy;
};

float3x3 TsuOPBRRectFrame(float3 N, float3 V)
{
    float3 T, B;
    TsuOPBRBasis(N, V, 1.0, T, B);
    return float3x3(T, B, N);
}

TsuOPBRRectSpecular TsuOPBRPrepareRectSpecular(TsukuyomiOpenPBRSpecularState s, bool high)
{
    TsuOPBRRectSpecular q = (TsuOPBRRectSpecular)0;
    // GGX is symmetric under B -> -B. Keep a right-handed frame so mirrored
    // mesh UVs cannot reverse the polygon winding and discard its integral.
    float3 B = cross(s.N, s.T);
    q.cosineFrame = float3x3(s.T, B, s.N);
    float alpha = sqrt(s.alpha.x * s.alpha.y);
    float2 stretch = alpha / s.alpha;
    float3x3 stretched = float3x3(s.T * stretch.x, B * stretch.y, s.N);
    float3 virtualV = TsuOPBRNormalize(mul(stretched, s.V), float3(0, 0, 1));
    q.frame = mul(TsuOPBRRectFrame(float3(0, 0, 1), virtualV), stretched);
    float2 uv = RectLutUV(float2(clamp(sqrt(alpha), .02, 1), sqrt(1 - saturate(virtualV.z))));
    float4 m = SAMPLE_TEXTURE2D_LOD(_TsukuyomiRectGGXMatrix, sampler_LinearClamp, uv, 0);
    q.inverseM = float3x3(m.x, 0, m.z, 0, 1, 0, m.y, 0, m.w);
    q.singleEnergy = s.energyBalanced;
    if (high)
    {
        // Preserve the OpenPBR single/multiple-scattering energy split. The
        // broad multiple-scattering lobe is approximated by a cosine LTC.
        float3 weightedMetalColor = max(s.f0 - (1 - s.metalness) * TsuOPBRF0(s.ior), 0);
        q.singleEnergy = saturate((1 - s.metalness) * TsuOPBRDielectricEnergy(s.NoV, s.roughness, s.ior)
            + weightedMetalColor * s.dfg.x + s.metalness * s.dfg.y);
        q.multipleEnergy = max(s.energyHigh - q.singleEnergy, 0);
    }
    if (dot(s.N, s.V) <= 0) { q.singleEnergy = 0; q.multipleEnergy = 0; }
    return q;
}

float3 TsuOPBRRectSpecularIntegral(TsuOPBRRectSpecular q, float3 c, float3 a, float3 b)
{
    float ss = RectGGXPolygon(q.inverseM, mul(q.frame, c), mul(q.frame, a), mul(q.frame, b));
    float3x3 identity = float3x3(1, 0, 0, 0, 1, 0, 0, 0, 1);
    float ms = RectGGXPolygon(identity, mul(q.cosineFrame, c), mul(q.cosineFrame, a), mul(q.cosineFrame, b));
    return ss * q.singleEnergy + ms * q.multipleEnergy;
}

float3 TsuOPBRURPRectLights(TsukuyomiOpenPBRPrepared p, InputData inputData, uint layers,
    TsukuyomiBRDFOcclusionFactor ao, TsukuyomiOpenPBRLightingOptions options, bool high)
{
    if (_TsukuyomiRectLightCount <= 0 || dot(p.geometry.geometricNormalWS, p.geometry.viewDirectionWS) <= 0
        || dot(p.geometry.normalWS, p.geometry.viewDirectionWS) <= 0) return 0;

    float3 baseEnergy = high ? p.baseSpecular.energyHigh : p.baseSpecular.energyBalanced;
    float3 coatEnergy = high ? p.coatSpecular.energyHigh : p.coatSpecular.energyBalanced;
    float3 diffuseColor = p.material.baseColor * (1 - p.material.metalness) * (1 - baseEnergy);
    float3x3 diffuseFrame = TsuOPBRRectFrame(p.geometry.normalWS, p.geometry.viewDirectionWS);
    float3x3 diffuseMatrix = (float3x3)0;
    float diffuseFGD = 0;
    if (_TsukuyomiRectLightHasHigh != 0)
    {
        float r = clamp(p.material.diffuseRoughness, .02, 1);
        float nv = saturate(p.baseSpecular.NoV);
        float4 m = SAMPLE_TEXTURE2D_LOD(_TsukuyomiRectDisneyMatrix, sampler_LinearClamp,
            RectLutUV(float2(r, sqrt(1 - nv))), 0);
        diffuseMatrix = float3x3(m.x, 0, m.y, 0, m.z, 0, m.w, 0, 1);
        diffuseFGD = SAMPLE_TEXTURE2D_LOD(_TsukuyomiRectDisneyFGD, sampler_LinearClamp,
            RectLutUV(float2(sqrt(nv), r)), 0).r + .5;
    }
#ifndef _SPECULARHIGHLIGHTS_OFF
    TsuOPBRRectSpecular baseLobe = TsuOPBRPrepareRectSpecular(p.baseSpecular, high);
    TsuOPBRRectSpecular coatLobe = (TsuOPBRRectSpecular)0;
    if (p.material.coatWeight > 0) coatLobe = TsuOPBRPrepareRectSpecular(p.coatSpecular, high);
    float3x3 fuzzFrame = float3x3(p.fuzzT, p.fuzzB, p.fuzzN);
    float3x3 fuzzMatrix = float3x3(p.fuzzLtc.x, 0, p.fuzzLtc.y, 0, p.fuzzLtc.x, 0, 0, 0, 1);
#endif
    float3 result = 0;
    [loop] for (int i = 0; i < min(_TsukuyomiRectLightCount, 64); ++i)
    {
        TsukuyomiRectLightData light = _TsukuyomiRectLights[i];
        if ((layers & light.layerMask) == 0) continue;
        float3 c = light.positionRange.xyz - inputData.positionWS;
        float d2 = dot(c, c);
        if (dot(light.forwardInvArea.xyz, -c) <= 1e-7 || d2 >= light.positionRange.w * light.positionRange.w) continue;
        float3 a = light.rightWidth.xyz * light.rightWidth.w;
        float3 b = light.upHeight.xyz * light.upHeight.w;
        // Do not reject a light merely because its center is below the horizon.
        // Reject only when all four corners are behind the geometric surface.
        float3 Ng = p.geometry.geometricNormalWS;
        if (dot(c, Ng) + abs(dot(a, Ng)) + abs(dot(b, Ng)) <= 0) continue;
        float3 radiance = light.colorIntensity.rgb * light.forwardInvArea.w * RectRangeFade(d2, light.positionRange.w);
        float3 dc = mul(diffuseFrame, c), da = mul(diffuseFrame, a), db = mul(diffuseFrame, b);
        float3 diffuse;
        if (light.quality == 0) diffuse = EvaluateRectDiffuseLow(light, c, p.geometry.normalWS, diffuseColor);
        else if (light.quality == 1) diffuse = RectLambertIrradiance(dc, da, db) * diffuseColor * radiance;
        else diffuse = RectDisneyPolygon(diffuseMatrix, dc, da, db) * diffuseFGD * diffuseColor * radiance;
        float3 specular = 0;
#ifndef _SPECULARHIGHLIGHTS_OFF
        specular = TsuOPBRRectSpecularIntegral(baseLobe, c, a, b) * radiance;
#endif
        if (p.material.coatWeight > 0)
        {
            // Absorption is evaluated at the light center; each reflection
            // lobe still integrates the rectangle in its own normal frame.
            float coatNoL = saturate(dot(p.geometry.coatNormalWS, RectSafeNormalize(c)));
            float3 transmission = high ? TsuOPBRCoatTransmission(p, coatNoL) : p.material.coatColor;
            float3 baseScale = lerp(1.0.xxx, (1 - coatEnergy) * transmission, p.material.coatWeight);
            diffuse *= baseScale; specular *= baseScale;
#ifndef _SPECULARHIGHLIGHTS_OFF
            specular += p.material.coatWeight * TsuOPBRRectSpecularIntegral(coatLobe, c, a, b) * radiance;
#endif
        }
        diffuse *= 1 - p.fuzzEnergy; specular *= 1 - p.fuzzEnergy;
#ifndef _SPECULARHIGHLIGHTS_OFF
        if (p.fuzzEnergy > 0 && p.fuzzLtc.x > 0 && dot(p.fuzzN, p.geometry.viewDirectionWS) > 0)
            specular += RectGGXPolygon(fuzzMatrix, mul(fuzzFrame, c), mul(fuzzFrame, a), mul(fuzzFrame, b))
                * p.material.fuzzColor * p.fuzzEnergy * radiance;
#endif
        float visibility = 1;
#if !defined(_RECEIVE_SHADOWS_OFF) && !defined(_SURFACE_TYPE_TRANSPARENT)
        TrySampleRTShadowSlice((int)light.shadowIndex, inputData.normalizedScreenSpaceUV, visibility);
#endif
        float microShadow = ComputeMicroShadowing(options.occlusion,
            saturate(dot(p.geometry.normalWS, RectSafeNormalize(c))), options.microShadowOpacity);
        // Polygon integrals already include cosine and 1/pi. No punctual PI bridge.
        result += (diffuse * ao.directAmbientOcclusion + specular * ao.directSpecularOcclusion) * (visibility * microShadow);
    }
    return result;
}
#endif
