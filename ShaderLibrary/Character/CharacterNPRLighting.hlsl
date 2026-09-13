#ifndef TSUKUYOMI_CHARACTER_HAIR_LIGHTING_INCLUDED
#define TSUKUYOMI_CHARACTER_HAIR_LIGHTING_INCLUDED

#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Character/CharacterNPRInput.hlsl"
#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Character/CharacterHairUrpAdapter.hlsl"

float3 TsukuyomiHairSafeNormalize(float3 value)
{
    return value * rsqrt(max(6.103515625e-05, dot(value, value)));
}

float3 TsukuyomiHairWorldToObjectBasis(float3 value)
{
    // HGRP b142 L480 uses row-vector multiplication by object-to-world.
    return mul(value, (float3x3)GetObjectToWorldMatrix());
}

float3 TsukuyomiHairReduceIndirectTint(float3 irradiance)
{
    // HGRP b142 L632-L647, after irradiance has received exposure.
    float b = irradiance.z;
    float g = irradiance.y;
    float4 p = lerp(float4(b, g, -1.0, 0.666666686534881591796875),
        float4(g, b, 0.0, -0.3333333432674407958984375), step(b, g).xxxx);
    float r = irradiance.x;
    float4 q = lerp(float4(p.x, p.y, p.w, r),
        float4(r, p.y, p.z, p.x), step(p.x, r).xxxx);
    float delta = q.x - min(q.w, q.y);
    float hue = frac(abs(q.z + ((q.w - q.y) /
        ((6.0 * delta) + 9.9999997473787516355514526367188e-05))));
    float saturation = min(delta /
        (q.x + 9.9999997473787516355514526367188e-05),
        lerp(0.699999988079071044921875, 0.3499999940395355224609375,
        smoothstep(0.449999988079071044921875, 0.3499999940395355224609375,
        abs(hue - 0.5))) * clamp(q.x, 0.0, 1.0));
    float value = 2.0 / (2.0 - saturation);
    return lerp(1.0.xxx,
        clamp(abs((frac(hue.xxx + float3(1.0,
        0.666666686534881591796875, 0.3333333432674407958984375)) * 6.0)
        - 3.0.xxx) - 1.0.xxx, 0.0.xxx, 1.0.xxx), saturation.xxx) * value;
}

float3 TsukuyomiHairAnisotropyDirection(
    float3 specularNormalWS,
    float3 tangentWS,
    float tangentSign,
    float metallic,
    float anisotropyValue,
    float strokeOffset)
{
    // HGRP b142 L478-L479 and L1053.
    float3 objectAnisotropy = TransformObjectToWorldDir(float3(_AnisotropyDirX, 1.0, 0.0), false);
    objectAnisotropy *= rsqrt(max(1.1754943508222875079687365372222e-38,
        dot(objectAnisotropy, objectAnisotropy)));
    float3 projectedObjectDirection = cross(specularNormalWS, objectAnisotropy);
    float3 selectedDirection = lerp(projectedObjectDirection, tangentWS, metallic.xxx);
    float3 baseDirection = cross(specularNormalWS, selectedDirection)
        * lerp(1.0, tangentSign, metallic);
    return normalize(baseDirection + (specularNormalWS
        * (((anisotropyValue * 2.0) - 1.0) + strokeOffset)));
}

float TsukuyomiHairEdgeFade(float3 specularNormalWS, float3 viewDirectionWS)
{
    // HGRP b142 L480-L481. The original compares object-space XZ projections.
    float3 normalOS = TsukuyomiHairWorldToObjectBasis(specularNormalWS);
    float3 viewOS = TsukuyomiHairWorldToObjectBasis(viewDirectionWS);
    float2 normalXZ = normalize(normalOS.xz);
    float2 viewXZ = normalize(viewOS.xz);
    return pow(clamp(dot(normalXZ, viewXZ), 0.0, 1.0), _AnisotropyEdgeFade);
}

float4 TsukuyomiHairSampleDiffuseRamp(float coordinate)
{
#if defined(_DIFF_RAMP_ON)
    return SAMPLE_TEXTURE2D_LOD(_DiffRampMap, sampler_LinearMirror,
        float2((clamp(coordinate, -1.0, 1.0) * 0.5) + 0.5, 0.5), 0.0);
#else
    float value = smoothstep(0.25, 1.0, clamp(coordinate, -1.0, 1.0));
    return value.xxxx;
#endif
}

float4 TsukuyomiHairSampleViewRamp(float coordinate)
{
#if defined(_DIFF_RAMP_ON)
    return SAMPLE_TEXTURE2D_LOD(_DiffRampMap, sampler_LinearMirror,
        float2((coordinate * 0.5) + 0.5, 0.5), 0.0);
#else
    float value = smoothstep(0.25, 1.0, coordinate);
    return value.xxxx;
#endif
}

