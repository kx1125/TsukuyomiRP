#ifndef TSUKUYOMI_SPECULAR_PBR_INPUT_INCLUDED
#define TSUKUYOMI_SPECULAR_PBR_INPUT_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/SurfaceType.hlsl"
#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Material/TsukuyomiSpecularPBRMath.hlsl"

// Same layout for every pass and local keyword variant.
CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
float4 _BaseMap_TexelSize;
float4 _BaseColor;
float4 _EmissionColor;
float4 _OcclusionColor;
float4 _TransmissionColor;
float _MaterialType;
float _Cutoff;
float _Roughness;
float _Metallic;
float _BumpScale;
float _BumpTile;
float _DetailNormalMapScale;
float _DetailNormalMapTile;
float TransmissionOcc;
float TransmissionShadows;
float TransmissionRange;
float DynamicPassTransmission;
float _MicroShadowOpacity;
float _IndirectDiffuseIntensity;
float _IndirectSpecularIntensity;
float _HorizonOcclusionPower;
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
float _Cull;

float _SourceMode, _SourceGloss, _SourceGlossWeight, _PoreStrength;
float _StudioSssOccluder;
float4 _AITPattern, _AITDetailUV1, _AITDetailUV2, _AITRotation, _AITDetail1, _AITDetail2, _AITWeatherUV, _AITTransmission;
float4 _SourceDetailMap_ST;
float _AITDither, _AITAlphaEx, _AITCurvature, _AITOcclusion, _AITTransShadow;
float4 _BodyRegionColor, _BodyRegionSubColor, _NailColor;
float _BodyRegionScale, _BodyRegionGloss, _BodyRegionBump;
float _NailGloss, _SubsurfaceWeight, _SubsurfaceSaturation, _VeinOpacity;
float _HairTopPower, _HairTipPower, _Opacity;
float _WetEffect, _WetSkinGloss, _WetStreakGloss, _WetStreakBump, _WetRegion, _HairAnisotropy;
float _WetGlossLevel, _WetSplatOpacity;
float _WetDropsAmount, _WetDropsScale, _WetDropOpacity, _WetDropBump;
float4 _WetVoronoi;
float _HairNoiseStrength, _FaceBlushStrength;
float4 _FaceBrowColor, _FaceBlushColor;
float4 _FaceBrowUV;
float _FaceBrowRotation, _FaceBrowGloss;
float _StudioEyeShadow, _StudioEyeShadowScale, _StudioLashes, _StudioLashCutout;
float4 _ClothBaseTint, _ClothColor2, _ClothColor3, _ClothColor4;
float4 _ClothGloss, _ClothGlossWeight, _ClothMetal, _ClothMetalWeight;
float4 _WetMap_TexelSize;
float4 _PoreUV, _PoreMap_TexelSize, _VeinColor;
float4 _StudioSubColor, _StudioSubAdjust, _StudioTransColor, _StudioTransParams;
float _StudioTransShadow, _StudioTransAmbient, _StudioAlbedoGain, _StudioUnitsPerWorldUnit;
float _StudioPunctualShadowBias;
float _StudioSpotShadowNear;
float4 _HairTopColor, _HairTipColor, _HairSpecular, _HairRingParams, _HairNoiseMap_ST;
float4 _IrisColor, _PupilColor, _HighlightColor, _IrisUV, _PupilUV;
float4 _EyeBaseOffset, _EyeHighlightUV, _EyeTransmission;
float _EyeParallax, _EyeHighlightRotation, _EyeEmission, _EyeShininess, _EyeOcclusionStrength, _EyeTransShadow;
CBUFFER_END

