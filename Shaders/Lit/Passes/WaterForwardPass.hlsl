#ifndef TSUKUYOMI_WATER_FORWARD_PASS_INCLUDED
#define TSUKUYOMI_WATER_FORWARD_PASS_INCLUDED

#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Material/TsukuyomiWaterInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/ProjectionUtils.hlsl"

TEXTURE2D(_TsukuyomiPlanarReflectionTexture);
SAMPLER(sampler_TsukuyomiPlanarReflectionTexture);
float4 _TsukuyomiPlanarReflectionTexelSize;
float _TsukuyomiPlanarReflectionEnabled;

struct WaterAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 texcoord : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct WaterVaryings
{
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    half3 normalWS : TEXCOORD2;
    half4 tangentWS : TEXCOORD3;
    float4 shadowCoord : TEXCOORD4;
    half fogCoord : TEXCOORD5;
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

void AccumulateGerstnerWave(float4 wave, float speed, float steepness, float time,
    inout float3 positionWS, inout float3 dPdx, inout float3 dPdz)
{
    float wavelength = max(wave.w, 0.01);
    float amplitude = wave.z;
    float2 direction = normalize(wave.xy + float2(1e-5, 0.0));
    float k = TWO_PI / wavelength;
    float phase = k * dot(direction, positionWS.xz) + speed * time;
    float sinPhase, cosPhase;
    sincos(phase, sinPhase, cosPhase);
    float qa = steepness * amplitude * 0.5;

    positionWS.xz += direction * qa * cosPhase;
    positionWS.y += amplitude * sinPhase;

    dPdx += float3(-qa * k * direction.x * direction.x * sinPhase, amplitude * k * direction.x * cosPhase, -qa * k * direction.x * direction.y * sinPhase);
    dPdz += float3(-qa * k * direction.x * direction.y * sinPhase, amplitude * k * direction.y * cosPhase, -qa * k * direction.y * direction.y * sinPhase);
}

WaterVaryings WaterForwardVertex(WaterAttributes input)
{
    WaterVaryings output = (WaterVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    float3 positionWS = vertexInput.positionWS;
    half3 normalWS = normalInput.normalWS;

#if defined(_WATER_VERTEX_WAVES)
    float3 dPdx = float3(1, 0, 0);
    float3 dPdz = float3(0, 0, 1);
    TsukuyomiAccumulateGerstnerWave(_GerstnerWaveA, _GerstnerSpeed.x, _GerstnerSteepness, _TimeParameters.x, positionWS, dPdx, dPdz);
    TsukuyomiAccumulateGerstnerWave(_GerstnerWaveB, _GerstnerSpeed.y, _GerstnerSteepness, _TimeParameters.x, positionWS, dPdx, dPdz);
    normalWS = normalize(cross(dPdz, dPdx));
#endif

    output.uv = input.texcoord;
    output.positionWS = positionWS;
    output.normalWS = normalWS;
    output.tangentWS = half4(normalInput.tangentWS.xyz, input.tangentOS.w * GetOddNegativeScale());
    output.shadowCoord = TransformWorldToShadowCoord(positionWS);
    output.fogCoord = ComputeFogFactor(TransformWorldToHClip(positionWS).z);
    output.positionCS = TransformWorldToHClip(positionWS);
    return output;
}

half3 WaterSampleReflection(float2 screenUv, half3 normalWS, half3 viewDirWS, float3 positionWS)
{
    half3 reflectVector = reflect(-viewDirWS, normalWS);
    half3 reflection = GlossyEnvironmentReflection(
        reflectVector, positionWS, _ReflectionRoughness, 1.0h, screenUv);
#if defined(_TSUKUYOMI_PLANAR_REFLECTION) && !defined(_ENVIRONMENTREFLECTIONS_OFF)
    if (_TsukuyomiPlanarReflectionEnabled > 0.5)
    {
        float maxDimension = max(_TsukuyomiPlanarReflectionTexelSize.z, _TsukuyomiPlanarReflectionTexelSize.w);
        half mip = PerceptualRoughnessToMipmapLevel(_ReflectionRoughness, (uint)floor(log2(max(maxDimension, 1.0))));
        //half2 normalVS = TransformWorldToViewDir(normalWS, true).xy;
        reflection = SAMPLE_TEXTURE2D_LOD(_TsukuyomiPlanarReflectionTexture, sampler_TsukuyomiPlanarReflectionTexture,
            saturate(screenUv + normalWS.xz * _ReflectDistortion), mip).rgb;
    }
#endif
    return reflection;
}

half4 WaterForwardFragment(WaterVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    float2 screenUv = GetNormalizedScreenSpaceUV(input.positionCS);
    float rawSceneDepth = SampleSceneDepth(screenUv);
    // Convert raw scene depth with the projection-aware helper; it handles UNITY_REVERSED_Z
    // and orthographic projection. SV_Position in the fragment stage is already window-space
    // data, so dividing its z by w would apply perspective division a second time. Derive the
    // water surface depth from view space instead; -viewZ is independent of depth-buffer direction.
    float sceneEyeDepth = LinearEyeDepthConsiderProjection(rawSceneDepth);
    float waterEyeDepth = -TransformWorldToView(input.positionWS).z;
    float thickness = max(sceneEyeDepth - waterEyeDepth, 0.0);
    half depth01 = saturate(thickness / max(_DepthMaxDistance, 0.001h));

    float2 waveUvA = input.uv * _WaveScroll.xy + _TimeParameters.x * _WaveScroll.zw;
    float2 waveUvB = input.uv * (_WaveScroll.xy * 1.73h) - _TimeParameters.x * (_WaveScroll.zw * 0.63h);
    half3 normalTS = normalize(UnpackNormalScale(SAMPLE_TEXTURE2D(_WaterNormal, sampler_WaterNormal, waveUvA), _NormalScale)
        + UnpackNormalScale(SAMPLE_TEXTURE2D(_WaterNormal, sampler_WaterNormal, waveUvB), _NormalScale));
    float tangentSign = input.tangentWS.w;
    half3 bitangentWS = tangentSign * cross(input.normalWS, input.tangentWS.xyz);
    half3 normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangentWS, input.normalWS)));
    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    half NoV = saturate(dot(normalWS, viewDirWS));

    half2 normalVS = TransformWorldToViewDir(normalWS, true).xy;
    half refractionFade = saturate(thickness / max(_RefractionDepth, 0.001h));
    half2 refractedUv = saturate(screenUv + normalVS * _RefractionStrength * refractionFade);
    half3 refractedScene = SampleSceneColor(refractedUv);
    half3 transmittance = exp(-max(_Absorption.rgb, 0.0h) * thickness);
    half3 waterTint = lerp(_WaterColor.rgb, _WaterColorDeep.rgb, depth01);
    half3 transmission = refractedScene * transmittance + waterTint * (1.0h - transmittance) * _ScatteringStrength;

    half3 reflection = WaterSampleReflection(screenUv, normalWS, viewDirWS, input.positionWS) * _ReflectIndensity;
    half fresnelBase = 1.0h - NoV;
    fresnelBase *= fresnelBase * fresnelBase * fresnelBase * fresnelBase;
    half fresnelFade = max(_FresnelFade, 0.001h);
    fresnelBase = smoothstep(_FresnelRange - fresnelFade, _FresnelRange + fresnelFade, fresnelBase);
    half fresnel = lerp(_FresnelF0, 1.0h, fresnelBase);
    half3 waterColor = lerp(transmission, reflection, saturate(fresnel));
    //return half4(waterColor, 1);

    Light mainLight = GetMainLight(input.shadowCoord);
