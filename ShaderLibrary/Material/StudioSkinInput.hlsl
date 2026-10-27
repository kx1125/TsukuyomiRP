#ifndef TSUKUYOMI_STUDIO_SKIN_INPUT_INCLUDED
#define TSUKUYOMI_STUDIO_SKIN_INPUT_INCLUDED

#ifndef _STUDIO_SKIN
#define _STUDIO_SKIN 1
#endif
#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Material/StudioMaterialData.hlsl"
#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Material/StudioSkinLayers.hlsl"
half4 SampleSpecularPBRMask(float2 uv)
{
    half4 result = SAMPLE_TEXTURE2D(_PBRMask, sampler_PBRMask, uv);

    half4 detail = SAMPLE_TEXTURE2D(_SourceDetailMap, sampler_BaseMap, uv);
    half4 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
    half glossMap = SAMPLE_TEXTURE2D(_SourceGlossMap, sampler_BaseMap, uv).r;
    half face = _SourceMode < 1.5 ? 1 : 0;
    half smoothness = (_SourceGloss + face * 0.1h) * lerp(1.0h, glossMap, _SourceGlossWeight);
    half localGloss = lerp(base.a * detail.b, saturate(detail.b * detail.a), face);
    smoothness = lerp(smoothness, 1.0h, localGloss);
    half nail = SAMPLE_TEXTURE2D(_RegionMask, sampler_LinearRepeat, uv).r * (1.0h - face);
    smoothness = lerp(smoothness, _NailGloss, nail);
    smoothness = lerp(smoothness, _BodyRegionGloss, StudioBodyRegionWeight());
    // The eyebrow gloss is applied before the wet layers, without color alpha.
    smoothness = lerp(smoothness, _FaceBrowGloss, StudioBrowWeight());
    half3 wet = SAMPLE_TEXTURE2D(_WetMap,sampler_BaseMap,uv).rgb;
    half region = SAMPLE_TEXTURE2D(_RegionMask,sampler_LinearRepeat,uv).g;
    half gate = lerp(1.0h,_WetRegion,wet.g)*saturate(_WetEffect);
    // Original skin shader adds only half the broad wetness value. Streak
    // coverage then interpolates to a separate gloss level, capped at 0.83.
    smoothness = saturate(smoothness + 0.5h*_WetEffect*_WetSkinGloss*region);
    half streak = saturate((gate*wet.r*_WetStreakGloss-(1-_WetEffect))/max(_WetEffect,0.0001h));
    streak = streak*streak*(3-2*streak);
    // Original streak/splat coverage can exceed one. Clamp the resulting
    // smoothness, rather than clamping coverage before the interpolation.
    StudioDropCoverage=0;
    if(_WetDropsAmount>0 && _WetEffect>0)
    {
        StudioDropVoronoiMask=StudioDropVoronoi(uv);
        StudioDropBaseHeight=StudioDropHeight(uv,StudioDropVoronoiMask);
        float value=saturate((gate*region*StudioDropBaseHeight*_WetDropOpacity-(1-_WetEffect))/max(_WetEffect,.000001));
        StudioDropCoverage=value*value*(3-2*value);
    }
    half coverage = streak + StudioDropCoverage + wet.b*_WetEffect*_WetSplatOpacity;
    smoothness = saturate(lerp(smoothness,min(_WetGlossLevel,0.83h),coverage));
    // Main texture alpha is a material mask, never skin transparency.
    result = half4(1, 1-saturate(smoothness), saturate(base.a*detail.r*detail.a), result.a);
    return result;
}
half3 SampleSpecularPBRNormal(float2 uv)
{
    half3 n = half3(0,0,1);
#if defined(_NORMALMAP)
    n = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv*max(_BumpTile,0.0001)), _BumpScale);
    n.z=sqrt(1-saturate(dot(n.xy,n.xy)));
#endif
#if defined(ENABLE_DETAIL_NORMALMAP)
    half3 detail = UnpackNormalScale(SAMPLE_TEXTURE2D(_DetailNormalMap, sampler_BumpMap,
        uv*max(_DetailNormalMapTile,0.0001)),_DetailNormalMapScale);
    detail.z=sqrt(1-saturate(dot(detail.xy,detail.xy)));
    n=normalize(float3(n.xy+detail.xy,n.z*detail.z));
#endif

