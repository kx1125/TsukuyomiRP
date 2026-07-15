#ifndef TSUKUYOMI_LIGHTING_INCLUDED
#define TSUKUYOMI_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonLighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/BRDF.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Debug/Debugging3D.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GlobalIllumination.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/AmbientOcclusion.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Lighting/TsukuyomiLightingData.hlsl"
#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Lighting/TsukuyomiEvaluateMaterial.hlsl"
#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Lighting/TsukuyomiBRDF.hlsl"
#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Lighting/TsukuyomiGlobalIllumination.hlsl"

#ifndef TSUKUYOMI_EVALUATE_AO_MULTI_BOUNCE
    #define TSUKUYOMI_EVALUATE_AO_MULTI_BOUNCE 1
#endif

half3 TsukuyomiLightingLambert(half3 lightColor, half3 lightDir, half3 normal)
{
    half NdotL = saturate(dot(normal, lightDir));
    return lightColor * NdotL;
}

half3 TsukuyomiVertexLighting(float3 positionWS, half3 normalWS)
{
    half3 vertexLightColor = half3(0.0h, 0.0h, 0.0h);

#ifdef _ADDITIONAL_LIGHTS_VERTEX
    uint lightsCount = GetAdditionalLightsCount();
    uint meshRenderingLayers = GetMeshRenderingLayer();

    LIGHT_LOOP_BEGIN(lightsCount)
        Light light = GetAdditionalLight(lightIndex, positionWS);

        #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
        #endif
        {
            half3 lightColor = light.color * light.distanceAttenuation;
            vertexLightColor += TsukuyomiLightingLambert(lightColor, light.direction, normalWS);
        }
    LIGHT_LOOP_END
#endif

    return vertexLightColor;
}

half3 TsukuyomiLightingPhysicallyBased(BRDFData brdfData, BRDFData brdfDataClearCoat,
    half3 lightColor, half3 lightDirectionWS,
    float lightAttenuation, half occlusion,
    half3 normalWS, half3 viewDirectionWS,
    half clearCoatMask, bool specularHighlightsOff,
    TsukuyomiBRDFOcclusionFactor aoFactor)
{
    float3 halfDir = SafeNormalize(float3(viewDirectionWS) + float3(lightDirectionWS));
    half NdotL = dot(normalWS, lightDirectionWS);
    float HdotV = max(dot(halfDir, viewDirectionWS), 0.0);
    half NdotH = saturate(dot(normalWS, halfDir));

    lightAttenuation *= NdotL >= 0.0h ? ComputeMicroShadowing(occlusion, NdotL, _MicroShadowOpacity) : 1.0h;
    NdotL = saturate(NdotL);

    half3 radiance = lightColor * (lightAttenuation * NdotL);
    float NdotV = dot(normalWS, viewDirectionWS);
    float clampNdotV = ClampNdotV(NdotV);
    float LdotV = dot(lightDirectionWS, viewDirectionWS);

#ifdef _DISNEY_DIFFUSE_BURLEY
    half3 diffuseTerm = TsukuyomiDirectBRDFDiffuseTermNoPI(NdotL, clampNdotV, LdotV, brdfData.perceptualRoughness).xxx;
    diffuseTerm *= brdfData.diffuse;
#else
    half3 roughDiffuseTerm = TsukuyomiDiffuseGGXRoughNoPI(brdfData.diffuse, brdfData.perceptualRoughness, clampNdotV, NdotL, HdotV, NdotH);
    half3 diffuseTerm = lerp(brdfData.diffuse, roughDiffuseTerm, saturate(_RoughDiffuseStrength));
#endif

    half3 brdf = diffuseTerm * aoFactor.directAmbientOcclusion;

#ifndef _SPECULARHIGHLIGHTS_OFF
    [branch] if (!specularHighlightsOff)
    {
        brdf += TsukuyomiLitSpecular(brdfData, normalWS, lightDirectionWS, viewDirectionWS) * aoFactor.directSpecularOcclusion;

        #if defined(_CLEARCOAT) || defined(_CLEARCOATMAP)
        half brdfCoat = kDielectricSpec.r * DirectBRDFSpecular(brdfDataClearCoat, normalWS, lightDirectionWS, viewDirectionWS);
        half NoV = saturate(dot(normalWS, viewDirectionWS));
        half coatFresnel = kDielectricSpec.x + kDielectricSpec.a * Pow4(1.0h - NoV);
        brdf = brdf * (1.0h - clearCoatMask * coatFresnel) + brdfCoat * clearCoatMask * aoFactor.directSpecularOcclusion;
        #endif
    }
#endif

    return brdf * radiance;
}

