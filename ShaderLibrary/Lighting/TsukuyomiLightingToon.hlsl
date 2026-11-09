#ifndef TSUKUYOMI_LIGHTING_TOON_INCLUDED
#define TSUKUYOMI_LIGHTING_TOON_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonLighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/BRDF.hlsl"
#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Toon/ToonDebugging3D.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GlobalIllumination.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/AmbientOcclusion.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Lighting/TsukuyomiLightingData.hlsl"
#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Lighting/TsukuyomiEvaluateMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Toon/ToonBRDFData.hlsl"

#ifndef TSUKUYOMI_EVALUATE_AO_MULTI_BOUNCE
    #define TSUKUYOMI_EVALUATE_AO_MULTI_BOUNCE 1
#endif

half3 TsukuyomiToonRamp(float value, float position, float smoothness, half shadowAttenuation)
{
    float ndotlRemap = smoothstep(position - smoothness, position + smoothness, value);
    half lit = min(ndotlRemap, shadowAttenuation);
    return lerp(_ShadowColor.rgb, 1, lit);
}

half3 SpecularLightingToon(ToonBRDFData brdfData, half3 normalWS, half3 lightDirectionWS, half3 viewDirectionWS)
{
    float3 halfDir = SafeNormalize(float3(lightDirectionWS) + float3(viewDirectionWS));
    float NdotH = saturate(dot(float3(normalWS), halfDir));
    float NdotL = saturate(dot(float3(normalWS), float3(lightDirectionWS)));
    float NdotV = saturate(dot(float3(normalWS), float3(viewDirectionWS)));
    float HdotV = saturate(dot(halfDir, float3(viewDirectionWS)));
    float roughness = brdfData.roughness;
    float roughnessSquare = brdfData.roughness2;

    // GGX distribution, evaluated in float to retain narrow highlights.
    float NdotH2 = NdotH * NdotH;
    float distributionDenominator = (1.0 - NdotH2) + NdotH2 * roughnessSquare;
    float NDF = roughnessSquare / max(PI * distributionDenominator * distributionDenominator, FLT_MIN);

    // Separable Schlick-GGX geometry with k = (roughness + 1)^2 / 8.
    float k = Sq(roughness + 1.0) * 0.125;
    float geometryL = NdotL / (NdotL * (1.0 - k) + k);
    float geometryV = NdotV / (NdotV * (1.0 - k) + k);
    float G = geometryL * geometryV;

    // InitializeBRDFData already supplies F0 = lerp(0.04, albedo, metallic).
    float3 F = F_Schlick(brdfData.specular, HdotV);
    float denominator = 4.0 * NdotV * NdotL + 0.0001;
    float3 BRDFSpec = NDF * G * F / denominator;

#if defined(SPECULAR_REMAP)
    float DV = NDF * G / denominator;
    float specRange = saturate(DV);
    float highlight = _SpecularSmoothness > 0.0h
        ? smoothstep(_SpecularPosition - _SpecularSmoothness, _SpecularPosition + _SpecularSmoothness, specRange)
        : step(_SpecularPosition, specRange);
    float3 specRampCol = highlight;

    // DV * PI is BRDFSpec * PI / F without dividing by zero Fresnel channels.
    // The caller applies the existing shadow and light attenuation once.
    return half3(clamp(specRampCol * 3.0 + DV * PI, 0.0, 10.0) * F * _SpecularColor.rgb);
#else
    return _SpecularColor.rgb * BRDFSpec * PI;
#endif
}

half3 ToonEnvironmentBRDFSpecularDFG(ToonBRDFData brdfData, half NoV, half fresnelTerm)
{
    float4 c0 = float4(-1.0, -0.0275, -0.572, 0.022);
    float4 c1 = float4(1.0, 0.0425, 1.04, -0.04);
    float4 r = brdfData.perceptualRoughness * c0 + c1;
    float a004 = min(r.x * r.x, exp2(-9.28 * NoV)) * r.x + r.y;
    float2 ab = float2(-1.04, 1.04) * a004 + r.zw;

    half3 dfgSpecular = max(half3(0.0h, 0.0h, 0.0h), brdfData.specular * ab.x + ab.y);
#if (UNITY_PLATFORM_META_QUEST)
    half surfaceReduction = half(1.0) / (brdfData.roughness2 + half(1.0));
#else
    float surfaceReduction = 1.0 / (brdfData.roughness2 + 1.0);
#endif
    half3 urpSpecular = surfaceReduction * lerp(brdfData.specular, brdfData.grazingTerm, fresnelTerm);
    return lerp(urpSpecular, dfgSpecular, saturate(_IndirectSpecularFGDStrength));
}

