#ifndef TSUKUYOMI_OPENPBR_LUTS_INCLUDED
#define TSUKUYOMI_OPENPBR_LUTS_INCLUDED

// Bound once from TsukuyomiOpenPBRResources. Explicit LOD: valid in fragments
// and compute kernels. No derivatives, no generated runtime integration.
Texture2D<float4> _TsukuyomiOpenPBRDFG;
Texture3D<float> _TsukuyomiOpenPBRDielectric;
Texture2D<float4> _TsukuyomiOpenPBRFuzz;
SamplerState sampler_TsukuyomiOpenPBR_linear_clamp;

float2 TsuOPBRLutUV(float2 uv, float size)
{
    return (saturate(uv) * (size - 1.0) + 0.5) / size;
}

float4 TsuOPBRDFG(float NoV, float roughness)
{
    return _TsukuyomiOpenPBRDFG.SampleLevel(sampler_TsukuyomiOpenPBR_linear_clamp,
        TsuOPBRLutUV(float2(NoV, roughness), 64.0), 0);
}

// Adobe table is flattened [ior][perceptual roughness][cos theta]. The IOR
// discontinuity at 1 is deliberate; do not interpolate across the two halves.
float TsuOPBRDielectricEnergy(float NoV, float roughness, float ior)
{
    float index = 16.0 + (min(ior, 2.5) - 1.0) * 10.0;
    float3 uv = (float3(saturate(NoV) * 31.0, saturate(roughness) * 31.0, index) + 0.5) / 32.0;
    float complement = _TsukuyomiOpenPBRDielectric.SampleLevel(sampler_TsukuyomiOpenPBR_linear_clamp, uv, 0);
    if (ior > 2.5)
        complement *= (1.0 - TsuOPBRF0(ior)) / (1.0 - TsuOPBRF0(2.5));
    return ior <= 1.00001 ? 0.0 : saturate(1.0 - complement);
}

float3 TsuOPBRFuzzCoefficients(float NoV, float roughness)
{
    return _TsukuyomiOpenPBRFuzz.SampleLevel(sampler_TsukuyomiOpenPBR_linear_clamp,
        TsuOPBRLutUV(float2(NoV, roughness), 32.0), 0).xyz;
}

#endif