half3 TsukuyomiLightingPhysicallyBased(BRDFData brdfData, BRDFData brdfDataClearCoat, Light light,
    InputData inputData, SurfaceData surfaceData, bool specularHighlightsOff,
    TsukuyomiBRDFOcclusionFactor aoFactor)
{
    return TsukuyomiLightingPhysicallyBased(brdfData, brdfDataClearCoat,
        light.color, light.direction,
        light.distanceAttenuation * light.shadowAttenuation, surfaceData.occlusion,
        inputData.normalWS, inputData.viewDirectionWS,
        surfaceData.clearCoatMask, specularHighlightsOff, aoFactor);
}

half4 TsukuyomiFragmentPBR(InputData inputData, SurfaceData surfaceData)
{
#if defined(_SPECULARHIGHLIGHTS_OFF)
    bool specularHighlightsOff = true;
#else
    bool specularHighlightsOff = false;
#endif

    BRDFData brdfData;
    InitializeBRDFData(surfaceData, brdfData);

#if defined(DEBUG_DISPLAY)
    half4 debugColor;
    if (CanDebugOverrideOutputColor(inputData, surfaceData, brdfData, debugColor))
    {
        return debugColor;
    }
#endif

    BRDFData brdfDataClearCoat = CreateClearCoatBRDFData(surfaceData, brdfData);
    half4 shadowMask = CalculateShadowMask(inputData);
    AmbientOcclusionFactor aoFactor = TsukuyomiCreateAmbientOcclusionFactor(inputData, surfaceData);

#if TSUKUYOMI_EVALUATE_AO_MULTI_BOUNCE
    #ifdef _SPECULAR_SETUP
        half3 brdfDiffuse = brdfData.albedo;
    #else
        half3 brdfDiffuse = ComputeDiffuseColor(brdfData.albedo, surfaceData.metallic);
    #endif
    float NdotV = max(saturate(dot(inputData.normalWS, inputData.viewDirectionWS)), 0.00001);
    TsukuyomiBRDFOcclusionFactor brdfOcclusionFactor = TsukuyomiCreateBRDFOcclusionFactorMultiBounce(aoFactor, NdotV, brdfData.perceptualRoughness,
        surfaceData.occlusion, brdfDiffuse, surfaceData.occlusion, brdfData.specular);
#else
    TsukuyomiBRDFOcclusionFactor brdfOcclusionFactor = TsukuyomiCreateBRDFOcclusionFactor(aoFactor);
#endif

    uint meshRenderingLayers = GetMeshRenderingLayer();
    Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, shadowMask);

    MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI);

    TsukuyomiLightingData lightingData = TsukuyomiCreateLightingData(inputData, surfaceData);
    lightingData.giColor = TsukuyomiGlobalIllumination(brdfData, brdfDataClearCoat, surfaceData.clearCoatMask,
        inputData.bakedGI, brdfOcclusionFactor, inputData.positionWS,
        inputData.normalWS, inputData.viewDirectionWS,
        inputData.normalizedScreenSpaceUV, meshRenderingLayers);

#ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
#endif
    {
        lightingData.mainLightColor = TsukuyomiLightingPhysicallyBased(brdfData, brdfDataClearCoat,
            mainLight, inputData, surfaceData, specularHighlightsOff, brdfOcclusionFactor);
    }

#if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();

    #if USE_CLUSTER_LIGHT_LOOP
    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK

        Light light = GetAdditionalLight(lightIndex, inputData.positionWS, shadowMask);

        #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
        #endif
        {
            lightingData.additionalLightsColor += TsukuyomiLightingPhysicallyBased(brdfData, brdfDataClearCoat,
                light, inputData, surfaceData, specularHighlightsOff, brdfOcclusionFactor);
        }
    }
    #endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData.positionWS, shadowMask);

        #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
        #endif
        {
            lightingData.additionalLightsColor += TsukuyomiLightingPhysicallyBased(brdfData, brdfDataClearCoat,
                light, inputData, surfaceData, specularHighlightsOff, brdfOcclusionFactor);
        }
    LIGHT_LOOP_END
#endif

#if defined(_ADDITIONAL_LIGHTS_VERTEX)
    lightingData.vertexLightingColor += inputData.vertexLighting * brdfData.diffuse;
#endif

#if REAL_IS_HALF
    return min(TsukuyomiCalculateFinalColor(lightingData, surfaceData.alpha), HALF_MAX);
#else
    return TsukuyomiCalculateFinalColor(lightingData, surfaceData.alpha);
#endif
}

#endif
