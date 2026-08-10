#ifndef TSUKUYOMI_WATER_INPUT_INCLUDED
#define TSUKUYOMI_WATER_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

CBUFFER_START(UnityPerMaterial)
half4 _WaterColor;
half4 _WaterColorDeep;
half4 _Absorption;
half4 _ShadowCol;
half4 _WaveScroll;
half4 _GerstnerWaveA;
half4 _GerstnerWaveB;
half4 _GerstnerSpeed;
half4 _FoamColor;
half4 _FoamScroll;
half4 _CausticsCol;
half4 _CausticsScroll;
half _NormalScale;
half _Alpha;
half _DepthMaxDistance;
half _ScatteringStrength;
half _RefractionStrength;
half _RefractionDepth;
half _ReflectDistortion;
half _ReflectIndensity;
half _ReflectionRoughness;
half _FresnelF0;
half _FresnelRange;
half _FresnelFade;
half _SunSpecularIntensity;
half _SunSpecularPower;
half _WaveScale;
half _GerstnerSteepness;
half _FoamNoiseCutoff;
half _FoamNoiseSmooth;
half _FoamMaxDistance;
half _FoamMinDistance;
half _CrestFoamThreshold;
half _CrestFoamSmooth;
half _CrestFoamIntensity;
half _CausticsIndensity;
half _CausticsDistortion;
CBUFFER_END

TEXTURE2D(_WaterNormal); SAMPLER(sampler_WaterNormal);
TEXTURE2D(_FoamNoise); SAMPLER(sampler_FoamNoise);
TEXTURE2D(_CausticsTex); SAMPLER(sampler_CausticsTex);

#endif
