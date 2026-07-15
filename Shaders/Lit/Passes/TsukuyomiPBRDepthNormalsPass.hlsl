#ifndef TSUKUYOMI_PBR_DEPTH_NORMALS_PASS_INCLUDED
#define TSUKUYOMI_PBR_DEPTH_NORMALS_PASS_INCLUDED

#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Material/TsukuyomiPBRInput.hlsl"

#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

struct TsukuyomiPBRDepthNormalsAttributes
{
    float4 positionOS : POSITION;
    float4 tangentOS : TANGENT;
    float2 texcoord : TEXCOORD0;
    float3 normalOS : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct TsukuyomiPBRDepthNormalsVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    half3 normalWS : TEXCOORD1;
    half4 tangentWS : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

TsukuyomiPBRDepthNormalsVaryings TsukuyomiPBRDepthNormalsVertex(TsukuyomiPBRDepthNormalsAttributes input)
{
    TsukuyomiPBRDepthNormalsVaryings output = (TsukuyomiPBRDepthNormalsVaryings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.normalWS = normalInput.normalWS;

    real sign = input.tangentOS.w * GetOddNegativeScale();
    output.tangentWS = half4(normalInput.tangentWS.xyz, sign);
    return output;
}

void TsukuyomiPBRDepthNormalsFragment(
    TsukuyomiPBRDepthNormalsVaryings input,
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

#if defined(_NORMALMAP)
    float tangentSign = input.tangentWS.w;
    float3 bitangent = tangentSign * cross(input.normalWS.xyz, input.tangentWS.xyz);
    half3 normalTS = SampleNormal(input.uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
    float3 normalWS = TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz));
#else
    float3 normalWS = input.normalWS;
#endif

    normalWS = NormalizeNormalPerPixel(normalWS);

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
