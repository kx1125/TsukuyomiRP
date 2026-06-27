Shader "Hidden/SSSSkin/SeparableBlur"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "SSSSkin Separable Blur"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D_X_FLOAT(_SSSSkinDepthTexture);
            TEXTURE2D_X(_SSSSkinNormalsTexture);
            TEXTURE2D_X(_SSSSkinMaskTexture);
            TEXTURE2D(NoiseTexture);
            SAMPLER(sampler_NoiseTexture);

            float4 _TexelOffsetScale;
            float4 _SSSSkinBlurColor;
            float _SSSSkinDepthTest;
            float _SSSSkinNormalTest;
            float _SSSSkinMaxDistance;
            float _SSSSkinRandomizedRotation;
            float _SSSSkinUseSharedDepthNormals;
            float DitherScale;
            float DitherIntensity;
            int _SSSSkinSampleCount;

            float3 Pow2(float3 value)
            {
                return value * value;
            }

            float2 RandN2(float2 pos, float2 random)
            {
                return frac(sin(dot(pos + random, float2(12.9898, 78.233))) * float2(43758.5453, 28001.8384));
            }

            float EdgeWeight(float centerDepth, float sampleDepth, float2 centerNormal, float2 sampleNormal, float sampleMask)
            {
                float depthPass = abs(sampleDepth - centerDepth) < max(_SSSSkinDepthTest, 0.00001);
                float normalXPass = abs(sampleNormal.x - centerNormal.x) < max(_SSSSkinNormalTest, 0.00001);
                float normalYPass = abs(sampleNormal.y - centerNormal.y) < max(_SSSSkinNormalTest, 0.00001);
                return depthPass * normalXPass * normalYPass * step(0.001, sampleMask);
            }

            float2 EncodeViewNormalStereoCompatible(float3 viewNormal)
            {
                const float kScale = 1.7777;
                float2 encodedNormal = viewNormal.xy / max(viewNormal.z + 1.0, 1e-4);
                encodedNormal /= kScale;
                return encodedNormal * 0.5 + 0.5;
            }

            float2 ComparableNormal(float3 normalWS)
            {
                float normalLength = length(normalWS);
                if (normalLength < 1e-4)
                    return 0.0.xx;

                normalWS /= normalLength;
                float3 normalVS = normalize(mul((float3x3)UNITY_MATRIX_V, normalWS));
                return EncodeViewNormalStereoCompatible(normalVS);
            }

            float2 SampleComparableNormal(float2 uv)
            {
                if (_SSSSkinUseSharedDepthNormals > 0.5)
                    return ComparableNormal(SampleSceneNormals(uv));

                float3 normalWS = SAMPLE_TEXTURE2D_X_LOD(_SSSSkinNormalsTexture, sampler_PointClamp, uv, 0).xyz;
                return ComparableNormal(normalWS);
            }

            float SampleLinearEyeDepth(float2 uv)
            {
                float rawDepth = _SSSSkinUseSharedDepthNormals > 0.5
                    ? SampleSceneDepth(uv)
                    : SAMPLE_TEXTURE2D_X_LOD(_SSSSkinDepthTexture, sampler_PointClamp, uv, 0).r;
                return LinearEyeDepth(rawDepth, _ZBufferParams);
            }

            float SampleSkinMask(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X_LOD(_SSSSkinMaskTexture, sampler_PointClamp, uv, 0).r;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord.xy;
                float4 centerColor = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0);
                float centerMask = SampleSkinMask(uv);
                if (centerMask <= 0.001)
                    return centerColor;

                float centerDepth = SampleLinearEyeDepth(uv);
                float2 centerNormal = SampleComparableNormal(uv);

                float2 scale = _TexelOffsetScale.xy * _BlitTexture_TexelSize.xy;
                scale *= 20.0 * rcp(max(centerDepth, 1e-4));

                float radiusCheck = abs(scale.x) + abs(scale.y);
                if (radiusCheck <= 0.001 || centerDepth >= _SSSSkinMaxDistance)
                    return centerColor;

                int sampleCount = max(_SSSSkinSampleCount, 2);
                float3 sssColor = max(_SSSSkinBlurColor.rgb, 1e-10);

                float3 blurred = 0.0;
                float3 weightSum = 0.0;

                [loop]
                for (int k = 0; k < sampleCount; k++)
                {
                    float stepValue = (float)k / sampleCount;
                    float2 offset = stepValue * scale;

                    if (_SSSSkinRandomizedRotation > 0.5)
                    {
                        float ditherScale = max(DitherScale, 0.0);
                        float ditherIntensity = saturate(DitherIntensity);
                        float2 random = RandN2(1.0.xx, SAMPLE_TEXTURE2D_LOD(NoiseTexture, sampler_NoiseTexture, uv + ditherScale.xx * _Time.xx, 0).xy);
                        float2 blueNoise = SAMPLE_TEXTURE2D_LOD(NoiseTexture, sampler_NoiseTexture, uv * ditherScale - random, 0).xy * 2.0 - 1.0;
                        float2 rotatedOffset = mul(offset, float2x2(blueNoise.x, blueNoise.y, -blueNoise.y, blueNoise.x));
                        offset = lerp(stepValue * scale, rotatedOffset, ditherIntensity);
                    }

                    float2 uvForward = saturate(uv + offset);
                    float2 uvBackward = saturate(uv - offset);

                    float2 normalForward = SampleComparableNormal(uvForward);
                    float2 normalBackward = SampleComparableNormal(uvBackward);
                    float depthForward = SampleLinearEyeDepth(uvForward);
                    float depthBackward = SampleLinearEyeDepth(uvBackward);
                    float maskForward = SampleSkinMask(uvForward);
                    float maskBackward = SampleSkinMask(uvBackward);

                    float edgeForward = EdgeWeight(centerDepth, depthForward, centerNormal, normalForward, maskForward);
                    float edgeBackward = EdgeWeight(centerDepth, depthBackward, centerNormal, normalBackward, maskBackward);

                    float3 weight = exp(-Pow2(stepValue / sssColor));
                    float3 weightForward = weight * edgeForward;
                    float3 weightBackward = weight * edgeBackward;

                    float3 colorForward = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uvForward, 0).rgb;
                    float3 colorBackward = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uvBackward, 0).rgb;

                    blurred += colorForward * weightForward * 0.5;
                    blurred += colorBackward * weightBackward * 0.5;
                    weightSum += (weightForward + weightBackward) * 0.5;
                }

                return half4(max(1e-6, blurred / max(weightSum, 1e-6)), centerColor.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
