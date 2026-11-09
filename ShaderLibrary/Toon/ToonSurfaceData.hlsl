#ifndef TOON_SURFACE_DATA_INCLUDED
#define TOON_SURFACE_DATA_INCLUDED

struct ToonSurfaceData
{
    half3 albedo;
    half3 specular;
    half metallic;
    half smoothness;
    half3 normalTS;
    half3 emission;
    half occlusion;
    half alpha;
    half clearCoatMask;
    half clearCoatSmoothness;
    float4 ToonData;
};

#endif
