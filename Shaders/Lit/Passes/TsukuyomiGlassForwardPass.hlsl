#ifndef GLASS_FORWARD_PASS_INCLUDED
#define GLASS_FORWARD_PASS_INCLUDED

#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Material/TsukuyomiGlassInput.hlsl"
#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Lighting/TsukuyomiLighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

TEXTURE2D(_MatCapMap);
SAMPLER(sampler_MatCapMap);

struct GlassAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 texcoord : TEXCOORD0;
    float2 staticLightmapUV : TEXCOORD1;
    float2 dynamicLightmapUV : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct GlassVaryings
{
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    half3 normalWS : TEXCOORD2;
    half4 tangentWS : TEXCOORD3;

#ifdef _ADDITIONAL_LIGHTS_VERTEX
    half4 fogFactorAndVertexLight : TEXCOORD5;
#else
    half fogFactor : TEXCOORD5;
#endif

    float4 shadowCoord : TEXCOORD6;
    TSUKUYOMI_DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 8);

#ifdef DYNAMICLIGHTMAP_ON
    float2 dynamicLightmapUV : TEXCOORD9;
#endif

#ifdef USE_APV_PROBE_OCCLUSION
    float4 probeOcclusion : TEXCOORD10;
#endif

    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

void GlassInitializeInputData(GlassVaryings input, half3 normalTS, out InputData inputData)
{
    inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
    inputData.positionCS = input.positionCS;
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

#if defined(_NORMALMAP)
    float tangentSign = input.tangentWS.w;
    float3 bitangent = tangentSign * cross(input.normalWS.xyz, input.tangentWS.xyz);
    half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz);
    inputData.tangentToWorld = tangentToWorld;
    inputData.normalWS = TransformTangentToWorld(normalTS, tangentToWorld);
#else
    inputData.normalWS = input.normalWS;
#endif

    inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
    inputData.shadowCoord = input.shadowCoord;

#ifdef _ADDITIONAL_LIGHTS_VERTEX
    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactorAndVertexLight.x);
    inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
#else
    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
#endif

    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

#if defined(DEBUG_DISPLAY)
    #if defined(DYNAMICLIGHTMAP_ON)
    inputData.dynamicLightmapUV = input.dynamicLightmapUV;
    #endif
    #if defined(LIGHTMAP_ON)
    inputData.staticLightmapUV = input.staticLightmapUV;
    #else
    inputData.vertexSH = input.vertexSH;
    #endif
    #if defined(USE_APV_PROBE_OCCLUSION)
    inputData.probeOcclusion = input.probeOcclusion;
    #endif
#endif
}

void GlassInitializeBakedGIData(GlassVaryings input, inout InputData inputData)
{
#if defined(_SCREEN_SPACE_IRRADIANCE)
    inputData.bakedGI = SAMPLE_GI(_ScreenSpaceIrradiance, input.positionCS.xy);
#elif defined(DYNAMICLIGHTMAP_ON)
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.dynamicLightmapUV, input.vertexSH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
#elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
    inputData.bakedGI = SAMPLE_GI(input.vertexSH,
        GetAbsolutePositionWS(inputData.positionWS), inputData.normalWS, inputData.viewDirectionWS,
        input.positionCS.xy, input.probeOcclusion, inputData.shadowMask);
#else
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
#endif
}

GlassVaryings GlassForwardVertex(GlassAttributes input)
{
    GlassVaryings output = (GlassVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    half3 vertexLight = TsukuyomiVertexLighting(vertexInput.positionWS, normalInput.normalWS);
    half fogFactor = 0.0h;
#if !defined(_FOG_FRAGMENT)
    fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
#endif

    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    output.positionWS = vertexInput.positionWS;
    output.normalWS = normalInput.normalWS;
    output.tangentWS = half4(normalInput.tangentWS.xyz, input.tangentOS.w * GetOddNegativeScale());
    output.shadowCoord = GetShadowCoord(vertexInput);
    TSUKUYOMI_OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
#ifdef DYNAMICLIGHTMAP_ON
    output.dynamicLightmapUV = input.dynamicLightmapUV.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
#endif
    TSUKUYOMI_OUTPUT_SH4(vertexInput.positionWS, output.normalWS.xyz,
        GetWorldSpaceNormalizeViewDir(vertexInput.positionWS), output.vertexSH, output.probeOcclusion);
#ifdef _ADDITIONAL_LIGHTS_VERTEX
    output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);
#else
    output.fogFactor = fogFactor;
#endif
    output.positionCS = vertexInput.positionCS;
    return output;
}

struct GlassRefractionModelResult
{
    float dist;
    float3 positionWS;
    float3 rayWS;
};

