#ifndef TSUKUYOMI_STUDIO_EYE_SHADOW_PASS_INCLUDED
#define TSUKUYOMI_STUDIO_EYE_SHADOW_PASS_INCLUDED

#include TSUKUYOMI_MATERIAL_INPUT
struct StudioEyeShadowAttributes
{
    float4 positionOS : POSITION;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};
struct StudioEyeShadowVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};
StudioEyeShadowVaryings StudioEyeShadowVertex(StudioEyeShadowAttributes input)
{
    StudioEyeShadowVaryings output=(StudioEyeShadowVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input,output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
    output.uv=TRANSFORM_TEX(input.uv,_BaseMap);
    return output;
}
void StudioEyeShadowFragment(StudioEyeShadowVaryings input, out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    float alpha=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).a;
    float coverage=min(pow(alpha,_StudioEyeShadowScale),1)*_BaseColor.a;
    outColor=half4(lerp(1.0.xxx,_BaseColor.rgb,coverage),1);
#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers=EncodeMeshRenderingLayer();
#endif
}

#endif
