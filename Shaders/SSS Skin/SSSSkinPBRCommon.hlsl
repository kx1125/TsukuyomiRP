#ifndef SSS_SKIN_PBR_COMMON_INCLUDED
#define SSS_SKIN_PBR_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#define _SPECULAR_SETUP 1
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);
TEXTURE2D(_OcclusionMap);
SAMPLER(sampler_OcclusionMap);
TEXTURE2D(_PBRMask);
SAMPLER(sampler_PBRMask);
TEXTURE2D(_BumpMap);
SAMPLER(sampler_BumpMap);
TEXTURE2D(_DetailNormalMap);
SAMPLER(sampler_DetailNormalMap);

CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
half4 _BaseColor;
half4 _OcclusionColor;
half4 _SpecColor;
half _Cutoff;
half _BumpScale;
half _BumpTile;
half _DetailNormalMapScale;
half _DetailNormalMapTile;
half _Metallic;
half _Roughness;
CBUFFER_END

struct SSSSkinPBRAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct SSSSkinPBRVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    half3 normalWS : TEXCOORD2;
    half4 tangentWS : TEXCOORD3;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

SSSSkinPBRVaryings SSSSkinPBRVert(SSSSkinPBRAttributes input)
{
    SSSSkinPBRVaryings output = (SSSSkinPBRVaryings)0;
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

half4 SamplePBRAlbedo(float2 uv)
{
    return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
}

half3 SamplePBRNormalWS(SSSSkinPBRVaryings input)
{
    float2 baseNormalUV = input.uv * max(_BumpTile, 0.0001h);
    half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, baseNormalUV), _BumpScale);
    #if defined(ENABLE_DETAIL_NORMALMAP)
    float2 detailNormalUV = input.uv * max(_DetailNormalMapTile, 0.0001h);
    half3 detailNormalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_DetailNormalMap, sampler_DetailNormalMap, detailNormalUV), _DetailNormalMapScale);
    normalTS = BlendNormalRNM(normalTS, detailNormalTS);
    #endif
    half3 bitangentWS = cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w;
    return TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangentWS, input.normalWS));
}

half SamplePBROcclusion(float2 uv)
{
    return SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv).r;
}

half4 SamplePBRMask(float2 uv)
{
    return SAMPLE_TEXTURE2D(_PBRMask, sampler_PBRMask, uv);
}

SurfaceData BuildSSSSkinPBRSurfaceData(half4 albedo, half3 normalTS, half occlusion, half4 pbrMask)
{
    half metallic = saturate(_Metallic * pbrMask.b);
    half roughness = saturate(lerp(pbrMask.g, 1.0h, _Roughness));
    half3 dielectricSpecular = saturate(_SpecColor.rgb * pbrMask.r);

    SurfaceData surfaceData = (SurfaceData)0;
    surfaceData.albedo = albedo.rgb;
    surfaceData.specular = lerp(dielectricSpecular, albedo.rgb, metallic);
    surfaceData.metallic = 0.0h;
    surfaceData.smoothness = saturate(1.0h - roughness);
    surfaceData.normalTS = normalTS;
    surfaceData.emission = half3(0.0h, 0.0h, 0.0h);
    surfaceData.occlusion = occlusion;
    surfaceData.alpha = albedo.a;
    surfaceData.clearCoatMask = 0.0h;
    surfaceData.clearCoatSmoothness = 1.0h;
    return surfaceData;
}

InputData BuildSSSSkinPBRInputData(SSSSkinPBRVaryings input, half3 normalWS, half3 viewDirWS)
{
    InputData inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
    inputData.positionCS = input.positionCS;
    inputData.normalWS = normalWS;
    inputData.viewDirectionWS = viewDirWS;
    #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
    #else
    inputData.shadowCoord = float4(0.0, 0.0, 0.0, 0.0);
    #endif
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    inputData.bakedGI = SampleSH(normalWS);
    inputData.shadowMask = half4(1.0h, 1.0h, 1.0h, 1.0h);
    #if defined(_ADDITIONAL_LIGHTS_VERTEX)
    inputData.vertexLighting = VertexLighting(input.positionWS, normalWS);
    #endif
    return inputData;
}

#endif
