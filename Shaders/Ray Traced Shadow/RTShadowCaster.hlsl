#ifndef TSUKUYOMI_RT_SHADOW_CASTER_INCLUDED
#define TSUKUYOMI_RT_SHADOW_CASTER_INCLUDED

// Include the material's existing Input.hlsl first to preserve UnityPerMaterial.
#include "UnityRayTracingMeshUtils.cginc"
#include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Ray Traced Shadow/RTShadowCommon.hlsl"

// InstanceID is assigned by TsukuyomiRayTracingScene; bit 0 overrides material
// face culling for Renderers whose ShadowCastingMode is TwoSided.

struct TsukuyomiRTShadowAttributes
{
    float2 barycentrics;
};

bool TsukuyomiRTShadowCullsFace()
{
    if ((InstanceID() & 1u) != 0u)
        return false;

    // DXR triangle hit kinds: front = 0xfe, back = 0xff.
    bool frontFace = HitKind() == 0xfeu;
    return (_Cull > 1.5 && !frontFace) || (_Cull > 0.5 && _Cull < 1.5 && frontFace);
}

float2 TsukuyomiRTShadowUV(TsukuyomiRTShadowAttributes attributes)
{
    uint3 indices = UnityRayTracingFetchTriangleIndices(PrimitiveIndex());
    float2 uv0 = UnityRayTracingFetchVertexAttribute2(indices.x, kVertexAttributeTexCoord0);
    float2 uv1 = UnityRayTracingFetchVertexAttribute2(indices.y, kVertexAttributeTexCoord0);
    float2 uv2 = UnityRayTracingFetchVertexAttribute2(indices.z, kVertexAttributeTexCoord0);
    float3 barycentrics = float3(1.0 - attributes.barycentrics.x - attributes.barycentrics.y,
        attributes.barycentrics);
    float2 uv = uv0 * barycentrics.x + uv1 * barycentrics.y + uv2 * barycentrics.z;
    return uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
}

[shader("anyhit")]
void TsukuyomiRTShadowAnyHit(inout TsukuyomiRTShadowPayload payload : SV_RayPayload,
    TsukuyomiRTShadowAttributes attributes : SV_IntersectionAttributes)
{
#if defined(_SURFACE_TYPE_TRANSPARENT)
    IgnoreHit();
    return;
#else
    if (TsukuyomiRTShadowCullsFace())
    {
        IgnoreHit();
        return;
    }

#if defined(_ALPHATEST_ON)
    // Hit shaders have no pixel derivatives; sample the alpha mask explicitly.
    float alpha = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap,
        TsukuyomiRTShadowUV(attributes), 0).a * _BaseColor.a;
    if (alpha < _Cutoff)
    {
        IgnoreHit();
        return;
    }
#endif

    payload.visibility = 0.0;
    payload.hitDistance = RayTCurrent();
    AcceptHitAndEndSearch();
#endif
}

[shader("closesthit")]
void TsukuyomiRTShadowClosestHit(inout TsukuyomiRTShadowPayload payload : SV_RayPayload,
    TsukuyomiRTShadowAttributes attributes : SV_IntersectionAttributes)
{
    // Also set the payload here: it must remain valid when traversal selects
    // this hit group without executing AnyHit for an opaque geometry.
    payload.visibility = 0.0;
    payload.hitDistance = RayTCurrent();
}

#endif