float3 TsukuyomiHairEvaluatePrimaryShape(
    TsukuyomiHairSurface surface,
    float3 specularNormalWS,
    float3 tangentWS,
    float tangentSign,
    float3 halfDirectionWS,
    float edgeFade,
    out float primaryMaximum)
{
    // Primary lobe: HGRP b142 L1053-L1064. This deliberately is not a
    // Blinn-Phong/GGX substitute; the generated shader uses sin(theta)^200.
    float3 primaryDirection = TsukuyomiHairAnisotropyDirection(specularNormalWS,
        tangentWS, tangentSign, surface.metallic, _AnisotropyValue, surface.strokeOffset);
    float primaryDot = dot(primaryDirection, halfDirectionWS);
    float primarySin = max(sqrt(max(1.0 - (primaryDot * primaryDot), 0.0)),
        9.9999997473787516355514526367188e-05);

    float3 primaryShape;
#if defined(_SPEC_RAMP_ON)
    float3 specularInput = clamp(pow(primarySin, 200.0).xxx * surface.specular,
        0.0.xxx, 1.0.xxx);
    float rampRow = float(primaryDot > 0.0) * (edgeFade * edgeFade);
    primaryShape = specularInput
        * SAMPLE_TEXTURE2D_LOD(_SpecRampMap, sampler_LinearMirror,
            float2(specularInput.x, rampRow), 0.0).xyz
        * edgeFade;
#else
    primaryShape = pow(primarySin, 200.0).xxx * edgeFade;
#endif

    primaryMaximum = max(max(primaryShape.x, primaryShape.y), primaryShape.z);
    return primaryShape;
}

float3 TsukuyomiHairEvaluateMainAnisotropy(
    TsukuyomiHairSurface surface,
    float3 specularNormalWS,
    float3 tangentWS,
    float tangentSign,
    float3 halfDirectionWS,
    float edgeFade,
    float3 shadingMultiplier,
    out float primaryMaximum)
{
    float3 primaryShape = TsukuyomiHairEvaluatePrimaryShape(surface,
        specularNormalWS, tangentWS, tangentSign, halfDirectionWS,
        edgeFade, primaryMaximum);

    float3 f0 = 0.039999999105930328369140625.xxx * surface.specular;
    float3 primary = (primaryShape * f0) * _AnisotropyIntensity * 5.0;

    float3 secondary = 0.0.xxx;
#if defined(_METALLICSPECGLOSSMAP)
    float3 secondaryDirection = TsukuyomiHairAnisotropyDirection(specularNormalWS,
        tangentWS, tangentSign, surface.metallic, _AnisotropyValue2, surface.strokeOffset);
    float secondaryDot = dot(secondaryDirection, halfDirectionWS);
    float secondaryPower = float(int(200.0 * max(1.0 - _AnisotropyRange2, 0.0)));
    float secondarySin = max(sqrt(max(1.0 - (secondaryDot * secondaryDot), 0.0)),
        9.9999997473787516355514526367188e-05);
    secondary = pow(secondarySin, secondaryPower).xxx * edgeFade
        * (_AnisotropyColor2.xyz * surface.smoothness);
    secondary = lerp(secondary, 0.0.xxx, primaryMaximum.xxx);
#endif

    return ((primary + secondary) * shadingMultiplier) * _CharacterParams13.w;
}

float TsukuyomiHairEvaluateLine(
    TsukuyomiHairSurface surface,
    float2 uv,
    float3 specularNormalWS,
    float3 tangentWS,
    float tangentSign,
    float3 halfDirectionWS,
    float primaryMaximum)
{
#if defined(_SPECULAR_LINE)
    float3 lineDirection = TsukuyomiHairAnisotropyDirection(specularNormalWS,
        tangentWS, tangentSign, surface.metallic, _LineValue, 0.0);
    float lineDot = dot(lineDirection, halfDirectionWS);
    float linePower = float(int(200.0 * max(1.0 - _LineRange, 0.0)));
    float lineShape = clamp(pow(max(sqrt(max(1.0 - (lineDot * lineDot), 0.0)),
        9.9999997473787516355514526367188e-05), linePower), 0.0, 1.0);
    float procedural = ceil(clamp(frac(uv.x * _LineAmount) - 0.5, 0.0, 1.0));
    float lineMask = lerp(procedural, 1.0 - surface.lineMap, _UseLineMap);
    float lineBase = lerp(1.0 - _LineIntensity, 1.0, lineMask);
    float lineSuppressed = lerp(lineBase, 1.0, primaryMaximum);
    return lerp(1.0, lerp(1.0, lineSuppressed, lineShape), surface.specular);
#else
    return 1.0;
#endif
}