#ifdef UNITY_DOTS_INSTANCING_ENABLED
UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
    UNITY_DOTS_INSTANCED_PROP(float4, _BaseColor)
    UNITY_DOTS_INSTANCED_PROP(float4, _EmissionColor)
    UNITY_DOTS_INSTANCED_PROP(float4, _OcclusionColor)
    UNITY_DOTS_INSTANCED_PROP(float4, _TransmissionColor)
    UNITY_DOTS_INSTANCED_PROP(float, _MaterialType)
    UNITY_DOTS_INSTANCED_PROP(float, _Cutoff)
    UNITY_DOTS_INSTANCED_PROP(float, _Roughness)
    UNITY_DOTS_INSTANCED_PROP(float, _Metallic)
    UNITY_DOTS_INSTANCED_PROP(float, _BumpScale)
    UNITY_DOTS_INSTANCED_PROP(float, _BumpTile)
    UNITY_DOTS_INSTANCED_PROP(float, _DetailNormalMapScale)
    UNITY_DOTS_INSTANCED_PROP(float, _DetailNormalMapTile)
    UNITY_DOTS_INSTANCED_PROP(float, TransmissionOcc)
    UNITY_DOTS_INSTANCED_PROP(float, TransmissionShadows)
    UNITY_DOTS_INSTANCED_PROP(float, TransmissionRange)
    UNITY_DOTS_INSTANCED_PROP(float, DynamicPassTransmission)
    UNITY_DOTS_INSTANCED_PROP(float, _MicroShadowOpacity)
    UNITY_DOTS_INSTANCED_PROP(float, _IndirectDiffuseIntensity)
    UNITY_DOTS_INSTANCED_PROP(float, _IndirectSpecularIntensity)
    UNITY_DOTS_INSTANCED_PROP(float, _HorizonOcclusionPower)
UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)
static float4 specularPBR_BaseColor;
static float4 specularPBR_EmissionColor;
static float4 specularPBR_OcclusionColor;
static float4 specularPBR_TransmissionColor;
static float specularPBR_MaterialType;
static float specularPBR_Cutoff;
static float specularPBR_Roughness;
static float specularPBR_Metallic;
static float specularPBR_BumpScale;
static float specularPBR_BumpTile;
static float specularPBR_DetailNormalMapScale;
static float specularPBR_DetailNormalMapTile;
static float specularPBRTransmissionOcc;
static float specularPBRTransmissionShadows;
static float specularPBRTransmissionRange;
static float specularPBRDynamicPassTransmission;
static float specularPBR_MicroShadowOpacity;
static float specularPBR_IndirectDiffuseIntensity;
static float specularPBR_IndirectSpecularIntensity;
static float specularPBR_HorizonOcclusionPower;
void SetupSpecularPBRMaterialPropertyCaches()
{
    specularPBR_BaseColor = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _BaseColor);
    specularPBR_EmissionColor = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _EmissionColor);
    specularPBR_OcclusionColor = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _OcclusionColor);
    specularPBR_TransmissionColor = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _TransmissionColor);
    specularPBR_MaterialType = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _MaterialType);
    specularPBR_Cutoff = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _Cutoff);
    specularPBR_Roughness = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _Roughness);
    specularPBR_Metallic = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _Metallic);
    specularPBR_BumpScale = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _BumpScale);
    specularPBR_BumpTile = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _BumpTile);
    specularPBR_DetailNormalMapScale = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _DetailNormalMapScale);
    specularPBR_DetailNormalMapTile = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _DetailNormalMapTile);
    specularPBRTransmissionOcc = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, TransmissionOcc);
    specularPBRTransmissionShadows = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, TransmissionShadows);
    specularPBRTransmissionRange = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, TransmissionRange);
    specularPBRDynamicPassTransmission = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, DynamicPassTransmission);
    specularPBR_MicroShadowOpacity = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _MicroShadowOpacity);
    specularPBR_IndirectDiffuseIntensity = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _IndirectDiffuseIntensity);
    specularPBR_IndirectSpecularIntensity = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _IndirectSpecularIntensity);
    specularPBR_HorizonOcclusionPower = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _HorizonOcclusionPower);
}
#undef UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES
#define UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES() SetupSpecularPBRMaterialPropertyCaches()
#define _BaseColor specularPBR_BaseColor
#define _EmissionColor specularPBR_EmissionColor
#define _OcclusionColor specularPBR_OcclusionColor
#define _TransmissionColor specularPBR_TransmissionColor
#define _MaterialType specularPBR_MaterialType
#define _Cutoff specularPBR_Cutoff
#define _Roughness specularPBR_Roughness
#define _Metallic specularPBR_Metallic
#define _BumpScale specularPBR_BumpScale
#define _BumpTile specularPBR_BumpTile
#define _DetailNormalMapScale specularPBR_DetailNormalMapScale
#define _DetailNormalMapTile specularPBR_DetailNormalMapTile
#define TransmissionOcc specularPBRTransmissionOcc
#define TransmissionShadows specularPBRTransmissionShadows
#define TransmissionRange specularPBRTransmissionRange
#define DynamicPassTransmission specularPBRDynamicPassTransmission
#define _MicroShadowOpacity specularPBR_MicroShadowOpacity
#define _IndirectDiffuseIntensity specularPBR_IndirectDiffuseIntensity
#define _IndirectSpecularIntensity specularPBR_IndirectSpecularIntensity
#define _HorizonOcclusionPower specularPBR_HorizonOcclusionPower
#endif

