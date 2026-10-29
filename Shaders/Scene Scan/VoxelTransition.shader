Shader "Hidden/Tsukuyomi RP/VoxelTransition"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        Pass
        {
            Name "Voxel Transition Sampling"
            ZTest Always ZWrite Off Cull Off Blend Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/SceneScanCommon.hlsl"
            struct TransitionOutput
            {
                half4 sampled : SV_Target0;
                half4 distorted : SV_Target1;
                float screenMask : SV_Target2;
            };

            TransitionOutput Frag(Varyings input)
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float rawDepth = SampleSceneDepth(uv);
                float3 expansion = ScreenExpansion(uv);
                TransitionOutput output;
                output.sampled = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp,
                    ClampSceneUV(VoxelSceneUV(uv, rawDepth) + expansion.xy));
                output.distorted = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp,
                    ClampSceneUV(uv + expansion.xy));
                output.screenMask = expansion.z;
                return output;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
