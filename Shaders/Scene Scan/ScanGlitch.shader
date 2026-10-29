Shader "Hidden/Tsukuyomi RP/Scene Scan/Glitch"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        HLSLINCLUDE
        #pragma target 3.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        float4 _ScanOriginProgress;
        float4 _ScanCurveParams; // blend, world, screen, reveal
        float4 _ScanGlitchParams; // signed horizontal UV offset, channel split, random, intensity
        float4 _ScanGlitchMode; // scan coverage, external voxel weight
        float4 _ScanGlitchGrid; // grid size, range, voxel strength, ring width

        float Hash(float3 p) { return frac(sin(dot(p, float3(127.1, 311.7, 74.7))) * 43758.5453); }

        half4 Voxel(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = input.texcoord;
            half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
            float rawDepth = SampleSceneDepth(uv);
            #if UNITY_REVERSED_Z
                if (rawDepth <= 0.000001) return source;
                float deviceDepth = rawDepth;
            #else
                if (rawDepth >= 0.999999) return source;
                float deviceDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
            #endif
            float3 positionWS = ComputeWorldSpacePosition(uv, deviceDepth, UNITY_MATRIX_I_VP);
            float transition = (1.0 - _ScanCurveParams.w) * (1.0 - smoothstep(0.94, 1.0, _ScanOriginProgress.w));
            transition = _ScanGlitchMode.x > 0.5 ? transition * _ScanGlitchMode.y : _ScanGlitchMode.y;
            float gridSize = max(0.01, _ScanGlitchGrid.x * lerp(0.025, 0.22, transition));
            float3 snapped = floor(positionWS / gridSize + 0.5) * gridSize;
            float4 clip = TransformWorldToHClip(snapped);
            if (clip.w <= 0.0001) return source;
            float2 snappedUV = ComputeNormalizedDeviceCoordinates(snapped, UNITY_MATRIX_VP);
            float2 safeUV = clamp(snappedUV, _BlitTexture_TexelSize.xy * 0.5, 1.0 - _BlitTexture_TexelSize.xy * 0.5);
            float3 cell = floor(positionWS / _ScanGlitchGrid.x + 0.5);
            float cellNoise = Hash(cell + floor(_ScanGlitchParams.z * 31.0));
            float selection = smoothstep(0.45, 0.75, cellNoise);
            float radial = 1.0 - smoothstep(_ScanGlitchGrid.y * _ScanCurveParams.y,
                _ScanGlitchGrid.y * _ScanCurveParams.y + _ScanGlitchGrid.w * 2.0,
                distance(positionWS, _ScanOriginProgress.xyz));
            if (_ScanGlitchMode.x < 0.5) radial = 1.0;
            float weight = saturate(transition * _ScanCurveParams.x * _ScanGlitchGrid.z * selection * radial);
            half4 voxel = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, safeUV);
            return half4(lerp(source.rgb, voxel.rgb, weight), source.a);
        }

        half4 Horizontal(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = input.texcoord;
            half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
            float band = floor(uv.y * 72.0);
            float noise = Hash(float3(band, floor(_ScanGlitchParams.z * 97.0), 17.0));
            float stripe = smoothstep(0.52, 0.72, noise);
            float strength = _ScanCurveParams.x * _ScanGlitchParams.w;
            float offset = _ScanGlitchParams.x * stripe * strength;
            float split = _ScanGlitchParams.y * (0.3 + stripe * 0.7) * strength;
            float2 centerUV = clamp(uv + float2(offset, 0.0), _BlitTexture_TexelSize.xy * 0.5, 1.0 - _BlitTexture_TexelSize.xy * 0.5);
            half3 color;
            color.r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(centerUV + float2(split, 0.0))).r;
            color.g = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, centerUV).g;
            color.b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(centerUV - float2(split, 0.0))).b;
            return half4(color, source.a);
        }
        ENDHLSL
        Pass
        {
            Name "World Voxel Glitch"
            ZTest Always ZWrite Off Cull Off Blend Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Voxel
            ENDHLSL
        }
        Pass
        {
            Name "Horizontal Channel Glitch"
            ZTest Always ZWrite Off Cull Off Blend Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Horizontal
            ENDHLSL
        }
    }
    Fallback Off
}