float3 TsukuyomiHairEvaluateMainLight(
    TsukuyomiHairLightInput light,
    TsukuyomiHairIndirectInput indirect,
    TsukuyomiHairSurface surface,
    float2 uv,
    float3 diffuseNormalWS,
    float3 specularNormalWS,
    float3 tangentWS,
    float tangentSign,
    float3 viewDirectionWS,
    float3 positionWS,
    float4 positionCS,
    float2 normalizedScreenSpaceUV)
{
    // Supported-scope material state after the excluded rain/snow branch:
    // HGRP b142 L997-L1007.
    float3 diffuseAlbedo = surface.baseColor * 0.959999978542327880859375;
    float3 shadowAlbedo = surface.shadowColor * 0.959999978542327880859375;

    float3 cameraForwardWS = normalize(mul((float3x3)UNITY_MATRIX_I_V,
        float3(0.0, 0.0, 1.0)));
    float3 lightDirection = lerp(light.direction, _CharacterParams11.xyz,
        _CharacterParams1.w.xxx);
    float3 horizontalLight = normalize(float3(lightDirection.x,
        6.103515625e-05, lightDirection.z));
    float horizontalView = clamp(-dot(horizontalLight.xz,
        normalize(cameraForwardWS.xz)), 0.0, 1.0);
    float perspectiveWeight = horizontalView
        * smoothstep(0.25, 0.75, 1.0 - abs(cameraForwardWS.y))
        * (1.0 - _CharacterParams12.x);
    float normalLight = dot(diffuseNormalWS, lightDirection);
    float warpedNormalLight = lerp(normalLight,
        (-normalLight) * ((normalLight * 0.5) - 1.0) + 0.5,
        perspectiveWeight) + (_CharacterParams11.w * _CharacterParams12.x);

    float4 diffuseRamp = TsukuyomiHairSampleDiffuseRamp(warpedNormalLight);
    float4 viewRamp = TsukuyomiHairSampleViewRamp(dot(diffuseNormalWS, cameraForwardWS));

    float characterShadow = light.characterShadow;
    float mainShadow = lerp(light.directionalShadow, 1.0, _CharacterParams1.z);
    float shadowMask = min(min(characterShadow, surface.shadowMask), diffuseRamp.w);
    float viewShadow = viewRamp.w * (surface.shadowMask * characterShadow);

    float3 normalRemap = ((clamp(dot(diffuseNormalWS, _CharacterParams6.xyz)
        + _CharacterParams7.x, 0.0, 1.0) * _CharacterParams7.y)
        + _CharacterParams7.z).xxx;

    float exposure = lerp(_EnvironmentGlobalParams0.x, 1.0, _CharacterParams12.w)
        * _ExposureWithMiscParams.x;
    float3 indirectIrradiance = indirect.irradiance * exposure;
    float3 indirectTint = TsukuyomiHairReduceIndirectTint(indirectIrradiance);
    float indirectIntensity = max(max(max(indirect.dominantIrradiance.x,
        indirect.dominantIrradiance.y), indirect.dominantIrradiance.z), 0.0)
        * exposure;
    if (_CharacterParams1.y >= 0.5)
    {
        indirect.dominantDirection = 0.0.xxxx;
        indirectIrradiance = 1.0.xxx;
        indirectTint = _CharacterParams2.xyz;
        indirectIntensity = exposure;
    }

    float3 shapedIndirect = normalRemap
        * lerp(indirectTint, 1.0.xxx, (_CharacterParams1.y * shadowMask).xxx);
    float3 selectedLightColor = lerp(light.color, _CharacterParams5.xyz,
        _CharacterParams12.y.xxx);
    float3 lightColor = selectedLightColor
        * lerp(light.directionalIntensityScale, 1.0, _CharacterParams12.w);

    float indirectScaleA = min(lerp(0.64999997615814208984375, 1.0,
        indirectIntensity), 1.5);
    float indirectScaleB = clamp(indirectIntensity, 1.25, 1.75);
    float3 indirectSide = shapedIndirect
        * lerp(indirectScaleA, indirectScaleB, _CharacterParams1.x)
        * _CharacterParams0.w;
    float3 directSide = (lerp(dot(lightColor, TsukuyomiHairLuminanceWeights).xxx,
        lightColor, shadowMask.xxx)
        + ((shapedIndirect * clamp(indirectIntensity, 0.0, 1.5))
        * ((1.0 - _CharacterParams12.y).xxx + (selectedLightColor
        * _CharacterParams12.y)))) * _CharacterParams0.y;
    float3 lightFactor = lerp(indirectSide, directSide, mainShadow.xxx);

    float3 scaledShadow = shadowAlbedo * _CharacterParams0.z;
    float3 darkShadow = scaledShadow * 0.64999997615814208984375;
    float diffuseRampDelta = max(max(diffuseRamp.x, diffuseRamp.y), diffuseRamp.z)
        - min(min(diffuseRamp.x, diffuseRamp.y), diffuseRamp.z);
    float albedoLuminance = dot(diffuseAlbedo, TsukuyomiHairLuminanceWeights);
    float3 rampBase = lerp(
        lerp(lerp(dot(darkShadow, TsukuyomiHairLuminanceWeights).xxx,
            darkShadow, 1.2000000476837158203125.xxx), scaledShadow,
            clamp((surface.shadowMask * characterShadow * viewRamp.w)
            + diffuseRamp.w, 0.0, 1.0).xxx),
        diffuseAlbedo, shadowMask.xxx);
    float3 rampColored = rampBase
        * ((1.0 - diffuseRampDelta).xxx + (diffuseRamp.xyz * diffuseRampDelta));
    float3 lightColorMix = lerp(
        lerp(scaledShadow,
            lerp(albedoLuminance.xxx, diffuseAlbedo,
                1.2000000476837158203125.xxx), viewShadow.xxx),
        rampColored * clamp(dot(rampBase, TsukuyomiHairLuminanceWeights)
            / max(dot(rampColored, TsukuyomiHairLuminanceWeights),
                0.001000000047497451305389404296875), 0.0, 1.5),
        mainShadow.xxx);

    float combinedShadow = lerp(viewShadow, shadowMask, mainShadow);
    float3 shadingMultiplier = lightFactor
        * ((((combinedShadow * 0.5) + 0.5)
        * lerp(_CharacterParams0.z, 1.0, combinedShadow)));

    float3 viewOS = TsukuyomiHairWorldToObjectBasis(viewDirectionWS);
    float modifiedY = lerp(0.5, lightDirection.y, mainShadow);
    float3 modifiedViewWS = TransformObjectToWorldDir(
        float3(viewOS.x, modifiedY, viewOS.z), false);
    float3 halfSum = normalize((lightDirection * mainShadow)
        + (modifiedViewWS * 2.0)) + viewDirectionWS;
    float3 halfDirection = TsukuyomiHairSafeNormalize(halfSum);
    float edgeFade = TsukuyomiHairEdgeFade(specularNormalWS, viewDirectionWS);
    float primaryMaximum;
    float3 anisotropy = TsukuyomiHairEvaluateMainAnisotropy(surface,
        specularNormalWS, tangentWS, tangentSign, halfDirection,
        edgeFade, shadingMultiplier, primaryMaximum);
    float lineValue = TsukuyomiHairEvaluateLine(surface, uv, specularNormalWS,
        tangentWS, tangentSign, halfDirection, primaryMaximum);
    float alphaPremultiply = (1.0 - _AlphaPremultiply)
        + (surface.alpha * _AlphaPremultiply);
    float3 baseLighting = (lightFactor * lightColorMix) * lineValue;
    baseLighting = lerp(dot(baseLighting, TsukuyomiHairLuminanceWeights).xxx,
        baseLighting, lerp(_LineSaturation, 1.0, lineValue).xxx);
    float3 color = (baseLighting * alphaPremultiply) + anisotropy;

    // HGRP b142 L1072-L1081 highlight contrast and the two rim terms.
    float luminance = dot(color, TsukuyomiHairLuminanceWeights);
    float highlight = clamp(luminance - 0.5, 0.0, 0.5);
    color = lerp(luminance.xxx, color, ((highlight * highlight) + 1.0).xxx);

    float3 screenDirection = lerp(float3(_CharacterParams9.xy, 0.0),
        (UNITY_MATRIX_V[0].xyz * _CharacterParams9.x)
        + (UNITY_MATRIX_V[1].xyz * _CharacterParams9.y),
        _CharacterParams15.w.xxx);
    float3 screenRimDirection = normalize(cross(cameraForwardWS, screenDirection));
    float2 screenNormal = normalize(mul((float3x3)UNITY_MATRIX_V,
        diffuseNormalWS).xy) * float2(_ScreenParams.y / _ScreenParams.x, 1.0);
    float2 screenMinimum = _ScreenParams.zw - 1.0.xx;
    float2 screenMaximum = 2.0.xx - _ScreenParams.zw;
    float2 depthUv = clamp(normalizedScreenSpaceUV
        + ((screenNormal * _CharacterParams9.w)
        * 0.006000000052154064178466796875), screenMinimum, screenMaximum);
    float sceneEyeDepth = TsukuyomiHairSampleSceneEyeDepth(depthUv);
    float fragmentEyeDepth = 1.0 / positionCS.w;
    float3 objectCenterWS = TransformObjectToWorld(0.0.xxx);
    float3 horizontalObjectRay = positionWS - objectCenterWS;
    horizontalObjectRay.y = 6.103515625e-05;
    horizontalObjectRay = normalize(horizontalObjectRay);
    float3 screenRim = (_CharacterParams8.xyz
        * smoothstep(0.100000001490116119384765625,
            0.20000000298023223876953125, sceneEyeDepth - fragmentEyeDepth)
        * _CharacterParams8.w)
        * min(min(clamp(dot(horizontalObjectRay, screenRimDirection) + 1.0,
            0.0, 1.0), surface.shadowMask), characterShadow)
        * (lerp(0.25.xxx, diffuseAlbedo, _CharacterParams9.z.xxx)
        * clamp(dot(screenRimDirection, diffuseNormalWS), 0.0, 1.0));

    float horizontalNormal = dot(horizontalLight, diffuseNormalWS);
    float dominant = dot(indirect.dominantDirection.xyz, diffuseNormalWS)
        * indirect.dominantDirection.w;
    float warpedHorizontal = (-horizontalNormal)
        * ((horizontalNormal * 0.5) - 1.0) + 0.5;
    float indirectDirection = clamp(lerp(dominant, warpedHorizontal,
        mainShadow), 0.0, 1.0);
    float shadowEdge = (1.0 - mainShadow) + (horizontalView * mainShadow);
    float viewEdge = smoothstep(0.60000002384185791015625,
        0.800000011920928955078125, 1.0 - abs(dot(viewDirectionWS, diffuseNormalWS)));
    float3 normalizedIndirect = indirectIrradiance
        / max(max(max(indirectIrradiance.x, indirectIrradiance.y),
            indirectIrradiance.z) * 0.5, 1.0).xxx;
    float3 indirectRim = lerp(normalizedIndirect, lightColor, mainShadow.xxx)
        * indirectDirection * (shadowEdge * (1.0 - _CharacterParams12.x))
        * viewEdge * min(surface.shadowMask, characterShadow)
        * ((1.0 - mainShadow) + (smoothstep(0.100000001490116119384765625,
            0.039999999105930328369140625, albedoLuminance) * mainShadow))
        * max(0.1500000059604644775390625.xxx, diffuseAlbedo);
    color += screenRim + indirectRim;
    return color;
}

