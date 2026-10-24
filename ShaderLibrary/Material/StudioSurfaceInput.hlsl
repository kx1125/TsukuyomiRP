#ifndef TSUKUYOMI_STUDIO_SURFACE_INPUT_INCLUDED
#define TSUKUYOMI_STUDIO_SURFACE_INPUT_INCLUDED

#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Material/StudioMaterialData.hlsl"

bool IsStudioHair() { return _SourceMode>2.5 && _SourceMode<3.5; }
bool IsStudioEye() { return _SourceMode>3.5 && _SourceMode<4.5; }
bool IsStudioAITCloth() {
#if defined(_STUDIO_AIT_CLOTH)
    return true;
#else
    return false;
#endif
}
#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Material/StudioCoverage.hlsl"
float2 StudioAITDetailUV(float2 uv,float4 layout,float rotation)
{
    float2 p=uv*_AITPattern.xy*layout.xy+layout.zw-.5;
    float si,co;sincos(rotation*PI,si,co);
    return float2(co*p.x+si*p.y,-si*p.x+co*p.y)+.5;
}
void StudioAITSurface(float2 uv,out SurfaceData surface)
{
    surface=(SurfaceData)0;
    float2 uv1=StudioAITDetailUV(uv,_AITDetailUV1,_AITRotation.x);
    float2 uv2=StudioAITDetailUV(uv,_AITDetailUV2,_AITRotation.y);
    float3 detail1=SAMPLE_TEXTURE2D(_PoreMap,sampler_BumpMap,uv1).rgb;
    float3 detail2=SAMPLE_TEXTURE2D(_AITDetailMap2,sampler_BumpMap,uv2).rgb;
    float2 detailMask=SAMPLE_TEXTURE2D(_ClothColorMask,sampler_BumpMap,uv).rg;
    float2 height1=float2(SAMPLE_TEXTURE2D(_PoreMap,sampler_BumpMap,uv1+float2(.0027,0)).g,
        SAMPLE_TEXTURE2D(_PoreMap,sampler_BumpMap,uv1+float2(0,.0027)).g);
    float2 height2=float2(SAMPLE_TEXTURE2D(_AITDetailMap2,sampler_BumpMap,uv2+float2(.0027,0)).g,
        SAMPLE_TEXTURE2D(_AITDetailMap2,sampler_BumpMap,uv2+float2(0,.0027)).g);
    float3 normal1=normalize(float3((detail1.g-height1)*2*detailMask.r*_AITDetail1.x,1));
    float3 normal2=normalize(float3((detail2.g-height2)*2*detailMask.g*_AITDetail2.x,1));
    float3 normal=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,uv),_BumpScale);
    normal.z=sqrt(1-saturate(dot(normal.xy,normal.xy)));
    normal=normalize(float3(normal.xy+normal1.xy,normal.z*normal1.z));
    surface.normalTS=normalize(float3(normal.xy+normal2.xy,normal.z*normal2.z));

    float4 main=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv);
    float4 occ=SAMPLE_TEXTURE2D(_SourceGlossMap,sampler_SourceGlossMap,uv);
    float4 packed=SAMPLE_TEXTURE2D(_ClothPackedMap,sampler_SourceGlossMap,uv);
    float4 local=SAMPLE_TEXTURE2D(_SourceDetailMap,sampler_BaseMap,uv*_SourceDetailMap_ST.xy+_SourceDetailMap_ST.zw);
    surface.albedo=saturate(main.rgb*_BaseColor.rgb*(1-.2*_WetEffect)+occ.r*_AITCurvature);
    surface.occlusion=saturate(1-_AITOcclusion+occ.g)
        *max(min(detail1.g,.75)*(4.0/3),1-detailMask.r*_AITDetail1.w)
        *max(min(detail2.g,.75)*(4.0/3),1-detailMask.g*_AITDetail2.w);
    surface.metallic=saturate(detail1.r-detailMask.r*_AITDetail1.z+1)
        *saturate(detail2.r-detailMask.g*_AITDetail2.z+1)*packed.r*local.r;
    float smooth=saturate(detail1.b-detailMask.r*_AITDetail1.y+1)
        *saturate(detail2.b-detailMask.g*_AITDetail2.y+1)*packed.a*local.b;
    float weatherAngle=(1-SAMPLE_TEXTURE2D(_AITWeatherMask,sampler_SourceGlossMap,uv).a)*PI;
    float2 weatherUV=uv*_AITPattern.xy*_AITWeatherUV.xy+_AITWeatherUV.zw;
    float si,co;sincos(weatherAngle,si,co);
    weatherUV=float2(co*weatherUV.x+si*weatherUV.y,-si*weatherUV.x+co*weatherUV.y);
    float wetGloss=(.9*SAMPLE_TEXTURE2D(_WetMap,sampler_SourceGlossMap,weatherUV).b+.6)*_WetEffect;
    surface.smoothness=max(wetGloss,smooth);
    surface.alpha=1;
}
half4 SampleSpecularPBRMask(float2 uv)
{
    half4 result = SAMPLE_TEXTURE2D(_PBRMask, sampler_PBRMask, uv);
    if (_SourceMode>4.5)
    {
        half3 region=SAMPLE_TEXTURE2D(_ClothColorMask,sampler_BaseMap,uv).rgb;
        half4 weights=half4(saturate(1-region.r-region.g-region.b),region);
        half3 packed=SAMPLE_TEXTURE2D(_ClothPackedMap,sampler_BaseMap,uv).rgb;
        half4 smooth=_ClothGloss*lerp(1.0h.xxxx,packed.rrrr,_ClothGlossWeight);
        half4 metal=_ClothMetal*lerp(1.0h.xxxx,packed.bbbb,_ClothMetalWeight);
        result = half4(1,1-saturate(dot(smooth,weights)),saturate(dot(metal,weights)),0);
    }
    return result;
}
half3 SampleSpecularPBRNormal(float2 uv)
{
    if(IsStudioAITCloth()) { SurfaceData cloth; StudioAITSurface(uv,cloth); return cloth.normalTS; }
    half3 n = half3(0,0,1);
#if defined(_NORMALMAP)
    n = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv*max(_BumpTile,0.0001)), _BumpScale);
    if(_SourceMode>4.5) n.z=sqrt(1-saturate(dot(n.xy,n.xy)));
