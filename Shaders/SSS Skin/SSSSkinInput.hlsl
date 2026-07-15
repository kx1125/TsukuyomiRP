#ifndef SSS_SKIN_INPUT_INCLUDED
#define SSS_SKIN_INPUT_INCLUDED

#ifndef _SPECULAR_SETUP
    #define _SPECULAR_SETUP 1
#endif

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/BRDF.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GlobalIllumination.hlsl"

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);
TEXTURE2D(_OcclusionMap);
SAMPLER(sampler_OcclusionMap);
TEXTURE2D(_PBRMask);
SAMPLER(sampler_PBRMask);
TEXTURE2D(_BumpMap);
SAMPLER(sampler_BumpMap);
TEXTURE2D(_DetailNormalMap);
SAMPLER(sampler_DetailNormalMap);
TEXTURE2D(_TransmissionMap);
SAMPLER(sampler_TransmissionMap);
TEXTURE2D_X(_LightingTexBlurred);

CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
half4 _BaseColor;
half4 _OcclusionColor;
half4 _TransmissionColor;
half4 _SpecColor;
half _Cutoff;
half _BumpScale;
half _BumpTile;
half _DetailNormalMapScale;
half _DetailNormalMapTile;
half _Metallic;
half _Roughness;
half SSS_shader;
half DynamicPassTransmission;
half TransmissionShadows;
half TransmissionOcc;
half TransmissionRange;
half _MicroShadowOpacity;
half _RoughDiffuseStrength;
half _IndirectSpecularFGDStrength;
half _IndirectDiffuseIntensity;
half _IndirectSpecularIntensity;
half _HorizonOcclusionPower;
CBUFFER_END

struct SSSSkinAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct SSSSkinVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    half3 normalWS : TEXCOORD2;
    half4 tangentWS : TEXCOORD3;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

half Alpha(half albedoAlpha, half4 color, half cutoff)
{
    half alpha = albedoAlpha * color.a;
    return AlphaDiscard(alpha, cutoff);
}

half4 SampleAlbedoAlpha(float2 uv, TEXTURE2D_PARAM(albedoAlphaMap, sampler_albedoAlphaMap))
{
    return half4(SAMPLE_TEXTURE2D(albedoAlphaMap, sampler_albedoAlphaMap, uv));
}

half4 SampleAlbedo(float2 uv)
{
    return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
}

half4 SampleAlbedoTexture(float2 uv)
{
    return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
}

half3 SampleNormalWS(SSSSkinVaryings input)
{
    float2 baseNormalUV = input.uv * max(_BumpTile, 0.0001h);
    half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, baseNormalUV), _BumpScale);

#if defined(ENABLE_DETAIL_NORMALMAP)
    float2 detailNormalUV = input.uv * max(_DetailNormalMapTile, 0.0001h);
    half3 detailNormalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_DetailNormalMap, sampler_DetailNormalMap, detailNormalUV), _DetailNormalMapScale);
    normalTS = BlendNormalRNM(normalTS, detailNormalTS);
#endif

    half3 bitangentWS = cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w;
    return TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangentWS, input.normalWS));
}

half3 SampleSSSOcclusion(float2 uv)
{
    half occlusion = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv).r;
    return lerp(_OcclusionColor.rgb, half3(1.0h, 1.0h, 1.0h), occlusion);
}

half SampleOcclusion(float2 uv)
{
    return SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv).r;
}

half4 SamplePBRMask(float2 uv)
{
    return SAMPLE_TEXTURE2D(_PBRMask, sampler_PBRMask, uv);
}

half ResolveSkinMask(half4 pbrMask)
{
    return saturate(pbrMask.a);
}

half SSSSkinMaskRoughness(half4 pbrMask)
{
    return saturate(lerp(pbrMask.g, 1.0h, _Roughness));
}

half3 SampleLightingAlbedo(float2 uv)
{
    return _BaseColor.rgb * SampleSSSOcclusion(uv);
}

half3 SampleBlurredSSSLighting(float4 positionCS)
{
    float2 screenUV = GetNormalizedScreenSpaceUV(positionCS);
    return SAMPLE_TEXTURE2D_X_LOD(_LightingTexBlurred, sampler_LinearClamp, screenUV, 0).rgb;
}

half3 SampleTransmission(float2 uv)
{
    half occlusion = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv).r;
    return SAMPLE_TEXTURE2D(_TransmissionMap, sampler_TransmissionMap, uv).rgb * lerp(1.0h, occlusion, TransmissionOcc) * _TransmissionColor.rgb;
}

SurfaceData BuildSSSSkinSurfaceData(half4 albedo, half3 normalTS, half occlusion, half4 pbrMask)
{
    half metallic = saturate(_Metallic * pbrMask.b);
    half roughness = SSSSkinMaskRoughness(pbrMask);
    half3 dielectricSpecular = saturate(_SpecColor.rgb * pbrMask.r);

    SurfaceData surfaceData = (SurfaceData)0;
    surfaceData.albedo = albedo.rgb;
    surfaceData.specular = lerp(dielectricSpecular, albedo.rgb, metallic);
    surfaceData.metallic = 0.0h;
    surfaceData.smoothness = saturate(1.0h - roughness);
    surfaceData.normalTS = normalTS;
    surfaceData.emission = half3(0.0h, 0.0h, 0.0h);
    surfaceData.occlusion = occlusion;
    surfaceData.alpha = albedo.a;
    surfaceData.clearCoatMask = 0.0h;
    surfaceData.clearCoatSmoothness = 1.0h;
    return surfaceData;
}

SurfaceData BuildSSSSkinDiffuseOnlySurfaceData(half alpha, half occlusion)
{
    SurfaceData surfaceData = (SurfaceData)0;
    surfaceData.albedo = half3(1.0h, 1.0h, 1.0h);
    surfaceData.specular = half3(0.0h, 0.0h, 0.0h);
    surfaceData.metallic = 0.0h;
    surfaceData.smoothness = 0.0h;
    surfaceData.normalTS = half3(0.0h, 0.0h, 1.0h);
    surfaceData.emission = half3(0.0h, 0.0h, 0.0h);
    surfaceData.occlusion = occlusion;
    surfaceData.alpha = alpha;
    surfaceData.clearCoatMask = 0.0h;
    surfaceData.clearCoatSmoothness = 1.0h;
    return surfaceData;
}

InputData BuildSSSSkinInputData(SSSSkinVaryings input, half3 normalWS, half3 viewDirWS)
{
    InputData inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
    inputData.positionCS = input.positionCS;
    inputData.normalWS = normalWS;
    inputData.viewDirectionWS = viewDirWS;

#if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
#else
    inputData.shadowCoord = float4(0.0, 0.0, 0.0, 0.0);
#endif

    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    inputData.bakedGI = SampleSH(normalWS);
    inputData.shadowMask = half4(1.0h, 1.0h, 1.0h, 1.0h);
    inputData.vertexLighting = half3(0.0h, 0.0h, 0.0h);
    return inputData;
}

#endif
