// Unity PPSv2, MIT license in ../Multi Scale Volumetric Occlusion/LICENSE.md.
Shader "Hidden/Tsukuyomi/StudioBloom"
{
    HLSLINCLUDE
        
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "StudioBloomSampling.hlsl"
        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        TEXTURE2D(_BloomTex); SAMPLER(sampler_BloomTex);
        struct VaryingsDefault { float4 positionCS:SV_POSITION; float2 texcoord:TEXCOORD0; float2 texcoordStereo:TEXCOORD1; };
        VaryingsDefault VertDefault(uint id:SV_VertexID)
        {
            VaryingsDefault o; o.positionCS=GetFullScreenTriangleVertexPosition(id);
            o.texcoord=GetFullScreenTriangleTexCoord(id); o.texcoordStereo=o.texcoord; return o;
        }
        #define UnityStereoAdjustedTexelSize(x) (x)
        half4 SafeHDR(half4 color) { return min(color,65504.0); }
        half4 QuadraticThreshold(half4 color,half threshold,half3 curve)
        {
            half br=Max3(color.r,color.g,color.b);
            half rq=clamp(br-curve.x,0.0,curve.y);rq=curve.z*rq*rq;
            return color*max(rq,br-threshold)/max(br,1e-4);
        }
        float4 _MainTex_TexelSize;
        float  _SampleScale;
        float4 _ColorIntensity;
        float4 _StudioBloomTint;
        float4 _Threshold; // x: threshold value (linear), y: threshold - knee, z: knee * 2, w: 0.25 / knee
        float4 _Params; // x: clamp, yzw: unused

        // ----------------------------------------------------------------------------------------
        // Prefilter

        half4 Prefilter(half4 color, float2 uv)
        {
            half autoExposure = 1.0;
            color *= autoExposure;
            color = min(_Params.x, color); // clamp to max
            color = QuadraticThreshold(color, _Threshold.x, _Threshold.yzw);
            return color;
        }

        half4 FragPrefilter13(VaryingsDefault i) : SV_Target
        {
            half4 color = DownsampleBox13Tap(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), i.texcoord, UnityStereoAdjustedTexelSize(_MainTex_TexelSize).xy);
            return Prefilter(SafeHDR(color), i.texcoord);
        }

        half4 FragPrefilter4(VaryingsDefault i) : SV_Target
        {
            half4 color = DownsampleBox4Tap(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), i.texcoord, UnityStereoAdjustedTexelSize(_MainTex_TexelSize).xy);
            return Prefilter(SafeHDR(color), i.texcoord);
        }

        // ----------------------------------------------------------------------------------------
        // Downsample

        half4 FragDownsample13(VaryingsDefault i) : SV_Target
        {
            half4 color = DownsampleBox13Tap(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), i.texcoord, UnityStereoAdjustedTexelSize(_MainTex_TexelSize).xy);
            return color;
        }

        half4 FragDownsample4(VaryingsDefault i) : SV_Target
        {
            half4 color = DownsampleBox4Tap(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), i.texcoord, UnityStereoAdjustedTexelSize(_MainTex_TexelSize).xy);
            return color;
        }

        // ----------------------------------------------------------------------------------------
        // Upsample & combine

        half4 Combine(half4 bloom, float2 uv)
        {
            half4 color = SAMPLE_TEXTURE2D(_BloomTex, sampler_BloomTex, uv);
            return bloom + color;
        }

        half4 FragUpsampleTent(VaryingsDefault i) : SV_Target
        {
            half4 bloom = UpsampleTent(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), i.texcoord, UnityStereoAdjustedTexelSize(_MainTex_TexelSize).xy, _SampleScale);
            return Combine(bloom, i.texcoordStereo);
        }

        half4 FragUpsampleBox(VaryingsDefault i) : SV_Target
        {
            half4 bloom = UpsampleBox(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), i.texcoord, UnityStereoAdjustedTexelSize(_MainTex_TexelSize).xy, _SampleScale);
            return Combine(bloom, i.texcoordStereo);
        }

        // ----------------------------------------------------------------------------------------
        // Debug overlays

        half4 FragDebugOverlayThreshold(VaryingsDefault i) : SV_Target
        {
            half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoordStereo);
            return half4(Prefilter(SafeHDR(color), i.texcoord).rgb, 1.0);
        }

        half4 FragDebugOverlayTent(VaryingsDefault i) : SV_Target
        {
            half4 bloom = UpsampleTent(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), i.texcoord, UnityStereoAdjustedTexelSize(_MainTex_TexelSize).xy, _SampleScale);
            return half4(bloom.rgb * _ColorIntensity.w * _StudioBloomTint.rgb, 1.0);
        }

        half4 FragDebugOverlayBox(VaryingsDefault i) : SV_Target
        {
            half4 bloom = UpsampleBox(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), i.texcoord, UnityStereoAdjustedTexelSize(_MainTex_TexelSize).xy, _SampleScale);
            return half4(bloom.rgb * _ColorIntensity.w * _StudioBloomTint.rgb, 1.0);
        }

    ENDHLSL

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        // 0: Prefilter 13 taps
        Pass
        {
            HLSLPROGRAM

                #pragma vertex VertDefault
                #pragma fragment FragPrefilter13

            ENDHLSL
        }

        // 1: Prefilter 4 taps
        Pass
        {
            HLSLPROGRAM

                #pragma vertex VertDefault
                #pragma fragment FragPrefilter4

            ENDHLSL
        }

        // 2: Downsample 13 taps
        Pass
        {
            HLSLPROGRAM

                #pragma vertex VertDefault
                #pragma fragment FragDownsample13

            ENDHLSL
        }

        // 3: Downsample 4 taps
        Pass
        {
            HLSLPROGRAM

                #pragma vertex VertDefault
                #pragma fragment FragDownsample4

            ENDHLSL
        }

        // 4: Upsample tent filter
        Pass
        {
            HLSLPROGRAM

                #pragma vertex VertDefault
                #pragma fragment FragUpsampleTent

            ENDHLSL
        }

        // 5: Upsample box filter
        Pass
        {
            HLSLPROGRAM

                #pragma vertex VertDefault
                #pragma fragment FragUpsampleBox

            ENDHLSL
        }

        // 6: Debug overlay (threshold)
        Pass
        {
            HLSLPROGRAM

                #pragma vertex VertDefault
                #pragma fragment FragDebugOverlayThreshold

            ENDHLSL
        }

        // 7: Debug overlay (tent filter)
        Pass
        {
            Blend One One, Zero One
            HLSLPROGRAM

                #pragma vertex VertDefault
                #pragma fragment FragDebugOverlayTent

            ENDHLSL
        }

        // 8: Debug overlay (box filter)
        Pass
        {
            HLSLPROGRAM

                #pragma vertex VertDefault
                #pragma fragment FragDebugOverlayBox

            ENDHLSL
        }
    }
}
