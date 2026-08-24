Shader "TsukuyomiRP/Particles/Particles_Decal"
{
    Properties
    {
        [HDR] _MainColor ("Main Color", Color) = (1,1,1,1)
        _MainTex ("Main Texture", 2D) = "white" {}
        _MainTex_MoveCenter ("Main Texture Move & Center", Vector) = (0,0,0.5,0.5)
        [KeywordEnum(Rotate_MainTex,Move_MainTex)] _MainTex_Rotator ("Main Texture UV Animation", Float) = 0
        _MainTex_Rota ("Main Texture Rotation Speed", Float) = 0
        [Toggle] _ClampMainUV ("Clamp Main UV", Float) = 0

        _NoiseTex ("Noise Texture", 2D) = "white" {}
        [Enum(R,0,G,1,B,2,A,3)] _NoiseTex_Channel ("Noise Channel", Float) = 0
        _NoiseTex_MoveCenter ("Noise Texture Move & Center", Vector) = (0,0,0.5,0.5)
        [KeywordEnum(Rotate_NoiseTex,Move_NoiseTex)] _NoiseTex_Rotator ("Noise Texture UV Animation", Float) = 0
        _NoiseTex_Rota ("Noise Texture Rotation Speed", Float) = 0
        [Toggle] _NoiseMask ("Noise Mask", Float) = 0
        _NoiseMaskInt ("Noise Mask Intensity", Range(0,1)) = 0
        [Toggle] _NoiseDistortion ("Noise Distortion", Float) = 0
        _NoiseDistortionInt ("Noise Distortion Intensity", Range(0,1)) = 0
        [Toggle] _NoiseDissolve ("Noise Dissolve", Float) = 0
        _NoiseDissolveInt ("Noise Dissolve Intensity", Range(0,1)) = 0

        _MaskTex ("Mask Texture", 2D) = "white" {}
        [Enum(R,0,G,1,B,2,A,3)] _MaskTex_Channel ("Mask Channel", Float) = 0
        _MaskTex_MoveCenter ("Mask Texture Move & Center", Vector) = (0,0,0.5,0.5)
        [KeywordEnum(Rotate_MaskTex,Move_MaskTex)] _MaskTex_Rotator ("Mask Texture UV Animation", Float) = 0
        _MaskTex_Rota ("Mask Texture Rotation Speed", Float) = 0
        _MaskPower ("Mask Power", Float) = 1

        _DistortionTex ("Distortion Texture", 2D) = "black" {}
        [Enum(R,0,G,1,B,2,A,3)] _DistortionTex_Channel ("Distortion Channel", Float) = 0
        _Distortion ("Distortion", Range(0,1)) = 0
        _DistortionTex_MoveCenter ("Distortion Texture Move & Center", Vector) = (0,0,0.5,0.5)
        [KeywordEnum(Rotate_DistortionTex,Move_DistortionTex)] _DistortionTex_Rotator ("Distortion Texture UV Animation", Float) = 0
        _DistortionTex_Rota ("Distortion Texture Rotation Speed", Float) = 0

        [Toggle(_USEDISSOLVE)] _UseDissolve ("Use Dissolve", Float) = 0
        _DissolveTex ("Dissolve Texture", 2D) = "white" {}
        [Enum(R,0,G,1,B,2,A,3)] _DissolveTex_Channel ("Dissolve Channel", Float) = 0
        _DissolveProgress ("Dissolve Progress", Range(0,1)) = 0
        [Toggle(_USECUSTOMDATA)] _UseCustomData ("Use Custom1.x", Float) = 0
        [HDR] _DissolveColor ("Dissolve Edge Color", Color) = (1,1,1,1)
        _DissolveRange ("Dissolve Edge Range", Range(0,1)) = 0.5
        _DissolveControl ("Dissolve Direction Strength", Range(0,3)) = 0.5
        [Toggle] _DissolveAlphaControl ("Dissolve Controls Alpha", Float) = 0
        [Toggle] _CenterDissolve ("Center Dissolve", Float) = 0
        _DissolveMoveSmooth ("Dissolve Edge Smoothness", Range(0,1)) = 1
        [Toggle] _DissolveFlip ("Flip Dissolve Progress", Float) = 0
        _DissolveAxisDir ("Dissolve Axis Direction", Vector) = (0,0,0,0)
        _DissolveTex_MoveCenter ("Dissolve Texture Move & Center", Vector) = (0,0,0.5,0.5)
        [KeywordEnum(Rotate_DissolveTex,Move_DissolveTex)] _DissolveTex_Rotator ("Dissolve Texture UV Animation", Float) = 0
        _DissolveTex_Rota ("Dissolve Texture Rotation Speed", Float) = 0

        [HDR] _GlowColor ("Emission Color", Color) = (0,0,0,0)
        _Glow ("Emission Intensity", Range(0,100)) = 0

        _AngleFadeStart ("Angle Fade Start", Range(0,89)) = 60
        _AngleFadeEnd ("Angle Fade End", Range(1,90)) = 90

        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilOp ("Stencil Pass Operation", Float) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilFailOp ("Stencil Fail Operation", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            Fail [_StencilFailOp]
        }

        Pass
        {
            Name "ParticleDecal"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Front

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vertParticleDecal
            #pragma fragment fragParticleDecal
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:ParticleInstancingSetup

            #pragma shader_feature_local _MAINTEX_ROTATOR_ROTATE_MAINTEX _MAINTEX_ROTATOR_MOVE_MAINTEX
            #pragma shader_feature_local _NOISETEX_ROTATOR_ROTATE_NOISETEX _NOISETEX_ROTATOR_MOVE_NOISETEX
            #pragma shader_feature_local _MASKTEX_ROTATOR_ROTATE_MASKTEX _MASKTEX_ROTATOR_MOVE_MASKTEX
            #pragma shader_feature_local _DISSOLVETEX_ROTATOR_ROTATE_DISSOLVETEX _DISSOLVETEX_ROTATOR_MOVE_DISSOLVETEX
            #pragma shader_feature_local _DISTORTIONTEX_ROTATOR_ROTATE_DISTORTIONTEX _DISTORTIONTEX_ROTATOR_MOVE_DISTORTIONTEX
            #pragma shader_feature_local _USEDISSOLVE
            #pragma shader_feature_local _USECUSTOMDATA

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // The field order must match the Particle System renderer's INSTANCED streams:
            // Position (transform), Color, AnimFrame, then Custom1.x.
            struct ParticleDecalInstanceData
            {
                float3x4 transform;
                uint color;
                float animFrame;
                float customData;
            };

            #define UNITY_PARTICLE_INSTANCE_DATA ParticleDecalInstanceData
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ParticlesInstancing.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _NoiseTex_ST;
                float4 _MaskTex_ST;
                float4 _DistortionTex_ST;
                float4 _DissolveTex_ST;

                float4 _MainTex_MoveCenter;
                float4 _NoiseTex_MoveCenter;
                float4 _MaskTex_MoveCenter;
                float4 _DistortionTex_MoveCenter;
                float4 _DissolveTex_MoveCenter;
                float4 _DissolveAxisDir;

                float _MainTex_Rota;
                float _NoiseTex_Rota;
                float _MaskTex_Rota;
                float _DistortionTex_Rota;
                float _DissolveTex_Rota;

                half4 _MainColor;
                half4 _DissolveColor;
                half4 _GlowColor;

                half _Distortion;
                half _DissolveFlip;
                half _CenterDissolve;
                half _DissolveMoveSmooth;
                half _DissolveControl;
                half _DissolveProgress;
                half _DissolveRange;
                half _DissolveAlphaControl;
                half _Glow;
                half _NoiseTex_Channel;
                half _MaskTex_Channel;
                half _DistortionTex_Channel;
                half _DissolveTex_Channel;
                half _MaskPower;
                half _ClampMainUV;
                half _NoiseMask;
                half _NoiseDistortion;
                half _NoiseDissolve;
                half _NoiseMaskInt;
                half _NoiseDistortionInt;
                half _NoiseDissolveInt;
                half _AngleFadeStart;
                half _AngleFadeEnd;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);
            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);
            TEXTURE2D(_DistortionTex);
            SAMPLER(sampler_DistortionTex);
            TEXTURE2D(_DissolveTex);
            SAMPLER(sampler_DissolveTex);

            struct AttributesParticleDecal
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsParticleDecal
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                nointerpolation half customData : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float2 RotateUV(float2 uv, float2 center, float angle)
            {
                float sine;
                float cosine;
                sincos(angle, sine, cosine);
                float2 centeredUV = uv - center;
                return mul(float2x2(cosine, -sine, sine, cosine), centeredUV) + center;
            }

            half SelectTextureChannel(half4 value, half channel)
            {
                if (channel < 0.5h) return value.r;
                if (channel < 1.5h) return value.g;
                if (channel < 2.5h) return value.b;
                return value.a;
            }

            float2 GetParticleSheetUV(float2 uv)
            {
                #if defined(UNITY_PARTICLE_INSTANCING_ENABLED)
                if (unity_ParticleUVShiftData.x != 0.0)
                {
                    ParticleDecalInstanceData data = unity_ParticleInstanceData[unity_InstanceID];
                    float index = floor(data.animFrame);
                    float tilesX = unity_ParticleUVShiftData.y;
                    float2 tileScale = unity_ParticleUVShiftData.zw;
                    float row = floor(index / tilesX);
                    float column = index - row * tilesX;
                    float2 offset = float2(
                        column * tileScale.x,
                        (1.0 - tileScale.y) - row * tileScale.y);
                    return uv * tileScale + offset;
                }
                #endif
                return uv;
            }

            VaryingsParticleDecal vertParticleDecal(AttributesParticleDecal input)
            {
                VaryingsParticleDecal output = (VaryingsParticleDecal)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                #if defined(UNITY_PARTICLE_INSTANCING_ENABLED)
                ParticleDecalInstanceData data = unity_ParticleInstanceData[unity_InstanceID];
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = lerp(half4(1, 1, 1, 1), input.color, unity_ParticleUseMeshColors);
                output.color *= half4(UnpackFromR8G8B8A8(data.color));
                output.customData = saturate(data.customData);
                #else
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = half4(1, 1, 1, 1);
                output.customData = 1;
                #endif

                return output;
            }

            half4 fragParticleDecal(VaryingsParticleDecal input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float rawDepth = SampleSceneDepth(screenUV);

                #if UNITY_REVERSED_Z
                clip(rawDepth - 0.00001);
                float deviceDepth = rawDepth;
                #else
                clip(0.99999 - rawDepth);
                float deviceDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif

                float3 positionWS = ComputeWorldSpacePosition(screenUV, deviceDepth, UNITY_MATRIX_I_VP);
                float3 positionOS = TransformWorldToObject(positionWS);
                float decalDepth = positionOS.z;
                float maxBoxDistance = max(abs(positionOS.x), max(abs(positionOS.y), abs(decalDepth)));
                clip(0.5001 - maxBoxDistance);

                float3 normalWS = SafeNormalize(cross(ddy(positionWS), ddx(positionWS)));
                // Local +Z is the decal normal/forward; projection travels along local -Z.
                float3 decalForwardWS = SafeNormalize(TransformObjectToWorldDir(float3(0, 0, 1)));
                half surfaceFacing = saturate(dot(normalWS, decalForwardWS));
                half startCos = cos(radians(_AngleFadeStart));
                half endCos = cos(radians(_AngleFadeEnd));
                half angleFade = saturate((surfaceFacing - endCos) / max(startCos - endCos, 0.0001h));
                clip(angleFade - 0.0001h);

                float2 decalUV = positionOS.xy + 0.5;

                float2 noiseUV = decalUV * _NoiseTex_ST.xy + _NoiseTex_ST.zw;
                #if defined(_NOISETEX_ROTATOR_ROTATE_NOISETEX)
                noiseUV = RotateUV(noiseUV, _NoiseTex_MoveCenter.zw, _NoiseTex_Rota * _Time.y);
                #elif defined(_NOISETEX_ROTATOR_MOVE_NOISETEX)
                noiseUV += _NoiseTex_MoveCenter.xy * _Time.y;
                #endif
                half noise = SelectTextureChannel(
                    SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV), _NoiseTex_Channel) * 2.0h - 1.0h;

                float2 maskUV = decalUV * _MaskTex_ST.xy + _MaskTex_ST.zw;
                if (_NoiseMask > 0.5h)
                    maskUV += noise * _NoiseMaskInt;
                #if defined(_MASKTEX_ROTATOR_ROTATE_MASKTEX)
                maskUV = RotateUV(maskUV, _MaskTex_MoveCenter.zw, _MaskTex_Rota * _Time.y);
                #elif defined(_MASKTEX_ROTATOR_MOVE_MASKTEX)
                maskUV += _MaskTex_MoveCenter.xy * _Time.y;
                #endif
                half mask = SelectTextureChannel(
                    SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, maskUV), _MaskTex_Channel);
                mask = pow(abs(mask), max(_MaskPower, 0.0001h));

                float2 distortionUV = decalUV * _DistortionTex_ST.xy + _DistortionTex_ST.zw;
                if (_NoiseDistortion > 0.5h)
                    distortionUV += noise * _NoiseDistortionInt;
                #if defined(_DISTORTIONTEX_ROTATOR_ROTATE_DISTORTIONTEX)
                distortionUV = RotateUV(distortionUV, _DistortionTex_MoveCenter.zw, _DistortionTex_Rota * _Time.y);
                #elif defined(_DISTORTIONTEX_ROTATOR_MOVE_DISTORTIONTEX)
                distortionUV += _DistortionTex_MoveCenter.xy * _Time.y;
                #endif
                half distortionSample = SelectTextureChannel(
                    SAMPLE_TEXTURE2D(_DistortionTex, sampler_DistortionTex, distortionUV), _DistortionTex_Channel);
                half distortion = (distortionSample * 2.0h - 1.0h) * _Distortion;

                half dissolveVisibility = 1.0h;
                half3 dissolveTint = _MainColor.rgb;
                #if defined(_USEDISSOLVE)
                half dissolveProgress = _DissolveProgress;
                #if defined(_USECUSTOMDATA)
                dissolveProgress = input.customData;
                #endif
                if (_DissolveFlip > 0.5h)
                    dissolveProgress = 1.0h - dissolveProgress;

                float2 dissolveUV = decalUV * _DissolveTex_ST.xy + _DissolveTex_ST.zw;
                if (_NoiseDissolve > 0.5h)
                    dissolveUV += noise * _NoiseDissolveInt;
                #if defined(_DISSOLVETEX_ROTATOR_ROTATE_DISSOLVETEX)
                dissolveUV = RotateUV(dissolveUV, _DissolveTex_MoveCenter.zw, _DissolveTex_Rota * _Time.y);
                #elif defined(_DISSOLVETEX_ROTATOR_MOVE_DISSOLVETEX)
                dissolveUV += _DissolveTex_MoveCenter.xy * _Time.y;
                #endif

                half dissolveSample = SelectTextureChannel(
                    SAMPLE_TEXTURE2D(_DissolveTex, sampler_DissolveTex, dissolveUV), _DissolveTex_Channel);

                float centerDistance = distance(decalUV, float2(0.5, 0.5));
                float centerFactor = centerDistance * (0.5 / 0.7071) + 0.5;
                if (_CenterDissolve > 0.5h)
                    dissolveSample = (dissolveSample + centerFactor * _DissolveControl) / (1.0h + _DissolveControl);

                float axisLength = length(_DissolveAxisDir.xy);
                if (axisLength > 0.0001)
                {
                    float directionFactor = dot(decalUV - 0.5, _DissolveAxisDir.xy / axisLength);
                    directionFactor = directionFactor * (0.5 / 0.7071) + 0.5;
                    dissolveSample = (dissolveSample + directionFactor * _DissolveControl) / (1.0h + _DissolveControl);
                }

                half threshold = dissolveProgress - 0.01h;
                half edgeStart = threshold - _DissolveRange;
                half edgeRange = max(_DissolveRange, 0.0001h);
                half edgeSmoothness = max(_DissolveMoveSmooth, 0.0001h);
                half edgeFactor = saturate((dissolveSample - edgeStart) / edgeRange);
                dissolveVisibility = smoothstep(edgeStart, edgeStart + edgeRange * edgeSmoothness, dissolveSample);
                half edgeMask = 1.0h - smoothstep(1.0h - edgeSmoothness, 1.0h, edgeFactor);
                dissolveTint = lerp(_MainColor.rgb, _DissolveColor.rgb, edgeMask);
                clip(dissolveSample - edgeStart);
                #endif

                float2 mainUV = GetParticleSheetUV(decalUV);
                mainUV = mainUV * _MainTex_ST.xy + _MainTex_ST.zw + distortion;
                #if defined(_MAINTEX_ROTATOR_ROTATE_MAINTEX)
                mainUV = RotateUV(mainUV, _MainTex_MoveCenter.zw, _MainTex_Rota * _Time.y);
                #elif defined(_MAINTEX_ROTATOR_MOVE_MAINTEX)
                mainUV += _MainTex_MoveCenter.xy * _Time.y;
                #endif
                if (_ClampMainUV > 0.5h)
                    mainUV = saturate(mainUV);

                half4 mainSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, mainUV);
                half customDataOpacity = 1.0h;
                #if defined(_USECUSTOMDATA)
                customDataOpacity = input.customData;
                #endif

                half alpha = mainSample.a * _MainColor.a * mask * input.color.a * angleFade * customDataOpacity;
                #if defined(_USEDISSOLVE)
                alpha *= lerp(1.0h, dissolveVisibility, _DissolveAlphaControl);
                #endif
                clip(alpha - 0.0001h);

                half3 color = mainSample.rgb * dissolveTint;
                color += _GlowColor.rgb * _Glow;
                color *= mask * dissolveVisibility * input.color.rgb;

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    CustomEditor "ParticlesDecalGUI"
}
