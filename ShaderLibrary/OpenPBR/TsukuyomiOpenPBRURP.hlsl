#ifndef TSUKUYOMI_OPENPBR_URP_INCLUDED
#define TSUKUYOMI_OPENPBR_URP_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Lighting/TsukuyomiEvaluateMaterial.hlsl"
#include "TsukuyomiOpenPBR.hlsl"

// Optional caller hook for planar reflection or another radiance provider.
// Signature matches GlossyEnvironmentReflection(R, position, roughness, AO, uv).
#ifndef TSUKUYOMI_OPENPBR_SAMPLE_ENVIRONMENT
#define TSUKUYOMI_OPENPBR_SAMPLE_ENVIRONMENT GlossyEnvironmentReflection
#endif

struct TsukuyomiOpenPBRLightingOptions
{
    float occlusion;
    float microShadowOpacity;
    float indirectDiffuseIntensity;
    float indirectSpecularIntensity;
    float horizonOcclusionPower;
};

TsukuyomiOpenPBRLightingOptions TsukuyomiOpenPBRDefaultLightingOptions()
{
    TsukuyomiOpenPBRLightingOptions o;
    o.occlusion = 1; o.microShadowOpacity = 1;
    o.indirectDiffuseIntensity = 1; o.indirectSpecularIntensity = 1;
    o.horizonOcclusionPower = 2;
    return o;
}

TsukuyomiOpenPBRMaterial TsukuyomiOpenPBRFromURPSurface(SurfaceData s)
{
    TsukuyomiOpenPBRMaterial m = TsukuyomiOpenPBRDefaultMaterial();
    m.baseColor = s.albedo; m.metalness = s.metallic;
    m.specularRoughness = 1.0 - s.smoothness;
    m.diffuseRoughness = m.specularRoughness; // legacy compatibility mapping only
    m.coatWeight = s.clearCoatMask; m.coatRoughness = 1.0 - s.clearCoatSmoothness;
    return m;
}

float3 TsuOPBRURPRadiance(float3 R, float r, InputData inputData, float3 geometricNormal, float horizonPower)
{
#ifdef _ENVIRONMENTREFLECTIONS_OFF
    return 0.0;
#else
    float horizon = saturate(1.0 + dot(R, geometricNormal));
    return TSUKUYOMI_OPENPBR_SAMPLE_ENVIRONMENT(R, inputData.positionWS, r, 1.0,
        inputData.normalizedScreenSpaceUV) * pow(horizon, max(horizonPower, 0.0));
#endif
}

float3 TsuOPBRURPBaseEnvironment(TsukuyomiOpenPBRPrepared p, InputData i, float horizon, bool high)
{
    float3 R = reflect(-p.geometry.viewDirectionWS, p.geometry.normalWS);
    float r = p.baseSpecular.roughness;
    if (p.material.anisotropy <= 0.0001)
        return TsuOPBRURPRadiance(R, r, i, p.geometry.geometricNormalWS, horizon);
    // A broad major-axis lobe is represented by a bent normal in Balanced.
    float3 anisoNormal = TsuOPBRNormalize(cross(p.baseSpecular.B,
        cross(p.geometry.viewDirectionWS, p.baseSpecular.B)), p.geometry.normalWS);
    float3 bentNormal = TsuOPBRNormalize(lerp(p.geometry.normalWS, anisoNormal,
        p.material.anisotropy * r), p.geometry.normalWS);
    R = reflect(-p.geometry.viewDirectionWS, bentNormal);
    if (!high) return TsuOPBRURPRadiance(R, r, i, p.geometry.geometricNormalWS, horizon);
    // Deterministic four-tap major-axis quadrature of prefiltered radiance.
    // This is a realtime approximation, not unbiased environment integration.
    float3 axis = TsuOPBRNormalize(p.baseSpecular.T - R * dot(R, p.baseSpecular.T), p.baseSpecular.B);
    float spread = p.material.anisotropy * r * 0.75;
    float minorRoughness = sqrt(p.baseSpecular.alpha.y);
    float3 sum = 0;
    [unroll] for (int k = 0; k < 4; ++k)
    {
        float offset = (float(k) - 1.5) / 1.5;
        float3 direction = TsuOPBRNormalize(R + axis * offset * spread, R);
        sum += TsuOPBRURPRadiance(direction, minorRoughness, i, p.geometry.geometricNormalWS, horizon);
    }
    return sum * 0.25;
}

float3 TsuOPBRURPFuzzEnvironment(TsukuyomiOpenPBRPrepared p, InputData i, float horizon, bool high)
{
    if (p.fuzzEnergy <= 0.0 || p.fuzzLtc.x <= 0.0) return 0.0;
    // Sample the fitted LTC transform (M) using fixed cosine-disk points.
    float3 sum = 0;
    int count = high ? 4 : 1;
    [unroll] for (int k = 0; k < count; ++k)
    {
        float phi = (float(k) + 0.5) * (2.0 * TSU_OPBR_PI / 4.0);
        float diskRadius = high ? 0.70710678 : 0.0;
        float z = sqrt(1.0 - diskRadius * diskRadius);
        float3 w = float3((diskRadius * cos(phi) - p.fuzzLtc.y * z) / p.fuzzLtc.x,
                         diskRadius * sin(phi) / p.fuzzLtc.x, z);
        float3 direction = TsuOPBRNormalize(p.fuzzT * w.x + p.fuzzB * w.y + p.fuzzN * w.z, p.fuzzN);
        sum += TsuOPBRURPRadiance(direction, p.material.fuzzRoughness, i, p.geometry.geometricNormalWS, horizon);
    }
    return sum / float(count);
}

