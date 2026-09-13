#ifndef TSUKUYOMI_CHARACTER_FACE_LIGHTING_INCLUDED
#define TSUKUYOMI_CHARACTER_FACE_LIGHTING_INCLUDED

#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Material/CharacterFaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// Equation references below are characternpr_skin/Sub0_Pass0_Fragment_b131.hlsl.
static const float TSUKUYOMI_FACE_EPSILON = 6.103515625e-05;

float3 TsukuyomiFaceNormalize(float3 value, float3 fallback)
{
    float lengthSquared = dot(value, value);
    return lengthSquared > 1e-8 && all(isfinite(value))
        ? value * rsqrt(lengthSquared) : fallback;
}

float3 TsukuyomiFaceFlatten(float3 value)
{
    return normalize(float3(value.x, TSUKUYOMI_FACE_EPSILON, value.z));
}

struct TsukuyomiFaceFrame
{
    float3x3 headToWorld;
    float3 anchorPositionWS;
    float3 radialNormalWS;
    float3 giNormalWS;
    float3 cameraForwardWS;
    float viewAlignment;
};

void TsukuyomiFaceGetAnchor(out float3x3 headToWorld, out float3 positionWS)
{
    // Adapter for the source's per-draw/skin matrix (b131 L403-L423).
    // Keep the external _AnchorTRS world-to-local contract. A missing or invalid
    // matrix uses the renderer transform. Strip scale from the shading frame.
    float3 a = _AnchorTRS[0].xyz;
    float3 b = _AnchorTRS[1].xyz;
    float3 c = _AnchorTRS[2].xyz;
    float determinant = dot(a, cross(b, c));
    float3x3 objectToWorld = (float3x3)GetObjectToWorldMatrix();
    positionWS = GetObjectToWorldMatrix()._m03_m13_m23;
    if (abs(determinant) > 1e-8 && isfinite(determinant)
        && all(isfinite(_AnchorTRS[0])) && all(isfinite(_AnchorTRS[1]))
        && all(isfinite(_AnchorTRS[2])))
    {
        objectToWorld = transpose(float3x3(cross(b, c), cross(c, a), cross(a, b))) / determinant;
        positionWS = -mul(objectToWorld, float3(_AnchorTRS[0].w, _AnchorTRS[1].w, _AnchorTRS[2].w));
    }
    float3 right = TsukuyomiFaceNormalize(mul(objectToWorld, float3(1, 0, 0)), float3(1, 0, 0));
    float3 up = TsukuyomiFaceNormalize(mul(objectToWorld, float3(0, 1, 0)), float3(0, 1, 0));
    float3 forward = TsukuyomiFaceNormalize(mul(objectToWorld, float3(0, 0, 1)), float3(0, 0, 1));
    headToWorld = transpose(float3x3(right, up, forward));
}

TsukuyomiFaceFrame TsukuyomiFaceBuildFrame(float3 positionWS, float3 normalWS,
    float4 mask, float3 cameraForwardWS)
{
    TsukuyomiFaceFrame frame;
    TsukuyomiFaceGetAnchor(frame.headToWorld, frame.anchorPositionWS);
    frame.radialNormalWS = TsukuyomiFaceFlatten(positionWS - frame.anchorPositionWS);
    // L435-L458: this flattened normal is for GI, not the specular NDF.
    frame.giNormalWS = TsukuyomiFaceNormalize(lerp(frame.radialNormalWS,
        TsukuyomiFaceFlatten(normalWS), mask.g), normalWS);
    frame.cameraForwardWS = TsukuyomiFaceNormalize(cameraForwardWS, float3(0, 0, 1));
    float3 cameraLocal = mul(frame.cameraForwardWS, frame.headToWorld);
    frame.viewAlignment = TsukuyomiFaceNormalize(float3(cameraLocal.x, 0, cameraLocal.z),
        float3(0, 0, 1)).z;
    return frame;
}

float3 TsukuyomiFaceSkinTint(TsukuyomiFaceSurface surface, TsukuyomiFaceFrame frame,
    float3 normalWS, float3 viewDirectionWS)
{
    // L611-L614: G controls front-view gating; B selects the two rim scales.
    float rimMask = surface.mask.r * lerp(saturate(frame.viewAlignment + 0.5), 1.0, surface.mask.g);
    float strength = lerp(_FaceRimOffScale, _SkinRimOffScale, surface.mask.b);
    float weight = saturate((1.0 - saturate(saturate(dot(normalWS, viewDirectionWS)) * 0.85 + 0.15))
        * rimMask * strength);
    return surface.albedo * lerp(1.0.xxx, _SDFRimColor.rgb, weight);
}

float TsukuyomiFaceSdfCurve(float value, float center)
{
    // L747-L751. Preserve abs/ceil and the bounded threshold interval.
    float range = clamp(0.5 - center, 0.001, 0.999);
    float t = smoothstep(max(2.0 * range - 1.0, 0.0), min(2.0 * range, 1.0), value);
    return lerp(-1.0, 1.0, abs(-t - center * ceil(center)));
}

