#ifndef TSUKUYOMI_STUDIO_SKIN_LAYERS_INCLUDED
#define TSUKUYOMI_STUDIO_SKIN_LAYERS_INCLUDED

// Per-fragment mesh attributes used by the source body overlay layers.
static float2 StudioOverlayUV = 0;
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


half3 StudioSubsurfaceSample(float2 uv)
{
    half4 sub = SAMPLE_TEXTURE2D(_SubsurfaceAlbedo, sampler_LinearRepeat, uv);
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
    half vein=SAMPLE_TEXTURE2D(_SubsurfaceAlbedo,sampler_LinearRepeat,uv).a*_VeinOpacity;
    sub=lerp(sub,_VeinColor.rgb,vein);
    float regionShade=SAMPLE_TEXTURE2D(_BodyRegionMap,sampler_BodyRegionMap,StudioBodyRegionUV()).r;
    return lerp(sub,_BodyRegionSubColor.rgb*regionShade,StudioBodyRegionWeight()*_BodyRegionSubColor.a);
}

#endif
