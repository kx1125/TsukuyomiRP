#ifndef TSUKUYOMI_STUDIO_HAIR_LIGHTING_INCLUDED
#define TSUKUYOMI_STUDIO_HAIR_LIGHTING_INCLUDED

// Shader Forge/hair_07 response recovered from the archived StudioCharacter.
// Light lookup, cookies, layers, distance attenuation and raster/RT shadows use
// TsukuyomiRP. No source light buffers, attenuation LUT or shadow bias are used.
float StudioHairPow5(float x) { float x2=x*x; return x2*x2*x; }
float StudioHairMax3(float3 v) { return max(v.x,max(v.y,v.z)); }

struct StudioHairSurface
{
    float3 strand;
    float3 occlusion;
    float gradient;
};

StudioHairSurface PrepareStudioHairSurface(float2 uv)
{
    uv=uv*_HairSourceMap_ST.xy+_HairSourceMap_ST.zw;
    float3 tex=SAMPLE_TEXTURE2D(_HairSourceMap,sampler_HairSourceMap,uv).rgb;
    float3 grade=SAMPLE_TEXTURE2D(_HairGradientMap,sampler_HairGradientMap,uv).rgb;
    float root=pow(saturate(grade.b),_HairRootPower);
    float tip=pow(saturate(grade.g),_HairTipPower);
    float3 baseColor=_HairBaseColor.rgb;
    float3 tint=lerp(lerp(baseColor,_HairRootColor.rgb,root),_HairTipColor.rgb,tip);
    float3 strand=lerp(baseColor*.745283,baseColor+float3(.084,.049,.049),tex.b);
    strand=lerp(strand,baseColor+float3(.070588,.066353,.066353),tex.g);
    StudioHairSurface surface;
    surface.gradient=root+tip;
    surface.strand=lerp(strand,tint,surface.gradient);
    surface.occlusion=SAMPLE_TEXTURE2D(_HairOcclusionMap,sampler_HairOcclusionMap,uv).rgb;
    return surface;
}

void AccumulateStudioHairLight(inout TsukuyomiOpenPBRResponse result, Light light,
    InputData inputData, StudioHairSurface surface, bool basePass)
{
    float3 N=inputData.normalWS,V=inputData.viewDirectionWS,L=SafeNormalize(light.direction);
    float3 H=SafeNormalize(L+V),R=reflect(-V,N);
    float nvRaw=dot(N,V),nv=abs(nvRaw),nlRaw=dot(N,L),nl=max(nlRaw,0);
    float lh=saturate(dot(L,H)),nh=saturate(dot(N,H));
    float smoothness=max(.8*_HairWetness,_HairSmoothness)*saturate(2*max(nvRaw,0)-.7);
    float roughness=1-smoothness,alpha=roughness*roughness,alpha2=alpha*alpha;
    float metallic=max(.5*_HairWetness,_HairMetallic)*saturate(2*max(nvRaw,0)-.1);
    float3 baseColor=_HairBaseColor.rgb;
    float power=max(16*_HairWetness,max(2.7,1+_HairRingPower+
        max(2*(StudioHairMax3(_HairSpecularColor.rgb)-StudioHairMax3(baseColor)),0)-4*StudioHairMax3(baseColor)));
    float rim=pow(max(1-nvRaw,0),max(8*_HairWetness,_HairRimPower));
    float viewRing=pow(1-min(abs(V.y-R.y),1),power);
    float halfRing=pow(1-min(abs(H.y-N.y),1),power);
    float ringMask=1-_HairRingSuppression;
    float3 rimRing=saturate(2*rim+viewRing*ringMask).xxx;
    float3 specTint=saturate(.7*min(StudioHairMax3(baseColor+.55),1)*_HairSpecularColor.rgb);
    float3 custom=rimRing*specTint+.2*_HairWetness*saturate(viewRing*ringMask+.4*halfRing*ringMask);
    float3 color=saturate(1-(1-custom)*(1-baseColor*.745283))+surface.occlusion*surface.strand;
    color=saturate(.24*lerp(color,surface.strand,surface.gradient));
    float3 f0=lerp(.04.xxx,color,metallic),diffuseColor=color*.96*(1-metallic);
    float visibility=.5/(nl*(nv*(1-alpha)+alpha)+nv*(nl*(1-alpha)+alpha)+.00001);
    float d=nh*nh*(alpha2-1)+1;
    float spec=max(alpha2*visibility/(d*d+1e-7)*saturate(nl),0);
    float3 radiance=light.color*light.distanceAttenuation*light.shadowAttenuation;
#if !defined(_SPECULARHIGHLIGHTS_OFF)
    result.specular+=spec*lerp(f0,1,StudioHairPow5(1-lh))*radiance;
#endif
    float fd90=2*lh*lh*roughness-.5;
    float disney=(1+fd90*StudioHairPow5(1-nl))*(1+fd90*StudioHairPow5(1-nv))*nl;
    result.diffuse+=(nl+max(-nlRaw,0)*light.shadowAttenuation+disney)*diffuseColor*radiance;
    if(basePass)
    {
        result.diffuse+=2*(inputData.bakedGI+.1)*diffuseColor*_IndirectDiffuseIntensity;
#if !defined(_ENVIRONMENTREFLECTIONS_OFF)
        float3 reflection=TSUKUYOMI_OPENPBR_SAMPLE_ENVIRONMENT(R,inputData.positionWS,roughness,1,inputData.normalizedScreenSpaceUV);
        float grazing=saturate(smoothness+1-.96*(1-metallic));
        result.specular+=(reflection+rimRing)*lerp(f0,grazing.xxx,StudioHairPow5(1-nv))/(1+alpha2)*_IndirectSpecularIntensity;
#endif
    }
}

float3 StudioHairLighting(InputData inputData, float2 uv)
{
    StudioHairSurface surface=PrepareStudioHairSurface(uv);
    TsukuyomiOpenPBRResponse result=(TsukuyomiOpenPBRResponse)0;
    half4 shadowMask=CalculateShadowMask(inputData);
    uint layers=GetMeshRenderingLayer();
    Light main=TsuOPBRGetMainLight(inputData,shadowMask);
    MixRealtimeAndBakedGI(main,inputData.normalWS,inputData.bakedGI);
#if defined(_LIGHT_LAYERS)
    if(!IsMatchingLightLayer(main.layerMask,layers)) main.color=0;
#endif
    AccumulateStudioHairLight(result,main,inputData,surface,true);
#if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount=GetAdditionalLightsCount();
#if USE_CLUSTER_LIGHT_LOOP
    [loop] for(uint lightIndex=0;lightIndex<min(URP_FP_DIRECTIONAL_LIGHTS_COUNT,MAX_VISIBLE_LIGHTS);++lightIndex)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        Light light=TsuOPBRGetAdditionalLight(lightIndex,inputData,shadowMask);
#if defined(_LIGHT_LAYERS)
        if(IsMatchingLightLayer(light.layerMask,layers))
#endif
            AccumulateStudioHairLight(result,light,inputData,surface,false);
    }
#endif
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light=TsuOPBRGetAdditionalLight(lightIndex,inputData,shadowMask);
#if defined(_LIGHT_LAYERS)
        if(IsMatchingLightLayer(light.layerMask,layers))
#endif
            AccumulateStudioHairLight(result,light,inputData,surface,false);
    LIGHT_LOOP_END
#endif
#if defined(_ADDITIONAL_LIGHTS_VERTEX)
    result.diffuse+=inputData.vertexLighting*surface.strand*.96*(1-_HairMetallic);
#endif
    return (result.diffuse+result.specular)*_BaseColor.rgb;
}

#endif
