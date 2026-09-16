#ifndef TSUKUYOMI_SPECULAR_PBR_LIGHTING_INCLUDED
#define TSUKUYOMI_SPECULAR_PBR_LIGHTING_INCLUDED

#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/OpenPBR/TsukuyomiOpenPBRURP.hlsl"
#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Lighting/TsukuyomiLightingData.hlsl"

float3 SpecularPBRVertexLighting(float3 positionWS, float3 normalWS)
{
    float3 color = 0;
#if defined(_ADDITIONAL_LIGHTS_VERTEX)
    uint pixelLightCount = GetAdditionalLightsCount();
    uint layers = GetMeshRenderingLayer();
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, positionWS);
#if defined(_LIGHT_LAYERS)
        if (IsMatchingLightLayer(light.layerMask, layers))
#endif
            color += light.color * light.distanceAttenuation * saturate(dot(normalWS, light.direction));
    LIGHT_LOOP_END
#endif
    return color;
}

float3 SpecularPBRTransmission(float3 transmission, float3 lightDir, float3 N, float3 V, float shadow)
{
    // Legacy artist-controlled thin transmission; only the Skin lighting pass uses it.
    transmission = 1.0 - exp(-max(transmission, 0.0));
    float blv = saturate(dot(-V, lightDir + N)) * 2.0;
    float bnl = saturate(dot(N, -lightDir) * TransmissionRange + TransmissionRange);
    float3 light = bnl + blv;
    float3 subsurface = transmission * light * 10.0 / max(1.0 - transmission, 1e-4);
    subsurface = 1.0 - exp(-subsurface);
    return subsurface * light * lerp(1.0, shadow, TransmissionShadows) * 10.0 * transmission * DynamicPassTransmission;
}

void SpecularPBRAccumulateLight(inout TsukuyomiOpenPBRResponse result, TsukuyomiOpenPBRPrepared p,
    Light light, TsukuyomiBRDFOcclusionFactor ao, TsukuyomiOpenPBRLightingOptions options,
    float specularStrength, float3 transmission)
{
    TsukuyomiOpenPBRResponse f = TsukuyomiOpenPBREvaluateDirectHigh(p, light.direction, specularStrength);
    float NoL = saturate(dot(p.geometry.normalWS, light.direction));
    float attenuation = light.distanceAttenuation * light.shadowAttenuation
        * ComputeMicroShadowing(options.occlusion, NoL, options.microShadowOpacity);
    // Exactly one PI conversion from physical OpenPBR to URP punctual-light units.
    float3 radiance = light.color * (TSU_OPBR_PI * NoL * attenuation);
    result.diffuse += f.diffuse * ao.directAmbientOcclusion * radiance;
#if !defined(_SPECULARHIGHLIGHTS_OFF)
    result.specular += f.specular * ao.directSpecularOcclusion * radiance;
#endif
#if defined(TSUKUYOMI_SPECULAR_PBR_SSS_LIGHTING_PASS) && defined(_SSS_ON) && defined(TRANSMISSION)
    result.diffuse += SpecularPBRTransmission(transmission, light.direction,
        p.geometry.normalWS, p.geometry.viewDirectionWS, light.shadowAttenuation)
        * light.color * light.distanceAttenuation;
#endif
}

TsukuyomiOpenPBRResponse SpecularPBRLightingHigh(InputData inputData, TsukuyomiOpenPBRPrepared p,
    float specularStrength, float occlusion, float3 transmission)
{
    TsukuyomiOpenPBRLightingOptions options = TsukuyomiOpenPBRDefaultLightingOptions();
    options.occlusion = occlusion;
    options.microShadowOpacity = _MicroShadowOpacity;
    options.indirectDiffuseIntensity = _IndirectDiffuseIntensity;
    options.indirectSpecularIntensity = _IndirectSpecularIntensity;
    options.horizonOcclusionPower = _HorizonOcclusionPower;
    AmbientOcclusionFactor screenAO = TsukuyomiGetScreenSpaceAmbientOcclusion(inputData.normalizedScreenSpaceUV);
    TsukuyomiBRDFOcclusionFactor ao = TsukuyomiCreateBRDFOcclusionFactorMultiBounce(screenAO,
        p.baseSpecular.NoV, p.material.specularRoughness, occlusion,
        p.material.baseColor * (1.0 - p.material.metalness), occlusion, p.baseSpecular.f0 * specularStrength);
    uint layers = GetMeshRenderingLayer();
    half4 shadowMask = CalculateShadowMask(inputData);
    Light main = GetMainLight(inputData.shadowCoord, inputData.positionWS, shadowMask);
    MixRealtimeAndBakedGI(main, inputData.normalWS, inputData.bakedGI);
    TsukuyomiOpenPBREnvironment environment = TsuOPBRURPSampleEnvironment(p, inputData, options, true);
    TsukuyomiOpenPBRResponse result = TsukuyomiOpenPBREvaluateIBLHigh(p, environment, specularStrength);
    result.diffuse *= ao.indirectAmbientOcclusion * options.indirectDiffuseIntensity;
    result.specular *= ao.indirectSpecularOcclusion * options.indirectSpecularIntensity;
#if defined(_LIGHT_LAYERS)
    if (IsMatchingLightLayer(main.layerMask, layers))
#endif
        SpecularPBRAccumulateLight(result, p, main, ao, options, specularStrength, transmission);
#if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();
#if USE_CLUSTER_LIGHT_LOOP
    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); ++lightIndex)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        Light light = GetAdditionalLight(lightIndex, inputData.positionWS, shadowMask);
#if defined(_LIGHT_LAYERS)
        if (IsMatchingLightLayer(light.layerMask, layers))
#endif
            SpecularPBRAccumulateLight(result, p, light, ao, options, specularStrength, transmission);
    }
#endif
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData.positionWS, shadowMask);
#if defined(_LIGHT_LAYERS)
        if (IsMatchingLightLayer(light.layerMask, layers))
#endif
            SpecularPBRAccumulateLight(result, p, light, ao, options, specularStrength, transmission);
    LIGHT_LOOP_END
#endif
#if defined(_ADDITIONAL_LIGHTS_VERTEX)
    result.diffuse += inputData.vertexLighting * p.material.baseColor * (1.0 - p.material.metalness);
#endif
    return result;
}

#endif