GlassRefractionModelResult GlassRefractionModelSphere(
    float3 viewDirectionWS,
    float3 positionWS,
    float3 normalWS,
    float ior,
    float thickness)
{
    float safeIor = max(ior, 1.0001);
    float safeThickness = max(thickness, 0.0);
    float3 refractedIn = refract(-viewDirectionWS, normalWS, rcp(safeIor));
    float3 sphereCenter = positionWS - normalWS * safeThickness * 0.5;

    float normalDotRay = dot(normalWS, refractedIn);
    float opticalDistance = -normalDotRay * safeThickness;
    float3 exitPositionWS = positionWS + refractedIn * opticalDistance;
    float3 exitNormalVector = sphereCenter - exitPositionWS;
    float exitNormalLengthSq = dot(exitNormalVector, exitNormalVector);
    float3 exitNormalWS = exitNormalVector * rsqrt(max(exitNormalLengthSq, 1e-8));
    float3 refractedOut = refract(refractedIn, exitNormalWS, safeIor);

    GlassRefractionModelResult result;
    result.dist = opticalDistance;
    result.positionWS = exitPositionWS;
    result.rayWS = refractedOut;
    return result;
}

float2 GlassComputeSphereRefractedUv(
    float2 screenUv,
    float3 positionWS,
    float3 normalWS,
    float3 viewDirectionWS)
{
    GlassRefractionModelResult refraction = GlassRefractionModelSphere(
        viewDirectionWS, positionWS, normalWS, _IndexOfRefraction, _Thickness);

    float rawBackgroundDepth = SampleSceneDepth(screenUv);
    float backgroundEyeDepth = LinearEyeDepth(rawBackgroundDepth, _ZBufferParams);
    float3 exitPositionVS = TransformWorldToView(refraction.positionWS);
    float3 refractedRayVS = TransformWorldToViewDir(refraction.rayWS, false);
    float rayLengthSq = dot(refraction.rayWS, refraction.rayWS);
    float rayDepthDenominator = refractedRayVS.z;
    float rayDistance = (-backgroundEyeDepth - exitPositionVS.z)
        / (abs(rayDepthDenominator) > 1e-5 ? rayDepthDenominator : -1e-5);

    bool validRay = rayLengthSq > 1e-6
        && rayDepthDenominator < -1e-5
        && rayDistance >= 0.0
        && rayDistance < _ProjectionParams.z * 2.0;

    float3 refractedPositionWS = refraction.positionWS + refraction.rayWS * max(rayDistance, 0.0);
    float4 refractedPositionCS = TransformWorldToHClip(refractedPositionWS);
    float4 refractedScreenPosition = ComputeScreenPos(refractedPositionCS);
    float2 refractedUv = refractedScreenPosition.xy / max(refractedScreenPosition.w, 1e-5);
    bool validUv = all(refractedUv == refractedUv) && all(abs(refractedUv) < 10000.0);
    return validRay && validUv ? refractedUv : screenUv;
}

half3 GlassSampleTransmission(
    float2 screenUv,
    float3 positionWS,
    half3 normalWS,
    half3 viewDirectionWS)
{
    float2 refractedUv = saturate(GlassComputeSphereRefractedUv(
        screenUv, positionWS, normalWS, viewDirectionWS));
    return SampleSceneColor(refractedUv);
}

half3 GlassEvaluateDirectSpecular(InputData inputData, SurfaceData surfaceData)
{
#if defined(_SPECULARHIGHLIGHTS_OFF)
    bool specularHighlightsOff = true;
#else
    bool specularHighlightsOff = false;
#endif

    BRDFData brdfData;
    InitializeBRDFData(surfaceData, brdfData);
    BRDFData brdfDataClearCoat = CreateClearCoatBRDFData(surfaceData, brdfData);
    half4 shadowMask = CalculateShadowMask(inputData);
    AmbientOcclusionFactor aoFactor = TsukuyomiCreateAmbientOcclusionFactor(inputData, surfaceData);

#if TSUKUYOMI_EVALUATE_AO_MULTI_BOUNCE
    #ifdef _SPECULAR_SETUP
        half3 brdfDiffuse = brdfData.albedo;
    #else
        half3 brdfDiffuse = ComputeDiffuseColor(brdfData.albedo, surfaceData.metallic);
    #endif
    float NoV = max(saturate(dot(inputData.normalWS, inputData.viewDirectionWS)), 0.00001);
    TsukuyomiBRDFOcclusionFactor brdfOcclusionFactor = TsukuyomiCreateBRDFOcclusionFactorMultiBounce(
        aoFactor,
        NoV,
        brdfData.perceptualRoughness,
        surfaceData.occlusion,
        brdfDiffuse,
        surfaceData.occlusion,
        brdfData.specular);
#else
    TsukuyomiBRDFOcclusionFactor brdfOcclusionFactor = TsukuyomiCreateBRDFOcclusionFactor(aoFactor);
#endif

    uint meshRenderingLayers = GetMeshRenderingLayer();
    Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, shadowMask);
    half3 mixedBakedGI = inputData.bakedGI;
    MixRealtimeAndBakedGI(mainLight, inputData.normalWS, mixedBakedGI);

    half3 directLighting = half3(0.0h, 0.0h, 0.0h);
#ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
#endif
    {
        directLighting += TsukuyomiLightingPhysicallyBased(
            brdfData,
            brdfDataClearCoat,
            mainLight,
            inputData,
            surfaceData,
            specularHighlightsOff,
            brdfOcclusionFactor);
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
            directLighting += TsukuyomiLightingPhysicallyBased(
                brdfData,
                brdfDataClearCoat,
                light,
                inputData,
                surfaceData,
                specularHighlightsOff,
                brdfOcclusionFactor);
        }
    }
    #endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData.positionWS, shadowMask);
        #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
        #endif
        {
            directLighting += TsukuyomiLightingPhysicallyBased(
                brdfData,
                brdfDataClearCoat,
                light,
                inputData,
                surfaceData,
                specularHighlightsOff,
                brdfOcclusionFactor);
        }
    LIGHT_LOOP_END
#endif

    return directLighting;
}

void GlassEvaluateEnvironmentReflection(
    InputData inputData,
    SurfaceData surfaceData,
    out half3 environmentReflection,
    out half reflectionMask)
{
    half NoV = saturate(abs(dot(inputData.normalWS, inputData.viewDirectionWS)));
    half edgeFactor = 1.0h - NoV;
    half reflectionRange = max(_EnvironmentReflectionRange, 0.001h);
    half rangeStart = 1.0h - reflectionRange;
    half rangeFactor = saturate((edgeFactor - rangeStart) / reflectionRange);
    reflectionMask = pow(rangeFactor, max(_EnvironmentReflectionSharpness, 0.001h));

    half3 reflectVector = reflect(-inputData.viewDirectionWS, inputData.normalWS);
    environmentReflection = TsukuyomiGlossyEnvironmentReflection(
        reflectVector,
        inputData.positionWS,
        1.0h - surfaceData.smoothness,
        1.0h,
        inputData.normalizedScreenSpaceUV);

    half horizon = saturate(1.0h + dot(reflectVector, inputData.normalWS));
    half reflectionIntensity = max(_IndirectSpecularIntensity, 0.0h);
    environmentReflection *= pow(horizon, _HorizonOcclusionPower) * max(reflectionIntensity, 1.0h);
    reflectionMask *= saturate(reflectionIntensity);

#if defined(_ENVIRONMENTREFLECTIONS_OFF)
    environmentReflection = half3(0.0h, 0.0h, 0.0h);
    reflectionMask = 0.0h;
#endif
}

void GlassForwardFragment(GlassVaryings input, out half4 outColor : SV_Target0)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    SurfaceData surfaceData;
    half3 transmissionTint;
    half transmissionOpacity;
    InitializeGlassSurfaceData(input.uv, surfaceData, transmissionTint, transmissionOpacity);

#ifdef LOD_FADE_CROSSFADE
    LODFadeCrossFade(input.positionCS);
#endif

    InputData inputData;
    GlassInitializeInputData(input, surfaceData.normalTS, inputData);
    SETUP_DEBUG_TEXTURE_DATA(inputData, UNDO_TRANSFORM_TEX(input.uv, _BaseMap));
    GlassInitializeBakedGIData(input, inputData);

    half3 directSpecularLighting = GlassEvaluateDirectSpecular(inputData, surfaceData);
#if !defined(_REFRACTION_OFF)
    half3 transmission = GlassSampleTransmission(
        inputData.normalizedScreenSpaceUV,
        inputData.positionWS,
        inputData.normalWS,
        inputData.viewDirectionWS);
    transmission *= lerp(half3(1.0h, 1.0h, 1.0h), transmissionTint,
        saturate(_Absorption * transmissionOpacity));
#else
    half3 transmission = transmissionTint;
#endif

    half3 environmentReflection;
    half reflectionMask;
    GlassEvaluateEnvironmentReflection(
        inputData, surfaceData, environmentReflection, reflectionMask);

    half3 matCap = half3(0.0h, 0.0h, 0.0h);
#if defined(_MATCAP_ON)
    half3 normalVS = normalize(TransformWorldToViewDir(inputData.normalWS, true));
    float2 matCapUv = normalVS.xy * 0.5 + 0.5;
    matCap = SAMPLE_TEXTURE2D(_MatCapMap, sampler_MatCapMap, saturate(matCapUv)).rgb * _MatCapIntensity;
#endif

#if defined(_REFRACTION_OFF)
    half3 color = environmentReflection + matCap + directSpecularLighting;
    color = MixFog(color, inputData.fogCoord);
    outColor = half4(color, transmissionOpacity * reflectionMask);
#else
    half3 color = lerp(transmission, environmentReflection + matCap, reflectionMask)
    + directSpecularLighting;
    color = MixFog(color, inputData.fogCoord);

    outColor = half4(color, 1.0h);
#endif
}

#endif
