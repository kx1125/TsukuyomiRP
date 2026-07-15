#ifndef SSS_SKIN_PASS_INCLUDED
#define SSS_SKIN_PASS_INCLUDED

#include "Packages/tsukuyomi.render-pipelines.universal/Shaders/SSS Skin/SSSSkinLighting.hlsl"

#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

SSSSkinVaryings SSSSkinVert(SSSSkinAttributes input)
{
    SSSSkinVaryings output = (SSSSkinVaryings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    output.positionCS = positionInputs.positionCS;
    output.positionWS = positionInputs.positionWS;
    output.normalWS = normalInputs.normalWS;
    output.tangentWS = half4(normalInputs.tangentWS, input.tangentOS.w * GetOddNegativeScale());
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    return output;
}

void SSSSkinSetupPBRData(SSSSkinVaryings input, out InputData inputData, out SurfaceData surfaceData,
    out half4 albedo, out half4 pbrMask, out half skinMask)
{
    albedo = SampleAlbedo(input.uv);

#if defined(_ALPHATEST_ON)
    clip(albedo.a - _Cutoff);
#endif

    half3 normalWS = NormalizeNormalPerPixel(SampleNormalWS(input));
    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    half occlusion = SampleOcclusion(input.uv);
    pbrMask = SamplePBRMask(input.uv);
    skinMask = ResolveSkinMask(pbrMask);

    inputData = BuildSSSSkinInputData(input, normalWS, viewDirWS);
#if defined(_ADDITIONAL_LIGHTS_VERTEX)
    inputData.vertexLighting = TsukuyomiVertexLighting(input.positionWS, normalWS);
#endif
    surfaceData = BuildSSSSkinSurfaceData(albedo, half3(0.0h, 0.0h, 1.0h), occlusion, pbrMask);
}

half4 SSSSkinPBRForwardFragment(SSSSkinVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    InputData inputData;
    SurfaceData surfaceData;
    half4 albedo;
    half4 pbrMask;
    half skinMask;
    SSSSkinSetupPBRData(input, inputData, surfaceData, albedo, pbrMask, skinMask);
    return SSSSkinFragmentPBR(inputData, surfaceData);
}

half4 SSSSkinForwardFragment(SSSSkinVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    InputData inputData;
    SurfaceData surfaceData;
    half4 albedo;
    half4 pbrMask;
    half skinMask;
    SSSSkinSetupPBRData(input, inputData, surfaceData, albedo, pbrMask, skinMask);

    half4 pbrColor = SSSSkinFragmentPBR(inputData, surfaceData);

    if (SSS_shader != 1.0h || skinMask <= 0.0h)
    {
        return pbrColor;
    }

    SurfaceData specularSurfaceData = surfaceData;
    specularSurfaceData.albedo = half3(0.0h, 0.0h, 0.0h);

    half3 specularLighting = SSSSkinFragmentPBR(inputData, specularSurfaceData).rgb;
    half3 sssDiffuse = SampleAlbedoTexture(input.uv).rgb * SampleBlurredSSSLighting(input.positionCS);
    half3 sssColor = specularLighting + sssDiffuse;

    return half4(lerp(pbrColor.rgb, sssColor, skinMask), albedo.a);
}

half4 SSSSkinMaskFragment(SSSSkinVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    half4 albedo = SampleAlbedo(input.uv);
#if defined(_ALPHATEST_ON)
    clip(albedo.a - _Cutoff);
#endif

    if (SSS_shader != 1.0h)
    {
        return 0.0h;
    }

    return ResolveSkinMask(SamplePBRMask(input.uv));
}

half4 SSSSkinLightingFragment(SSSSkinVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    half alpha = 1.0h;
#if defined(_ALPHATEST_ON)
    alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a;
    clip(alpha - _Cutoff);
#endif

    if (SSS_shader != 1.0h)
    {
        return half4(0.0h, 0.0h, 0.0h, 1.0h);
    }

    half4 pbrMask = SamplePBRMask(input.uv);
    half skinMask = ResolveSkinMask(pbrMask);
    if (skinMask <= 0.0h)
    {
        return half4(0.0h, 0.0h, 0.0h, 1.0h);
    }

    half3 normalWS = NormalizeNormalPerPixel(SampleNormalWS(input));
    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    half occlusion = SampleOcclusion(input.uv);

    InputData inputData = BuildSSSSkinInputData(input, normalWS, viewDirWS);
#if defined(_ADDITIONAL_LIGHTS_VERTEX)
    inputData.vertexLighting = TsukuyomiVertexLighting(input.positionWS, normalWS);
#endif

    SurfaceData surfaceData = BuildSSSSkinDiffuseOnlySurfaceData(alpha, occlusion);
    half4 shadowMask = CalculateShadowMask(inputData);
    AmbientOcclusionFactor aoFactor = TsukuyomiCreateAmbientOcclusionFactor(inputData, surfaceData);
    half perceptualRoughness = SSSSkinMaskRoughness(pbrMask);

#if TSUKUYOMI_EVALUATE_AO_MULTI_BOUNCE
    TsukuyomiBRDFOcclusionFactor brdfOcclusionFactor = TsukuyomiCreateBRDFOcclusionFactorMultiBounce(aoFactor,
        max(saturate(dot(normalWS, viewDirWS)), 0.00001),
        perceptualRoughness,
        occlusion,
        SampleLightingAlbedo(input.uv),
        occlusion,
        _SpecColor.rgb);
#else
    TsukuyomiBRDFOcclusionFactor brdfOcclusionFactor = TsukuyomiCreateBRDFOcclusionFactor(aoFactor);
#endif

    uint meshRenderingLayers = GetMeshRenderingLayer();
    half3 lightingAlbedo = SampleLightingAlbedo(input.uv);
    half3 transmission = SampleTransmission(input.uv);
    half3 lighting = inputData.bakedGI * lightingAlbedo * brdfOcclusionFactor.indirectAmbientOcclusion * _IndirectDiffuseIntensity;

    Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, shadowMask);
#if defined(_LIGHT_LAYERS)
    if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
#endif
    {
        lighting += SSSSkinDirectLight(mainLight, lightingAlbedo, perceptualRoughness, occlusion, normalWS, viewDirWS, transmission, brdfOcclusionFactor);
    }

#if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();

    #if USE_CLUSTER_LIGHT_LOOP
    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        Light light = GetAdditionalLight(lightIndex, inputData.positionWS, shadowMask);
        #if defined(_LIGHT_LAYERS)
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
        #endif
        {
            lighting += SSSSkinDirectLight(light, lightingAlbedo, perceptualRoughness, occlusion, normalWS, viewDirWS, transmission, brdfOcclusionFactor);
        }
    }
    #endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData.positionWS, shadowMask);
        #if defined(_LIGHT_LAYERS)
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
        #endif
        {
            lighting += SSSSkinDirectLight(light, lightingAlbedo, perceptualRoughness, occlusion, normalWS, viewDirWS, transmission, brdfOcclusionFactor);
        }
    LIGHT_LOOP_END
