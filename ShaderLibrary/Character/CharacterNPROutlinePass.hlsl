#ifndef TSUKUYOMI_CHARACTER_HAIR_OUTLINE_PASS_INCLUDED
#define TSUKUYOMI_CHARACTER_HAIR_OUTLINE_PASS_INCLUDED

#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Character/CharacterNPRLighting.hlsl"

struct TsukuyomiHairOutlineAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 texcoord : TEXCOORD0;
    float2 staticLightmapUV : TEXCOORD1;
    float2 dynamicLightmapUV : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct TsukuyomiHairOutlineVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 2);
#ifdef DYNAMICLIGHTMAP_ON
    float2 dynamicLightmapUV : TEXCOORD5;
#endif
#ifdef USE_APV_PROBE_OCCLUSION
    float4 probeOcclusion : TEXCOORD6;
#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

TsukuyomiHairOutlineVaryings TsukuyomiHairOutlineVertex(
    TsukuyomiHairOutlineAttributes input)
{
    TsukuyomiHairOutlineVaryings output = (TsukuyomiHairOutlineVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    // HGRP pass-1 b307 L422-L483. The source mesh uses a separate packed
    // smooth-normal stream. The target Hair mesh exposes only Position,
    // Normal, Tangent and TexCoord0, so the adapter maps that unavailable
    // stream to the authored standard normal without inventing a UV channel.
    float3 normalOS = input.normalOS;
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float4 positionCS = mul(_NonJitteredViewProjMatrix, float4(positionWS, 1.0));
    float3 normalWS = TransformObjectToWorldNormal(normalOS);
    float projectionReciprocal = -1.0 / UNITY_MATRIX_P[1].y;
    float projectionAbsolute = abs(projectionReciprocal);
    bool projectionBelowOne = projectionAbsolute < 1.0;
    float projectionRatio = projectionBelowOne
        ? projectionAbsolute : (1.0 / projectionAbsolute);
    float projectionRatioSquared = projectionRatio * projectionRatio;
    float projectionAtanApproximation = (1.0
        + ((-0.3018949925899505615234375
        + (0.087292902171611785888671875 * projectionRatioSquared))
        * projectionRatioSquared)) * projectionRatio;
    float projectionAngle = projectionBelowOne ? projectionAtanApproximation
        : (1.57079637050628662109375 - projectionAtanApproximation);
    projectionAngle = projectionReciprocal < 0.0 ? -projectionAngle : projectionAngle;

    float2 projectedNormal = normalize(mul(
        (float3x3)_NonJitteredViewProjMatrix, normalWS).xy)
        * float2(_ScreenParams.y / _ScreenParams.x, 1.0);
    float2 outlineOffset = projectedNormal
        * (_OutlineWidth
        * (0.3926990330219268798828125 / projectionAngle))
        * clamp((positionCS.w
        * (projectionAngle * 114.5915679931640625))
        * 0.039999999105930328369140625, 0.0, 1.0)
        * 0.004999999888241291046142578125;
    float2 minimumPixelOffset = (1.0 / _ScreenParams.xy)
        * clamp(positionCS.w, 0.0,
            1.57079613208770751953125 / projectionAngle);
    float2 signedMinimum = minimumPixelOffset * sign(outlineOffset);
    outlineOffset = float2(
        abs(outlineOffset.x) < minimumPixelOffset.x ? signedMinimum.x : outlineOffset.x,
        abs(outlineOffset.y) < minimumPixelOffset.y ? signedMinimum.y : outlineOffset.y);
    positionCS.xy += outlineOffset;

    if (unity_OrthoParams.w == 0.0)
    {
        float offsetViewDepth = (-positionCS.w)
            + (_OutlineOffsetZ * -0.100000001490116119384765625);
        positionCS.z = (((offsetViewDepth * UNITY_MATRIX_P[2].z)
            + UNITY_MATRIX_P[2].w) * positionCS.w) / (-offsetViewDepth);
    }
    else
    {
        positionCS.z += (_OutlineOffsetZ
            * -0.100000001490116119384765625) / _ProjectionParams.z;
    }
    output.positionCS = positionCS;
    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    output.positionWS = positionWS;
    OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST,
        output.staticLightmapUV);
#ifdef DYNAMICLIGHTMAP_ON
    output.dynamicLightmapUV = input.dynamicLightmapUV
        * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
#endif
    OUTPUT_SH4(positionWS, normalWS, GetWorldSpaceNormalizeViewDir(positionWS),
        output.vertexSH, output.probeOcclusion);
    return output;
}

void TsukuyomiHairOutlineInitializeBakedGI(
    TsukuyomiHairOutlineVaryings input,
    inout InputData inputData)
{
#if defined(DYNAMICLIGHTMAP_ON)
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV,
        input.dynamicLightmapUV, input.vertexSH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
#elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
    inputData.bakedGI = SAMPLE_GI(input.vertexSH,
        GetAbsolutePositionWS(inputData.positionWS), inputData.normalWS,
        inputData.viewDirectionWS, input.positionCS.xy,
        input.probeOcclusion, inputData.shadowMask);
#else
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV,
        input.vertexSH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
#endif
}

float4 TsukuyomiHairOutlineFragment(TsukuyomiHairOutlineVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    clip(_EnableOutline - 0.5);

    float4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_LinearRepeat, input.uv)
        * _BaseColor;
    float3 scaled = baseSample.xyz * _OutlineColorBrightness;
    float3 outlineColor = lerp(dot(scaled, TsukuyomiHairLuminanceWeights).xxx,
        scaled, _OutlineColorSaturation.xxx);
    float outlineAlpha = clamp(baseSample.w * 2.0, 0.0, 1.0);

    InputData inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
    inputData.positionCS = input.positionCS;
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    float4 sourcePositionCS = mul(_NonJitteredViewProjMatrix,
        float4(input.positionWS, 1.0));
    inputData.normalWS = TsukuyomiHairSampleSceneNormal(
        GetNormalizedScreenSpaceUV(sourcePositionCS));
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
#else
    inputData.shadowCoord = 0.0.xxxx;
#endif
    TsukuyomiHairOutlineInitializeBakedGI(input, inputData);
    float3 color = TsukuyomiHairOutlineFragmentLighting(inputData,
        outlineColor, outlineAlpha, inputData.normalWS);
    return float4(color, 1.0);
}

#endif
