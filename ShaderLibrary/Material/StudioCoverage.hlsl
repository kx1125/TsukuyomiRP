#ifndef TSUKUYOMI_STUDIO_COVERAGE_INCLUDED
#define TSUKUYOMI_STUDIO_COVERAGE_INCLUDED

float StudioBayer4(float2 screenPixel)
{
    const float bayer[16]={1,9,3,11,13,5,15,7,4,12,2,10,16,8,14,6};
    uint2 p=(uint2)floor(screenPixel)%4;
    return bayer[p.x+4*p.y]/16;
}

void StudioHairCoverage(half alpha,float2 screenPixel)
{
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
    float coverage=alpha*saturate(2*_BaseColor.a);
    clip(max(coverage,step(StudioBayer4(screenPixel),coverage))-_Cutoff);
    return alpha;
}

void StudioAITCoverage(float2 uv,float2 screenPixel)
{
    float4 main=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv);
    float4 mask=SAMPLE_TEXTURE2D(_SourceGlossMap,sampler_SourceGlossMap,uv);
    float pattern=StudioBayer4(screenPixel);
    float alpha=main.a*step(pattern,max(mask.a,1-_WetEffect));
    if(_AITDither>.5) alpha=step(pattern,alpha);
    float tearing=(min(pow(mask.b,12*(1-_AITAlphaEx)),1)-.03)*1.030928;
    clip(min(alpha,tearing)-_Cutoff);
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

#endif