#endif

#if defined(_ADDITIONAL_LIGHTS_VERTEX)
    lighting += inputData.vertexLighting * lightingAlbedo * brdfOcclusionFactor.directAmbientOcclusion;
#endif

    return half4(lighting * skinMask, 1.0h);
}

struct SSSSkinDepthNormalsVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    half3 normalWS : TEXCOORD1;
    half4 tangentWS : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

SSSSkinDepthNormalsVaryings SSSSkinDepthNormalsVertex(SSSSkinAttributes input)
{
    SSSSkinDepthNormalsVaryings output = (SSSSkinDepthNormalsVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.normalWS = normalInputs.normalWS;
    output.tangentWS = half4(normalInputs.tangentWS, input.tangentOS.w * GetOddNegativeScale());
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    return output;
}

void SSSSkinDepthNormalsFragment(
    SSSSkinDepthNormalsVaryings input,
    out half4 outNormalWS : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

#if defined(_ALPHATEST_ON)
    Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);
#endif

#if defined(LOD_FADE_CROSSFADE)
    LODFadeCrossFade(input.positionCS);
#endif

    SSSSkinVaryings skinInput = (SSSSkinVaryings)0;
    skinInput.positionCS = input.positionCS;
    skinInput.uv = input.uv;
    skinInput.normalWS = input.normalWS;
    skinInput.tangentWS = input.tangentWS;

    float3 normalWS = NormalizeNormalPerPixel(SampleNormalWS(skinInput));

#if defined(_GBUFFER_NORMALS_OCT)
    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
    float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
    half3 packedNormalWS = PackFloat2To888(remappedOctNormalWS);
    outNormalWS = half4(packedNormalWS, 0.0h);
#else
    outNormalWS = half4(normalWS, 0.0h);
#endif

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

#endif