TEXTURE2D(_PBRMask); SAMPLER(sampler_PBRMask);
TEXTURE2D(_OcclusionMap); SAMPLER(sampler_OcclusionMap);
TEXTURE2D(_DetailNormalMap); SAMPLER(sampler_DetailNormalMap);
TEXTURE2D(_TransmissionMap); SAMPLER(sampler_TransmissionMap);


TEXTURE2D(_SourceDetailMap);
TEXTURE2D(_SourceGlossMap);
SAMPLER(sampler_SourceGlossMap);
TEXTURE2D(_RegionMask);
TEXTURE2D(_BodyRegionMap);
TEXTURE2D(_PoreMap);
TEXTURE2D(_SubsurfaceAlbedo);
TEXTURE2D(_HairGradeMap);
TEXTURE2D(_IrisMap);
SAMPLER(sampler_IrisMap);
TEXTURE2D(_PupilMap);
TEXTURE2D(_HighlightMap);
TEXTURE2D(_WetMap);
SAMPLER(sampler_PoreMap);
SAMPLER(sampler_BodyRegionMap);
#if defined(_STUDIO_SKIN)
// This sampler must survive variants that do not use the pore normal texture.
#define STUDIO_SKIN_MAP_SAMPLER sampler_LinearRepeat
#else
#define STUDIO_SKIN_MAP_SAMPLER sampler_BaseMap
#endif
SAMPLER(sampler_FaceBrowMap);
TEXTURE2D(_HairNoiseMap);
SAMPLER(sampler_HairNoiseMap);
TEXTURE2D(_FaceBrowMap);
TEXTURE2D(_ClothColorMask);
TEXTURE2D(_ClothPackedMap);
TEXTURE2D(_AITDetailMap2);
TEXTURE2D(_AITWeatherMask);

#if defined(_STUDIO_AIT_CLOTH)
bool IsStudioSkin() { return false; }
bool IsStudioEye() { return false; }
bool IsStudioHair() { return false; }
bool IsStudioAITCloth() { return true; }
#elif defined(_STUDIO_SKIN)
bool IsStudioSkin() { return true; }
bool IsStudioEye() { return false; }
bool IsStudioHair() { return false; }
bool IsStudioAITCloth() { return false; }
#else
bool IsStudioSkin() { return false; }
bool IsStudioEye() { return _SourceMode > 3.5 && _SourceMode < 4.5; }
bool IsStudioHair() { return _SourceMode > 2.5 && _SourceMode < 3.5; }
bool IsStudioAITCloth() { return false; }
#endif
float2 _StudioHairCoverageOffset;
void StudioHairCoverage(half alpha,float2 screenPixel)
{
    screenPixel+=_StudioHairCoverageOffset;
    float2 parity=fmod(floor(screenPixel),2);
    float pattern=lerp(lerp(.2,.6,parity.y),lerp(.8,.4,parity.y),parity.x);
    clip(alpha+pattern-1);
}
void StudioClothCoverage(float2 uv,float2 screenPixel)
{
    float4 tex=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv);
    float3 region=SAMPLE_TEXTURE2D(_ClothColorMask,sampler_BaseMap,uv).rgb;
    float alpha=tex.a*dot(float4(1-region.r-region.g-region.b,region),float4(_BaseColor.a,_ClothColor2.a,_ClothColor3.a,_ClothColor4.a));
    const float bayer[64]={1,49,13,61,4,52,16,64,33,17,45,29,36,20,48,32,9,57,5,53,12,60,8,56,41,25,37,21,44,28,40,24,3,51,15,63,2,50,14,62,35,19,47,31,34,18,46,30,11,59,7,55,10,58,6,54,43,27,39,23,42,26,38,22};
    uint2 p=(uint2)floor(screenPixel)%8;
    clip(step(bayer[p.x+8*p.y]/64,alpha)-_Cutoff);
}
float StudioLashAlpha(float mask,float2 screenPixel)
{
    float alpha=min(pow(mask,_StudioLashCutout+2*(1-_BaseColor.a)),1);
    const float bayer[16]={1,9,3,11,13,5,15,7,4,12,2,10,16,8,14,6};
    uint2 p=(uint2)floor(screenPixel)%4;
    float coverage=alpha*saturate(2*_BaseColor.a);
    clip(max(coverage,step(bayer[p.x+4*p.y]/16,coverage))-_Cutoff);
    return alpha;
}
// AIT/Clothes True, reconstructed from the captured D3D11 programs.
// These source materials have zero weathering, emission and bitangent rim.
// ExGloss still controls wet coverage and the weather map's blue gloss channel.
static float StudioAITTransmissionMask=0;
static float StudioAITFrontFace=1;