float3 TsukuyomiHairEvaluateOutlineMainLight(
    TsukuyomiHairLightInput light,
    TsukuyomiHairIndirectInput indirect,
    float3 outlineColor,
    float outlineAlpha,
    float3 normalWS,
    float3 positionWS,
    float4 positionCS,
    float2 normalizedScreenSpaceUV)
{
    // HGRP Hair pass-1 b307 L578-L622. Outline has its own diffuse-only
    // lighting path and does not reuse the forward anisotropic equation.
    float3 diffuseAlbedo = outlineColor * 0.959999978542327880859375;
    float3 shadowInput = outlineColor * _ShadowColorBrightness;
    float3 shadowAlbedo = lerp(dot(shadowInput,
        TsukuyomiHairLuminanceWeights).xxx, shadowInput,
        _ShadowColorSaturation.xxx) * 0.959999978542327880859375;

    float3 cameraForwardWS = normalize(mul((float3x3)UNITY_MATRIX_I_V,
        float3(0.0, 0.0, 1.0)));
    float3 lightDirection = lerp(light.direction, _CharacterParams11.xyz,
        _CharacterParams1.w.xxx);
    float3 horizontalLight = normalize(float3(lightDirection.x,
        6.103515625e-05, lightDirection.z));
    float horizontalView = clamp(-dot(horizontalLight.xz,
        normalize(cameraForwardWS.xz)), 0.0, 1.0);
    float normalLight = dot(normalWS, lightDirection);
    float warpedNormalLight = lerp(normalLight,
        (-normalLight) * ((normalLight * 0.5) - 1.0) + 0.5,
        horizontalView * smoothstep(0.25, 0.75,
        1.0 - abs(cameraForwardWS.y)) * (1.0 - _CharacterParams12.x))
        + (_CharacterParams11.w * _CharacterParams12.x);
    float4 diffuseRamp = TsukuyomiHairSampleDiffuseRamp(warpedNormalLight);
    float4 viewRamp = TsukuyomiHairSampleViewRamp(dot(normalWS, cameraForwardWS));

    float characterShadow = light.characterShadow;
    float mainShadow = lerp(light.directionalShadow, 1.0, _CharacterParams1.z);
    float shadowMask = min(characterShadow, diffuseRamp.w);
    float viewShadow = viewRamp.w * characterShadow;

    float exposure = lerp(_EnvironmentGlobalParams0.x, 1.0,
        _CharacterParams12.w) * _ExposureWithMiscParams.x;
    float3 irradiance = indirect.irradiance * exposure;
    float irradianceLuminance = dot(irradiance, TsukuyomiHairLuminanceWeights);
    float3 tintSource = lerp(irradianceLuminance.xxx, irradiance,
        (clamp((irradianceLuminance * 10.0)
        - 0.100000001490116119384765625, 0.0, 1.0) * 0.5).xxx);
    float3 indirectTint = max(tintSource,
        0.001000000047497451305389404296875.xxx)
        / max(max(max(tintSource.x, tintSource.y), tintSource.z),
        0.001000000047497451305389404296875).xxx;
    float indirectIntensity = max(max(max(indirect.dominantIrradiance.x,
        indirect.dominantIrradiance.y), indirect.dominantIrradiance.z), 0.0)
        * exposure;
    if (_CharacterParams1.y >= 0.5)
    {
        indirectTint = _CharacterParams2.xyz;
        indirectIntensity = exposure;
    }

    float3 normalRemap = ((clamp(dot(normalWS, _CharacterParams6.xyz)
        + _CharacterParams7.x, 0.0, 1.0) * _CharacterParams7.y)
        + _CharacterParams7.z).xxx;
    float3 shapedIndirect = normalRemap
        * lerp(indirectTint, 1.0.xxx,
            (_CharacterParams1.y * shadowMask).xxx);
    float3 selectedLightColor = lerp(light.color, _CharacterParams5.xyz,
        _CharacterParams12.y.xxx);
    float3 lightColor = selectedLightColor
        * lerp(light.directionalIntensityScale, 1.0, _CharacterParams12.w);
    float indirectScaleA = min(lerp(0.64999997615814208984375, 1.0,
        indirectIntensity), 1.5);
    float indirectScaleB = clamp(indirectIntensity, 1.25, 1.75);
    float3 indirectSide = shapedIndirect
        * lerp(indirectScaleA, indirectScaleB, _CharacterParams1.x)
        * _CharacterParams0.w;
    float3 directSide = (lerp(dot(lightColor,
        TsukuyomiHairLuminanceWeights).xxx, lightColor, shadowMask.xxx)
        + ((shapedIndirect * clamp(indirectIntensity, 0.0, 1.5))
        * ((1.0 - _CharacterParams12.y).xxx
        + (selectedLightColor * _CharacterParams12.y)))) * _CharacterParams0.y;
    float3 lightFactor = lerp(indirectSide, directSide, mainShadow.xxx);

    float3 scaledShadow = shadowAlbedo * _CharacterParams0.z;
    float3 darkShadow = scaledShadow * 0.64999997615814208984375;
    float diffuseRampDelta = max(max(diffuseRamp.x, diffuseRamp.y), diffuseRamp.z)
        - min(min(diffuseRamp.x, diffuseRamp.y), diffuseRamp.z);
    float albedoLuminance = dot(diffuseAlbedo, TsukuyomiHairLuminanceWeights);
    float3 rampBase = lerp(
        lerp(lerp(dot(darkShadow, TsukuyomiHairLuminanceWeights).xxx,
            darkShadow, 1.2000000476837158203125.xxx), scaledShadow,
            clamp((characterShadow * viewRamp.w) + diffuseRamp.w,
                0.0, 1.0).xxx),
        diffuseAlbedo, shadowMask.xxx);
    float3 rampColored = rampBase
        * ((1.0 - diffuseRampDelta).xxx + (diffuseRamp.xyz * diffuseRampDelta));
    float3 lightColorMix = lerp(
        lerp(scaledShadow,
            lerp(albedoLuminance.xxx, diffuseAlbedo,
                1.2000000476837158203125.xxx), viewShadow.xxx),
        rampColored * clamp(dot(rampBase, TsukuyomiHairLuminanceWeights)
            / max(dot(rampColored, TsukuyomiHairLuminanceWeights),
                0.001000000047497451305389404296875), 0.0, 1.5),
        mainShadow.xxx);

    float alphaPremultiply = (1.0 - _AlphaPremultiply)
        + (outlineAlpha * _AlphaPremultiply);
    float3 color = lightFactor * lightColorMix * alphaPremultiply;
    float luminance = dot(color, TsukuyomiHairLuminanceWeights);
    float highlight = clamp(luminance - 0.5, 0.0, 0.5);
    color = lerp(luminance.xxx, color, ((highlight * highlight) + 1.0).xxx);

    float3 screenDirection = lerp(float3(_CharacterParams9.xy, 0.0),
        (UNITY_MATRIX_V[0].xyz * _CharacterParams9.x)
        + (UNITY_MATRIX_V[1].xyz * _CharacterParams9.y),
        _CharacterParams15.w.xxx);
    float3 screenRimDirection = normalize(cross(cameraForwardWS, screenDirection));
    float2 screenNormal = normalize(mul((float3x3)UNITY_MATRIX_V, normalWS).xy)
        * float2(_ScreenParams.y / _ScreenParams.x, 1.0);
    float2 depthUv = clamp(normalizedScreenSpaceUV
        + ((screenNormal * _CharacterParams9.w)
        * 0.006000000052154064178466796875),
        _ScreenParams.zw - 1.0.xx, 2.0.xx - _ScreenParams.zw);
    float sceneEyeDepth = TsukuyomiHairSampleSceneEyeDepth(depthUv);
    float fragmentEyeDepth = 1.0 / positionCS.w;
    float3 objectCenterWS = TransformObjectToWorld(0.0.xxx);
    float3 horizontalObjectRay = positionWS - objectCenterWS;
    horizontalObjectRay.y = 6.103515625e-05;
    horizontalObjectRay = normalize(horizontalObjectRay);
    float3 screenRim = (_CharacterParams8.xyz
        * smoothstep(0.100000001490116119384765625,
            0.20000000298023223876953125, sceneEyeDepth - fragmentEyeDepth)
        * _CharacterParams8.w)
        * min(clamp(dot(horizontalObjectRay, screenRimDirection) + 1.0,
            0.0, 1.0), characterShadow)
        * (lerp(0.25.xxx, diffuseAlbedo, _CharacterParams9.z.xxx)
        * clamp(dot(screenRimDirection, normalWS), 0.0, 1.0));
    return color + screenRim;
}

