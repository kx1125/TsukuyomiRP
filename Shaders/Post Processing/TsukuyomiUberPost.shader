Shader "Hidden/Tsukuyomi/Post Processing/UberPost"
{
    HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        TEXTURE2D_X(_TsukuyomiBloomTexture);
        TEXTURE2D_X(_TsukuyomiBloomMipDown0);
        TEXTURE2D_X(_TsukuyomiBloomMipDown1);
        TEXTURE2D_X(_TsukuyomiBloomMipDown2);

        float4 _TsukuyomiBloomParams; // x: threshold, y: lum range scale, z: prefilter scale, w: intensity
        float4 _TsukuyomiBloomColorTint;
        float2 _TsukuyomiBloomBlurScaler;
        float4 _TsukuyomiBloomBlurCompositeWeight;
        float4 _TsukuyomiToneMapParams0; // x: max brightness, y: contrast, z: linear start, w: linear length
        float4 _TsukuyomiToneMapParams1; // x: black pow, y: black min

        #define BloomThreshold      _TsukuyomiBloomParams.x
        #define BloomLumRangeScale  _TsukuyomiBloomParams.y
        #define BloomPreFilterScale _TsukuyomiBloomParams.z
        #define BloomIntensity      _TsukuyomiBloomParams.w

        half4 EncodeHDR(half3 color)
        {
        #if UNITY_COLORSPACE_GAMMA
            color = sqrt(color);
        #endif

            return half4(color, 1.0);
        }

        half3 DecodeHDR(half4 data)
        {
            half3 color = data.xyz;

        #if UNITY_COLORSPACE_GAMMA
            color *= color;
        #endif

            return color;
        }

        half3 TsukuyomiAcesSimpleTonemap(half3 color)
        {
            float3 x = max((float3)color, 0.0);
            float3 numerator = x * (x * 1.36 + 0.047);
            float3 denominator = x * (x * 0.93 + 0.56) + 0.14;
            return saturate(numerator / denominator);
        }

        half3 TsukuyomiGranTurismoTonemap(half3 color)
        {
            float3 x = max((float3)color, 0.0);
            float P = max(_TsukuyomiToneMapParams0.x, 0.0001);
            float a = max(_TsukuyomiToneMapParams0.y, 0.0001);
            float m = max(_TsukuyomiToneMapParams0.z, 0.0001);
            float l = _TsukuyomiToneMapParams0.w;
            float c = max(_TsukuyomiToneMapParams1.x, 0.0001);
            float b = _TsukuyomiToneMapParams1.y;

            float l0 = ((P - m) * l) / a;
            float L0 = m - m / a;
            float S0 = m + l0;
            float S1 = m + a * l0;
            float C2 = (a * P) / max(P - S1, 0.0001);
            float CP = -C2 / P;

            float3 w0 = 1.0 - smoothstep(0.0, m, x);
            float3 w2 = step(S0, x);
            float3 w1 = 1.0 - w0 - w2;

            float3 T = m * pow(max(x / m, 0.0), c) + b;
            float3 L = m + a * (x - m);
            float3 S = P - (P - S1) * exp(CP * (x - S0));
            return saturate(T * w0 + L * w1 + S * w2);
        }

        half3 ApplyTsukuyomiTonemap(half3 color)
        {
        #if defined(_TSUKUYOMI_TONEMAP_GT)
            return TsukuyomiGranTurismoTonemap(color);
        #elif defined(_TSUKUYOMI_TONEMAP_ACES_SIMPLE)
            return TsukuyomiAcesSimpleTonemap(color);
        #elif defined(_TSUKUYOMI_TONEMAP_ACES)
            float3 aces = unity_to_ACES(color);
            return saturate(AcesTonemap(aces));
        #elif defined(_TSUKUYOMI_TONEMAP_NEUTRAL)
            return saturate(NeutralTonemap(color));
        #else
            return color;
        #endif
        }

        half4 FragUber(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);
            half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

        #if defined(_TSUKUYOMI_CUSTOM_BLOOM)
            half3 bloom = DecodeHDR(SAMPLE_TEXTURE2D_X(_TsukuyomiBloomTexture, sampler_LinearClamp, uv));
            color.rgb += bloom * BloomIntensity;
        #endif

            color.rgb = ApplyTsukuyomiTonemap(color.rgb);
            return color;
        }

        half4 FragBloomPrefilter(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);
            float2 texelSize = _BlitTexture_TexelSize.xy;

            half3 color = 0.0;
            color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + half2(-1.0, -1.0) * texelSize).rgb;
            color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + half2( 1.0, -1.0) * texelSize).rgb;
            color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + half2(-1.0,  1.0) * texelSize).rgb;
            color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + half2( 1.0,  1.0) * texelSize).rgb;
            color *= 0.25;

            color *= rcp(1.0 + BloomLumRangeScale * Luminance(color));
            color = max(half3(0.0, 0.0, 0.0), color - BloomThreshold);
            color *= BloomPreFilterScale;
            color = lerp(color, _TsukuyomiBloomColorTint.rgb * Luminance(color), _TsukuyomiBloomColorTint.a);
            return EncodeHDR(color);
        }

        half4 FragBloomDownsample(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);
            float2 texelSize = _BlitTexture_TexelSize.xy;

            half3 color = 0.0;
            color += DecodeHDR(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + half2( 0.96,  0.25) * texelSize));
            color += DecodeHDR(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + half2( 0.25, -0.96) * texelSize));
            color += DecodeHDR(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + half2(-0.96, -0.25) * texelSize));
            color += DecodeHDR(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + half2(-0.25,  0.96) * texelSize));
            return EncodeHDR(color * 0.25);
        }

        void AddBlurPair(inout half3 color, float2 uv, float2 scaler, float offset, float weight)
        {
            float2 offsetUv0 = clamp(uv + scaler * offset, _BlitTexture_TexelSize.xy, 1.0 - _BlitTexture_TexelSize.xy);
            float2 offsetUv1 = clamp(uv - scaler * offset, _BlitTexture_TexelSize.xy, 1.0 - _BlitTexture_TexelSize.xy);
            color += DecodeHDR(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, offsetUv0)) * weight;
            color += DecodeHDR(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, offsetUv1)) * weight;
        }

        void AddBlurCenter(inout half3 color, float2 uv, float weight)
        {
            float2 offsetUv = clamp(uv, _BlitTexture_TexelSize.xy, 1.0 - _BlitTexture_TexelSize.xy);
            color += DecodeHDR(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, offsetUv)) * weight;
        }

        half4 FragBloomBlurKernel(Varyings input, int kernel)
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);
            float2 scaler = _TsukuyomiBloomBlurScaler * _BlitTexture_TexelSize.xy;
            half3 color = 0.0;

            if (kernel == 0)
            {
                AddBlurPair(color, uv, scaler, 5.307122, 0.03527068);
                AddBlurPair(color, uv, scaler, 3.373378, 0.12735710);
                AddBlurPair(color, uv, scaler, 1.444753, 0.25972970);
                AddBlurCenter(color, uv, 0.15528520);
            }
            else if (kernel == 1)
            {
                AddBlurPair(color, uv, scaler, 7.324664, 0.01700169);
                AddBlurPair(color, uv, scaler, 5.368860, 0.05872535);
                AddBlurPair(color, uv, scaler, 3.415373, 0.13847290);
                AddBlurPair(color, uv, scaler, 1.463444, 0.22298470);
                AddBlurCenter(color, uv, 0.12563070);
            }
            else if (kernel == 2)
            {
                AddBlurPair(color, uv, scaler, 15.365450, 0.002165789);
                AddBlurPair(color, uv, scaler, 13.382110, 0.006026655);
                AddBlurPair(color, uv, scaler, 11.399060, 0.014561720);
                AddBlurPair(color, uv, scaler, 9.416246, 0.030551590);
                AddBlurPair(color, uv, scaler, 7.433644, 0.055660430);
                AddBlurPair(color, uv, scaler, 5.451206, 0.088055510);
                AddBlurPair(color, uv, scaler, 3.468890, 0.120967400);
                AddBlurPair(color, uv, scaler, 1.486653, 0.144306200);
                AddBlurCenter(color, uv, 0.075409520);
            }
            else
            {
                AddBlurPair(color, uv, scaler, 19.391510, 0.001667595);
                AddBlurPair(color, uv, scaler, 17.402340, 0.003832045);
                AddBlurPair(color, uv, scaler, 15.413260, 0.008048251);
                AddBlurPair(color, uv, scaler, 13.424270, 0.015449170);
                AddBlurPair(color, uv, scaler, 11.435350, 0.027104610);
                AddBlurPair(color, uv, scaler, 9.446500, 0.043462710);
                AddBlurPair(color, uv, scaler, 7.457702, 0.063698220);
                AddBlurPair(color, uv, scaler, 5.468947, 0.085324850);
                AddBlurPair(color, uv, scaler, 3.480224, 0.104463000);
                AddBlurPair(color, uv, scaler, 1.491521, 0.116892900);
                AddBlurCenter(color, uv, 0.060113440);
            }

            return EncodeHDR(color);
        }

        half4 FragBloomPreBlur(Varyings input) : SV_Target
        {
            return FragBloomBlurKernel(input, 0);
        }

        half4 FragBloomBlur0(Varyings input) : SV_Target
        {
            return FragBloomBlurKernel(input, 1);
        }

        half4 FragBloomBlur1(Varyings input) : SV_Target
        {
            return FragBloomBlurKernel(input, 2);
        }

        half4 FragBloomBlur2(Varyings input) : SV_Target
        {
            return FragBloomBlurKernel(input, 3);
        }

        half4 FragBloomCombine(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);
            float4 weights = _TsukuyomiBloomBlurCompositeWeight;

            half3 color = DecodeHDR(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv)) * weights.x;
            color += DecodeHDR(SAMPLE_TEXTURE2D_X(_TsukuyomiBloomMipDown0, sampler_LinearClamp, uv)) * weights.y;
            color += DecodeHDR(SAMPLE_TEXTURE2D_X(_TsukuyomiBloomMipDown1, sampler_LinearClamp, uv)) * weights.z;
            color += DecodeHDR(SAMPLE_TEXTURE2D_X(_TsukuyomiBloomMipDown2, sampler_LinearClamp, uv)) * weights.w;
            return EncodeHDR(color);
        }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "Tsukuyomi Uber Composite"

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragUber
                #pragma multi_compile_local_fragment _ _TSUKUYOMI_CUSTOM_BLOOM
                #pragma multi_compile_local_fragment _ _TSUKUYOMI_TONEMAP_NEUTRAL _TSUKUYOMI_TONEMAP_ACES _TSUKUYOMI_TONEMAP_ACES_SIMPLE _TSUKUYOMI_TONEMAP_GT
            ENDHLSL
        }

        Pass
        {
            Name "Custom Bloom Prefilter"

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragBloomPrefilter
            ENDHLSL
        }

        Pass
        {
            Name "Custom Bloom Downsample"

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragBloomDownsample
            ENDHLSL
        }

        Pass
        {
            Name "Custom Bloom Pre Blur"

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragBloomPreBlur
            ENDHLSL
        }

        Pass
        {
            Name "Custom Bloom Mip Blur 0"

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragBloomBlur0
            ENDHLSL
        }

        Pass
        {
            Name "Custom Bloom Mip Blur 1"

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragBloomBlur1
            ENDHLSL
        }

        Pass
        {
            Name "Custom Bloom Mip Blur 2"

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragBloomBlur2
            ENDHLSL
        }

        Pass
        {
            Name "Custom Bloom Combine"

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragBloomCombine
            ENDHLSL
        }
    }
}
