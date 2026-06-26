Shader "Hidden/Tsukuyomi/ScreenSpacePCSSShadows"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

        #define TSUKUYOMI_PCSS_DISK_SAMPLE_COUNT 64

        TEXTURE2D_X(_TsukuyomiPenumbraMaskTex);
        TEXTURE2D_X(_ContactShadowMap);
        TEXTURE2D_X(_TsukuyomiBaseScreenSpaceShadowmapTexture);
        float4 _TsukuyomiPenumbraMaskTex_TexelSize;
        float4 _TsukuyomiColorAttachmentTexelSize;

        float _TsukuyomiEnablePCSS;
        float _TsukuyomiEnableMainLightShadow;
        float _TsukuyomiPcssFindBlockerSampleCount;
        float _TsukuyomiPcssPcfSampleCount;
        float _TsukuyomiPcssAngularDiameter;
        float _TsukuyomiPcssBlockerSearchAngularDiameter;
        float _TsukuyomiPcssMinFilterMaxAngularDiameter;
        float _TsukuyomiPcssMaxPenumbraSize;
        float _TsukuyomiPcssMaxSamplingDistance;
        float _TsukuyomiPcssMinFilterSizeTexels;

        static const float2 TsukuyomiPenumbraOffsets[4] =
        {
            float2(-1.0, 1.0),
            float2(1.0, 1.0),
            float2(-1.0, -1.0),
            float2(1.0, -1.0)
        };

        static const float2 TsukuyomiPcssDisk[TSUKUYOMI_PCSS_DISK_SAMPLE_COUNT] =
        {
            float2(1, 0),
            float2(-0.7373689, 0.6754903),
            float2(0.08742572, -0.996171),
            float2(0.6084389, 0.7936008),
            float2(-0.9847135, -0.174182),
            float2(0.8437553, -0.5367281),
            float2(-0.2596043, 0.9657151),
            float2(-0.460907, -0.8874484),
            float2(0.9393213, 0.3430386),
            float2(-0.9243456, 0.3815564),
            float2(0.423846, -0.9057343),
            float2(0.2992839, 0.9541641),
            float2(-0.8652112, -0.5014076),
            float2(0.9766758, -0.2147194),
            float2(-0.5751294, 0.8180624),
            float2(-0.1285107, -0.9917081),
            float2(0.764649, 0.644447),
            float2(-0.9991461, 0.04131783),
            float2(0.7088294, -0.7053799),
            float2(-0.04619145, 0.9989326),
            float2(-0.6407092, -0.7677837),
            float2(0.9910694, 0.133347),
            float2(-0.8208583, 0.5711319),
            float2(0.2194814, -0.9756167),
            float2(0.4971809, 0.8676469),
            float2(-0.9526928, -0.303935),
            float2(0.9077911, -0.4194225),
            float2(-0.3860611, 0.9224732),
            float2(-0.3384523, -0.9409836),
            float2(0.8851894, 0.4652308),
            float2(-0.96697, 0.2548902),
            float2(0.5408377, -0.8411269),
            float2(0.1693762, 0.9855515),
            float2(-0.7906232, -0.612303),
            float2(0.9965857, -0.08256509),
            float2(-0.6790793, 0.7340649),
            float2(0.004878277, -0.9999881),
            float2(0.6718852, 0.7406553),
            float2(-0.9957327, -0.09228428),
            float2(0.7965594, -0.6045602),
            float2(-0.1789836, 0.9838521),
            float2(-0.5326056, -0.8463636),
            float2(0.9644372, 0.2643122),
            float2(-0.8896863, 0.4565723),
            float2(0.3476168, -0.9376367),
            float2(0.3770427, 0.9261959),
            float2(-0.9036559, -0.4282594),
            float2(0.9556128, -0.2946256),
            float2(-0.5056224, 0.8627549),
            float2(-0.2099524, -0.9777116),
            float2(0.8152471, 0.5791133),
            float2(-0.9923232, 0.1236713),
            float2(0.6481695, -0.7614961),
            float2(0.03644322, 0.9993357),
            float2(-0.7019137, -0.712262),
            float2(0.9986954, 0.05106397),
            float2(-0.7709001, 0.6369561),
            float2(0.1381801, -0.9904071),
            float2(0.5671207, 0.8236347),
            float2(-0.9745344, -0.2242381),
            float2(0.870062, -0.4929423),
            float2(-0.3085789, 0.9511988),
            float2(-0.4149891, -0.9098264),
            float2(0.9205789, 0.3905566)
        };

        float SampleRawMainLightShadow(float4 shadowCoord)
        {
            return SAMPLE_TEXTURE2D_SHADOW(_MainLightShadowmapTexture, sampler_LinearClampCompare, shadowCoord.xyz);
        }

        float SampleMainLightShadowWithStrength(float4 shadowCoord)
        {
            if (BEYOND_SHADOW_FAR(shadowCoord))
                return 1.0;

            return LerpWhiteTo(SampleRawMainLightShadow(shadowCoord), GetMainLightShadowParams().x);
        }

        float2 RotateSample(float2 sample, float2 jitter)
        {
            return float2(sample.x * jitter.y + sample.y * jitter.x,
                          sample.x * -jitter.x + sample.y * jitter.y);
        }

        float2 SampleJitter(float2 positionCS)
        {
            float angle = InterleavedGradientNoise(positionCS, 0) * TWO_PI;
            return float2(sin(angle), cos(angle));
        }

        float RawShadowDepth(float2 uv)
        {
            return SAMPLE_TEXTURE2D_LOD(_MainLightShadowmapTexture, sampler_LinearClamp, uv, 0).r;
        }

        float EstimateBaseRadius(float receiverDepth)
        {
            float texelRadius = max(_MainLightShadowmapSize.x, _MainLightShadowmapSize.y) * max(1.0, _TsukuyomiPcssMinFilterSizeTexels);
            float blockerAngle = max(_TsukuyomiPcssBlockerSearchAngularDiameter, _TsukuyomiPcssAngularDiameter);
            float angularRadius = tan(0.5 * radians(blockerAngle)) * max(0.0001, _TsukuyomiPcssMaxSamplingDistance);
            float depthScale = UNITY_REVERSED_Z ? (1.0 - receiverDepth) : receiverDepth;
            return max(texelRadius, angularRadius * saturate(depthScale));
        }

        float FindAverageBlocker(float4 shadowCoord, float searchRadius, float2 jitter, int sampleCount)
        {
            float depthSum = 0.0;
            float depthCount = 0.0;
            float sampleCountInv = rcp((float)sampleCount);

            [loop]
            for (int i = 0; i < sampleCount && i < TSUKUYOMI_PCSS_DISK_SAMPLE_COUNT; ++i)
            {
                float sampleDist = (float)i * sampleCountInv;
                sampleDist = sampleDist * sampleDist * sampleDist;
                float2 uv = shadowCoord.xy + RotateSample(TsukuyomiPcssDisk[i], jitter) * sampleDist * searchRadius;

                if (any(uv < 0.0) || any(uv > 1.0))
                    continue;

                float sampleDepth = RawShadowDepth(uv);
                if (COMPARE_DEVICE_DEPTH_CLOSER(sampleDepth, shadowCoord.z))
                {
                    depthSum += sampleDepth;
                    depthCount += 1.0;
                }
            }

            return depthCount > 0.0 ? depthSum / depthCount : 0.0;
        }

        float FilterShadow(float4 shadowCoord, float filterRadius, float2 jitter, int sampleCount)
        {
            float sum = 0.0;
            float count = 0.0;
            float sampleCountInv = rcp((float)sampleCount);
            float sampleBias = 0.5 * sampleCountInv;

            [loop]
            for (int i = 0; i < sampleCount && i < TSUKUYOMI_PCSS_DISK_SAMPLE_COUNT; ++i)
            {
                float sampleDist = sqrt((float)i * sampleCountInv + sampleBias);
                float2 uv = shadowCoord.xy + RotateSample(TsukuyomiPcssDisk[i], jitter) * sampleDist * filterRadius;

                if (any(uv < 0.0) || any(uv > 1.0))
                    continue;

                sum += SAMPLE_TEXTURE2D_SHADOW(_MainLightShadowmapTexture, sampler_LinearClampCompare, float3(uv, shadowCoord.z));
                count += 1.0;
            }

            return count > 0.0 ? sum / count : 1.0;
        }

        float SampleMainLightPCSS(float4 shadowCoord, float2 positionCS)
        {
            if (BEYOND_SHADOW_FAR(shadowCoord))
                return 1.0;

            int blockerSamples = clamp((int)_TsukuyomiPcssFindBlockerSampleCount, 1, TSUKUYOMI_PCSS_DISK_SAMPLE_COUNT);
            int pcfSamples = clamp((int)_TsukuyomiPcssPcfSampleCount, 1, TSUKUYOMI_PCSS_DISK_SAMPLE_COUNT);
            float2 jitter = SampleJitter(positionCS);

            float searchRadius = EstimateBaseRadius(shadowCoord.z);
            float avgBlockerDepth = FindAverageBlocker(shadowCoord, searchRadius, jitter, blockerSamples);

            float texelRadius = max(_MainLightShadowmapSize.x, _MainLightShadowmapSize.y) * max(1.0, _TsukuyomiPcssMinFilterSizeTexels);
            float filterRadius = texelRadius;
            if (avgBlockerDepth > 0.0)
            {
                float blockerDistance = abs(shadowCoord.z - avgBlockerDepth);
                float penumbra = saturate(blockerDistance / max(0.0001, _TsukuyomiPcssMaxSamplingDistance));
                float angularFilter = tan(0.5 * radians(max(_TsukuyomiPcssMinFilterMaxAngularDiameter, _TsukuyomiPcssAngularDiameter)));
                filterRadius = max(texelRadius, penumbra * angularFilter * max(0.0, _TsukuyomiPcssMaxPenumbraSize));
            }

            return LerpWhiteTo(FilterShadow(shadowCoord, filterRadius, jitter, pcfSamples), GetMainLightShadowParams().x);
        }

        float4 LoadWorldAndShadow(float2 uv, out float deviceDepth)
        {
            deviceDepth = SampleSceneDepth(uv);
#if !UNITY_REVERSED_Z
            deviceDepth = deviceDepth * 2.0 - 1.0;
#endif
            float3 positionWS = ComputeWorldSpacePosition(uv, deviceDepth, unity_MatrixInvVP);
            return TransformWorldToShadowCoord(positionWS);
        }

        float PCSSPenumbraMaskFrag(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            float shadowAttenuation = 0.0;

            [unroll]
            for (int i = 0; i < 4; ++i)
            {
                float2 uv = input.texcoord.xy + TsukuyomiPenumbraOffsets[i] * _TsukuyomiColorAttachmentTexelSize.xy * 10.0;
                float sampleDepth;
                float4 shadowCoord = LoadWorldAndShadow(uv, sampleDepth);
                float realtimeShadow = SampleRawMainLightShadow(shadowCoord);

                shadowAttenuation += 0.25 * lerp(1.0, realtimeShadow, step(Eps_float(), sampleDepth));
            }

            return shadowAttenuation > 1.0 - Eps_float() ? 0.0 : 1.0;
        }

        float PCSSBlurHorizontalFrag(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            float texelSize = _TsukuyomiPenumbraMaskTex_TexelSize.x * 2.0;
            float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);

            float m0 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - float2(texelSize * 4.0, 0.0)).r;
            float m1 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - float2(texelSize * 3.0, 0.0)).r;
            float m2 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - float2(texelSize * 2.0, 0.0)).r;
            float m3 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - float2(texelSize * 1.0, 0.0)).r;
            float m4 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).r;
            float m5 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(texelSize * 1.0, 0.0)).r;
            float m6 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(texelSize * 2.0, 0.0)).r;
            float m7 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(texelSize * 3.0, 0.0)).r;
            float m8 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(texelSize * 4.0, 0.0)).r;

            return m0 * 0.01621622 + m1 * 0.05405405 + m2 * 0.12162162 + m3 * 0.19459459
                + m4 * 0.22702703
                + m5 * 0.19459459 + m6 * 0.12162162 + m7 * 0.05405405 + m8 * 0.01621622;
        }

        float PCSSBlurVerticalFrag(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            float texelSize = _TsukuyomiPenumbraMaskTex_TexelSize.y;
            float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);

            float m0 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - float2(0.0, texelSize * 3.23076923)).r;
            float m1 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - float2(0.0, texelSize * 1.38461538)).r;
            float m2 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).r;
            float m3 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(0.0, texelSize * 1.38461538)).r;
            float m4 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(0.0, texelSize * 3.23076923)).r;

            return m0 * 0.07027027 + m1 * 0.31621622
                + m2 * 0.22702703
                + m3 * 0.31621622 + m4 * 0.07027027;
        }

        half4 ScreenSpacePCSSFrag(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            float deviceDepth;
            float4 shadowCoord = LoadWorldAndShadow(input.texcoord.xy, deviceDepth);

            if (deviceDepth <= Eps_float())
                return half4(1.0, 1.0, 1.0, 1.0);

            float penumbraMask = SAMPLE_TEXTURE2D_X(_TsukuyomiPenumbraMaskTex, sampler_LinearClamp, UnityStereoTransformScreenSpaceTex(input.texcoord)).r;
            float attenuation = 1.0;
            if (_TsukuyomiEnableMainLightShadow > 0.5)
            {
                attenuation = (_TsukuyomiEnablePCSS > 0.5 && penumbraMask > Eps_float())
                    ? SampleMainLightPCSS(shadowCoord, input.positionCS.xy)
                    : SampleMainLightShadowWithStrength(shadowCoord);
            }

#ifdef _CONTACT_SHADOWS
            float contactShadow = 1.0 - LOAD_TEXTURE2D_X(_ContactShadowMap, input.positionCS.xy).r;
            attenuation = min(attenuation, contactShadow);
#endif

            return half4(attenuation, attenuation, attenuation, attenuation);
        }

        half4 ContactShadowCompositeFrag(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            float2 uv = UnityStereoTransformScreenSpaceTex(input.texcoord);
            float attenuation = (_TsukuyomiEnableMainLightShadow > 0.5)
                ? SAMPLE_TEXTURE2D_X(_TsukuyomiBaseScreenSpaceShadowmapTexture, sampler_PointClamp, uv).r
                : 1.0;

#ifdef _CONTACT_SHADOWS
            float contactShadow = 1.0 - LOAD_TEXTURE2D_X(_ContactShadowMap, input.positionCS.xy).r;
            attenuation = min(attenuation, contactShadow);
#endif

            return half4(attenuation, attenuation, attenuation, attenuation);
        }

        ENDHLSL

        Pass
        {
            Name "Tsukuyomi PCSS Penumbra Mask"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma vertex Vert
            #pragma fragment PCSSPenumbraMaskFrag
            ENDHLSL
        }

        Pass
        {
            Name "Tsukuyomi PCSS Penumbra Blur Horizontal"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment PCSSBlurHorizontalFrag
            ENDHLSL
        }

        Pass
        {
            Name "Tsukuyomi PCSS Penumbra Blur Vertical"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment PCSSBlurVerticalFrag
            ENDHLSL
        }

        Pass
        {
            Name "Tsukuyomi Screen Space PCSS Shadows"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma multi_compile _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _CONTACT_SHADOWS
            #pragma vertex Vert
            #pragma fragment ScreenSpacePCSSFrag
            ENDHLSL
        }
        Pass
        {
            Name "Tsukuyomi Contact Shadow Composite"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma multi_compile_fragment _ _CONTACT_SHADOWS
            #pragma vertex Vert
            #pragma fragment ContactShadowCompositeFrag
            ENDHLSL
        }
    }
}