float3 TsukuyomiHairEvaluateOutlinePunctualLight(
    TsukuyomiHairLightInput light,
    float3 outlineColor,
    float outlineAlpha,
    float3 normalWS)
{
    float3 diffuseAlbedo = outlineColor * 0.959999978542327880859375;
    float3 shadowInput = outlineColor * _ShadowColorBrightness;
    float3 shadowAlbedo = lerp(dot(shadowInput,
        TsukuyomiHairLuminanceWeights).xxx, shadowInput,
        _ShadowColorSaturation.xxx) * 0.959999978542327880859375
        * light.punctualShadowColorScale;
    float diffuseBlend = clamp(clamp(dot(normalWS, light.direction)
        + light.punctualDiffuseBias, -1.0, 1.0), 0.0, 1.0)
        * light.characterShadow;
    float alphaPremultiply = (1.0 - _AlphaPremultiply)
        + (outlineAlpha * _AlphaPremultiply);
    return (light.color * light.distanceAttenuation)
        * lerp(shadowAlbedo, diffuseAlbedo, diffuseBlend.xxx)
        * alphaPremultiply;
}

float3 TsukuyomiHairEvaluatePunctualLight(
    TsukuyomiHairLightInput light,
    TsukuyomiHairSurface surface,
    float3 diffuseNormalWS,
    float3 specularNormalWS,
    float3 tangentWS,
    float tangentSign,
    float3 viewDirectionWS)
{
    // HGRP b142 L1372-L1411, light type 1. This path intentionally contains
    // only the source primary lobe (no secondary lobe and no specular line).
    float nDotL = dot(diffuseNormalWS, light.direction);
    float diffuseBlend = clamp(clamp(nDotL + light.punctualDiffuseBias,
        -1.0, 1.0), 0.0, 1.0) * light.characterShadow;
    float3 diffuseAlbedo = surface.baseColor * 0.959999978542327880859375;
    float3 shadowAlbedo = surface.shadowColor * 0.959999978542327880859375
        * light.punctualShadowColorScale;
    float3 diffuse = lerp(shadowAlbedo, diffuseAlbedo, diffuseBlend.xxx);

    float3 halfDirection = TsukuyomiHairSafeNormalize(light.direction + viewDirectionWS);
    float edgeFade = TsukuyomiHairEdgeFade(specularNormalWS, viewDirectionWS);
    float primaryMaximum;
    float3 primaryShape = TsukuyomiHairEvaluatePrimaryShape(surface,
        specularNormalWS, tangentWS, tangentSign, halfDirection,
        edgeFade, primaryMaximum);
    float3 specular = primaryShape
        * (0.039999999105930328369140625.xxx * surface.specular)
        * _AnisotropyIntensity * 5.0 * light.punctualSpecularScale;
    float alphaPremultiply = (1.0 - _AlphaPremultiply)
        + (surface.alpha * _AlphaPremultiply);
    float3 radiance = light.color * light.distanceAttenuation;
    return (radiance * diffuse * alphaPremultiply)
        + (radiance * specular * diffuseBlend);
}