half3 ToonGlobalIllumination(ToonBRDFData brdfData, half3 bakedGI, TsukuyomiBRDFOcclusionFactor aoFactor,
    half3 normalWS, half3 viewDirectionWS)
{
    if (IsOnlyAOLightingFeatureEnabled())
    {
        return aoFactor.indirectAmbientOcclusion + aoFactor.indirectSpecularOcclusion;
    }

    half3 indirectDiffuse = bakedGI * brdfData.diffuse * aoFactor.indirectAmbientOcclusion * _IndirectDiffuseIntensity;
#if defined(_ENVIRONMENTREFLECTIONS_OFF)
    return indirectDiffuse;
#else
    float3 reflectVector = reflect(-float3(viewDirectionWS), float3(normalWS));
    float2 uv = DirectionToLatLongCoordinate(reflectVector);
    uint maxMip = (uint)log2(max(1.0, max(_EnvironmentMap_TexelSize.z, _EnvironmentMap_TexelSize.w)));
    half mip = PerceptualRoughnessToMipmapLevel(brdfData.perceptualRoughness, maxMip);
    half3 indirectSpecular = SAMPLE_TEXTURE2D_LOD(_EnvironmentMap, sampler_Trilinear_RepeatU_ClampV, uv, mip).rgb;

    half NoV = saturate(dot(normalWS, viewDirectionWS));
    half fresnelTerm = Pow4(1.0h - NoV);
    half horizon = saturate(1.0h + dot(reflectVector, normalWS));
    indirectSpecular *= pow(horizon, _HorizonOcclusionPower);
    indirectSpecular *= ToonEnvironmentBRDFSpecularDFG(brdfData, NoV, fresnelTerm)
        * aoFactor.indirectSpecularOcclusion * _IndirectSpecularIntensity;
    return indirectDiffuse + indirectSpecular;
#endif
}

half3 TsukuyomiLightingToon(ToonBRDFData brdfData, ToonBRDFData brdfDataClearCoat,
    half3 lightColor, float3 lightDirectionWS,
    float distanceAttenuation, half shadowAttenuation, half occlusion,
    float3 normalWS, float3 viewDirectionWS, float3 positionWS, float2 normalizedScreenSpaceUV,
    half clearCoatMask,TsukuyomiBRDFOcclusionFactor aoFactor)
{
    half NdotL = dot(normalWS, lightDirectionWS);
    half3 NdotLRemap = TsukuyomiToonRamp(NdotL * 0.5 + 0.5, _ShadowPosition, _ShadowSmoothness, shadowAttenuation);

    // The diffuse ramp replaces Lambert's cosine; multiplying it by NdotL again
    // would darken the lit band and introduce negative back-facing illumination.
    half3 radiance = lightColor * distanceAttenuation;
    half3 diffuseTerm = brdfData.diffuse * NdotLRemap;
    half3 specularAttenuation = NdotLRemap;
    half3 specularTerm = SpecularLightingToon(brdfData, normalWS, lightDirectionWS, viewDirectionWS);

    half3 brdf = diffuseTerm * aoFactor.directAmbientOcclusion + specularTerm * specularAttenuation;

    //EDGE
    half edgeMask = brdfData.ToonData.x;
    float3 halfDir = SafeNormalize(float3(lightDirectionWS) + float3(viewDirectionWS));
    float NdotH = dot(float3(normalWS), halfDir);
    float nohRemap = smoothstep(_EdgeLightPosition - _EdgeLightSmoothness, _EdgeLightPosition + _EdgeLightSmoothness, NdotH * 0.5 + 0.5);
    half edgeTerm = nohRemap * edgeMask;
    brdf += edgeTerm * _EdgeLightColor.rgb * brdfData.ToonData.y;

    //RIM
    float3 originWS = TransformObjectToWorld(_RimOriginBias.xyz);
    float3 sphereNormal = normalize(positionWS - originWS);

    half NSdotL = saturate(dot(sphereNormal, lightDirectionWS));

    // Evaluate the noisy width independently for each light.
    float rimWidth = _RimWidth * NSdotL;
    // Preserve nearby width and narrow distant rims without changing resolution scaling.
    float cameraDistance = distance(GetCameraPositionWS(), positionWS);
    float referenceDistance = max(_RimReferenceDistance, 0.0001);
    float rimDistanceScale = referenceDistance / max(cameraDistance, referenceDistance);
    rimWidth *= lerp(1.0, rimDistanceScale, _RimDistanceFade);
    float3 normalVS = TransformWorldToViewDir(normalWS, true);
    // Use screen-height-relative width; 1080 preserves the existing material scale.
    float2 screenSize = GetScaledScreenParams().xy;
    float2 rimOffsetUV = normalVS.xy * (rimWidth / 1080.0);
    rimOffsetUV.x *= screenSize.y / screenSize.x;
    float2 rimUV = normalizedScreenSpaceUV + rimOffsetUV;

    float rawDepth = SampleSceneDepth(normalizedScreenSpaceUV);
    float rimRawDepth = SampleSceneDepth(saturate(rimUV));
    float eyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
    float rimEyeDepth = LinearEyeDepth(rimRawDepth, _ZBufferParams);

    float depthRim = smoothstep(_RimPosition, _RimPosition + _RimSmoothness,
        rimEyeDepth - eyeDepth);
    depthRim *= all(rimUV >= 0.0) && all(rimUV <= 1.0) ? 1.0 : 0.0;
    //(brdfData.ToonData.y * 2.0 - 1.0) * _RimNoiseStrength
    half rimNoise = lerp(1, brdfData.ToonData.y * brdfData.ToonData.y, _RimNoiseStrength);
    half3 depthRimColor = depthRim * NSdotL * _RimColor.rgb * rimNoise;

    half NdotV = saturate(dot(normalWS, viewDirectionWS));
    half fresnelMask = smoothstep(_FresnelPosition, _FresnelPosition + _FresnelSmoothness, 1 - NdotV);
    brdf += depthRimColor + fresnelMask * _FresnelColor.rgb;
    return brdf;
    return brdf * radiance;
}

