Shader "Universal Render Pipeline/Particles_Standard"
{
	Properties
	{
		[HDR] _MainColor ("MainColor", color) = (1,1,1,1)
		//_ColorIntensity ("ColorIntensity", Range(0, 5)) = 1
		_MainTex ("MainTex", 2D) = "white" {}
		_MainTex_MoveCenter ("MainTex_Move&Center", Vector) = (0,0,0,0)
		[KeywordEnum(Rotate_MainTex,Move_MainTex)] _MainTex_Rotator ("MainTex_Rotator", Float) = 0
		_MainTex_Rota ("MainTex_Rota", Float) = 0
		[Toggle] _UseCustomData ("Use CustomData", Float) = 0
		[Toggle] _ClampMainUV ("Clamp MainUV", Float) = 0
		_NoiseTex ("NoiseTex", 2D) = "white" {}
		[Enum(R,0,G,1,B,2,A,3)] _NoiseTex_Channel ("NoiseTex Channel", Float) = 0
		_NoiseTex_MoveCenter ("NoiseTex_Move&Center", Vector) = (0,0,0.5,0.5)
		[KeywordEnum(Rotate_NoiseTex,Move_NoiseTex)] _NoiseTex_Rotator ("NoiseTex_Rotator", Float) = 0
		_NoiseTex_Rota ("NoiseTex_Rota", Float) = 0
		[Toggle] _NoiseMask("Noise_Mask", float) = 0
		_NoiseMaskInt("Noise_Mask Intensity", range(0,1)) = 0
		[Toggle] _NoiseDistortion("Noise_Distortion", float) = 0
		_NoiseDistortionInt("Noise_Distortion Intensity", range(0,1)) = 0
		[Toggle] _NoiseDissolve("Noise_Dissolve", float) = 0
		_NoiseDissolveInt("Noise_Dissolve Intensity", range(0,1)) = 0
		[Toggle] _NoiseFresnel("Noise_Fresnel", float) = 0
		_NoiseFresnelInt("Noise_Fresnel Intensity", range(0,1)) = 0
		_MaskTex ("MaskTex", 2D) = "white" {}
		[Enum(R,0,G,1,B,2,A,3)] _MaskTex_Channel ("MaskTex Channel", Float) = 0
		_MaskTex_MoveCenter ("MaskTex_Move&Center", Vector) = (0,0,0.5,0.5)
		[KeywordEnum(Rotate_MaskTex,Move_MaskTex)] _MaskTex_Rotator ("MaskTex_Rotator", Float) = 0
		_MaskTex_Rota ("Mask_Rota", Float) = 0
		_MaskPower ("MaskPower", Float) = 1
		_DistortionTex ("DistortionTex", 2D) = "black" {}
		[Enum(R,0,G,1,B,2,A,3)] _DistortionTex_Channel ("DistortionTex Channel", Float) = 0
		_Distortion ("Distortion", Range(0, 1)) = 0
		_DistortionTex_MoveCenter ("DistortionTex_Move&Center", Vector) = (0,0,0.5,0.5)
		[KeywordEnum(Rotate_DistortionTex,Move_DistortionTex)] _DistortionTex_Rotator ("DistortionTex_Rotator", Float) = 0
		_DistortionTex_Rota ("DistortionTex_Rota", Float) = 0
		//[Toggle(_USEDISSOLVE)] _UseDissolve ("Use Dissolve", Float) = 0
		_DissolveTex ("DissolveTex", 2D) = "white" {}
		[Enum(R,0,G,1,B,2,A,3)] _DissolveTex_Channel ("DissolveTex Channel", Float) = 0
		_DissolveProgress ("DissolveProgress", Range(0, 1)) = 0
		[HDR]_DissolveColor ("DissolveColor", color) = (1,1,1,1)
		_DissolveRange ("DissolveRange", Range(0, 1)) = 0.5
		_DissolveControl ("_DissolveControl", Range(0, 3)) = 0.5
		[Toggle] _DissolveAlphaControl ("_DissolveAlphaControl", Float) = 0
		[Toggle] _CenterDissolve ("Center Dissolve", Float) = 0
		_DissolveMoveSmooth ("Dissolve Move Smooth", Range(0,1)) = 1
		[Toggle] _DissolveFlip ("Dissolve Flip", Float) = 0
		_DissolveAxisDir ("Dissolve Axis Dir", Vector) = (0,0,0,0)
		_DissolveTex_MoveCenter ("DissolveTex_Move&Center", Vector) = (0,0,0.5,0.5)
		[KeywordEnum(Rotate_DissolveTex,Move_DissolveTex)] _DissolveTex_Rotator ("DistortionTex_Rotator", Float) = 0
		_DissolveTex_Rota ("DissolveTex_Rota", Float) = 0
		_GlowColor ("Glow Color", Color) = (0,0,0,0)
		_Glow ("Glow Color Intensity", Range(0, 100)) = 0

		[Toggle] _SoftParticlesEnabled ("Soft Particles", Float) = 0
		_SoftParticlesNearFadeDistance ("Soft Particles Near Fade", Float) = 0
		_SoftParticlesFarFadeDistance ("Soft Particles Far Fade", Float) = 1
		[HideInInspector] _SoftParticleFadeParams ("Soft Particle Fade Params", Vector) = (0,1,0,0)
		
		[Enum(Cull Off,0, Cull Front,1, Cull Back,2)] _CullMode ("Culling", Float) = 2
		[Enum(UnityEngine.Rendering.BlendMode)]_SrcBlend ("SrcBlend", Float) = 5
		[Enum(UnityEngine.Rendering.BlendMode)]_DstBlend ("DstBlend", Float) = 10

		[Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", float) = 0
		[Enum(UnityEngine.Rendering.StencilOp)] _StencilOp ("Stencil Pass Operation", Float) = 0
		[Enum(UnityEngine.Rendering.StencilOp)] _StencilFailOp ("Stencil Fail Operation", Float) = 0
		
		//[Toggle(_USEFRESNEL)] _Fresnel ("Use Fresnel", Float) = 0
		[Toggle] _FresnelAlphaControl("Fresnel Control Alpha", float) = 0
		_FresnelColor ("Fresnel Color", color) = (1,1,1,1)
		_FresnelRange ("Fresnel Range", Range(0,1)) = 0.5
		_FresnelSmooth ("Fresnel Smooth", Range(0,1)) = 0.1
		
		//[KeywordEnum(Additive,Blend,Opaque,Cutout,Transparent,Subtractive,Modulate,BlendZ)] _BlendMode ("Blend Mode", Float) = 1
		//[HideInInspector] _BlendOp ("__blendop", Float) = 0
		//[HideInInspector] _ZWrite ("ZWrite On", Float) = 0
		//_GlowGlobal ("Global Glow Intensity", Range(0, 10)) = 0.5
		//_PixelSize ("Pixel Size", Float) = 0
		//_BloomFactor ("Bloom Factor", Float) = 0
		//_ColorMask ("Color Mask", Float) = 14
		//_ZTest ("ZTest", Float) = 4
		//_WorldClipHeight ("WorldClipHeight", Float) = -1000000
		//_StencilWriteMask ("Stencil Write Mask", Float) = 255
		//_StencilReadMask ("Stencil Read Mask", Float) = 255
	}
	
	SubShader{
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
			Name "Particle"
			
			//BlendOp[_BlendOp]
			//Blend [_SrcBlend] [_DstBlend]
			Blend [_SrcBlend] [_DstBlend]
			ZWrite Off
            Cull[_CullMode]
            
			HLSLPROGRAM
            #pragma target 2.0
			#pragma vertex vertParticle
			#pragma fragment fragParticle
            
            #pragma multi_compile_instancing
            
            #pragma shader_feature_local _MAINTEX_ROTATOR_ROTATE_MAINTEX _MAINTEX_ROTATOR_MOVE_MAINTEX
            #pragma shader_feature_local _NOISETEX_ROTATOR_ROTATE_NOISETEX _NOISETEX_ROTATOR_MOVE_NOISETEX
            #pragma shader_feature_local _MASKTEX_ROTATOR_ROTATE_MASKTEX _MASKTEX_ROTATOR_MOVE_MASKTEX
            #pragma shader_feature_local _DISSOLVETEX_ROTATOR_ROTATE_DISSOLVETEX _DISSOLVETEX_ROTATOR_MOVE_DISSOLVETEX
            #pragma shader_feature_local _DISTORTIONTEX_ROTATOR_ROTATE_DISTORTIONTEX _DISTORTIONTEX_ROTATOR_MOVE_DISTORTIONTEX
            #pragma shader_feature_local _USEFRESNEL
            #pragma shader_feature_local _USEDISSOLVE
            #pragma shader_feature_local _USECUSTOMDATA
			#pragma shader_feature_local _SOFTPARTICLES_ON
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
			#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"

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
            half4 _FresnelColor;

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
            half _FresnelRange;
            half _FresnelSmooth;
            half _FresnelAlphaControl;
            half _ClampMainUV;
            half _NoiseMask;
			half _NoiseDistortion;
			half _NoiseDissolve;
			half _NoiseFresnel;
            half _NoiseMaskInt;
            half _NoiseDistortionInt;
            half _NoiseDissolveInt;
            half _NoiseFresnelInt;
			float4 _SoftParticleFadeParams;
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

			struct AttributesParticle
			{
			    float4 positionOS               : POSITION;
			    half4 color                     : COLOR;
            	float3 texcoords				: TEXCOORD0;
				#ifdef _USEFRESNEL
				float3 normal					: NORMAL;
				#endif
            	UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct VaryingsParticle
			{
			    float4 clipPos                  : SV_POSITION;
			    float2 texcoord                 : TEXCOORD0;
				
				#ifdef _USEFRESNEL
				float3 normal					: TEXCOORD1;
				float3 positionWS				: TEXCOORD2;
				#endif
				
			    half4 color                     : COLOR;

				#ifdef _SOFTPARTICLES_ON
				float4 projectedPosition        : TEXCOORD4;
				#endif

				#ifdef _USECUSTOMDATA
				half dissolveProgress			: TEXCOORD3;
				#endif
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

            float2 RotateUV(float2 uv, float2 center, float angle)
            {
	            float2x2 rotateMatrix = float2x2(cos(angle), -sin(angle), sin(angle), cos(angle));
            	uv -= center;
            	float2 rotatedUV = mul(rotateMatrix, uv) + center;
            	return rotatedUV;
            }

			half SelectTextureChannel(half4 value, half channel)
			{
				if (channel < 0.5h) return value.r;
				if (channel < 1.5h) return value.g;
				if (channel < 2.5h) return value.b;
				return value.a;
			}

			#if defined(_SOFTPARTICLES_ON)
			float SoftParticleFade(float4 projectedPosition)
			{
				float2 uv = UnityStereoTransformScreenSpaceTex(projectedPosition.xy / projectedPosition.w);
				#if defined(UNITY_PRETRANSFORM_TO_DISPLAY_ORIENTATION)
				uv = RemovePretransformRotation(uv);
				#endif
				uv = FoveatedRemapLinearToNonUniform(uv);

				// Match URP's particle shaders: normalized sampling remains valid when the
				// camera depth texture resolution differs from the final screen resolution.
				float rawDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_PointClamp, uv).r;
				float sceneDepth = (unity_OrthoParams.w == 0.0)
					? LinearEyeDepth(rawDepth, _ZBufferParams)
					: LinearDepthToEyeDepth(rawDepth);
				float particleDepth = LinearEyeDepth(projectedPosition.z / projectedPosition.w, _ZBufferParams);
				return saturate(_SoftParticleFadeParams.y * ((sceneDepth - _SoftParticleFadeParams.x) - particleDepth));
			}
			#endif

			VaryingsParticle vertParticle(AttributesParticle input)
			{
			    VaryingsParticle output = (VaryingsParticle)0;
            	UNITY_SETUP_INSTANCE_ID(input);
			    UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            	
				VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
				output.clipPos = vertexInput.positionCS;
				output.texcoord = input.texcoords.xy;
            	output.color = input.color;
				#ifdef _SOFTPARTICLES_ON
				output.projectedPosition = vertexInput.positionNDC;
				#endif
            	#ifdef _USEFRESNEL
            	output.normal = input.normal;
            	output.positionWS = vertexInput.positionWS;
            	#endif

            	#ifdef _USECUSTOMDATA
            	output.dissolveProgress = input.texcoords.z;
            	#endif
				
				return output;
			}
            

			half4 fragParticle(VaryingsParticle input) : SV_TARGET
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
				//NOISE
				float2 noiseTexUV = input.texcoord * _NoiseTex_ST.xy + _NoiseTex_ST.zw;
				#ifdef _NOISETEX_ROTATOR_ROTATE_NOISETEX
				noiseTexUV = RotateUV(noiseTexUV, _NoiseTex_MoveCenter.zw, _NoiseTex_Rota * _Time.y);
				#elif _NOISETEX_ROTATOR_MOVE_NOISETEX
				noiseTexUV += _NoiseTex_MoveCenter.xy * _Time.y;
				#endif
				half noiseTex_sample = SelectTextureChannel(
					SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseTexUV), _NoiseTex_Channel);
				noiseTex_sample = noiseTex_sample * 2 - 1;

				//MASK
				float2 maskTexUV = input.texcoord * _MaskTex_ST.xy + _MaskTex_ST.zw;
				maskTexUV = _NoiseMask > 0.5 ? maskTexUV + noiseTex_sample * _NoiseMaskInt : maskTexUV;
				#ifdef _MASKTEX_ROTATOR_ROTATE_MASKTEX
				maskTexUV = RotateUV(maskTexUV, _MaskTex_MoveCenter.zw, _MaskTex_Rota * _Time.y);
				#elif _MASKTEX_ROTATOR_MOVE_MASKTEX
				maskTexUV += _MaskTex_MoveCenter.xy * _Time.y;
				#endif
				half4 maskTex_sample = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, maskTexUV);
				half mask = SelectTextureChannel(maskTex_sample, _MaskTex_Channel);
				mask = pow(abs(mask), _MaskPower);

				//DISTORTION
				float2 distortionUV = input.texcoord * _DistortionTex_ST.xy + _DistortionTex_ST.zw;
				distortionUV = _NoiseDistortion > 0.5 ? distortionUV + noiseTex_sample * _NoiseDistortionInt : distortionUV;
				#ifdef _DISTORTIONTEX_ROTATOR_ROTATE_DISTORTIONTEX
				distortionUV = RotateUV(distortionUV, _DistortionTex_MoveCenter.zw, _DistortionTex_Rota * _Time.y);
				#elif _DISTORTIONTEX_ROTATOR_MOVE_DISTORTIONTEX
				distortionUV += _DistortionTex_MoveCenter.xy * _Time.y;
				#endif
				half distortionTex_sample = SelectTextureChannel(
					SAMPLE_TEXTURE2D(_DistortionTex, sampler_DistortionTex, distortionUV), _DistortionTex_Channel);
				half distortion = (distortionTex_sample * 2 - 1) * _Distortion;

				//DISSOLVE
				half4 dissolveColor = half4(1,1,1,1);
				half d = 1;
				#ifdef _USEDISSOLVE
				#ifdef _USECUSTOMDATA
				_DissolveProgress = input.dissolveProgress;
				#endif
				float2 dissolveUV = input.texcoord.xy * _DissolveTex_ST.xy + _DissolveTex_ST.zw;
				dissolveUV = _NoiseDissolve > 0.5 ? dissolveUV + noiseTex_sample * _NoiseDissolveInt : dissolveUV;
				#ifdef _DISSOLVETEX_ROTATOR_ROTATE_DISSOLVETEX
				dissolveUV = RotateUV(dissolveUV, _DissolveTex_MoveCenter.zw, _DissolveTex_Rota * _Time.y);
				#elif _DISSOLVETEX_ROTATOR_MOVE_DISSOLVETEX
				dissolveUV += _DissolveTex_MoveCenter.xy * _Time.y;
				#endif
				half dissolveTex_sample = SelectTextureChannel(
					SAMPLE_TEXTURE2D(_DissolveTex, sampler_DissolveTex, dissolveUV), _DissolveTex_Channel);
				_DissolveProgress = _DissolveFlip > 0.5 ? 1 - _DissolveProgress : _DissolveProgress;

				float centerDist = distance(input.texcoord, float2(0.5, 0.5));
				float centerFactor = (centerDist * 1 / 0.7071) * 0.5 + 0.5;
				
				dissolveTex_sample = _CenterDissolve > 0? (dissolveTex_sample + centerFactor * _DissolveControl) / (1 + _DissolveControl) : dissolveTex_sample;
				
				float dirFactor = dot(input.texcoord - 0.5, normalize(_DissolveAxisDir.xy));
				dirFactor *= 1 / 0.7071;
				dirFactor = dirFactor * 0.5 + 0.5;
				dissolveTex_sample = length(_DissolveAxisDir.xy) > 0? (dissolveTex_sample + dirFactor * _DissolveControl) / (1 + _DissolveControl) : dissolveTex_sample;
				half dissolveThreshold = _DissolveProgress - 0.01;
				half edgeStart = dissolveThreshold - _DissolveRange;
				half edgeRange = max(_DissolveRange, 0.0001);
				half edgeFactor = saturate((dissolveTex_sample - edgeStart) / edgeRange);
				half edgeSmoothness = max(_DissolveMoveSmooth, 0.0001);
				d = smoothstep(edgeStart, edgeStart + edgeRange * edgeSmoothness, dissolveTex_sample);
				half edgeMask = 1 - smoothstep(1.0 - edgeSmoothness, 1.0, edgeFactor);
				dissolveColor = lerp(_MainColor,_DissolveColor, edgeMask);
				clip(dissolveTex_sample - edgeStart);
				#endif
				

				//MAINTEX
				float2 mainTexUV = input.texcoord * _MainTex_ST.xy + _MainTex_ST.zw + distortion;
				#ifdef _MAINTEX_ROTATOR_ROTATE_MAINTEX
				mainTexUV = RotateUV(mainTexUV, _MainTex_MoveCenter.zw, _MainTex_Rota * _Time.y);
				#elif	_MAINTEX_ROTATOR_MOVE_MAINTEX
				mainTexUV += _MainTex_MoveCenter.xy * _Time.y;
				#endif
				
				
				mainTexUV = _ClampMainUV > 0.5 ? clamp(float2(0,0), float2(1,1), mainTexUV) : mainTexUV;
				
				
				half4 mainTex_sample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, mainTexUV);
				mainTex_sample.rgb *= _MainColor.rgb * dissolveColor.rgb;
				
				half4 finalColor = mainTex_sample * mask;
				finalColor.rgb += _GlowColor.rgb * _Glow;

				//FRESNEL
				#ifdef _USEFRESNEL
				float3 viewDir = normalize(_WorldSpaceCameraPos - input.positionWS);
				float3 normalWS = TransformObjectToWorldNormal(input.normal);
				half fresnel = 1 - dot(normalWS, viewDir);
				_FresnelRange = _NoiseFresnel > 0.5 ? _FresnelRange + noiseTex_sample * _NoiseFresnelInt : _FresnelRange;
				fresnel = smoothstep(_FresnelRange - _FresnelSmooth, _FresnelRange + _FresnelSmooth, fresnel);
				finalColor.rgb = lerp(finalColor.rgb, _FresnelColor.rgb, fresnel);
				finalColor.a = _FresnelAlphaControl > 0.5? finalColor.a * fresnel : finalColor.a;
				#endif
				
				finalColor.a = _DissolveAlphaControl > 0.5 ? finalColor.a * d : finalColor.a;
				finalColor.a *= d;
				#ifdef _SOFTPARTICLES_ON
				finalColor.a *= SoftParticleFade(input.projectedPosition);
				#endif
				//return half4(mask,0,0,1);
				return finalColor * input.color;
			}

			ENDHLSL
		}
	}
	CustomEditor "ParticlesStandardGUI"
}
