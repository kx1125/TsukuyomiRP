#ifndef TSUKUYOMI_CHARACTER_HAIR_DEPTH_NORMALS_PASS_INCLUDED
#define TSUKUYOMI_CHARACTER_HAIR_DEPTH_NORMALS_PASS_INCLUDED

#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Character/CharacterNPRInput.hlsl"

#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

struct TsukuyomiHairDepthNormalsAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 texcoord : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct TsukuyomiHairDepthNormalsVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
    float4 tangentWS : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

TsukuyomiHairDepthNormalsVaryings TsukuyomiHairDepthNormalsVertex(
    TsukuyomiHairDepthNormalsAttributes input)
{
    TsukuyomiHairDepthNormalsVaryings output = (TsukuyomiHairDepthNormalsVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    output.normalWS = normalInputs.normalWS;
    output.tangentWS = float4(normalInputs.tangentWS,
        input.tangentOS.w * GetOddNegativeScale());
    return output;
}

void TsukuyomiHairDepthNormalsFragment(
    TsukuyomiHairDepthNormalsVaryings input,
    out float4 outNormalWS : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    TsukuyomiHairAlphaClip(input.uv);
#if defined(LOD_FADE_CROSSFADE)
    LODFadeCrossFade(input.positionCS);
#endif
    float3 normalWS = normalize(input.normalWS);
#if defined(_NORMALMAP)
    float3 tangentWS = normalize(input.tangentWS.xyz);
    float3 bitangentWS = input.tangentWS.w * cross(normalWS, tangentWS);
    float3 normalTS;
    #if defined(_SPECULAR_NORMALMAP)
    normalTS = TsukuyomiHairDecodeLinearNormal(
        SAMPLE_TEXTURE2D(_SplitNormalMap, sampler_SplitNormalMap, input.uv).xy,
        _BumpScale);
    #else
    normalTS = TsukuyomiHairDecodeDxt5Normal(
        SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
    #endif
    normalWS = normalize(TransformTangentToWorld(normalTS,
        float3x3(tangentWS, bitangentWS, normalWS)));
#endif
#if defined(_GBUFFER_NORMALS_OCT)
    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
    float3 packedNormalWS = PackFloat2To888(saturate(octNormalWS * 0.5 + 0.5));
    outNormalWS = float4(packedNormalWS, 0.0);
#else
    outNormalWS = float4(normalWS, 0.0);
#endif
#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

#endif
