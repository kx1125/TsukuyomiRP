// Unity Standard Assets GlobalFog math, reconstructed from the reference shader.
// It runs after opaques and before transparents, matching ImageEffectOpaque.
Shader "Hidden/Tsukuyomi/StudioGlobalFog"
{
    Properties
    {
        _StudioFogColor ("Fog Color (linear)", Vector) = (0,0,0,1)
        _StudioFogParams ("Height, height density, fog density, source near", Vector) = (57,4.60054,0.0001633331,0.01)
        _StudioSourceTransform ("Source offset xyz, inverse world scale", Vector) = (0,0,0,10)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        ZTest Always ZWrite Off Cull Off
        Pass
        {
            Name "Studio Height Fog"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _StudioFogColor;
            float4 _StudioFogParams;
            float4 _StudioSourceTransform;
            CBUFFER_END
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float depth = SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z
                depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, depth);
                #endif
                float3 world = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                float3 camera = GetCameraPositionWS() * _StudioSourceTransform.w + _StudioSourceTransform.xyz;
                float3 direction = (world - GetCameraPositionWS()) * _StudioSourceTransform.w;
                float distanceToPixel = length(direction);
                float cameraHeight = camera.y - _StudioFogParams.x;
                float pixelHeight = cameraHeight + direction.y;
                float k = cameraHeight <= 0.0 ? 1.0 : 0.0;
                float c1 = k * (pixelHeight + cameraHeight);
                float c2 = min((1.0 - 2.0 * k) * pixelHeight, 0.0);
                float halfSpace = -distanceToPixel * _StudioFogParams.y * 0.5 *
                    (c1 - c2 * c2 / abs(direction.y + 1e-5));
                float fogCoord = max(0.0, distanceToPixel - _StudioFogParams.w + halfSpace);
                float extinction = fogCoord * _StudioFogParams.z * 1.2011224;
                float transmission = saturate(exp2(-extinction * extinction));
                color.rgb = lerp(_StudioFogColor.rgb, color.rgb, transmission);
                return color;
            }
            ENDHLSL
        }
    }
}