#endif
#if defined(ENABLE_DETAIL_NORMALMAP)
    half3 detail = UnpackNormalScale(SAMPLE_TEXTURE2D(_DetailNormalMap, sampler_BumpMap,
        uv*max(_DetailNormalMapTile,0.0001)),_DetailNormalMapScale);
    n = BlendNormalRNM(n,detail);
#endif
    if (_SourceMode>2.5 && _SourceMode<3.5)
    {
        half3 fine=UnpackNormal(SAMPLE_TEXTURE2D(_HairNoiseMap,sampler_HairNoiseMap,uv*16*_HairNoiseMap_ST.xy+_HairNoiseMap_ST.zw));
        n=normalize(lerp(n,fine,_HairNoiseStrength));
    }
    return normalize(n);
}
void InitializeTsukuyomiSpecularPBRSurfaceData(float2 uv, out SurfaceData s)
{
    if(IsStudioAITCloth()) { StudioAITSurface(uv,s); return; }
    s=(SurfaceData)0;
    half4 base=SampleAlbedoAlpha(uv,TEXTURE2D_ARGS(_BaseMap,sampler_BaseMap));
    half4 mask=SampleSpecularPBRMask(uv);
    // Alpha() discards fragments. HLSL's ternary can evaluate that side effect
    // even for hair, whose coverage was already handled by the source dither.
    if(_SourceMode>4.5) s.alpha=1;
    else if(IsStudioHair()||_StudioLashes>.5) s.alpha=base.a*_Opacity;
    else s.alpha=Alpha(base.a,_BaseColor,_Cutoff)*_Opacity;
    s.albedo=base.rgb*_BaseColor.rgb;

    if (_SourceMode>4.5)
    {
        half3 region=SAMPLE_TEXTURE2D(_ClothColorMask,sampler_BaseMap,uv).rgb;
        half3 tint=_BaseColor.rgb*saturate(1-region.r-region.g-region.b)+_ClothColor2.rgb*region.r+_ClothColor3.rgb*region.g+_ClothColor4.rgb*region.b;
        s.albedo=base.rgb*_ClothBaseTint.rgb*tint;
    }
    s.metallic=(_SourceMode>4.5)?mask.b:saturate(mask.b*_Metallic);
    s.specular=saturate(mask.r).xxx;
    s.smoothness=1-SpecularPBRRoughness(mask.g,_Roughness);
    s.normalTS=SampleSpecularPBRNormal(uv);
    s.occlusion=SAMPLE_TEXTURE2D(_OcclusionMap,sampler_OcclusionMap,uv).r;
    if (_SourceMode>2.5 && _SourceMode<3.5)
    {
        half4 grade=SAMPLE_TEXTURE2D(_HairGradeMap,sampler_LinearRepeat,uv);
        // Source gradient uses B for roots, G for tips. Main RGB is strand
        // modulation, not a grayscale diffuse map. Its final color scale is .24.
        half root=pow(saturate(grade.b),max(_HairTopPower,0.001));
        half tip=pow(saturate(grade.g),max(_HairTipPower,0.001));
        half3 tint=lerp(_BaseColor.rgb,_HairTopColor.rgb,root);
        tint=lerp(tint,_HairTipColor.rgb,tip);
        half3 strand=lerp(_BaseColor.rgb*.745283h,_BaseColor.rgb+half3(.084,.049,.049),base.b);
        strand=lerp(strand,_BaseColor.rgb+half3(.070588,.066353,.066353),base.g);
        s.albedo=saturate(lerp(strand,tint,saturate(root+tip)))*.24h;
        s.smoothness=max(_SourceGloss,.8h*_WetEffect);
    }
    if (_SourceMode>3.5 && _SourceMode<4.5)
    {
        // Iris and pupil colors are decoded before the shared OpenPBR evaluation.
        s.normalTS=UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,uv));
        s.normalTS.xy*=3*_HighlightColor.a;
        s.normalTS.z=sqrt(1-saturate(dot(s.normalTS.xy,s.normalTS.xy)));
        s.metallic=0;
        s.smoothness=_SourceGloss;
    }
