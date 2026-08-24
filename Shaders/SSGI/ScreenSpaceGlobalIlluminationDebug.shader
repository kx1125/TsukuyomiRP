Shader "Hidden/Tsukuyomi RP/Debug/SSGI Output"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        Pass
        {
            Name "SSGI Debug Output"

            ZTest Always
            ZWrite Off
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D_X(_TsukuyomiScreenSpaceGlobalIlluminationTexture);

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 indirectDiffuse = SAMPLE_TEXTURE2D_X_LOD(
                    _TsukuyomiScreenSpaceGlobalIlluminationTexture,
                    sampler_LinearClamp,
                    input.texcoord,
                    0.0).rgb;
                return half4(indirectDiffuse, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
