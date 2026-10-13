Shader "Hidden/Tsukuyomi/MSVOComposite"
{
    Properties { _MsvoOcclusionColor ("Occlusion Color", Color) = (.0980392173, 0, 0, 1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        ZTest Always ZWrite Off Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_MsvoOcclusionTexture);
        float4 _MsvoOcclusionColor;
        struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
        Varyings Vert(uint id:SV_VertexID)
        {
            Varyings o; o.positionCS=GetFullScreenTriangleVertexPosition(id);
            o.uv=GetFullScreenTriangleTexCoord(id); return o;
        }
        half4 Composite(Varyings input):SV_Target
        {
            float visibility=SAMPLE_TEXTURE2D(_MsvoOcclusionTexture,sampler_LinearClamp,input.uv).r;
            return half4((1-visibility)*(1-_MsvoOcclusionColor.rgb),0);
        }
        half4 Debug(Varyings input):SV_Target
        {
            float visibility=SAMPLE_TEXTURE2D(_MsvoOcclusionTexture,sampler_LinearClamp,input.uv).r;
            return half4(visibility.xxx,1);
        }
        ENDHLSL
        Pass
        {
            Name "Color Composite"
            Blend Zero OneMinusSrcColor, Zero One
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Composite
            ENDHLSL
        }
        Pass
        {
            Name "Debug Occlusion"
            Blend One Zero
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Debug
            ENDHLSL
        }
    }
}