#if defined(_EMISSION)
    s.emission=SAMPLE_TEXTURE2D(_EmissionMap,sampler_EmissionMap,uv).rgb*_EmissionColor.rgb;
#endif
}

// Eye adapter only decodes textures; all light response comes from OpenPBR.
void StudioApplyEyeSurface(float2 uv,float3 viewTS,inout SurfaceData surface)
{
    float2 offset=_IrisUV.zw+_EyeBaseOffset.xy;
    float2 layout=offset+float2(.5-.5*_IrisUV.x,(_IrisUV.y-1)*(-.44));
    float height=(min(2*length(uv+layout-.5),.5)-.5)*_EyeParallax;
    viewTS=normalize(viewTS);
    float2 parallax=height*viewTS.xy/(viewTS.z+.42);
    float3 iris=SAMPLE_TEXTURE2D(_IrisMap,sampler_IrisMap,uv*_IrisUV.xy+layout+parallax).rgb;
    float2 pupilScale=_IrisUV.xy*_PupilUV.xy*_PupilUV.z;
    float2 pupilUV=uv*pupilScale-.5*(pupilScale-1)+offset*_PupilUV.z+3*parallax;
    float4 pupil=SAMPLE_TEXTURE2D(_PupilMap,sampler_IrisMap,pupilUV);
    surface.albedo=lerp(surface.albedo,iris.r*_IrisColor.rgb,iris.b*_IrisColor.a);
    surface.albedo=lerp(surface.albedo,_PupilColor.rgb,pupil.a*_PupilColor.a);
    surface.occlusion=saturate(1-_EyeOcclusionStrength+surface.occlusion);
    surface.emission+=iris.r*iris.r*_IrisColor.rgb*iris.b*_IrisColor.a*_EyeEmission*12;
}

#endif