#if !defined(STUDIO_GEOMETRIC_SKIN_DEPTH)
    if (_PoreStrength > 0.0)
    {
        float2 p = uv*_PoreUV.xy+_PoreUV.zw;
        float2 stepUV = _SourceMode<1.5 ? .009 : .00625;
        float h = SAMPLE_TEXTURE2D(_PoreMap,sampler_PoreMap,p).a;
        float hx = SAMPLE_TEXTURE2D(_PoreMap,sampler_PoreMap,p+float2(stepUV.x,0)).a;
        float hy = SAMPLE_TEXTURE2D(_PoreMap,sampler_PoreMap,p+float2(0,stepUV.y)).a;
        float region = SAMPLE_TEXTURE2D(_RegionMask,sampler_LinearRepeat,uv).g;
        float poreWeight=1-StudioBodyRegionWeight();
        poreWeight*=1-StudioBrowWeight();
        float2 slope = float2(h-hx,h-hy)*_PoreStrength*region*poreWeight;
        float3 pore=normalize(float3(slope,1));
        n=normalize(float3(n.xy+pore.xy,n.z*pore.z));
    }
    if (_SourceMode>1.5 && _SourceMode<2.5)
    {
        float2 p=StudioBodyRegionUV();
        float h=SAMPLE_TEXTURE2D(_BodyRegionMap,sampler_BodyRegionMap,p).g;
        float hx=SAMPLE_TEXTURE2D(_BodyRegionMap,sampler_BodyRegionMap,p+float2(.00625,0)).g;
        float hy=SAMPLE_TEXTURE2D(_BodyRegionMap,sampler_BodyRegionMap,p+float2(0,.00625)).g;
        float3 overlay=normalize(float3(float2(h-hx,h-hy)*_BodyRegionBump*StudioVertexColor.r,1));
        n=lerp(n,overlay,saturate(StudioBodyRegionWeight()));
    }
    if (_WetEffect>0.0)
    {
        float2 texel=pow(max(_WetStreakBump,0.00001),3.4)*.5;
        float3 wet=SAMPLE_TEXTURE2D(_WetMap,sampler_BaseMap,uv).rgb;
        float h=wet.r;
        float hx=SAMPLE_TEXTURE2D(_WetMap,sampler_BaseMap,uv+float2(texel.x,0)).r;
        float hy=SAMPLE_TEXTURE2D(_WetMap,sampler_BaseMap,uv+float2(0,texel.y)).r;
        float gate=lerp(1,_WetRegion,wet.g)*ceil(_WetEffect);
        float streak=saturate((gate*wet.r*_WetStreakGloss-(1-_WetEffect))/max(_WetEffect,.0001));
        streak=streak*streak*(3-2*streak);
        n=lerp(n,normalize(float3(float2(h-hx,h-hy)*gate,1)),streak);
        if(StudioDropCoverage>0)
        {
            float dropStep=.4*pow(max(_WetDropBump,.000001),_SourceMode<1.5?5.64:6.83);
            float dx=StudioDropHeight(uv+float2(dropStep,0),StudioDropVoronoiMask);
            float dy=StudioDropHeight(uv+float2(0,dropStep),StudioDropVoronoiMask);
            float3 dropNormal=normalize(float3((StudioDropBaseHeight-float2(dx,dy))*gate,1));
            n=lerp(n,dropNormal,StudioDropCoverage);
        }
    }
    if (_SourceMode>.5 && _SourceMode<1.5)
    {
        float2 p=StudioBrowUV();
        float h=SAMPLE_TEXTURE2D(_FaceBrowMap,sampler_FaceBrowMap,p).g;
        float hx=SAMPLE_TEXTURE2D(_FaceBrowMap,sampler_FaceBrowMap,p+float2(.0008,0)).g;
        float hy=SAMPLE_TEXTURE2D(_FaceBrowMap,sampler_FaceBrowMap,p+float2(0,.0008)).g;
        n=lerp(n,normalize(float3(float2(h-hx,h-hy)*5,1)),StudioBrowWeight()*_FaceBrowColor.a);
    }
#endif // STUDIO_GEOMETRIC_SKIN_DEPTH
    return normalize(n);
}
void InitializeTsukuyomiSpecularPBRSurfaceData(float2 uv, out SurfaceData s)
{
    s=(SurfaceData)0;
    half4 base=SampleAlbedoAlpha(uv,TEXTURE2D_ARGS(_BaseMap,sampler_BaseMap));
    half4 mask=SampleSpecularPBRMask(uv);
    s.alpha=1;
    s.albedo=base.rgb*_BaseColor.rgb;
    if (_SourceMode>1.5 && _SourceMode<2.5)
    {
        float nail=SAMPLE_TEXTURE2D(_RegionMask,sampler_LinearRepeat,uv).r*_NailColor.a;
        float3 body=saturate(lerp(base.rgb,base.rgb*_NailColor.rgb,nail));
        float shade=SAMPLE_TEXTURE2D(_BodyRegionMap,sampler_BodyRegionMap,StudioBodyRegionUV()).r;
        s.albedo=lerp(body,_BodyRegionColor.rgb*shade,StudioBodyRegionWeight())*_BaseColor.rgb;
    }

    s.metallic=mask.b;
    s.specular=saturate(mask.r).xxx;
    s.smoothness=1-mask.g;
    s.normalTS=SampleSpecularPBRNormal(uv);
    s.occlusion=1;


#if defined(_EMISSION)
    s.emission=SAMPLE_TEXTURE2D(_EmissionMap,sampler_EmissionMap,uv).rgb*_EmissionColor.rgb;
#endif
}

#endif
