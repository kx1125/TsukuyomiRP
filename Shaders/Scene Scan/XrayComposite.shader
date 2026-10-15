Shader "Hidden/Tsukuyomi RP/XrayRender"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        Pass
        {
            Name "Xray Color Contribution"
            ZTest Always ZWrite Off Cull Off Blend Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/SceneScanCommon.hlsl"
            float4 _XrayPrimaryColor, _XraySecondaryColor;
            float _XrayIntensity;

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float rawDepth = SampleSceneDepth(uv);
                bool hasSurface = !IsSky(rawDepth);
                float2 target = TargetSample(uv);
                float outlinePixels = max(1.0, _CameraDepthTexture_TexelSize.w * (1.5 / 1080.0));
                float2 outlineStep = _CameraDepthTexture_TexelSize.xy * outlinePixels;
                float3 contribution = 0.0;
                if (target.x > 0.5 && target.y > 0.0)
                {
                    float3 targetWS = ComputeWorldSpacePosition(uv, DeviceDepth(RawDepthFromEye(target.y)), UNITY_MATRIX_I_VP);
                    float2 targetScan = ScanMasks(targetWS);
                    float targetEdge = max(max(abs(TargetSample(uv + float2(outlineStep.x, 0.0)).x - target.x),
                        abs(TargetSample(uv - float2(outlineStep.x, 0.0)).x - target.x)),
                        max(abs(TargetSample(uv + float2(0.0, outlineStep.y)).x - target.x),
                        abs(TargetSample(uv - float2(0.0, outlineStep.y)).x - target.x)));
                    targetEdge = saturate(targetEdge);
                    // Target eye depth is stored in half precision: tolerate its increasing quantization at range.
                    float depthBias = max(0.04, target.y * 0.001);
                    float blocked = hasSurface ? step(LinearEyeDepthConsiderProjection(rawDepth) + depthBias, target.y) : 0.0;
                    float3 targetColor = target.x > 1.5 ? _XraySecondaryColor.rgb : _XrayPrimaryColor.rgb;
                    contribution += targetColor * ((0.008 + blocked * 0.012 + targetEdge * 1.8) * targetScan.x
                        + (0.2 + targetEdge * 3.0) * targetScan.y);
                }

                return float4(contribution * _XrayIntensity, 0.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
