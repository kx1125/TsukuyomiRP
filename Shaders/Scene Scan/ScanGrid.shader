Shader "Hidden/Tsukuyomi RP/Scene Scan/Grid"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }
        Pass
        {
            Name "Spatial Scan Grid"
            ZTest Always ZWrite Off Cull Off
            Blend SrcAlpha One, Zero One
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/ProjectionUtils.hlsl"

            TEXTURE2D_X(_ScanGridDepth);
            TEXTURE2D_X(_ScanGridExcluded);
            float4 _ScanGridAnchor; // world anchor, grid spacing
            float4 _ScanGridOrigin; // continuous player origin, normalized time
            float4 _ScanGridCurves; // overall blend, world progress, screen progress, reveal
            float4 _ScanGridStyle; // line width, dot size, strength, pulse speed
            float4 _ScanGridRange; // spatial radius, scan range, ring width, noise
            float4 _ScanGridColor;
            float _ScanGridCoverageMode; // 0: full coverage, 1: externally driven scan

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 axis : NORMAL;
                float2 corner : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 corner : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 centerWS : TEXCOORD2;
                float eyeDepth : TEXCOORD3;
                nointerpolation float isLine : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 SafeDirection(float3 v) { return v * rsqrt(max(dot(v, v), 0.000001)); }
            float Hash(float3 p) { return frac(sin(dot(p, float3(127.1, 311.7, 74.7))) * 43758.5453); }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 centerWS = input.positionOS * _ScanGridAnchor.w + _ScanGridAnchor.xyz;
                float3 rightWS = mul((float3x3)UNITY_MATRIX_I_V, float3(1.0, 0.0, 0.0));
                float3 upWS = mul((float3x3)UNITY_MATRIX_I_V, float3(0.0, 1.0, 0.0));
                float isLine = step(0.5, dot(input.axis, input.axis));
                float3 positionWS;
                if (isLine > 0.5)
                {
                    float3 side = cross(input.axis, SafeDirection(GetWorldSpaceViewDir(centerWS)));
                    // Keep the side vector stable when looking almost along a grid axis.
                    if (dot(side, side) < 0.00001)
                        side = cross(input.axis, abs(dot(input.axis, upWS)) < 0.9 ? upWS : rightWS);
                    positionWS = centerWS + input.axis * input.corner.x * _ScanGridAnchor.w * 0.5
                        + SafeDirection(side) * input.corner.y * _ScanGridStyle.x * 0.5;
                }
                else
                    positionWS = centerWS + (rightWS * input.corner.x + upWS * input.corner.y) * _ScanGridStyle.y * 0.5;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.centerWS = centerWS;
                output.corner = input.corner;
                output.eyeDepth = -TransformWorldToView(positionWS).z;
                output.isLine = isLine;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                float excluded = SAMPLE_TEXTURE2D_X(_ScanGridExcluded, sampler_PointClamp, uv).r;
                float sceneRawDepth = SAMPLE_TEXTURE2D_X(_ScanGridDepth, sampler_PointClamp, uv).r;
                float sceneDepth = LinearEyeDepthConsiderProjection(sceneRawDepth);
                float blocked = step(sceneDepth + max(0.02, sceneDepth * 0.0002), input.eyeDepth);
                float distanceToOrigin = distance(input.positionWS, _ScanGridOrigin.xyz);
                float spatial = 1.0 - smoothstep(max(0.0, _ScanGridRange.x - _ScanGridAnchor.w), _ScanGridRange.x, distanceToOrigin);
                float3 cell = floor(input.centerWS / _ScanGridAnchor.w + 0.5);
                float distanceWS = distance(cell * _ScanGridAnchor.w, _ScanGridOrigin.xyz)
                    + (Hash(cell) * 2.0 - 1.0) * _ScanGridRange.w;
                float radius = _ScanGridCurves.y * _ScanGridRange.y;
                float width = max(_ScanGridRange.z, 0.01);
                float inside = 1.0 - smoothstep(radius - width * 0.35, radius + width * 0.35, distanceWS);
                float ring = saturate(1.0 - abs(distanceWS - radius) / width)
                    * (1.0 - smoothstep(0.975, 1.0, _ScanGridOrigin.w));
                float started = smoothstep(0.0, 0.012, _ScanGridCurves.y);
                float pulse = pow(saturate(1.0 - abs(distanceToOrigin / _ScanGridRange.y
                    - frac(_Time.y * _ScanGridStyle.w * 0.125)) / 0.06), 2.0)
                    * smoothstep(0.8, 1.0, _ScanGridOrigin.w);
                float aspect = _ScreenParams.x / max(_ScreenParams.y, 1.0);
                float2 screen = (uv - 0.5) * float2(aspect, 1.0);
                float screenRadius = _ScanGridCurves.z * length(float2(aspect, 1.0)) * 0.58;
                float screenMask = 1.0 - smoothstep(screenRadius, screenRadius + 0.08, length(screen));
                if (_ScanGridCoverageMode < 0.5)
                {
                    inside = 1.0; ring = 0.0; started = 1.0; screenMask = 1.0;
                }
                float pointShape = 1.0 - smoothstep(0.65, 1.0, max(abs(input.corner.x), abs(input.corner.y)));
                float lineShape = (1.0 - smoothstep(0.4, 1.0, abs(input.corner.y)))
                    * (1.0 - smoothstep(0.97, 1.0, abs(input.corner.x)));
                float shape = lerp(pointShape, lineShape, input.isLine);
                float frontOpacity = 1.0;
                float backOpacity = lerp(0.1, 0.01, input.isLine);
                float alpha = lerp(frontOpacity, backOpacity, blocked) * shape * spatial * started
                    * saturate(inside + ring) * screenMask * _ScanGridCurves.x * (1.0 - saturate(excluded));
                // Reference instances: line RGB is 5% of dot RGB; blocked dots also dim emission to 1%.
                float baseColorScale = lerp(1.0, 0.05, input.isLine);
                float blockedEmission = lerp(0.01, 1.0, input.isLine);
                float boost = lerp(lerp(2.0, 5.0, input.isLine), lerp(5.0, 3.0, input.isLine), blocked);
                float emission = baseColorScale * lerp(1.0, blockedEmission, blocked)
                    * (1.0 + ring * (boost - 1.0) + pulse * 0.75) * _ScanGridStyle.z;
                return half4(_ScanGridColor.rgb * emission, saturate(alpha));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
