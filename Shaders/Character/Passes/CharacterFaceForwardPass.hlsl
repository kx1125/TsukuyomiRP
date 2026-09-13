#ifndef TSUKUYOMI_CHARACTER_FACE_FORWARD_PASS_INCLUDED
#define TSUKUYOMI_CHARACTER_FACE_FORWARD_PASS_INCLUDED

#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Character/CharacterFaceLighting.hlsl"
#include "Packages/tsukuyomi.render-pipelines.universal/Shaders/SSGI/TsukuyomiScreenSpaceGlobalIllumination.hlsl"
#if defined(LOD_FADE_CROSSFADE)
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

struct TsukuyomiFaceAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct TsukuyomiFaceVaryings
{
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    half3 normalWS : TEXCOORD2;
    half4 tangentWS : TEXCOORD3;
    half3 vertexSH : TEXCOORD4;
    half fogFactor : TEXCOORD5;
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord : TEXCOORD6;
#endif
#ifdef USE_APV_PROBE_OCCLUSION
    float4 probeOcclusion : TEXCOORD7;
#endif
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

float3 TsukuyomiFaceCameraForwardWS()
{
    // The source uses inverse-view +Z, not the head's +Z (b131 L443).
    return mul((float3x3)UNITY_MATRIX_I_V, float3(0, 0, 1));
}

TsukuyomiFaceVaryings TsukuyomiFaceForwardVertex(TsukuyomiFaceAttributes input)
{
    TsukuyomiFaceVaryings output = (TsukuyomiFaceVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    output.positionWS = position.positionWS;
    output.positionCS = position.positionCS;
    output.normalWS = normal.normalWS;
    output.tangentWS = half4(normal.tangentWS, input.tangentOS.w * GetOddNegativeScale());
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
#if !defined(_FOG_FRAGMENT)
    output.fogFactor = ComputeFogFactor(position.positionCS.z);
#endif
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    output.shadowCoord = GetShadowCoord(position);
#endif
    // APV may evaluate at the vertex. Use the same horizontal frame there.
    // Fragment SH is evaluated below with the sampled G mask for exact mask blending.
    TsukuyomiFaceFrame frame = TsukuyomiFaceBuildFrame(position.positionWS,
        normal.normalWS, float4(0, 1, 0, 0), TsukuyomiFaceCameraForwardWS());
#ifdef USE_APV_PROBE_OCCLUSION
    output.vertexSH = SampleProbeSHVertex(GetAbsolutePositionWS(position.positionWS),
        frame.giNormalWS, GetWorldSpaceNormalizeViewDir(position.positionWS), output.probeOcclusion);
#else
    output.vertexSH = SampleProbeSHVertex(GetAbsolutePositionWS(position.positionWS),
        frame.giNormalWS, GetWorldSpaceNormalizeViewDir(position.positionWS));
#endif
    return output;
}

void TsukuyomiFaceInitializeGI(TsukuyomiFaceVaryings input, TsukuyomiFaceFrame frame, inout InputData data)
{
    data.shadowMask = 1.0;
#if defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2)
    float4 vertexOcclusion = 1.0.xxxx;
#ifdef USE_APV_PROBE_OCCLUSION
    vertexOcclusion = input.probeOcclusion;
#endif
    // Call the APV function directly: SAMPLE_GI changes signature when screen GI
    // is enabled, but APV occlusion is still needed for the light shadow mask.
    data.bakedGI = SampleProbeVolumePixel(input.vertexSH, GetAbsolutePositionWS(data.positionWS),
        frame.giNormalWS, data.viewDirectionWS, input.positionCS.xy, vertexOcclusion, data.shadowMask);
#else
    // The G mask is a per-pixel quantity; don't reuse SH baked with a mesh normal.
    data.bakedGI = SampleSH(frame.giNormalWS);
#endif
#if defined(_TSUKUYOMI_SCREEN_SPACE_GLOBAL_ILLUMINATION)
    data.bakedGI = SampleTsukuyomiScreenSpaceGlobalIllumination(data.normalizedScreenSpaceUV);
#elif defined(_SCREEN_SPACE_IRRADIANCE)
    data.bakedGI = SampleScreenSpaceGI(input.positionCS.xy);
#endif
}

half4 TsukuyomiFaceForwardFragment(TsukuyomiFaceVaryings input,
    FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target0
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
#if defined(LOD_FADE_CROSSFADE)
    LODFadeCrossFade(input.positionCS);
#endif
    TsukuyomiFaceSurface surface = TsukuyomiFaceSampleSurface(input.uv);
    float3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
    float3 normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(surface.normalTS,
        float3x3(input.tangentWS.xyz, bitangent, input.normalWS)))
        * TsukuyomiFaceBackFaceSign(IS_FRONT_VFACE(facing, true, false));
    InputData inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
    inputData.positionCS = input.positionCS;
    inputData.normalWS = normalWS;
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1), input.fogFactor);
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    inputData.shadowCoord = input.shadowCoord;
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
#endif
    TsukuyomiFaceFrame frame = TsukuyomiFaceBuildFrame(input.positionWS, normalWS,
        surface.mask, TsukuyomiFaceCameraForwardWS());
    TsukuyomiFaceInitializeGI(input, frame, inputData);
    Light mainLight = GetMainLight(inputData.shadowCoord, input.positionWS, inputData.shadowMask);
    TsukuyomiFaceMainResponse main = TsukuyomiFaceMainLight(surface, frame, input.uv,
        normalWS, inputData.viewDirectionWS, mainLight);
    float3 additional = 0.0.xxx;
#if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();
#if USE_CLUSTER_LIGHT_LOOP
    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        Light light = GetAdditionalLight(lightIndex, input.positionWS, inputData.shadowMask);
        additional += TsukuyomiFaceAdditionalLight(surface, main, normalWS, inputData.viewDirectionWS, light);
    }
#endif
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, input.positionWS, inputData.shadowMask);
        additional += TsukuyomiFaceAdditionalLight(surface, main, normalWS, inputData.viewDirectionWS, light);
    LIGHT_LOOP_END
#endif
    float3 color = TsukuyomiFaceCompose(surface, main, additional, inputData.bakedGI,
        mainLight.color * mainLight.distanceAttenuation);
    return half4(MixFog(color, inputData.fogCoord), 1.0);
}

#endif