struct TsukuyomiFaceSdf
{
    float4 lightmap;
    float3 normalWS;
    float coordinate;
};

TsukuyomiFaceSdf TsukuyomiFaceEvaluateSdf(float2 uv, float3 lightDirectionWS,
    float3 normalWS, TsukuyomiFaceSurface surface, TsukuyomiFaceFrame frame)
{
    TsukuyomiFaceSdf sdf;
    sdf.lightmap = 0.0.xxxx;
    sdf.normalWS = normalWS;
    sdf.coordinate = clamp(dot(normalWS, lightDirectionWS) + _DiffuseOffset, -1.0, 1.0);
#if defined(_SDFLIGHTMAP)
    // L732-L751: head-local light selects the UV mirror and the B-channel normal.
    float3 lightLocal = TsukuyomiFaceFlatten(mul(lightDirectionWS, frame.headToWorld));
    float right = float(lightLocal.x > 0.0);
    float2 sdfUV = float2(lerp(1.0 - uv.x, uv.x, right), uv.y);
    sdf.lightmap = SAMPLE_TEXTURE2D_LOD(_SDFLightmap, sampler_LinearMirror, sdfUV, 0);
    float x = lerp(1.0 - sdf.lightmap.b * 2.0, sdf.lightmap.b * 2.0 - 1.0, right);
    float3 sdfNormalWS = mul(frame.headToWorld,
        normalize(float3(x, TSUKUYOMI_FACE_EPSILON, 1.0 - abs(x))));
    sdf.normalWS = TsukuyomiFaceNormalize(lerp(sdfNormalWS, normalWS, surface.mask.g), normalWS);
    float viewCorrection = saturate(-dot(TsukuyomiFaceFlatten(lightDirectionWS).xz,
        TsukuyomiFaceNormalize(float3(frame.cameraForwardWS.x, 0, frame.cameraForwardWS.z), float3(0, 0, 1)).xz));
    // Source global light-override weight is zero; diffuse bias is a local adapter control.
    float center = lerp(lightLocal.z, -lightLocal.z * (lightLocal.z * 0.5 - 1.0) + 0.5,
        viewCorrection * saturate(-lightLocal.z)) * 0.5;
    sdf.coordinate = lerp(TsukuyomiFaceSdfCurve(dot(sdf.lightmap.rg, 0.5.xx), center),
        sdf.coordinate, surface.mask.g);
#endif
    return sdf;
}

float4 TsukuyomiFaceSampleRamp(float coordinate)
{
#if defined(_DIFF_RAMP_ON)
    return SAMPLE_TEXTURE2D_LOD(_DiffRampMap, sampler_LinearMirror,
        float2(coordinate * 0.5 + 0.5, 0.5), 0);
#else
    return smoothstep(0.25, 1.0, coordinate).xxxx;
#endif
}

float3 TsukuyomiFaceSpecularF0(TsukuyomiFaceSurface surface, float3 skinTint)
{
    // L614, L710-L715: the G mask gates dielectric specular, not metallic F0.
    return lerp((0.04 * _Specular * surface.mask.g).xxx, skinTint, _Metallic);
}

float TsukuyomiFaceSpecularShape(float3 normalWS, float3 viewDirectionWS, float3 halfDirectionWS)
{
    // L713-L715, L774-L790: perceptual roughness^2 floor, GGX-like NDF,
    // visibility, epsilon subtraction, and the source's upper bound of 20.
    float roughness = max((1.0 - _Smoothness) * (1.0 - _Smoothness), 0.0078125);
    float roughness2 = roughness * roughness;
    float nh = dot(normalWS, halfDirectionWS);
    float nv = saturate(dot(normalWS, viewDirectionWS));
    float d = (nh * roughness2 - nh) * nh + 1.0;
    float denominator = d * d;
    float ndf = roughness2 != denominator ? roughness2 / max(denominator, 1e-12) : 1.0;
    return clamp(ndf * (0.5 / (2.0 * nv + roughness + 0.0001)) - TSUKUYOMI_FACE_EPSILON, 0.0, 20.0);
}

float3 TsukuyomiFaceSampleHighlight(float2 uv, float3 viewDirectionWS, TsukuyomiFaceFrame frame)
{
#if defined(_HIGHLIGHT_MAP)
    // L780: independent X/Y offsets in head coordinates. This is not emission.
    float2 offset = mul(viewDirectionWS, frame.headToWorld).xy * _HighlightMapVector.xy;
    return SAMPLE_TEXTURE2D(_HighlightMap, sampler_LinearRepeat, uv + offset).rgb;
#else
    return 0.0.xxx;
#endif
}

struct TsukuyomiFaceMainResponse
{
    float3 color;
    float3 diffuseAlbedo;
    float3 specularF0;
    float3 highlight;
    float highlightWeight;
    TsukuyomiFaceSdf sdf;
};