float3 TsukuyomiHairOutlineFragmentLighting(
    InputData inputData,
    float3 outlineColor,
    float outlineAlpha,
    float3 normalWS)
{
    float4 shadowMask = CalculateShadowMask(inputData);
    Light urpMainLight = GetMainLight(inputData.shadowCoord,
        inputData.positionWS, shadowMask);
    float3 color = TsukuyomiHairEvaluateOutlineMainLight(
        TsukuyomiHairAdaptLight(urpMainLight),
        TsukuyomiHairAdaptIndirect(inputData.bakedGI), outlineColor,
        outlineAlpha, normalWS, inputData.positionWS, inputData.positionCS,
        inputData.normalizedScreenSpaceUV);

#if defined(_ADDITIONAL_LIGHTS)
    uint meshRenderingLayers = GetMeshRenderingLayer();
    uint pixelLightCount = GetAdditionalLightsCount();

    #if USE_CLUSTER_LIGHT_LOOP
    [loop] for (uint lightIndex = 0;
        lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS);
        lightIndex++)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        Light urpLight = GetAdditionalLight(lightIndex,
            inputData.positionWS, shadowMask);
        #if defined(_LIGHT_LAYERS)
        if (!IsMatchingLightLayer(urpLight.layerMask, meshRenderingLayers))
            continue;
        #endif
        color += TsukuyomiHairEvaluateOutlinePunctualLight(
            TsukuyomiHairAdaptLight(urpLight), outlineColor,
            outlineAlpha, normalWS);
    }
    #endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light urpLight = GetAdditionalLight(lightIndex,
            inputData.positionWS, shadowMask);
        #if defined(_LIGHT_LAYERS)
        if (IsMatchingLightLayer(urpLight.layerMask, meshRenderingLayers))
        #endif
        {
            color += TsukuyomiHairEvaluateOutlinePunctualLight(
                TsukuyomiHairAdaptLight(urpLight), outlineColor,
                outlineAlpha, normalWS);
        }
    LIGHT_LOOP_END
