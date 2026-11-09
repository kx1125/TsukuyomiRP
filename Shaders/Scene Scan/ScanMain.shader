Shader "Hidden/Tsukuyomi RP/Scene Scan/Main"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        Pass
        {
            Name "Scene Scan Color Composite"
            ZTest Always ZWrite Off Cull Off Blend Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_local_fragment _ _SCENE_OUTLINE _SCENE_OUTLINE_MASKED _SCENE_OUTLINE_TEMPORAL
            #include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/SceneScanCommon.hlsl"
            #include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/SceneOutlineResponse.hlsl"
            TEXTURE2D_X(_TransitionSampled);
            TEXTURE2D_X(_TransitionDistorted);
            TEXTURE2D_X(_TransitionScreenMask);
            TEXTURE2D_X(_XrayContribution);
            #if defined(_SCENE_OUTLINE) || defined(_SCENE_OUTLINE_MASKED) || defined(_SCENE_OUTLINE_TEMPORAL)
                #define HAS_SCENE_OUTLINE 1
                float4 _SceneOutlineColor;
                float4 _SceneOutlineParams; // UV step, relative depth threshold, normal threshold (-1 disables).
            #endif
            #if defined(_SCENE_OUTLINE_MASKED)
                TEXTURE2D_X(_SceneOutlineExcludedMask);
            #endif
            #if defined(_SCENE_OUTLINE_TEMPORAL)
                TEXTURE2D_X(_SceneOutlineCoverage);
                float2 _SceneOutlineJitter;
            #endif

            float2 CompareEdge(float2 uv, float eyeDepth, float3 normalWS, bool useNormals)
            {
                #if defined(_SCENE_OUTLINE_MASKED)
                    if (SAMPLE_TEXTURE2D_X(_SceneOutlineExcludedMask, sampler_PointClamp, uv).r > 0.5) return 0.0;
                #endif
                float depthDelta = abs(LinearEyeDepthConsiderProjection(SampleSceneDepth(uv)) - eyeDepth);
                float normalDelta = useNormals ? length(normalWS - SafeNormal(SampleSceneNormals(uv))) : 0.0;
                return float2(depthDelta, normalDelta);
            }

            float2 SceneEdgeDifferences(float2 uv, float2 offset, float eyeDepth, float3 normalWS, bool useNormals)
            {
                #if defined(_SCENE_OUTLINE_MASKED)
                    if (SAMPLE_TEXTURE2D_X(_SceneOutlineExcludedMask, sampler_PointClamp, uv).r > 0.5) return 0.0;
                #endif
                float2 horizontal = max(CompareEdge(saturate(uv + float2(offset.x, 0)), eyeDepth, normalWS, useNormals),
                                        CompareEdge(saturate(uv - float2(offset.x, 0)), eyeDepth, normalWS, useNormals));
                float2 vertical = max(CompareEdge(saturate(uv + float2(0, offset.y)), eyeDepth, normalWS, useNormals),
                                      CompareEdge(saturate(uv - float2(0, offset.y)), eyeDepth, normalWS, useNormals));
                return max(horizontal, vertical);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 original = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float rawDepth = SampleSceneDepth(uv);
                bool hasSurface = !IsSky(rawDepth);
                bool scan = _ScanModuleFlags.x > 0.5;
                bool opening = _ScanModuleFlags.y > 0.5;
                half4 source = opening ? SAMPLE_TEXTURE2D_X(_TransitionSampled, sampler_PointClamp, uv) : original;
                half3 outsideScan = opening ? SAMPLE_TEXTURE2D_X(_TransitionDistorted, sampler_PointClamp, uv).rgb : original.rgb;
                float transitionScreenMask = opening ? SAMPLE_TEXTURE2D_X(_TransitionScreenMask, sampler_PointClamp, uv).r : 1.0;
                float excluded = SAMPLE_TEXTURE2D_X(_ScanExcludedMask, sampler_PointClamp, uv).r;
                float2 target = TargetSample(uv);
                // Keep scene/target contours at 1.5 pixels at 1080p (3 pixels at 2160p).
                // Use the depth target's actual size so render scale does not change their screen-space width.
                float outlinePixels = max(1.0, _CameraDepthTexture_TexelSize.w * (1.5 / 1080.0));
                float2 outlineStep = _CameraDepthTexture_TexelSize.xy * outlinePixels;
                float screenMask = transitionScreenMask;
                float3 result = source.rgb;
                float2 masks = 0.0;
                float3 normalWS = float3(0.0, 1.0, 0.0);
                float3 positionWS = 0.0;
                if (hasSurface && scan)
                {
                    positionWS = ComputeWorldSpacePosition(uv, DeviceDepth(rawDepth), UNITY_MATRIX_I_VP);
                    masks = ScanMasks(positionWS);
                }
                bool needEdges = scan;
                bool useNormals = scan;
                #if defined(HAS_SCENE_OUTLINE)
                    needEdges = true;
                    useNormals = scan || _SceneOutlineParams.w >= 0.0;
                    float takeover = saturate(_ScanCurveParams.x * masks.x * screenMask * (1.0 - excluded));
                    outlineStep = lerp(max(_CameraDepthTexture_TexelSize.xy, _SceneOutlineParams.xy), outlineStep, takeover);
                    float baseAlpha = 0.0;
                #endif
                float outline = 0.0;
                #if defined(_SCENE_OUTLINE_TEMPORAL)
                float3 stable = SAMPLE_TEXTURE2D_X(_SceneOutlineCoverage, sampler_PointClamp, uv).rgb;
                // Present converged pixel coverage in the jitter phase expected by the upscaler.
                float2 stableEdges = stable.xy;
                if (any(_SceneOutlineJitter))
                {
                    float2 phased = SAMPLE_TEXTURE2D_X(_SceneOutlineCoverage, sampler_LinearClamp, uv - _SceneOutlineJitter).rg;
                    stableEdges = lerp(stable.xy, phased, saturate((stable.z - 2.0) * .25));
                }
                baseAlpha = stableEdges.x * _SceneOutlineColor.a;
                outline = stableEdges.y;
                if (hasSurface && scan) normalWS = SafeNormal(SampleSceneNormals(uv));
                #else
                if (hasSurface && needEdges)
                {
                    if (useNormals) normalWS = SafeNormal(SampleSceneNormals(uv));
                    float depth = LinearEyeDepthConsiderProjection(rawDepth);
                    float2 delta = SceneEdgeDifferences(uv, outlineStep, depth, normalWS, useNormals);
                    outline = SceneOutlineScanStrength(delta, depth);
                    #if defined(HAS_SCENE_OUTLINE)
                        baseAlpha = SceneOutlineBaseCoverage(delta, depth, _SceneOutlineParams.zw) * _SceneOutlineColor.a;
                    #endif
                }
                #endif
                #if defined(HAS_SCENE_OUTLINE)
                    float3 baseColor = lerp(original.rgb, _SceneOutlineColor.rgb, baseAlpha);
                    if (!scan && !opening && _ScanModuleFlags.z < 0.5) return half4(baseColor, original.a);
                #else
                    float3 baseColor = original.rgb;
                #endif
                if (hasSurface && scan)
                {
                    float fresnel = pow(1.0 - saturate(dot(normalWS, SafeNormal(GetWorldSpaceViewDir(positionWS)))), 3.0);

                    float2 gridUV = GridUV(positionWS, normalWS);
                    float2 phase = frac(gridUV + 0.5);
                    float2 gridDistance = min(phase, 1.0 - phase);
                    float2 gridAA = max(fwidth(gridUV), 0.0001);
                    float2 lines = 1.0 - smoothstep(0.005, 0.005 + gridAA, gridDistance);
                    float dots = (1.0 - smoothstep(0.018, 0.018 + gridAA.x, gridDistance.x))
                        * (1.0 - smoothstep(0.018, 0.018 + gridAA.y, gridDistance.y));
                    float grid = max(lines.x, lines.y);
                    // A travelling world-space pulse keeps the held scan alive without moving the grid.
                    float pulseDistance = distance(positionWS, _ScanOriginProgress.xyz) / _ScanGridRange.y;
                    float pulsePhase = frac(_Time.y * _ScanGridFlash.w * 0.125);
                    float pulse = pow(saturate(1.0 - abs(pulseDistance - pulsePhase) / 0.06), 2.0);
                    pulse *= smoothstep(0.8, 1.0, _ScanOriginProgress.w);
                    float basic = _ScanGridSettings.y;
                    float grayscale = dot(source.rgb, float3(0.2126, 0.7152, 0.0722));
                    float3 desaturated = lerp(source.rgb, grayscale.xxx, _ScanStyleParams.x);
                    float3 tinted = desaturated * lerp(1.0.xxx, _ScanTintColor.rgb, _ScanStyleParams.y);
                    result = lerp(source.rgb, tinted, masks.x * basic);

                    // MI_PP_ScanScene multiplies alpha for outline/fresnel/dots, but not WorldGrid.
                    // BasicIntensity blends the base scene style; the separately added grid keeps its own intensity.
                    float steadyGlow = (outline * _ScanStyleParams.z * 0.10
                        + fresnel * _ScanStyleParams.w * 0.05) * basic
                        + _ScanGridFlash.x * (grid * 0.20 + dots * 0.25);
                    float boostGlow = (outline * _ScanStyleParams.z * 3.5
                        + fresnel * _ScanStyleParams.w * 0.35) * basic
                        + _ScanGridFlash.x * dots * 14.0;
                    float glow = steadyGlow * masks.x + boostGlow * masks.y;
                    glow += (outline * _ScanStyleParams.z * 0.1 + fresnel * _ScanStyleParams.w * 0.5
                        + dots * _ScanGridFlash.x) * pulse * masks.x;
                    result += _ScanDebugColor.rgb * glow;

                    if (_ScanGridFlash.z > 0.5 && _ScanGridFlash.z < 1.5)
                    {
                        float3 cell = floor(positionWS / _ScanGridRange.x + 0.5);
                        float checker = frac((cell.x + cell.y + cell.z) * 0.5) >= 0.5;
                        result = lerp(source.rgb, _ScanDebugColor.rgb * lerp(0.15, 0.7, checker), masks.x);
                    }
                }

                if (_ScanGridFlash.z > 1.5 && _ScanGridFlash.z < 2.5) return half4(masks, 0.0, 1.0);
                if (_ScanGridFlash.z > 2.5 && _ScanGridFlash.z < 3.5) return half4(normalWS * 0.5 + 0.5, 1.0);
                if (_ScanGridFlash.z > 3.5 && _ScanGridFlash.z < 4.5)
                    return half4(target.x == 1.0, target.x == 2.0, excluded, 1.0);
                if (_ScanGridFlash.z > 4.5 && _ScanGridFlash.z < 5.5)
                    return half4((target.y / _ScanGridRange.y).xxx, 1.0);
                if (_ScanGridFlash.z > 5.5) return half4(excluded.xxx, 1.0);

                // UE's Scene Reveal is a constant color outside the world scan, inside the screen opening.
                // Its alpha curve brings the reveal from 3% to full color between t=.6 and t=1.
                float3 revealColor = _ScanDebugColor.rgb * lerp(0.03, 1.0, saturate(_ScanGridSettings.z));
                result += (revealColor - source.rgb) * (1.0 - masks.x) * (hasSurface && _ScanModuleFlags.x > 0.5 ? 1.0 : 0.0);

                #if defined(HAS_SCENE_OUTLINE)
                    // Replace the base line only where the scan has arrived. Screen opening
                    // and overall playback fade are applied below, once, to both styles.
                    result = lerp(result, _SceneOutlineColor.rgb, baseAlpha * (1.0 - masks.x));
                    outsideScan = lerp(outsideScan, _SceneOutlineColor.rgb, baseAlpha);
                #endif
                result += SAMPLE_TEXTURE2D_X(_XrayContribution, sampler_PointClamp, uv).rgb;

                // UE blends the distorted original scene into the voxel-sampled world scan.
                // Applying pixelation after this composite loses the scene detail under Reveal.
                result = lerp(outsideScan, result, screenMask);
                // Flash is added after the screen blend; the UE material reads its RGB and ignores its alpha.
                result += _ScanDebugColor.rgb * _ScanCurveParams.w * _ScanGridFlash.y;
                result = lerp(baseColor, result, saturate(_ScanCurveParams.x) * (1.0 - saturate(excluded)));
                return half4(result, original.a);
            }

            ENDHLSL
        }
    }
    Fallback Off
}