float2 StudioAITDetailUV(float2 uv,float4 layout,float rotation)
{
    float2 p=uv*_AITPattern.xy*layout.xy+layout.zw-.5;
    float si,co;sincos(rotation*PI,si,co);
    return float2(co*p.x+si*p.y,-si*p.x+co*p.y)+.5;
}

void StudioAITCoverage(float2 uv,float2 screenPixel)
{
    float4 main=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv);
    float4 mask=SAMPLE_TEXTURE2D(_SourceGlossMap,sampler_SourceGlossMap,uv);
    const float bayer[16]={1,9,3,11,13,5,15,7,4,12,2,10,16,8,14,6};
    uint2 pixel=(uint2)floor(screenPixel)%4;
    float pattern=bayer[pixel.x+4*pixel.y]/16;
    float alpha=main.a*step(pattern,max(mask.a,1-_WetEffect));
    if(_AITDither>.5) alpha=step(pattern,alpha);
    float tearing=(min(pow(mask.b,12*(1-_AITAlphaEx)),1)-.03)*1.030928;
    clip(min(alpha,tearing)-_Cutoff);
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
    StudioAITTransmissionMask=packed.b*StudioAITFrontFace;
}

void StudioDepthAlpha(float2 uv,float2 screenPixel)
{
    half alpha=SampleAlbedoAlpha(uv,TEXTURE2D_ARGS(_BaseMap,sampler_BaseMap)).a;
    if(IsStudioHair()) StudioHairCoverage(alpha,screenPixel);
    else if(IsStudioAITCloth()) StudioAITCoverage(uv,screenPixel);
    else if(_SourceMode>4.5) StudioClothCoverage(uv,screenPixel);
    else if(_StudioLashes>.5) StudioLashAlpha(alpha,screenPixel);
    else Alpha(alpha,_BaseColor,_Cutoff);
}

// Per-fragment mesh attributes used by the source body overlay layers.
static float2 StudioOverlayUV = 0;
static float3 StudioViewTS = float3(0,0,1);
static float4 StudioVertexColor = 1;
float2 StudioBrowUV()
{
    float2 p=StudioOverlayUV+_FaceBrowUV.xy-float2(.3,.4);
    float si,co;sincos(_FaceBrowRotation*PI,si,co);
    return saturate((float2(co*p.x+si*p.y,-si*p.x+co*p.y)+float2(.2,-.03))*_FaceBrowUV.zw+float2(.1,.43));
}
float StudioBrowWeight()
{
    return (_SourceMode>.5 && _SourceMode<1.5) ? SAMPLE_TEXTURE2D(_FaceBrowMap,sampler_BaseMap,StudioBrowUV()).b*(1-StudioVertexColor.b) : 0;
}
float2 StudioBodyRegionUV() { return saturate((StudioOverlayUV-.5)*(4-3.2*_BodyRegionScale)+.5); }
float StudioBodyRegionWeight() {
    return (_SourceMode>1.5 && _SourceMode<2.5) ?
        SAMPLE_TEXTURE2D(_BodyRegionMap,sampler_BodyRegionMap,StudioBodyRegionUV()).b*_BodyRegionColor.a*(1-StudioVertexColor.g) : 0;
}