#endif

    return color * _ExposureWithMiscParams.y;
}

float3 TsukuyomiHairFragmentLighting(
    InputData inputData,
    float2 uv,
    TsukuyomiHairSurface surface,
    float3 diffuseNormalWS,
    float3 specularNormalWS,
    float3 tangentWS,
    float tangentSign)
{
    float4 shadowMask = CalculateShadowMask(inputData);
    Light urpMainLight = GetMainLight(inputData.shadowCoord,
        inputData.positionWS, shadowMask);
    TsukuyomiHairLightInput mainLight = TsukuyomiHairAdaptLight(urpMainLight);
    TsukuyomiHairIndirectInput indirect = TsukuyomiHairAdaptIndirect(inputData.bakedGI);
    float3 color = TsukuyomiHairEvaluateMainLight(mainLight, indirect,
        surface, uv, diffuseNormalWS, specularNormalWS, tangentWS,
        tangentSign, inputData.viewDirectionWS, inputData.positionWS,
        inputData.positionCS, inputData.normalizedScreenSpaceUV);

#if defined(_ADDITIONAL_LIGHTS)
    uint meshRenderingLayers = GetMeshRenderingLayer();
    uint pixelLightCount = GetAdditionalLightsCount();

    #if USE_CLUSTER_LIGHT_LOOP
    [loop] for (uint lightIndex = 0;
        lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS);
        lightIndex++)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        Light urpLight = GetAdditionalLight(lightIndex, inputData.positionWS, shadowMask);
        #if defined(_LIGHT_LAYERS)
        if (!IsMatchingLightLayer(urpLight.layerMask, meshRenderingLayers))
            continue;
        #endif
        color += TsukuyomiHairEvaluatePunctualLight(TsukuyomiHairAdaptLight(urpLight),
            surface, diffuseNormalWS, specularNormalWS, tangentWS,
            tangentSign, inputData.viewDirectionWS);
    }
    #endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light urpLight = GetAdditionalLight(lightIndex, inputData.positionWS, shadowMask);
        #if defined(_LIGHT_LAYERS)
        if (IsMatchingLightLayer(urpLight.layerMask, meshRenderingLayers))
        #endif
        {
            color += TsukuyomiHairEvaluatePunctualLight(TsukuyomiHairAdaptLight(urpLight),
                surface, diffuseNormalWS, specularNormalWS, tangentWS,
                tangentSign, inputData.viewDirectionWS);
        }
    LIGHT_LOOP_END
#endif

    float alphaPremultiply = (1.0 - _AlphaPremultiply)
        + (surface.alpha * _AlphaPremultiply);
    color += surface.emission * alphaPremultiply;
    return color * _ExposureWithMiscParams.y;
}

#endif
