Shader "Hidden/Tsukuyomi RP/Debug/GTAO Output"
{
    Properties
    {
        _DebugScale("Debug Scale", Float) = 1
        _DebugBias("Debug Bias", Float) = 0
        _DebugInvert("Debug Invert", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }
        
        ZWrite On
        Cull Off

        Pass
        {
            Name "GTAO Output"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_X(_ScreenSpaceOcclusionTexture);
            TEXTURE2D_X(_OcclusionTexture);
            

            float _DebugScale;
            float _DebugBias;
            float _DebugInvert;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float ao = SAMPLE_TEXTURE2D_X(_ScreenSpaceOcclusionTexture, sampler_LinearClamp, input.uv).r;
                ao = saturate(ao * _DebugScale + _DebugBias);
                // ao = lerp(ao, 1.0 - ao, saturate(_DebugInvert));
                return half4(ao, ao, ao, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
