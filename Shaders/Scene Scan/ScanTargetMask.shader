Shader "Hidden/Tsukuyomi/ScanTargetMask"
{
    Properties
    {
        [HideInInspector] _BaseMap("Base Map", 2D) = "white" {}
        [HideInInspector] _BaseColor("Base Color", Color) = (1,1,1,1)
        [HideInInspector] _Cutoff("Cutoff", Float) = 0.5
        [HideInInspector] _AlphaClip("Alpha Clip", Float) = 0
        [HideInInspector] _Cull("Cull", Float) = 2
        [HideInInspector] _ScanTargetClass("Target Class", Float) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float _Cutoff;
            float _AlphaClip;
            float _Cull;
            float _ScanTargetClass;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float eyeDepth : TEXCOORD1;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings Vert(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
            output.positionCS = position.positionCS;
            output.eyeDepth = max(0.0, -position.positionVS.z);
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            return output;
        }

        void ApplyAlphaClip(Varyings input)
        {
            if (_AlphaClip > 0.5)
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a - _Cutoff);
        }

        float4 FragTarget(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            ApplyAlphaClip(input);
            return float4(_ScanTargetClass, min(input.eyeDepth, 65504.0), 0.0, 0.0);
        }

        float4 FragExcluded(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            ApplyAlphaClip(input);
            float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
            float rawDepth = SampleSceneDepth(uv);
            float sceneEyeDepth = IsPerspectiveProjection()
                ? LinearEyeDepth(rawDepth, _ZBufferParams) : LinearDepthToEyeDepth(rawDepth);
            // The depth texture and this draw can differ by a small quantization error.
            // A tolerance of 0.5 cm at ordinary distances preserves the visible surface.
            float tolerance = max(0.005, sceneEyeDepth * 0.00005);
            clip(sceneEyeDepth + tolerance - input.eyeDepth);
            return float4(1.0, 0.0, 0.0, 0.0);
        }
        ENDHLSL

        Pass
        {
            Name "TargetMask"
            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            Blend Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragTarget
            #pragma multi_compile_instancing
            ENDHLSL
        }

        Pass
        {
            Name "VisibleExcludedMask"
            Cull [_Cull]
            ZWrite Off
            ZTest Always
            Blend Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragExcluded
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
}
