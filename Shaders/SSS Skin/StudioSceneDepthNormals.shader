Shader "Hidden/TsukuyomiRP/Studio Scene Depth Normals"
{
    Properties
    {
        _BaseMap("Base",2D)="white"{}
        _BaseColor("Color",Color)=(1,1,1,1)
        _Cutoff("Cutoff",Float)=.5
        _AlphaClip("Alpha clip",Float)=0
        _StudioLashes("Studio eyelashes",Float)=0
        _SourceMode("Original material mode",Float)=0
        _StudioSourceAlphaMap("Source replacement alpha",2D)="white"{}
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline"}
        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_StudioSourceAlphaMap); SAMPLER(sampler_StudioSourceAlphaMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float _Cutoff;
            float _AlphaClip;
            float _StudioLashes;
            float _SourceMode;
            CBUFFER_END
            struct Attributes {float3 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float4 normalDepth:TEXCOORD1;};
            Varyings Vertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                float3 world=TransformObjectToWorld(input.positionOS);
                output.positionCS=TransformWorldToHClip(world);
                output.uv=input.uv*_BaseMap_ST.xy+_BaseMap_ST.zw;
                output.normalDepth.xyz=normalize(TransformWorldToViewDir(TransformObjectToWorldNormal(input.normalOS),false));
                output.normalDepth.w=-TransformWorldToView(world).z*_ProjectionParams.w;
                return output;
            }
            float4 Fragment(Varyings input,FRONT_FACE_TYPE front:FRONT_FACE_SEMANTIC):SV_Target
            {
                bool treeCutout=_SourceMode>5.5;
                if(!treeCutout)clip(IS_FRONT_VFACE(front,1,-1));
                if(_AlphaClip>.5)
                {
                    float alpha=_StudioLashes>.5
                        ? SAMPLE_TEXTURE2D(_StudioSourceAlphaMap,sampler_StudioSourceAlphaMap,input.uv).a
                        : SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).a;
                    // AIT Clothes True retains Built-in TreeTransparentCutout:
                    // raw alpha, two sides, with reversed back-face normals.
                    clip(alpha*(treeCutout?1:_BaseColor.a)-_Cutoff);
                }
                // Built-in normal encoding uses the interpolated vertex normal
                // without renormalizing it in the fragment shader.
                float3 viewNormal=input.normalDepth.xyz;
                if(treeCutout)viewNormal*=IS_FRONT_VFACE(front,1,-1);
                float2 normal=viewNormal.xy/(viewNormal.z+1)*(.5/1.7777)+.5;
                float2 depth=frac(input.normalDepth.ww*float2(1,255));
                depth.x-=depth.y/255;
                return float4(normal,depth);
            }
            float4 DepthFragment(Varyings input):SV_Target
            {
                // Studio's LightingPass replacement has no tree-cutout variant.
                // Original events 697/703 and 711/717 appear only in normals.
                if(_SourceMode>5.5)clip(-1);
                // Studio LightingPassCutout camera depth uses raw texture alpha;
                // its depth-normal replacement separately multiplies color alpha.
                if(_AlphaClip>.5)clip(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).a-_Cutoff);
                return 0;
            }
        ENDHLSL
        Pass
        {
            Cull Off ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Cull Back ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
}
