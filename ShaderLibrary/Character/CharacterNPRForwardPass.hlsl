#ifndef TSUKUYOMI_CHARACTER_HAIR_FORWARD_PASS_INCLUDED
#define TSUKUYOMI_CHARACTER_HAIR_FORWARD_PASS_INCLUDED

#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Character/CharacterNPRInput.hlsl"
#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Character/CharacterNPRLighting.hlsl"
#include "Packages/tsukuyomi.render-pipelines.universal/Shaders/SSGI/TsukuyomiScreenSpaceGlobalIllumination.hlsl"

#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

struct TsukuyomiHairAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 texcoord : TEXCOORD0;
    float2 staticLightmapUV : TEXCOORD1;
    float2 dynamicLightmapUV : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct TsukuyomiHairVaryings
{
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    float3 normalWS : TEXCOORD2;
    float4 tangentWS : TEXCOORD3;
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord : TEXCOORD4;
#endif
    DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 5);
#ifdef DYNAMICLIGHTMAP_ON
    float2 dynamicLightmapUV : TEXCOORD8;
#endif
#ifdef USE_APV_PROBE_OCCLUSION
    float4 probeOcclusion : TEXCOORD9;
#endif
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

TsukuyomiHairVaryings TsukuyomiHairForwardVertex(TsukuyomiHairAttributes input)
{
    TsukuyomiHairVaryings output = (TsukuyomiHairVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    output.positionWS = positionInputs.positionWS;
    output.normalWS = normalInputs.normalWS;
    output.tangentWS = float4(normalInputs.tangentWS,
        input.tangentOS.w * GetOddNegativeScale());
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    output.shadowCoord = GetShadowCoord(positionInputs);
#endif
    OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST,
        output.staticLightmapUV);
#ifdef DYNAMICLIGHTMAP_ON
    output.dynamicLightmapUV = input.dynamicLightmapUV
        * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
#endif
    OUTPUT_SH4(positionInputs.positionWS, output.normalWS,
        GetWorldSpaceNormalizeViewDir(positionInputs.positionWS),
        output.vertexSH, output.probeOcclusion);
    output.positionCS = positionInputs.positionCS;
    return output;
}

void TsukuyomiHairInitializeBakedGI(
    TsukuyomiHairVaryings input,
    inout InputData inputData)
{
#if defined(_TSUKUYOMI_SCREEN_SPACE_GLOBAL_ILLUMINATION) && !defined(LIGHTMAP_ON) && !defined(DYNAMICLIGHTMAP_ON)
    #if defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2)
    inputData.bakedGI = SAMPLE_GI(input.vertexSH,
        GetAbsolutePositionWS(inputData.positionWS), inputData.normalWS,
        inputData.viewDirectionWS, input.positionCS.xy,
        input.probeOcclusion, inputData.shadowMask);
    #else
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
    #endif
    inputData.bakedGI = SampleTsukuyomiScreenSpaceGlobalIllumination(
        inputData.normalizedScreenSpaceUV);
#elif defined(_SCREEN_SPACE_IRRADIANCE)
    inputData.bakedGI = SAMPLE_GI(_ScreenSpaceIrradiance, input.positionCS.xy);
#elif defined(DYNAMICLIGHTMAP_ON)
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV,
        input.dynamicLightmapUV, input.vertexSH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
#elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
    inputData.bakedGI = SAMPLE_GI(input.vertexSH,
        GetAbsolutePositionWS(inputData.positionWS), inputData.normalWS,
        inputData.viewDirectionWS, input.positionCS.xy,
        input.probeOcclusion, inputData.shadowMask);
#else
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV,
        input.vertexSH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
#endif
}

void TsukuyomiHairForwardFragment(
    TsukuyomiHairVaryings input,
    FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC,
    out float4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    TsukuyomiHairSurface surface = TsukuyomiHairSampleSurface(input.uv);
#if defined(LOD_FADE_CROSSFADE)
    LODFadeCrossFade(input.positionCS);
#endif

    float tangentSign = input.tangentWS.w;
    float3 tangentWS = normalize(input.tangentWS.xyz);
    float3 geometricNormalWS = normalize(input.normalWS);
    float3 bitangentWS = tangentSign * cross(geometricNormalWS, tangentWS);
    float3x3 tangentToWorld = float3x3(tangentWS, bitangentWS, geometricNormalWS);
    float faceSign = IS_FRONT_VFACE(facing, 1.0,
        (-1.0) + (2.0 * _BackFaceNormalFlip));
    float3 diffuseNormalWS = normalize(
        TransformTangentToWorld(surface.diffuseNormalTS, tangentToWorld)) * faceSign;
    float3 specularNormalWS = normalize(
        TransformTangentToWorld(surface.specularNormalTS, tangentToWorld));

    InputData inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
    inputData.positionCS = input.positionCS;
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    inputData.normalWS = diffuseNormalWS;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    inputData.shadowCoord = input.shadowCoord;
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
#else
    inputData.shadowCoord = 0.0.xxxx;
#endif
    TsukuyomiHairInitializeBakedGI(input, inputData);

    float3 color = TsukuyomiHairFragmentLighting(inputData, input.uv,
        surface, diffuseNormalWS, specularNormalWS, tangentWS, tangentSign);
    outColor = float4(color, surface.alpha);

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

#endif