// Procedural drop height recovered from the source skin programs.
float4 StudioPermute(float4 x) { return fmod(((x*34)+1)*x,289); }
float StudioDropNoise(float3 v)
{
    float3 i=floor(v+dot(v,1.0/3.0));
    float3 x0=v-i+dot(i,1.0/6.0);
    float3 g=step(x0.yzx,x0.xyz),l=1-g;
    float3 i1=min(g,l.zxy),i2=max(g,l.zxy);
    float3 x1=x0-i1+1.0/6.0,x2=x0-i2+1.0/3.0,x3=x0-.5;
    i=i-floor(i/289)*289;
    float4 p=StudioPermute(StudioPermute(StudioPermute(i.z+float4(0,i1.z,i2.z,1))+i.y+float4(0,i1.y,i2.y,1))+i.x+float4(0,i1.x,i2.x,1));
    float4 j=p-49*floor(p/49);
    float4 x_=floor(j/7),y_=floor(j-7*x_);
    float4 x=(2*x_+.5)/7-1,y=(2*y_+.5)/7-1,h=1-abs(x)-abs(y);
    float4 b0=float4(x.xy,y.xy),b1=float4(x.zw,y.zw);
    float4 s0=floor(b0)*2+1,s1=floor(b1)*2+1,sh=-step(h,0);
    float4 a0=b0.xzyw+s0.xzyw*sh.xxyy,a1=b1.xzyw+s1.xzyw*sh.zzww;
    float3 p0=float3(a0.xy,h.x),p1=float3(a0.zw,h.y),p2=float3(a1.xy,h.z),p3=float3(a1.zw,h.w);
    float4 norm=1.79284291400159-.85373472095314*float4(dot(p0,p0),dot(p1,p1),dot(p2,p2),dot(p3,p3));
    p0*=norm.x;p1*=norm.y;p2*=norm.z;p3*=norm.w;
    float4 m=max(.6-float4(dot(x0,x0),dot(x1,x1),dot(x2,x2),dot(x3,x3)),0);m*=m;
    return .5+21*dot(m*m,float4(dot(p0,x0),dot(p1,x1),dot(p2,x2),dot(p3,x3)));
}
float StudioDropVoronoi(float2 uv)
{
    float2 cell=floor(uv*_WetVoronoi.z),p=frac(uv*_WetVoronoi.z);
    float nearest=8;
    [unroll] for(int y=-1;y<=1;y++) [unroll] for(int x=-1;x<=1;x++)
    {
        float2 q=cell+float2(x,y);
        float2 hash=frac(sin(float2(dot(q,float2(127.1,311.7)),dot(q,float2(269.5,183.3))))*43758.546875);
        float2 cellPoint=.5+.5*sin(_WetVoronoi.y*PI+6.2831*hash);
        float2 delta=p-float2(x,y)-cellPoint;
        nearest=min(nearest,.5*dot(delta,delta));
    }
    return saturate(1-_WetVoronoi.x/max(nearest,.0000001));
}
float StudioDropHeight(float2 uv,float voronoi)
{
    float noise=StudioDropNoise(float3(uv*_WetDropsScale,0));
    float ratio=_WetDropsAmount/max(noise,.000001);
    float value=saturate((ratio*ratio-_WetDropsAmount)/max(1-_WetDropsAmount,.000001));
    return value*value*(3-2*value)*voronoi;
}
static float StudioDropCoverage=0,StudioDropBaseHeight=0,StudioDropVoronoiMask=0;


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
    if (!IsStudioSkin()) return result;
    half4 detail = SAMPLE_TEXTURE2D(_SourceDetailMap, sampler_BaseMap, uv);
    half4 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
    half glossMap = SAMPLE_TEXTURE2D(_SourceGlossMap, sampler_BaseMap, uv).r;
    half face = _SourceMode < 1.5 ? 1 : 0;
    half smoothness = (_SourceGloss + face * 0.1h) * lerp(1.0h, glossMap, _SourceGlossWeight);
    half localGloss = lerp(base.a * detail.b, saturate(detail.b * detail.a), face);
    smoothness = lerp(smoothness, 1.0h, localGloss);
    half nail = SAMPLE_TEXTURE2D(_RegionMask, STUDIO_SKIN_MAP_SAMPLER, uv).r * (1.0h - face);
    smoothness = lerp(smoothness, _NailGloss, nail);
    smoothness = lerp(smoothness, _BodyRegionGloss, StudioBodyRegionWeight());
    // The eyebrow gloss is applied before the wet layers, without color alpha.
    smoothness = lerp(smoothness, _FaceBrowGloss, StudioBrowWeight());
    half3 wet = SAMPLE_TEXTURE2D(_WetMap,sampler_BaseMap,uv).rgb;
    half region = SAMPLE_TEXTURE2D(_RegionMask,STUDIO_SKIN_MAP_SAMPLER,uv).g;
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

