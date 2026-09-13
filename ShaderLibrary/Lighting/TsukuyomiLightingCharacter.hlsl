#ifndef TSUKUYOMI_LIGHTING_CHARACTER_INCLUDED
#define TSUKUYOMI_LIGHTING_CHARACTER_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonLighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/BRDF.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Debug/Debugging3D.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GlobalIllumination.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/AmbientOcclusion.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Lighting/TsukuyomiLightingData.hlsl"
#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Lighting/TsukuyomiEvaluateMaterial.hlsl"
#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Lighting/TsukuyomiBRDF.hlsl"
#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Lighting/TsukuyomiGlobalIllumination.hlsl"

static const float NEAR_ZERO_Y = 6.103515625e-05;

#ifndef TSUKUYOMI_EVALUATE_AO_MULTI_BOUNCE
    #define TSUKUYOMI_EVALUATE_AO_MULTI_BOUNCE 1
#endif

half3 TsukuyomiLightingLambert(half3 lightColor, half3 lightDir, half3 normal)
{
    half NdotL = saturate(dot(normal, lightDir));
    return lightColor * NdotL;
}

// _AnchorTRS is the supplied Anchor world-to-local matrix. Return its inverse linear part.
float3x3 CharacterFaceGetAnchorObjectToWorldMatrix()
{
    float3 row0 = _AnchorTRS[0].xyz;
    float3 row1 = _AnchorTRS[1].xyz;
    float3 row2 = _AnchorTRS[2].xyz;

    float3 cofactor0 = cross(row1, row2);
    float3 cofactor1 = cross(row2, row0);
    float3 cofactor2 = cross(row0, row1);
    float determinant = dot(row0, cofactor0);
    float safeDeterminant = (abs(determinant) < 1e-8)
        ? (determinant < 0.0 ? -1e-8 : 1e-8)
        : determinant;

    return transpose(float3x3(cofactor0, cofactor1, cofactor2)) / safeDeterminant;
}

float3x3 CharacterFaceGetAnchorWorldToObjectMatrix()
{
    return float3x3(
        _AnchorTRS[0].xyz,
        _AnchorTRS[1].xyz,
        _AnchorTRS[2].xyz);
}

float3 CharacterFaceGetAnchorPositionWS()
{
    float3 anchorTranslation = float3(_AnchorTRS[0].w, _AnchorTRS[1].w, _AnchorTRS[2].w);
    return -mul(CharacterFaceGetAnchorObjectToWorldMatrix(), anchorTranslation);
}

float3 CharacterFaceGetNormalWS(float3 normalWS, float3 positionWS, CharacterFaceData faceData)
{
    float3 anchorWS = CharacterFaceGetAnchorPositionWS();
    float3 nRadial = positionWS - anchorWS;
    nRadial.y = 0.0001;
    nRadial = normalize(nRadial);

    float3 normalFlat = normalize(float3(normalWS.x, 0.0001, normalWS.z));
    return normalize(lerp(nRadial, normalFlat, faceData.faceMask.g));
}