half3 TsukuyomiLightingToon(ToonBRDFData brdfData, ToonBRDFData brdfDataClearCoat, Light light,
    InputData inputData, ToonSurfaceData surfaceData,TsukuyomiBRDFOcclusionFactor aoFactor)
{
    return TsukuyomiLightingToon(brdfData, brdfDataClearCoat,
        light.color, light.direction,
        light.distanceAttenuation, light.shadowAttenuation, surfaceData.occlusion,
        inputData.normalWS, inputData.viewDirectionWS,inputData.positionWS, inputData.normalizedScreenSpaceUV,
        surfaceData.clearCoatMask, aoFactor);
}

half4 TsukuyomiFragmentToon(InputData inputData, ToonSurfaceData surfaceData)
{
    // Filter highlights smaller than a pixel before building the BRDF so the
    // toon mask and GGX amplitude use the same antialiased roughness.
    surfaceData.smoothness = GeometricNormalFiltering(surfaceData.smoothness, inputData.normalWS, 0.25, 0.1);

    ToonBRDFData brdfData;
    InitializeToonBRDFData(surfaceData, brdfData);

#if defined(DEBUG_DISPLAY)
    half4 debugColor;
    if (CanDebugOverrideOutputColor(inputData, surfaceData, brdfData, debugColor))
    {
        return debugColor;
    }
#endif

    ToonBRDFData brdfDataClearCoat = CreateToonClearCoatBRDFData(surfaceData, brdfData);
    half4 shadowMask = CalculateShadowMask(inputData);
    AmbientOcclusionFactor aoFactor = TsukuyomiGetScreenSpaceAmbientOcclusion(inputData.normalizedScreenSpaceUV);
    aoFactor.indirectAmbientOcclusion = min(aoFactor.indirectAmbientOcclusion, surfaceData.occlusion);

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

    TsukuyomiLightingData lightingData = (TsukuyomiLightingData)0;
    lightingData.emissionColor = surfaceData.emission;
    lightingData.giColor = ToonGlobalIllumination(brdfData, inputData.bakedGI, brdfOcclusionFactor,
        inputData.normalWS, inputData.viewDirectionWS);

#ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
#endif
    {
        lightingData.mainLightColor = TsukuyomiLightingToon(brdfData, brdfDataClearCoat,
            mainLight, inputData, surfaceData, brdfOcclusionFactor);
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
            lightingData.additionalLightsColor += TsukuyomiLightingToon(brdfData, brdfDataClearCoat,
                light, inputData, surfaceData, brdfOcclusionFactor);
        }
    }
    #endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData.positionWS, shadowMask);

        #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
        #endif
        {
            lightingData.additionalLightsColor += TsukuyomiLightingToon(brdfData, brdfDataClearCoat,
                light, inputData, surfaceData, brdfOcclusionFactor);
        }
    LIGHT_LOOP_END
#endif

#if REAL_IS_HALF
    return min(TsukuyomiCalculateFinalColor(lightingData, surfaceData.alpha), HALF_MAX);
#else
    //lightingData.mainLightColor = 0;
    return TsukuyomiCalculateFinalColor(lightingData, surfaceData.alpha);
#endif
}

#endif
