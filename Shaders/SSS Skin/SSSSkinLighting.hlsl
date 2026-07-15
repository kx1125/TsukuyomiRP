#ifndef SSS_SKIN_LIGHTING_INCLUDED
#define SSS_SKIN_LIGHTING_INCLUDED

#include "Packages/tsukuyomi.render-pipelines.universal/Shaders/SSS Skin/SSSSkinInput.hlsl"
#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Lighting/TsukuyomiLighting.hlsl"

half4 SSSSkinFragmentPBR(InputData inputData, SurfaceData surfaceData)
{
    return TsukuyomiFragmentPBR(inputData, surfaceData);
}

half3 SSSSkinTransmissionDynamic(half3 transmission, half3 lightDirWS, half3 normalWS, half3 viewDirWS, half atten)
{
    transmission = 1.0h - exp(-transmission);
    half blv = saturate(dot(-viewDirWS, lightDirWS + normalWS)) * 2.0h;
    half bnl = saturate(dot(normalWS, -lightDirWS) * TransmissionRange + TransmissionRange);
    half3 light = bnl + blv;
    half3 subsurface = transmission * light * 10.0h;
    subsurface /= max(1.0h - transmission, 1e-4h);
    subsurface = 1.0h - exp(-subsurface);
    return subsurface * light * lerp(1.0h, atten, TransmissionShadows) * 10.0h * transmission * DynamicPassTransmission;
}

half3 SSSSkinTsukuyomiDirectDiffuse(Light light, half3 lightingAlbedo, half perceptualRoughness, half occlusion,
    half3 normalWS, half3 viewDirWS, TsukuyomiBRDFOcclusionFactor aoFactor)
{
    float3 halfDir = SafeNormalize(float3(viewDirWS) + float3(light.direction));
    half NdotL = dot(normalWS, light.direction);
    float HdotV = max(dot(halfDir, viewDirWS), 0.0);
    half NdotH = saturate(dot(normalWS, halfDir));
    float NdotV = dot(normalWS, viewDirWS);
    float clampNdotV = ClampNdotV(NdotV);

    float lightAttenuation = light.distanceAttenuation * light.shadowAttenuation;
    lightAttenuation *= NdotL >= 0.0h ? ComputeMicroShadowing(occlusion, NdotL, _MicroShadowOpacity) : 1.0h;
    NdotL = saturate(NdotL);

    half3 roughDiffuse = TsukuyomiDiffuseGGXRoughNoPI(lightingAlbedo, perceptualRoughness, clampNdotV, NdotL, HdotV, NdotH);
    half3 diffuse = lerp(lightingAlbedo, roughDiffuse, saturate(_RoughDiffuseStrength));
    return diffuse * aoFactor.directAmbientOcclusion * light.color * (lightAttenuation * NdotL);
}

half3 SSSSkinDirectLight(Light light, half3 lightingAlbedo, half perceptualRoughness, half occlusion,
    half3 normalWS, half3 viewDirWS, half3 transmission, TsukuyomiBRDFOcclusionFactor aoFactor)
{
    half3 diffuse = SSSSkinTsukuyomiDirectDiffuse(light, lightingAlbedo, perceptualRoughness, occlusion, normalWS, viewDirWS, aoFactor);

#if defined(TRANSMISSION)
    diffuse += SSSSkinTransmissionDynamic(transmission, light.direction, normalWS, viewDirWS, light.shadowAttenuation) * light.color;
#endif

    return diffuse;
}

#endif