half3 StudioSubsurfaceSample(float2 uv)
{
    half4 sub = SAMPLE_TEXTURE2D(_SubsurfaceAlbedo, STUDIO_SKIN_MAP_SAMPLER, uv);
    float si,co;sincos(_StudioSubAdjust.x*TWO_PI,si,co);
    float3 axis=float3(.577350269,.577350269,.577350269);
    sub.rgb=sub.rgb*co+cross(axis,sub.rgb)*si+axis*dot(axis,sub.rgb)*(1-co);
    sub.rgb=(sub.rgb-.5)*(2*_StudioSubAdjust.y)+.5+2*_StudioSubAdjust.z-1;
    half luminance=dot(sub.rgb,half3(.39,.59,.11));
    return lerp(luminance.xxx,sub.rgb,2*_StudioSubAdjust.w);
}

half3 StudioDiffusionTint(float2 uv)
{
    half3 sub=lerp(_StudioSubColor.rgb,StudioSubsurfaceSample(uv),_SubsurfaceWeight);
    half vein=SAMPLE_TEXTURE2D(_SubsurfaceAlbedo,STUDIO_SKIN_MAP_SAMPLER,uv).a*_VeinOpacity;
    sub=lerp(sub,_VeinColor.rgb,vein);
    float regionShade=SAMPLE_TEXTURE2D(_BodyRegionMap,sampler_BodyRegionMap,StudioBodyRegionUV()).r;
    return lerp(sub,_BodyRegionSubColor.rgb*regionShade,StudioBodyRegionWeight()*_BodyRegionSubColor.a);
}

half3 SampleSpecularPBRNormal(float2 uv)
{
    if(IsStudioAITCloth()) { SurfaceData cloth; StudioAITSurface(uv,cloth); return cloth.normalTS; }
    half3 n = half3(0,0,1);
#if defined(_NORMALMAP)
    n = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv*max(_BumpTile,0.0001)), _BumpScale);
    if(IsStudioSkin()||_SourceMode>4.5) n.z=sqrt(1-saturate(dot(n.xy,n.xy)));
#endif
#if defined(ENABLE_DETAIL_NORMALMAP)
    half3 detail = UnpackNormalScale(SAMPLE_TEXTURE2D(_DetailNormalMap, sampler_BumpMap,
        uv*max(_DetailNormalMapTile,0.0001)),_DetailNormalMapScale);
    if(IsStudioSkin()) {
        detail.z=sqrt(1-saturate(dot(detail.xy,detail.xy)));
        n=normalize(float3(n.xy+detail.xy,n.z*detail.z));
    } else n = BlendNormalRNM(n,detail);
#endif
    if (_SourceMode>2.5 && _SourceMode<3.5)
    {
        half3 fine=UnpackNormal(SAMPLE_TEXTURE2D(_HairNoiseMap,sampler_HairNoiseMap,uv*16*_HairNoiseMap_ST.xy+_HairNoiseMap_ST.zw));
        n=normalize(lerp(n,fine,_HairNoiseStrength));
    }
