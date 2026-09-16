#ifndef TSUKUYOMI_SPECULAR_PBR_MATH_INCLUDED
#define TSUKUYOMI_SPECULAR_PBR_MATH_INCLUDED

float3 SpecularPBRRasterNormal(float3 normalWS, float3 viewDirectionWS)
{
    // A visible rasterized triangle can have a back-facing interpolated or
    // normal-mapped normal at its silhouette. Reflect that normal into the
    // view hemisphere, following Unity's GetViewReflectedNormal convention.
    // Adjust the vector, not just NdotV, so the BRDF and reflection lookup agree.
    float NoV = dot(normalWS, viewDirectionWS);
    float correction = max(-2.0 * NoV, 0.0) + max(1e-4 - abs(NoV), 0.0);
    return normalize(normalWS + correction * viewDirectionWS);
}

float SpecularPBRRoughness(float mask, float roughness)
{
    // Preserve the authored Standard PBR / Standard SSS control, including white = 1.
    return saturate(lerp(mask, 1.0, roughness));
}

float3 SpecularPBRComposite(float3 diffuse, float3 specular, float3 scatteredDiffuse, float skinMask, float3 emission)
{
    return lerp(diffuse, scatteredDiffuse, saturate(skinMask)) + specular + emission;
}

#endif