TsukuyomiFaceMainResponse TsukuyomiFaceMainLight(TsukuyomiFaceSurface surface,
    TsukuyomiFaceFrame frame, float2 uv, float3 normalWS, float3 viewDirectionWS, Light light)
{
    TsukuyomiFaceMainResponse result;
    result.sdf = TsukuyomiFaceEvaluateSdf(uv, light.direction, normalWS, surface, frame);
    float3 skinTint = TsukuyomiFaceSkinTint(surface, frame, normalWS, viewDirectionWS);
    float diffuseWeight = 0.96 * (1.0 - _Metallic);
    result.diffuseAlbedo = skinTint * diffuseWeight;
    float3 shadowAlbedo = surface.shadowAlbedo * diffuseWeight;
    float4 ramp = TsukuyomiFaceSampleRamp(result.sdf.coordinate);
    float shadow = saturate(light.shadowAttenuation);
    // L758-L769. URP resolves directional and character shadow into one input.
    // Global albedo/exposure multipliers are neutral; no extra Lambert cutoff.
    float receive = max(surface.mask.g, surface.mask.b * smoothstep(0.75, 0.25, frame.viewAlignment));
    float characterShadow = lerp(1.0, shadow, receive);
    float lit = min(surface.alphaMask, min(characterShadow, ramp.a));
    float alphaShadow = surface.alphaMask * characterShadow;
    float3 darkAlbedo = TsukuyomiFaceSaturateColor(shadowAlbedo * 0.65, 1.2);
    float3 albedo = lerp(lerp(darkAlbedo, shadowAlbedo,
        saturate(surface.alphaMask * (1.0 - surface.mask.g + characterShadow * surface.mask.g) + ramp.a)),
        result.diffuseAlbedo, lit);
    float chroma = max(max(ramp.r, ramp.g), ramp.b) - min(min(ramp.r, ramp.g), ramp.b);
    float3 rampAlbedo = albedo * lerp(1.0.xxx, ramp.rgb, chroma);
    rampAlbedo *= clamp(TsukuyomiFaceLuminance(albedo) / max(TsukuyomiFaceLuminance(rampAlbedo), 0.001), 0.0, 1.5);
    albedo = lerp(lerp(shadowAlbedo, TsukuyomiFaceSaturateColor(result.diffuseAlbedo, 1.2), alphaShadow),
        rampAlbedo, shadow);

    // URP adapter: Light.color includes intensity, distance attenuation is separate.
    float3 radiance = light.color * light.distanceAttenuation;
    result.highlightWeight = lerp(alphaShadow, lit, shadow) * 0.5 + 0.5;
    float3 fakeSun = float3(frame.cameraForwardWS.x, lerp(0.5, light.direction.y, shadow), frame.cameraForwardWS.z);
    float3 halfDirection = TsukuyomiFaceNormalize(
        light.direction * shadow + TsukuyomiFaceNormalize(fakeSun, frame.cameraForwardWS) * 2.0
        + viewDirectionWS * (2.0 + shadow), viewDirectionWS);
    result.specularF0 = TsukuyomiFaceSpecularF0(surface, skinTint);
    float3 specular = result.specularF0
        * TsukuyomiFaceSpecularShape(normalWS, viewDirectionWS, halfDirection) * result.highlightWeight;
    result.color = (albedo + specular) * radiance;
    result.highlight = TsukuyomiFaceSampleHighlight(uv, viewDirectionWS, frame);
    return result;
}

float3 TsukuyomiFaceAdditionalLight(TsukuyomiFaceSurface surface, TsukuyomiFaceMainResponse main,
    float3 normalWS, float3 viewDirectionWS, Light light)
{
    // b131 L1100-L1153: ordinary HGRP punctual type 1. Custom bias, shadow-color
    // scale and specular scale map to neutral 0/1/1; special light types omitted.
    float noL = dot(main.sdf.normalWS, light.direction);
    float diffuse = max(smoothstep(0.0, -0.2, main.sdf.lightmap.a),
        smoothstep(0.0, lerp(0.1, 1.0, surface.mask.g), clamp(noL, -1.0, 1.0)));
    float3 shadowAlbedo = surface.shadowAlbedo * (0.96 * (1.0 - _Metallic));
    float3 albedo = lerp(shadowAlbedo, main.diffuseAlbedo, diffuse);
    float3 halfDirection = TsukuyomiFaceNormalize(light.direction + viewDirectionWS, normalWS);
    float3 specular = main.specularF0 * TsukuyomiFaceSpecularShape(normalWS, viewDirectionWS, halfDirection) * diffuse;
    return (albedo + specular) * light.color * light.distanceAttenuation * light.shadowAttenuation;
}

float3 TsukuyomiFaceCompose(TsukuyomiFaceSurface surface, TsukuyomiFaceMainResponse main,
    float3 additional, float3 bakedGI, float3 mainRadiance)
{
    // Simplified URP composition, deliberately separate from the source equations.
    // GI/highlight/emission are evaluated once, never inside the additional-light loop.
    float3 gi = max(bakedGI, 0.0.xxx);
    return main.color + additional + main.diffuseAlbedo * gi * surface.alphaMask
        + main.highlight * (mainRadiance + gi) * main.highlightWeight + surface.emission;
}

#endif