#if !defined(_SPECULARHIGHLIGHTS_OFF)
    half3 halfDir = SafeNormalize(mainLight.direction + viewDirWS);
    half specular = pow(saturate(dot(normalWS, halfDir)), _SunSpecularPower) * _SunSpecularIntensity;
    waterColor += specular * mainLight.color * mainLight.shadowAttenuation;
#endif
    //waterColor *= lerp(_ShadowCol.rgb, 1.0, mainLight.shadowAttenuation);
    waterColor += waterTint * saturate(dot(normalWS, mainLight.direction)) * _WaveScale;

    half2 foamUv = input.uv * _FoamScroll.xy + _TimeParameters.x * _FoamScroll.zw;
    half foamNoise = SAMPLE_TEXTURE2D(_FoamNoise, sampler_FoamNoise, foamUv).r;
    half3 sceneNormal = SampleSceneNormals(screenUv);
    half foamDistance = lerp(_FoamMaxDistance, _FoamMinDistance, saturate(dot(TransformWorldToViewDir(input.normalWS, true), sceneNormal)));
    half shoreFoam = saturate(1.0h - thickness / max(foamDistance, 0.001h));
    half crestFoam = smoothstep(_CrestFoamThreshold - _CrestFoamSmooth, _CrestFoamThreshold + _CrestFoamSmooth, 1.0h - normalTS.z) * _CrestFoamIntensity;
    half foamMask = smoothstep(1.0h - _FoamNoiseSmooth, 1.0h, max(shoreFoam, crestFoam) * foamNoise * _FoamNoiseCutoff);
    waterColor = lerp(waterColor, _FoamColor.rgb, foamMask);

#if defined(_CAUSTICS_ON)
    float3 scenePositionWS = ComputeWorldSpacePosition(screenUv, rawSceneDepth, UNITY_MATRIX_I_VP);
    half2 causticsUvA = scenePositionWS.xz * _CausticsScroll.xy + normalWS.xz * _CausticsDistortion;
    half2 causticsUvB = scenePositionWS.xz * (_CausticsScroll.xy * 1.37h) - _TimeParameters.x * _CausticsScroll.zw;
    half caustics = min(SAMPLE_TEXTURE2D(_CausticsTex, sampler_CausticsTex, causticsUvA).r,
        SAMPLE_TEXTURE2D(_CausticsTex, sampler_CausticsTex, causticsUvB).r);
    waterColor += caustics * (1.0h - depth01) * _CausticsIndensity * _CausticsCol.rgb;
#endif

    return half4(MixFog(waterColor, input.fogCoord), 1.0h);
}

#endif
