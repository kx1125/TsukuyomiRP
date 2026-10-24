#ifndef TSUKUYOMI_SPECULAR_PBR_SSS_INCLUDED
#define TSUKUYOMI_SPECULAR_PBR_SSS_INCLUDED

// Shared by SpecularPBR and material adapters. Mask controls participation and
// final blending; it never scales the source radiance a second time.
TEXTURE2D_X(_LightingTexBlurred);
TEXTURE2D_X(_SSSSkinLightingTexture);
TEXTURE2D_X(_SSSSkinMaskTexture);
TEXTURE2D_X_FLOAT(_TsukuyomiSpecularPBRSSSDepth);
float _TsukuyomiSpecularPBRSSSReady;
float4 _TsukuyomiSpecularPBRSSSLightingScale;

bool SpecularPBRHasSkinLighting(float3 positionWS, float2 screenUV)
{
    if (_TsukuyomiSpecularPBRSSSReady < 0.5) return false;
    float coverage = SAMPLE_TEXTURE2D_X_LOD(_SSSSkinMaskTexture, sampler_PointClamp, screenUV, 0).r;
    if (coverage <= 0.0) return false;
    float rawDepth = SAMPLE_TEXTURE2D_X_LOD(_TsukuyomiSpecularPBRSSSDepth, sampler_PointClamp, screenUV, 0).r;
    float skinDepth = IsPerspectiveProjection() ? LinearEyeDepth(rawDepth, _ZBufferParams) : LinearDepthToEyeDepth(rawDepth);
    float surfaceDepth = -TransformWorldToView(positionWS).z;
    return abs(skinDepth - surfaceDepth) <= max(0.002, surfaceDepth * 0.002);
}

void SpecularPBRPrepareDiffusionSurface(inout SurfaceData surface, float3 diffusionTint)
{
    surface.albedo = _BaseColor.rgb * diffusionTint;
    surface.emission = 0;
}

float3 SpecularPBRSampleTransmission(float2 uv, float occlusion)
{
#if defined(TRANSMISSION)
    return SAMPLE_TEXTURE2D(_TransmissionMap, sampler_TransmissionMap, uv).rgb
        * _TransmissionColor.rgb * lerp(1.0, occlusion, TransmissionOcc);
#else
    return 0;
#endif
}

float3 SpecularPBRCompositeSkin(TsukuyomiOpenPBRResponse lighting, float3 baseTexture,
    float skinMask, float3 positionWS, float2 screenUV, float3 emission)
{
#if defined(_SSS_ON)
    if (skinMask > 0.0 && SpecularPBRHasSkinLighting(positionWS, screenUV))
    {
        float3 scattered = SAMPLE_TEXTURE2D_X_LOD(_LightingTexBlurred, sampler_LinearClamp, screenUV, 0).rgb * baseTexture;
        return SpecularPBRComposite(lighting.diffuse, lighting.specular, scattered, skinMask, emission);
    }
#endif
    return lighting.diffuse + lighting.specular + emission;
}

#endif
