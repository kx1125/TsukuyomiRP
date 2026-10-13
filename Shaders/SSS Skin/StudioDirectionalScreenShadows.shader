Shader "Hidden/TsukuyomiRP/Studio Directional Screen Shadows"
{
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline"}
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Character/StudioDirectionalShadow.hlsl"
            TEXTURE2D_X(_StudioSceneDepthTexture);
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
            Varyings Vertex(uint vertexID:SV_VertexID)
            {
                Varyings o;o.positionCS=GetFullScreenTriangleVertexPosition(vertexID);
                o.uv=GetFullScreenTriangleTexCoord(vertexID);return o;
            }
            float4 Fragment(Varyings input):SV_Target
            {
                float depth=SAMPLE_TEXTURE2D_X_LOD(_StudioSceneDepthTexture,sampler_PointClamp,input.uv,0).r;
                float3 position=ComputeWorldSpacePosition(input.uv,depth,UNITY_MATRIX_I_VP);
                return StudioDirectionalShadowRaw(1,position).xxxx;
            }
            ENDHLSL
        }
    }
}
