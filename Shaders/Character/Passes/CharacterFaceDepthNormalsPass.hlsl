#ifndef TSUKUYOMI_CHARACTER_FACE_DEPTH_NORMALS_INCLUDED
#define TSUKUYOMI_CHARACTER_FACE_DEPTH_NORMALS_INCLUDED

#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Material/CharacterFaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"
#if defined(LOD_FADE_CROSSFADE)
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

struct TsukuyomiFaceDepthAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct TsukuyomiFaceDepthVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    half3 normalWS : TEXCOORD1;
    half4 tangentWS : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

TsukuyomiFaceDepthVaryings TsukuyomiFaceDepthNormalsVertex(TsukuyomiFaceDepthAttributes input)
{
    TsukuyomiFaceDepthVaryings output = (TsukuyomiFaceDepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.normalWS = normal.normalWS;
    output.tangentWS = half4(normal.tangentWS, input.tangentOS.w * GetOddNegativeScale());
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    return output;
}

void TsukuyomiFaceDepthNormalsFragment(TsukuyomiFaceDepthVaryings input,
    FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC, out half4 outNormalWS : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
#if defined(_ALPHATEST_ON)
    Alpha(SAMPLE_TEXTURE2D(_BaseMap, sampler_LinearRepeat, input.uv).a, _BaseColor, _AlphaClipThreshold);
#endif
#if defined(LOD_FADE_CROSSFADE)
    LODFadeCrossFade(input.positionCS);
#endif
    float3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
    float3 normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(TsukuyomiFaceSampleNormalTS(input.uv),
        float3x3(input.tangentWS.xyz, bitangent, input.normalWS)))
        * TsukuyomiFaceBackFaceSign(IS_FRONT_VFACE(facing, true, false));
#if defined(_GBUFFER_NORMALS_OCT)
    float2 oct = PackNormalOctQuadEncode(normalWS);
    outNormalWS = half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0);
#else
    outNormalWS = half4(normalWS, 0);
#endif
#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

#endif
