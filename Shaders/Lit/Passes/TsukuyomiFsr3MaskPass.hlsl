#ifndef TSUKUYOMI_FSR3_MASK_PASS_INCLUDED
#define TSUKUYOMI_FSR3_MASK_PASS_INCLUDED

struct TsukuyomiFsr3MaskAttributes
{
    float4 positionOS : POSITION;
    float2 texcoord : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct TsukuyomiFsr3MaskVaryings
{
    float2 uv : TEXCOORD0;
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

struct TsukuyomiFsr3MaskOutput
{
    half reactive : SV_Target0;
    half composition : SV_Target1;
};

TsukuyomiFsr3MaskVaryings TsukuyomiFsr3MaskVertex(TsukuyomiFsr3MaskAttributes input)
{
    TsukuyomiFsr3MaskVaryings output = (TsukuyomiFsr3MaskVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    return output;
}

TsukuyomiFsr3MaskOutput TsukuyomiFsr3MaskFragment(TsukuyomiFsr3MaskVaryings input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    half alpha = saturate(SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a * _BaseColor.a);
#if defined(_ALPHATEST_ON)
    clip(alpha - _Cutoff);
#endif

    TsukuyomiFsr3MaskOutput output;
    output.reactive = min(alpha * max(_Fsr3ReactiveScale, 0.0h), 0.9h);
    output.composition = saturate(alpha * max(_Fsr3CompositionScale, 0.0h));
    return output;
}

#endif