TsukuyomiOpenPBREnvironment TsuOPBRURPSampleEnvironment(TsukuyomiOpenPBRPrepared p, InputData i,
    TsukuyomiOpenPBRLightingOptions options, bool high)
{
    TsukuyomiOpenPBREnvironment e = (TsukuyomiOpenPBREnvironment)0;
    e.diffuse = i.bakedGI; // APV, lightmap or SSGI: caller chooses the source once.
    e.baseSpecular = TsuOPBRURPBaseEnvironment(p, i, options.horizonOcclusionPower, high);
    if (p.material.coatWeight > 0.0)
        e.coatSpecular = TsuOPBRURPRadiance(reflect(-p.geometry.viewDirectionWS, p.geometry.coatNormalWS),
            p.coatSpecular.roughness, i, p.geometry.geometricNormalWS, options.horizonOcclusionPower);
    e.fuzz = TsuOPBRURPFuzzEnvironment(p, i, options.horizonOcclusionPower, high);
    return e;
}

float3 TsuOPBRURPLight(TsukuyomiOpenPBRPrepared p, Light light,
    TsukuyomiBRDFOcclusionFactor ao, TsukuyomiOpenPBRLightingOptions options, bool high)
{
    TsukuyomiOpenPBRResponse f;
    if (high) f = TsukuyomiOpenPBREvaluateDirectHigh(p, light.direction);
    else f = TsukuyomiOpenPBREvaluateDirectBalanced(p, light.direction);
#ifdef _SPECULARHIGHLIGHTS_OFF
    f.specular = 0;
#endif
    float NoL = saturate(dot(p.geometry.normalWS, light.direction));
    float attenuation = light.distanceAttenuation * light.shadowAttenuation;
    attenuation *= ComputeMicroShadowing(options.occlusion, NoL, options.microShadowOpacity);
    // URP punctual lights use the historical no-PI diffuse convention. The
    // physical core stays normalized; this is the ONLY direct-light PI bridge.
    return (f.diffuse * ao.directAmbientOcclusion + f.specular * ao.directSpecularOcclusion)
        * light.color * (TSU_OPBR_PI * NoL * attenuation);
}

float3 TsuOPBRURPFragment(InputData inputData, TsukuyomiOpenPBRPrepared p,
    TsukuyomiOpenPBRLightingOptions options, bool high)
{
    AmbientOcclusionFactor screenAO = TsukuyomiGetScreenSpaceAmbientOcclusion(inputData.normalizedScreenSpaceUV);
    TsukuyomiBRDFOcclusionFactor ao = TsukuyomiCreateBRDFOcclusionFactorMultiBounce(screenAO,
        p.baseSpecular.NoV, p.material.specularRoughness, options.occlusion,
        p.material.baseColor * (1.0 - p.material.metalness), options.occlusion, p.baseSpecular.f0);
    uint layers = GetMeshRenderingLayer();
    half4 shadowMask = CalculateShadowMask(inputData);
    Light main = GetMainLight(inputData.shadowCoord, inputData.positionWS, shadowMask);
    MixRealtimeAndBakedGI(main, inputData.normalWS, inputData.bakedGI);
    TsukuyomiOpenPBREnvironment e = TsuOPBRURPSampleEnvironment(p, inputData, options, high);
    TsukuyomiOpenPBRResponse indirect;
    if (high) indirect = TsukuyomiOpenPBREvaluateIBLHigh(p, e);
    else indirect = TsukuyomiOpenPBREvaluateIBLBalanced(p, e);
    float3 color = indirect.diffuse * ao.indirectAmbientOcclusion * options.indirectDiffuseIntensity
                 + indirect.specular * ao.indirectSpecularOcclusion * options.indirectSpecularIntensity;
#ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(main.layerMask, layers))
#endif
        color += TsuOPBRURPLight(p, main, ao, options, high);
#if defined(_ADDITIONAL_LIGHTS)
    uint count = GetAdditionalLightsCount();
    #if USE_CLUSTER_LIGHT_LOOP
    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); ++lightIndex)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        Light light = GetAdditionalLight(lightIndex, inputData.positionWS, shadowMask);
        #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, layers))
        #endif
            color += TsuOPBRURPLight(p, light, ao, options, high);
    }
    #endif
    LIGHT_LOOP_BEGIN(count)
        Light light = GetAdditionalLight(lightIndex, inputData.positionWS, shadowMask);
        #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, layers))
        #endif
            color += TsuOPBRURPLight(p, light, ao, options, high);
    LIGHT_LOOP_END
#endif
#if defined(_ADDITIONAL_LIGHTS_VERTEX)
    // Vertex irradiance has no incoming directions: diffuse-only approximation.
    color += inputData.vertexLighting * p.material.baseColor * (1.0 - p.material.metalness);
#endif
    return color;
}

float3 TsukuyomiOpenPBRFragmentBalanced(InputData i, TsukuyomiOpenPBRPrepared p, TsukuyomiOpenPBRLightingOptions o)
{ return TsuOPBRURPFragment(i, p, o, false); }
float3 TsukuyomiOpenPBRFragmentHigh(InputData i, TsukuyomiOpenPBRPrepared p, TsukuyomiOpenPBRLightingOptions o)
{ return TsuOPBRURPFragment(i, p, o, true); }

#endif
