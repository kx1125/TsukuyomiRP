#ifndef TSUKUYOMI_BILATERAL_FILTER_INCLUDED
#define TSUKUYOMI_BILATERAL_FILTER_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#ifdef _DEFERRED_RENDERING_PATH
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"
#endif
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

#ifndef _DEFERRED_RENDERING_PATH
    #define _DEFERRED_RENDERING_PATH 0
#endif

TEXTURE2D(_DepthTexture);

#if !_DEFERRED_RENDERING_PATH && defined(BILATERAL_ROUGHNESS)
    TEXTURE2D_HALF(_ForwardGBuffer);
#endif

float sqr(float value)
{
    return value * value;
}

float gaussian(float radius, float sigma)
{
    return exp(-sqr(radius / sigma));
}

#define NORMAL_WEIGHT 1.0
#define PLANE_WEIGHT 1.0
#define DEPTH_WEIGHT 1.0

struct BilateralData
{
    float3 position;
    float z01;
    float zNF;
    float3 normal;
#if defined(BILATERAL_ROUGHNESS)
    float roughness;
#endif
#if defined(BILATERLAL_UNLIT)
    bool isUnlit;
#endif
};

void GetNormalAndPerceptualRoughness(uint2 positionSS, out float3 N, out float perceptualRoughness)
{
#if _DEFERRED_RENDERING_PATH
    half4 gbuffer2 = LOAD_TEXTURE2D(_GBuffer2, positionSS);
    N = normalize(UnpackNormal(gbuffer2.xyz));
#else
    N = LoadSceneNormals(positionSS);
#endif

#ifdef BILATERAL_ROUGHNESS
    #if _DEFERRED_RENDERING_PATH
        float smoothness = gbuffer2.a;
    #else
        float4 gbuffer = LOAD_TEXTURE2D(_ForwardGBuffer, positionSS);
        float smoothness = gbuffer.r;
    #endif
    perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(smoothness);
#else
    perceptualRoughness = 0;
#endif
}

BilateralData TapBilateralData(uint2 coordSS)
{
    BilateralData key;
    PositionInputs posInput;

    if (DEPTH_WEIGHT > 0.0 || PLANE_WEIGHT > 0.0)
    {
        posInput.deviceDepth = LOAD_TEXTURE2D(_DepthTexture, coordSS).r;
        key.z01 = Linear01Depth(posInput.deviceDepth, _ZBufferParams);
        key.zNF = LinearEyeDepth(posInput.deviceDepth, _ZBufferParams);
    }

#if defined(BILATERLAL_UNLIT)
    key.isUnlit = false;
#endif

    if (PLANE_WEIGHT > 0.0)
    {
        posInput = GetPositionInput(coordSS, _ScreenSize.zw, posInput.deviceDepth, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);
        key.position = posInput.positionWS;
    }

    if (NORMAL_WEIGHT > 0.0 || PLANE_WEIGHT > 0.0)
    {
        float3 normal;
        float perceptualRoughness;
        GetNormalAndPerceptualRoughness(coordSS, normal, perceptualRoughness);
        key.normal = normal;
#ifdef BILATERAL_ROUGHNESS
        key.roughness = perceptualRoughness;
#endif
    }

    return key;
}

float ComputeBilateralWeight(BilateralData center, BilateralData tap)
{
    float depthWeight = 1.0;
    float normalWeight = 1.0;
    float planeWeight = 1.0;

    if (DEPTH_WEIGHT > 0.0)
    {
        depthWeight = max(0.0, 1.0 - abs(tap.z01 - center.z01) * DEPTH_WEIGHT);
    }

    if (NORMAL_WEIGHT > 0.0)
    {
        const float normalCloseness = sqr(sqr(max(0.0, dot(tap.normal, center.normal))));
        const float normalError = 1.0 - normalCloseness;
        normalWeight = max(0.0, 1.0 - normalError * NORMAL_WEIGHT);
    }

    if (PLANE_WEIGHT > 0.0)
    {
        const float3 dq = center.position - tap.position;
        const float distance2 = dot(dq, dq);
        const float planeError = max(abs(dot(dq, tap.normal)), abs(dot(dq, center.normal)));
        planeWeight = distance2 < 0.0001 ? 1.0 :
            pow(max(0.0, 1.0 - 2.0 * PLANE_WEIGHT * planeError / sqrt(distance2)), 2.0);
    }

    return depthWeight * normalWeight * planeWeight;
}

#endif