half3 TsukuyomiLightingCharacterFace(BRDFData brdfData,
    half3 lightColor, float3 lightDirectionWS,
    float lightAttenuation, half occlusion,
    float3 normalWS, float3 viewDirectionWS, float3 positionWS, half3 gi, CharacterFaceData faceData)
{
    half NdotL = dot(normalWS, lightDirectionWS);

    float NdotV = dot(normalWS, viewDirectionWS);

    float3x3 anchorWorldToObject = CharacterFaceGetAnchorWorldToObjectMatrix();

    //Fake Fresnel
    float3 cameraForwardWS = SafeNormalize(float3(
        UNITY_MATRIX_I_V[0].z,
        UNITY_MATRIX_I_V[1].z,
        UNITY_MATRIX_I_V[2].z));
    float3 cameraForwardLocal = mul(anchorWorldToObject, cameraForwardWS);
    float2 cameraForwardXZ = normalize(cameraForwardLocal.xz);
    float viewAlignFactor = cameraForwardXZ.y;
    float viewFrontMask = clamp(viewAlignFactor + 0.5, 0.0, 1.0);
    float fresnelMask = faceData.faceMask.r * lerp(viewFrontMask, 1.0, faceData.faceMask.g);
    float edgeInt = lerp(_FresnelStrengthMin, _FresnelStrengthMax, faceData.faceMask.b);
    float fresnelWeight = saturate(0.85 * (1.0 - saturate(NdotV)) * fresnelMask * edgeInt);

    half3 baseColor = lerp(brdfData.albedo, brdfData.albedo * _FresnelColor.rgb, fresnelWeight);

    //Normal
    float3 lightDirOS = mul(anchorWorldToObject, lightDirectionWS);
    float3 lightDirFlat = normalize(float3(lightDirOS.x, NEAR_ZERO_Y, lightDirOS.z));
    half isLightRight = half(lightDirFlat.x > 0.0);
    float2 sdfUV = faceData.uv;
    if (isLightRight < 0.5)
    {
        sdfUV.x = 1 - sdfUV.x;
    }
    half4 faceSDF = SAMPLE_TEXTURE2D(_FaceSDFMap, sampler_LinearClamp, sdfUV);
    float sdfXZ;
    if (isLightRight > 0.5)
    {
        sdfXZ = faceSDF.b * 2.0 - 1.0;
    }
    else
    {
        sdfXZ = 1.0 - faceSDF.b * 2.0;
    }
    float3 sdfNormal = normalize(float3(sdfXZ, NEAR_ZERO_Y, 1 - abs(sdfXZ)));
    float3 sdfNormalWS = SafeNormalize(mul(sdfNormal, anchorWorldToObject));
    float3 shadingNormal = normalize(lerp(sdfNormalWS, faceData.normalBlend, faceData.faceMask.g));

    //Lighting
    half isLookingBack = saturate(-dot(lightDirFlat.xz, float2(0.0, 1.0)));
    half isLightingBack = saturate(-lightDirFlat.z);
    float correction = isLookingBack * isLightingBack;
    float curveCenter = -lightDirFlat.z * (lightDirFlat.z * 0.5 - 1.0) + 0.5;
    float biasCenter = lerp(lightDirFlat.z, curveCenter, correction) * 0.5;

    float biasRange = clamp(0.5 - biasCenter, 0.001, 0.999);
    float sdfVal = (faceSDF.r + faceSDF.g) * 0.5;

    float stepLo = max(2.0 * biasRange - 1.0, 0.0);
    float stepHi = min(2.0 * biasRange, 1.0);
    float sdfShadow = smoothstep(stepLo, stepHi, sdfVal) + max(biasCenter, 0.0);
    sdfShadow = sdfShadow * 2.0 - 1.0;
    float physNoL = clamp(NdotL + _DiffuseOffset, -1.0, 1.0);
    sdfShadow = lerp(sdfShadow, physNoL, faceData.faceMask.g);
    half4 rampColor = SAMPLE_TEXTURE2D(_DiffuseRampMap, sampler_LinearClamp, float2(sdfShadow * 0.5 + 0.5, 0.5));
    float rampChroma = max(max(rampColor.r, rampColor.g), rampColor.b) - min(min(rampColor.r, rampColor.g), rampColor.b);

    float litMask = min(lightAttenuation,  rampColor.a);

    half3 shadowColor = _ShadowColor.rgb * baseColor;
    half3 tintAlbedo = lerp(shadowColor, baseColor, litMask);
    tintAlbedo = tintAlbedo * lerp(half3(1.0, 1.0, 1.0), rampColor.rgb, rampChroma);

    half3 specTint = gi * (lightAttenuation * 0.5 + 0.5);
    // For a world-to-local TRS, the third row is local +Z in world space, up to scale.
    float3 objForwardWS = SafeNormalize(_AnchorTRS[2].xyz);
    float3 fakeSunDir = float3(objForwardWS.x, lightDirectionWS.y, objForwardWS.z);
    float3 fakeHalfDir = normalize(lightDirectionWS + fakeSunDir * 2.0 + viewDirectionWS * 3);
    float NdotH = dot(shadingNormal, fakeHalfDir);
    float NdotV_Stylized = saturate(dot(shadingNormal, viewDirectionWS));

    float rough4 = brdfData.roughness2 * brdfData.roughness2;
    float NDF_Denom = (NdotH * rough4 - NdotH) * NdotH + 1.0;
    float D = rough4 / (NDF_Denom * NDF_Denom);
    float specInt = clamp(D * 0.5 / (2.0 * NdotV_Stylized + brdfData.roughness2 + 0.0001), 0.0, 20.0);
    float3 ggx = brdfData.specular * specInt * specTint;

    float3 viewDirLocal = mul(anchorWorldToObject, viewDirectionWS);
    float2 parallaxUV = faceData.uv + viewDirLocal.xy * _EmissiveParaInt;
    half3 emissive = SAMPLE_TEXTURE2D(_EmissiveMap, sampler_LinearClamp,parallaxUV).rgb * specTint;

    //Rim
    float3 rimLightDir = normalize(cross(objForwardWS, float3(_FakeRimLight.xy, 0.0)));
    float rimFresnel = 1.0 - abs(dot(rimLightDir, shadingNormal));
    half isSideView = smoothstep(0.9, 1.0, abs(viewAlignFactor));
    half rimFalloffA = smoothstep(_RimSmoothness.x, _RimSmoothness.y, rimFresnel) * isSideView;
    half rimFalloffB = max(isSideView, float(dot(objForwardWS, rimLightDir) < -0.01)) * faceData.faceMask.w;
    half rimFalloff = lerp(rimFalloffA, rimFalloffB, _FakeRimLight.z);
    half3 rimColor = rimFalloff * _RimColor.rgb * lightAttenuation * tintAlbedo;

    //return rimFresnel.xxx;
    return tintAlbedo + gi * tintAlbedo + ggx + emissive;
}

half3 TsukuyomiLightingCharacterFace(BRDFData brdfData, Light light, InputData inputData, SurfaceData surfaceData, CharacterFaceData faceData)
{
    return TsukuyomiLightingCharacterFace(brdfData,
        light.color, light.direction,
        light.distanceAttenuation * light.shadowAttenuation, surfaceData.occlusion,
        inputData.normalWS, inputData.viewDirectionWS, inputData.positionWS, inputData.bakedGI, faceData);
}

half4 TsukuyomiFragmentCharacterFace(InputData inputData, SurfaceData surfaceData, CharacterFaceData faceData)
{
    BRDFData brdfData;
    InitializeBRDFData(surfaceData, brdfData);

    half4 shadowMask = CalculateShadowMask(inputData);

    Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, shadowMask);

    half3 lightData = TsukuyomiLightingCharacterFace(brdfData, mainLight, inputData, surfaceData, faceData);

    //CharacterFaceData faceData;
    //MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI);

    return half4(lightData,1);
}

#endif