#if !defined(STUDIO_GEOMETRIC_SKIN_DEPTH)
    if (IsStudioSkin() && _PoreStrength > 0.0)
    {
        float2 p = uv*_PoreUV.xy+_PoreUV.zw;
        float2 stepUV = _SourceMode<1.5 ? .009 : .00625;
        float h = SAMPLE_TEXTURE2D(_PoreMap,sampler_PoreMap,p).a;
        float hx = SAMPLE_TEXTURE2D(_PoreMap,sampler_PoreMap,p+float2(stepUV.x,0)).a;
        float hy = SAMPLE_TEXTURE2D(_PoreMap,sampler_PoreMap,p+float2(0,stepUV.y)).a;
        float region = SAMPLE_TEXTURE2D(_RegionMask,STUDIO_SKIN_MAP_SAMPLER,uv).g;
        float poreWeight=1-StudioBodyRegionWeight();
        poreWeight*=1-StudioBrowWeight();
        float2 slope = float2(h-hx,h-hy)*_PoreStrength*region*poreWeight;
        float3 pore=normalize(float3(slope,1));
        n=normalize(float3(n.xy+pore.xy,n.z*pore.z));
    }
    if (IsStudioSkin() && _SourceMode>1.5 && _SourceMode<2.5)
    {
        float2 p=StudioBodyRegionUV();
        float h=SAMPLE_TEXTURE2D(_BodyRegionMap,sampler_BodyRegionMap,p).g;
        float hx=SAMPLE_TEXTURE2D(_BodyRegionMap,sampler_BodyRegionMap,p+float2(.00625,0)).g;
        float hy=SAMPLE_TEXTURE2D(_BodyRegionMap,sampler_BodyRegionMap,p+float2(0,.00625)).g;
        float3 overlay=normalize(float3(float2(h-hx,h-hy)*_BodyRegionBump*StudioVertexColor.r,1));
        n=lerp(n,overlay,saturate(StudioBodyRegionWeight()));
    }
    if (IsStudioSkin() && _WetEffect>0.0)
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
    if(IsStudioAITCloth()) { StudioAITSurface(uv,s); return; }
    s=(SurfaceData)0;
    half4 base=SampleAlbedoAlpha(uv,TEXTURE2D_ARGS(_BaseMap,sampler_BaseMap));
    half4 mask=SampleSpecularPBRMask(uv);
    // Alpha() discards fragments. HLSL's ternary can evaluate that side effect
    // even for hair, whose coverage was already handled by the source dither.
    if(IsStudioSkin()||_SourceMode>4.5) s.alpha=1;
    else if(IsStudioHair()||_StudioLashes>.5) s.alpha=base.a*_Opacity;
    else s.alpha=Alpha(base.a,_BaseColor,_Cutoff)*_Opacity;
    s.albedo=base.rgb*_BaseColor.rgb;
    if (_SourceMode>1.5 && _SourceMode<2.5)
    {
        float nail=SAMPLE_TEXTURE2D(_RegionMask,STUDIO_SKIN_MAP_SAMPLER,uv).r*_NailColor.a;
        float3 body=saturate(lerp(base.rgb,base.rgb*_NailColor.rgb,nail));
        float shade=SAMPLE_TEXTURE2D(_BodyRegionMap,sampler_BodyRegionMap,StudioBodyRegionUV()).r;
        s.albedo=lerp(body,_BodyRegionColor.rgb*shade,StudioBodyRegionWeight())*_BaseColor.rgb;
    }
    if (_SourceMode>4.5)
    {
        half3 region=SAMPLE_TEXTURE2D(_ClothColorMask,sampler_BaseMap,uv).rgb;
        half3 tint=_BaseColor.rgb*saturate(1-region.r-region.g-region.b)+_ClothColor2.rgb*region.r+_ClothColor3.rgb*region.g+_ClothColor4.rgb*region.b;
        s.albedo=base.rgb*_ClothBaseTint.rgb*tint;
    }
    s.metallic=(IsStudioSkin()||_SourceMode>4.5)?mask.b:saturate(mask.b*_Metallic);
    s.specular=saturate(mask.r).xxx;
    s.smoothness=IsStudioSkin()?1-mask.g:1-SpecularPBRRoughness(mask.g,_Roughness);
    s.normalTS=SampleSpecularPBRNormal(uv);
    s.occlusion=IsStudioSkin()?1:SAMPLE_TEXTURE2D(_OcclusionMap,sampler_OcclusionMap,uv).r;
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
        // Eye color is decoded per light by StudioEyeLighting, as in the original shader.
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
#endif
