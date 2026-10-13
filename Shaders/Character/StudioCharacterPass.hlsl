#ifndef TSUKUYOMI_SPECULAR_PBR_PASS_INCLUDED
#define TSUKUYOMI_SPECULAR_PBR_PASS_INCLUDED

#include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Character/StudioCharacterInput.hlsl"
#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Lighting/TsukuyomiSpecularPBRLighting.hlsl"
#include "Packages/tsukuyomi.render-pipelines.universal/Shaders/SSGI/TsukuyomiScreenSpaceGlobalIllumination.hlsl"

#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

struct TsukuyomiSpecularPBRAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 texcoord : TEXCOORD0;
    half4 vertexColor : COLOR;
    float2 staticLightmapUV : TEXCOORD1;
    float2 dynamicLightmapUV : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct TsukuyomiSpecularPBRVaryings
{
    float2 uv : TEXCOORD0;
    float4 studioOverlay : TEXCOORD7;
    float3 studioVertexColor : TEXCOORD11;
    float3 studioBitangentWS : TEXCOORD12;
    float3 studioAmbientL2 : TEXCOORD13;
    float3 positionWS : TEXCOORD1;
    half3 normalWS : TEXCOORD2;
    half4 tangentWS : TEXCOORD3;

#ifdef _ADDITIONAL_LIGHTS_VERTEX
    half4 fogFactorAndVertexLight : TEXCOORD5;
#else
    half fogFactor : TEXCOORD5;
#endif

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord : TEXCOORD6;
#endif

    TSUKUYOMI_DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 8);

#ifdef DYNAMICLIGHTMAP_ON
    float2 dynamicLightmapUV : TEXCOORD9;
#endif

#ifdef USE_APV_PROBE_OCCLUSION
    float4 probeOcclusion : TEXCOORD10;
#endif

    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};


void TsukuyomiSpecularPBRInitializeInputData(TsukuyomiSpecularPBRVaryings input, half3 normalTS, out InputData inputData)
{
    inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
    inputData.positionCS = input.positionCS;

    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

#if defined(_NORMALMAP) || defined(ENABLE_DETAIL_NORMALMAP)
    float tangentSign = input.tangentWS.w;
    float3 bitangent = input.studioBitangentWS;
    half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz);
    inputData.tangentToWorld = tangentToWorld;
    inputData.normalWS = TransformTangentToWorld(normalTS, tangentToWorld);
#else
    inputData.normalWS = input.normalWS;
#endif

    inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
    inputData.viewDirectionWS = viewDirWS;
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    inputData.shadowCoord = input.shadowCoord;
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
#else
    inputData.shadowCoord = float4(0, 0, 0, 0);
#endif

#ifdef _ADDITIONAL_LIGHTS_VERTEX
    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactorAndVertexLight.x);
    inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
#else
    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
#endif

    inputData.normalizedScreenSpaceUV = SpecularPBRScreenUV(input.positionCS.xy);

#if defined(DEBUG_DISPLAY)
    #if defined(DYNAMICLIGHTMAP_ON)
    inputData.dynamicLightmapUV = input.dynamicLightmapUV;
    #endif
    #if defined(LIGHTMAP_ON)
    inputData.staticLightmapUV = input.staticLightmapUV;
    #else
    inputData.vertexSH = input.vertexSH;
    #endif
    #if defined(USE_APV_PROBE_OCCLUSION)
    inputData.probeOcclusion = input.probeOcclusion;
    #endif
#endif
}

void TsukuyomiSpecularPBRInitializeBakedGIData(TsukuyomiSpecularPBRVaryings input, inout InputData inputData)
{
#if defined(_TSUKUYOMI_SCREEN_SPACE_GLOBAL_ILLUMINATION) && !defined(LIGHTMAP_ON) && !defined(DYNAMICLIGHTMAP_ON)
    // Preserve probe occlusion for realtime shadow mixing before SSGI replaces indirect diffuse.
    #if defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2)
    inputData.bakedGI = SAMPLE_GI(input.vertexSH,
        GetAbsolutePositionWS(inputData.positionWS),
        inputData.normalWS,
        inputData.viewDirectionWS,
        input.positionCS.xy,
        input.probeOcclusion,
        inputData.shadowMask);
    #else
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
    #endif
    inputData.bakedGI = SampleTsukuyomiScreenSpaceGlobalIllumination(inputData.normalizedScreenSpaceUV);
#elif defined(_SCREEN_SPACE_IRRADIANCE)
    inputData.bakedGI = SAMPLE_GI(_ScreenSpaceIrradiance, input.positionCS.xy);
#elif defined(DYNAMICLIGHTMAP_ON)
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.dynamicLightmapUV, input.vertexSH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
#elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
    inputData.bakedGI = SAMPLE_GI(input.vertexSH,
        GetAbsolutePositionWS(inputData.positionWS),
        inputData.normalWS,
        inputData.viewDirectionWS,
        input.positionCS.xy,
        input.probeOcclusion,
        inputData.shadowMask);
#else
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
#endif
}

TsukuyomiSpecularPBRVaryings TsukuyomiSpecularPBRForwardVertex(TsukuyomiSpecularPBRAttributes input)
{
    TsukuyomiSpecularPBRVaryings output = (TsukuyomiSpecularPBRVaryings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    half fogFactor = 0.0h;

#if !defined(_FOG_FRAGMENT)
    fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
#endif

    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    output.studioOverlay = float4(input.staticLightmapUV, input.vertexColor.b, 0);
    output.studioVertexColor = input.vertexColor.rgb;
    output.positionWS = vertexInput.positionWS;
    output.normalWS = normalInput.normalWS;
    output.studioAmbientL2=SHEvalLinearL2(normalize(normalInput.normalWS),unity_SHBr,unity_SHBg,unity_SHBb,unity_SHC);

    real sign = input.tangentOS.w * GetOddNegativeScale();
    output.tangentWS = half4(normalize(normalInput.tangentWS.xyz), sign);
    output.studioBitangentWS=sign*cross(normalize(normalInput.normalWS),normalize(normalInput.tangentWS));
    if(IsStudioHair()) output.studioBitangentWS=normalize(output.studioBitangentWS);

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    output.shadowCoord = GetShadowCoord(vertexInput);
#endif

    TSUKUYOMI_OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);

#ifdef DYNAMICLIGHTMAP_ON
    output.dynamicLightmapUV = input.dynamicLightmapUV.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
#endif

    TSUKUYOMI_OUTPUT_SH4(vertexInput.positionWS, output.normalWS.xyz, GetWorldSpaceNormalizeViewDir(vertexInput.positionWS), output.vertexSH, output.probeOcclusion);

#ifdef _ADDITIONAL_LIGHTS_VERTEX
    output.fogFactorAndVertexLight = half4(fogFactor, SpecularPBRVertexLighting(vertexInput.positionWS, normalInput.normalWS));
#else
    output.fogFactor = fogFactor;
#endif

    output.positionCS = vertexInput.positionCS;
    return output;
}


#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Lighting/TsukuyomiSpecularPBRSSS.hlsl"
float _StudioDebugOutput;

// Captured Built-in screen-shadow selection and filtering for the reference
// camera. All other cameras retain the normal URP shadow receiver.
float _StudioCapturedDirectionalShadowReady;
float4x4 _StudioDirectionalWorldToShadow[4];
float4 _StudioDirectionalSplitNear,_StudioDirectionalSplitFar;
float _StudioDirectionalShadowStrength;
float _StudioDirectionalScreenShadowReady;
TEXTURE2D_X(_StudioDirectionalScreenShadowTexture);
float StudioDirectionalShadowRaw(float fallback,float3 positionWS)
{
    if(_StudioCapturedDirectionalShadowReady>.5)
    {
        float depth=-TransformWorldToView(positionWS).z;
        float4 selected=step(_StudioDirectionalSplitNear,depth.xxxx)
            *(1-step(_StudioDirectionalSplitFar,depth.xxxx));
        if(selected.x+selected.y<=0)return 1;
        uint cascade=selected.y>.5?1:0;
        float3 coordinate=mul(_StudioDirectionalWorldToShadow[cascade],float4(positionWS,1)).xyz;
        real weights[16];real2 positions[16];
        SampleShadow_ComputeSamples_Tent_7x7(float4(1.0/4096,1.0/4096,4096,4096),coordinate.xy,weights,positions);
        float shadow=0;
        [unroll]for(int i=0;i<16;i++)
            shadow+=weights[i]*SAMPLE_TEXTURE2D_SHADOW(_MainLightShadowmapTexture,sampler_LinearClampCompare,float3(positions[i],coordinate.z));
        return lerp(1,shadow,_StudioDirectionalShadowStrength);
    }
    return fallback;
}
float StudioDirectionalShadow(float fallback,float3 positionWS)
{
#if !defined(_RECEIVE_SHADOWS_OFF)
    if(_StudioCapturedDirectionalShadowReady>.5)
    {
        if(_StudioDirectionalScreenShadowReady>.5)
        {
            float4 screen=ComputeScreenPos(TransformWorldToHClip(positionWS));
            return SAMPLE_TEXTURE2D_X_LOD(_StudioDirectionalScreenShadowTexture,sampler_LinearClamp,screen.xy/screen.w,0).r;
        }
        return StudioDirectionalShadowRaw(fallback,positionWS);
    }
#endif
    return fallback;
}

// Skin branches recovered from the captured Hanmen 3.96 D3D11 programs.
// URP supplies lights, shadows and probes; Tsukuyomi supplies the diffusion pass.
float StudioPow5(float x) {float x2=x*x;return x2*x2*x;}
static float3 StudioAmbientL2=0;
TEXTURE2D(_StudioLegacyLightAttenuation);
float _StudioLegacyLightAttenuationReady;

float StudioLegacyDistanceAttenuation(float rangeFraction)
{
    if(_StudioLegacyLightAttenuationReady>.5)
        return SAMPLE_TEXTURE2D(_StudioLegacyLightAttenuation,sampler_LinearClamp,float2(rangeFraction,.5)).r;
    return rcp(1+25*rangeFraction)*step(rangeFraction,1);
}

// Built-in D3D11 spot shadows use a 3x3 tent with four weighted comparison
// fetches. URP's Low setting uses four equally weighted offsets instead.
float StudioPunctualShadow(float fallback,uint index,float3 positionWS)
{
#if defined(ADDITIONAL_LIGHT_CALCULATE_SHADOWS)
    half4 parameters=GetAdditionalLightShadowParams(index);
    if(parameters.w>=0 && parameters.z<.5 && parameters.y>0)
    {
        int slice=(int)parameters.w;
#if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
        float4 shadow=mul(_AdditionalLightsWorldToShadow_SSBO[slice],float4(positionWS,1));
#else
        float4 shadow=mul(_AdditionalLightsWorldToShadow[slice],float4(positionWS,1));
#endif
        shadow.xyz/=shadow.w;
        if(BEYOND_SHADOW_FAR(shadow)) return 1;
        ShadowSamplingData sampling=GetAdditionalLightShadowSamplingData(index);
        real weights[4];real2 positions[4];
        SampleShadow_ComputeSamples_Tent_3x3(sampling.shadowmapSize,shadow.xy,weights,positions);
        float attenuation=0;
        [unroll] for(int tap=0;tap<4;tap++)
            attenuation+=weights[tap]*SAMPLE_TEXTURE2D_SHADOW(_AdditionalLightsShadowmapTexture,sampler_LinearClampCompare,float3(positions[tap],shadow.z));
        attenuation=lerp(1,attenuation,parameters.x);
        return lerp(attenuation,1,GetAdditionalLightShadowFade(positionWS));
    }
    if(parameters.w>=0 && parameters.z>.5)
    {
#if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
        float3 lightPosition=_AdditionalLightsBuffer[index].position.xyz;
#else
        float3 lightPosition=_AdditionalLightsPosition[index].xyz;
#endif
        float3 delta=positionWS-lightPosition;
        float major=max(max(abs(delta.x),abs(delta.y)),abs(delta.z));
        float units=max(_StudioUnitsPerWorldUnit,.0001);
        // Built-in cube shadows apply bias at the receiver, not the vertex.
        // Original GPU _LightProjectionParams.zw = (0.0015, 0.97).
        float compareDistance=max(major-_StudioPunctualShadowBias/units,.000001)*.97;
        static const float3 offsets[4]={float3(1,1,1),float3(-1,-1,1),float3(-1,1,-1),float3(1,-1,-1)};
        float attenuation=0;
        [unroll] for(int tap=0;tap<4;tap++)
        {
            // Shader Forge / Hanmen use four fixed 1/128 source-unit offsets.
            float3 direction=delta+offsets[tap]*(parameters.y>0 ? .0078125/units : 0);
            int slice=(int)parameters.w+CubeMapFaceID(direction);
            float tapMajor=max(max(abs(direction.x),abs(direction.y)),abs(direction.z));
            float3 samplePosition=lightPosition+direction*(compareDistance/max(tapMajor,.000001));
#if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
            float4 shadow=mul(_AdditionalLightsWorldToShadow_SSBO[slice],float4(samplePosition,1));
#else
            float4 shadow=mul(_AdditionalLightsWorldToShadow[slice],float4(samplePosition,1));
#endif
            shadow.xyz/=shadow.w;
            attenuation+=.25*SAMPLE_TEXTURE2D_SHADOW(_AdditionalLightsShadowmapTexture,sampler_LinearClampCompare,shadow.xyz);
        }
        attenuation=lerp(1,attenuation,parameters.x);
        return lerp(attenuation,1,GetAdditionalLightShadowFade(positionWS));
    }
#endif
    return fallback;
}

Light StudioLegacyAttenuation(Light light,uint index,float3 positionWS,bool diffusion)
{
#if !USE_CLUSTER_LIGHT_LOOP
    index=GetPerObjectLightIndex(index);
#endif
    light.shadowAttenuation=StudioPunctualShadow(light.shadowAttenuation,index,positionWS);
#if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
    float4 position=_AdditionalLightsBuffer[index].position;
    float4 attenuation=_AdditionalLightsBuffer[index].attenuation;
    float3 spotDirection=_AdditionalLightsBuffer[index].spotDirection.xyz;
#else
    float4 position=_AdditionalLightsPosition[index];
    float4 attenuation=_AdditionalLightsAttenuation[index];
    float3 spotDirection=_AdditionalLightsSpotDir[index].xyz;
#endif
    if(position.w>.5)
    {
        float3 delta=position.xyz-positionWS;
        float rangeFraction=dot(delta,delta)*attenuation.x;
        // Scene conversion bakes range^2/25 into punctual light intensity.
        light.distanceAttenuation=25*attenuation.x*StudioLegacyDistanceAttenuation(rangeFraction);
        light.distanceAttenuation*=AngleAttenuation(spotDirection,light.direction,attenuation.zw);
        // Hanmen's ForwardAdd specular program uses the unnormalized punctual
        // vector in NdotL, the half-vector and Fresnel. Restore source units.
        if(!diffusion) light.direction*=length(delta)*_StudioUnitsPerWorldUnit;
    }
    return light;
}

// AIT/Eye Translucency: source UV layout, parallax, layered iris and legacy BRDF.
struct StudioEyeSample
{
    float3 iris;
    float4 pupil;
    float2 highlight;
    float3 baseColor;
    float3 inverseNormalWS;
    float3 emission;
    float occlusion;
};
StudioEyeSample StudioSampleEye(InputData input,float2 uv)
{
    StudioEyeSample e=(StudioEyeSample)0;
    float2 offset=_IrisUV.zw+_EyeBaseOffset.xy;
    float2 layout=offset+float2(.5-.5*_IrisUV.x,(_IrisUV.y-1)*(-.44));
    float height=(min(2*length(uv+layout-.5),.5)-.5)*_EyeParallax;
    float3 viewTS=normalize(StudioViewTS);
    float2 parallax=height*viewTS.xy/(viewTS.z+.42);
    e.iris=SAMPLE_TEXTURE2D(_IrisMap,sampler_IrisMap,uv*_IrisUV.xy+layout+parallax).rgb;
    float2 pupilScale=_IrisUV.xy*_PupilUV.xy*_PupilUV.z;
    float2 pupilUV=uv*pupilScale-.5*(pupilScale-1)+offset*_PupilUV.z+3*parallax;
    e.pupil=SAMPLE_TEXTURE2D(_PupilMap,sampler_IrisMap,pupilUV);
    float si,co;sincos(_EyeHighlightRotation*PI,si,co);
    float2 hp=uv+_EyeHighlightUV.zw-.5;
    float2 highlightUV=(float2(co*hp.x+si*hp.y,-si*hp.x+co*hp.y)+.5)*_EyeHighlightUV.xy-.5*(_EyeHighlightUV.xy-1);
    e.highlight=SAMPLE_TEXTURE2D(_HighlightMap,sampler_IrisMap,highlightUV).rg;
    e.baseColor=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv).rgb*_BaseColor.rgb;
    float3 inverseNormal=UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,uv));
    inverseNormal.xy*=-3;inverseNormal.z=sqrt(1-saturate(dot(inverseNormal.xy,inverseNormal.xy)));
    e.inverseNormalWS=normalize(TransformTangentToWorld(inverseNormal,input.tangentToWorld));
    float3 emission=e.iris.r*_IrisColor.rgb*(e.iris.b*_IrisColor.a)*e.iris.r*_EyeEmission*12;
    e.emission=lerp(emission,e.pupil.r*_PupilColor.rgb,e.pupil.a*_PupilColor.a);
    e.occlusion=saturate(1-_EyeOcclusionStrength+SAMPLE_TEXTURE2D(_OcclusionMap,sampler_OcclusionMap,uv).r);
    return e;
}
void StudioEyeSurface(StudioEyeSample e,InputData input,Light light,out float3 albedo,out float3 transmission)
{
    float3 irisColor=e.iris.r*_IrisColor.rgb;
    float level=clamp(e.iris.r,.5,1);
    float3 bright=saturate(level>.5?irisColor/max(2*(1-level),.000001):1-(1-irisColor)*.5/level);
    float3 H=SafeNormalize(input.viewDirectionWS+light.direction);
    float intensity=(max(dot(e.inverseNormalWS,light.direction),0)+pow(max(dot(e.inverseNormalWS,H),0),_EyeShininess*128))*light.color.r;
    irisColor=lerp(irisColor,bright,saturate(intensity*intensity)*e.iris.g);
    albedo=lerp(e.baseColor,irisColor,e.iris.b*_IrisColor.a);
    albedo=lerp(albedo,_PupilColor.rgb,e.pupil.a*_PupilColor.a);
    albedo=lerp(albedo,_HighlightColor.rgb,e.highlight.r*_HighlightColor.a*_SourceGloss);
    transmission=albedo;
    albedo+=2*e.highlight.r*_HighlightColor.rgb*_HighlightColor.a*_SourceGloss;
}
// The iris uses the unattenuated source light color nonlinearly. Undo the
// scene's range-squared intensity conversion while preserving final radiance.
Light StudioEyeSourceLight(Light light,uint index)
{
#if !USE_CLUSTER_LIGHT_LOOP
    index=GetPerObjectLightIndex(index);
#endif
#if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
    float4 position=_AdditionalLightsBuffer[index].position;
    float4 attenuation=_AdditionalLightsBuffer[index].attenuation;
#else
    float4 position=_AdditionalLightsPosition[index];
    float4 attenuation=_AdditionalLightsAttenuation[index];
#endif
    if(position.w>.5)
    {
        float scale=25*attenuation.x;
        light.color*=scale;
        light.distanceAttenuation/=scale;
    }
    return light;
}
void StudioEyeLight(inout TsukuyomiOpenPBRResponse result,Light light,InputData input,StudioEyeSample e,float3 f0,float smoothness,bool basePass)
{
    float3 albedo,transmission;StudioEyeSurface(e,input,light,albedo,transmission);
    float3 N=input.normalWS,V=input.viewDirectionWS,L=light.direction,H=SafeNormalize(V+L);
    float nl=saturate(dot(N,L)),nv=abs(dot(N,V)),nh=saturate(dot(N,H)),lh=saturate(dot(L,H));
    float rough=1-smoothness,a=max(rough*rough,.002),a2=a*a;
    float fd90=2*lh*lh*rough-.5;
    float diffuse=(1+fd90*StudioPow5(1-nl))*(1+fd90*StudioPow5(1-nv))*nl;
    float visibility=.5/(nl*(nv*(1-a)+a)+nv*(nl*(1-a)+a)+.00001);
    float d=nh*nh*(a2-1)+1;
    float spec=a2*visibility/(d*d+1e-7)*nl;
    float3 radiance=light.color*light.distanceAttenuation*light.shadowAttenuation;
    float reflectivity=max(f0.r,max(f0.g,f0.b));
    result.diffuse+=albedo*(1-reflectivity)*radiance*diffuse;
    result.specular+=lerp(f0,1,StudioPow5(1-lh))*radiance*spec;
    float back=pow(saturate(dot(V,-(L+N*_EyeTransmission.x))),_EyeTransmission.y);
    result.diffuse+=transmission*albedo*light.color*light.distanceAttenuation
        *(basePass?lerp(1,light.shadowAttenuation,_EyeTransShadow):light.shadowAttenuation)*back*_EyeTransmission.z*_EyeTransmission.w;
}
TsukuyomiOpenPBRResponse StudioEyeLighting(InputData inputData,float2 uv)
{
    TsukuyomiOpenPBRResponse result=(TsukuyomiOpenPBRResponse)0;
    StudioEyeSample e=StudioSampleEye(inputData,uv);
    float3 f0=e.highlight.g*_HighlightColor.rgb*.3;
    float smoothness=e.highlight.g*_SourceGloss*.92*(.4+.6*_HighlightColor.r);
    half4 shadowMask=CalculateShadowMask(inputData);
    Light main=GetMainLight(inputData.shadowCoord,inputData.positionWS,shadowMask);
    main.shadowAttenuation=StudioDirectionalShadow(main.shadowAttenuation,inputData.positionWS);
    StudioEyeLight(result,main,inputData,e,f0,smoothness,true);
#if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount=GetAdditionalLightsCount();
#if USE_CLUSTER_LIGHT_LOOP
    [loop] for(uint lightIndex=0;lightIndex<min(URP_FP_DIRECTIONAL_LIGHTS_COUNT,MAX_VISIBLE_LIGHTS);++lightIndex)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        Light light=GetAdditionalLight(lightIndex,inputData.positionWS,shadowMask);
        light=StudioLegacyAttenuation(light,lightIndex,inputData.positionWS,true);
        light=StudioEyeSourceLight(light,lightIndex);
        StudioEyeLight(result,light,inputData,e,f0,smoothness,false);
    }
#endif
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light=GetAdditionalLight(lightIndex,inputData.positionWS,shadowMask);
        light=StudioLegacyAttenuation(light,lightIndex,inputData.positionWS,true);
        light=StudioEyeSourceLight(light,lightIndex);
        StudioEyeLight(result,light,inputData,e,f0,smoothness,false);
    LIGHT_LOOP_END
#endif
    float roughness=1-smoothness,alpha=max(roughness*roughness,.002);
    float reflectivity=max(f0.r,max(f0.g,f0.b));
    float3 fresnel=lerp(f0,saturate(smoothness+reflectivity).xxx,StudioPow5(1-abs(dot(inputData.normalWS,inputData.viewDirectionWS))));
    result.specular+=GlossyEnvironmentReflection(reflect(-inputData.viewDirectionWS,inputData.normalWS),inputData.positionWS,roughness,e.occlusion,inputData.normalizedScreenSpaceUV)*fresnel/(1+alpha*alpha);
    result.diffuse+=e.emission;
    return result;
}

// Recovered Shader Forge/hair_07 light response for the captured Studio material.
// The captured materials use ColorMask A, a white ring mask and a black MeshMask B.
float StudioMax3(float3 v) { return max(v.x,max(v.y,v.z)); }

float StudioHairSourceAttenuation(Light light,uint index)
{
#if !USE_CLUSTER_LIGHT_LOOP
    index=GetPerObjectLightIndex(index);
#endif
#if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
    float4 position=_AdditionalLightsBuffer[index].position;
    float4 attenuation=_AdditionalLightsBuffer[index].attenuation;
#else
    float4 position=_AdditionalLightsPosition[index];
    float4 attenuation=_AdditionalLightsAttenuation[index];
#endif
    return light.shadowAttenuation*(position.w>.5 ? light.distanceAttenuation/(25*attenuation.x) : 1);
}

void StudioHairLight(inout TsukuyomiOpenPBRResponse result,Light light,InputData inputData,
    float2 uv,bool basePass,float sourceAttenuation)
{
    float3 N=inputData.normalWS,V=inputData.viewDirectionWS,L=normalize(light.direction);
    float3 H=normalize(L+V),R=reflect(-V,N);
    float nvRaw=dot(N,V),nv=abs(nvRaw),nlRaw=dot(N,L),nl=max(nlRaw,0);
    float lh=saturate(dot(L,H)),nh=saturate(dot(N,H));
    float smoothness=max(.8*_WetEffect,_SourceGloss)*saturate(2*max(nvRaw,0)-.7);
    float roughness=1-smoothness,alpha=roughness*roughness,alpha2=alpha*alpha;
    float metallic=max(.5*_WetEffect,_Metallic)*saturate(2*max(nvRaw,0)-.1);
    float3 baseColor=_BaseColor.rgb;
    float power=max(16*_WetEffect,max(2.7,1+_HairRingParams.y+
        max(2*(StudioMax3(_HairSpecular.rgb)-StudioMax3(baseColor)),0)-4*StudioMax3(baseColor)));
    float rim=pow(max(1-nvRaw,0),max(8*_WetEffect,_HairRingParams.x));
    // The original view ring compares the view and reflection Y components.
    float viewRing=pow(1-min(abs(V.y-R.y),1),power);
    float halfRing=pow(1-min(abs(H.y-N.y),1),power);
    float ringMask=1-_HairRingParams.z;
    float3 rimRing=saturate(2*rim+viewRing*ringMask).xxx;
    float3 specTint=saturate(.7*min(StudioMax3(baseColor+.55),1)*_HairSpecular.rgb);
    float3 custom=rimRing*specTint+.2*_WetEffect*saturate(viewRing*ringMask+.4*halfRing*ringMask);

    float4 tex=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv);
    // Source gradient and base normal use Bilinear/anisotropy 1; strands use 6.
    // The source's normal-free shadow hair has a constant white gradient.
#if defined(_NORMALMAP)
    float3 grade=SAMPLE_TEXTURE2D(_HairGradeMap,sampler_BumpMap,uv).rgb;
#else
    float3 grade=SAMPLE_TEXTURE2D(_HairGradeMap,sampler_BaseMap,uv).rgb;
#endif
    float root=pow(saturate(grade.b),_HairTopPower),tip=pow(saturate(grade.g),_HairTipPower);
    float gradient=root+tip;
    float3 tint=lerp(lerp(baseColor,_HairTopColor.rgb,root),_HairTipColor.rgb,tip);
    float3 strand=lerp(baseColor*.745283,baseColor+float3(.084,.049,.049),tex.b);
    strand=lerp(strand,baseColor+float3(.070588,.066353,.066353),tex.g);
    strand=lerp(strand,tint,gradient);
    float3 occlusion=SAMPLE_TEXTURE2D(_OcclusionMap,sampler_OcclusionMap,uv).rgb;
    float3 color=saturate(1-(1-custom)*(1-baseColor*.745283))+occlusion*strand;
    color=saturate(.24*lerp(color,strand,gradient));
    float3 f0=lerp(.04.xxx,color,metallic),diffuseColor=color*.96*(1-metallic);
    float visibility=.5/(nl*(nv*(1-alpha)+alpha)+nv*(nl*(1-alpha)+alpha)+.00001);
    float d=nh*nh*(alpha2-1)+1;
    float spec=max(alpha2*visibility/(d*d+1e-7)*saturate(nl),0);
    float3 radiance=light.color*light.distanceAttenuation*light.shadowAttenuation;
    result.specular+=spec*lerp(f0,1,StudioPow5(1-lh))*radiance;
    float fd90=2*lh*lh*roughness-.5;
    float disney=(1+fd90*StudioPow5(1-nl))*(1+fd90*StudioPow5(1-nv))*nl;
    result.diffuse+=(nl+max(-nlRaw,0)*sourceAttenuation+disney)*diffuseColor*radiance;
    if(basePass)
    {
        result.diffuse+=2*(inputData.bakedGI+.1)*diffuseColor;
        float3 reflection=GlossyEnvironmentReflection(R,inputData.positionWS,roughness,1,inputData.normalizedScreenSpaceUV);
        float grazing=saturate(smoothness+1-.96*(1-metallic));
        result.specular+=(reflection+rimRing)*lerp(f0,grazing.xxx,StudioPow5(1-nv))/(1+alpha2);
    }
}

TsukuyomiOpenPBRResponse StudioHairLighting(InputData inputData,float2 uv)
{
    TsukuyomiOpenPBRResponse result=(TsukuyomiOpenPBRResponse)0;
    half4 shadowMask=CalculateShadowMask(inputData);
    Light main=GetMainLight(inputData.shadowCoord,inputData.positionWS,shadowMask);
    main.shadowAttenuation=StudioDirectionalShadow(main.shadowAttenuation,inputData.positionWS);
    StudioHairLight(result,main,inputData,uv,true,main.shadowAttenuation);
#if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount=GetAdditionalLightsCount();
#if USE_CLUSTER_LIGHT_LOOP
    [loop] for(uint lightIndex=0;lightIndex<min(URP_FP_DIRECTIONAL_LIGHTS_COUNT,MAX_VISIBLE_LIGHTS);++lightIndex)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        Light light=GetAdditionalLight(lightIndex,inputData.positionWS,shadowMask);
        light=StudioLegacyAttenuation(light,lightIndex,inputData.positionWS,true);
        StudioHairLight(result,light,inputData,uv,false,StudioHairSourceAttenuation(light,lightIndex));
    }
#endif
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light=GetAdditionalLight(lightIndex,inputData.positionWS,shadowMask);
        light=StudioLegacyAttenuation(light,lightIndex,inputData.positionWS,true);
        StudioHairLight(result,light,inputData,uv,false,StudioHairSourceAttenuation(light,lightIndex));
    LIGHT_LOOP_END
#endif
    return result;
}

float3 StudioLashLighting(InputData inputData,float3 albedo)
{
    half4 shadowMask=CalculateShadowMask(inputData);
    Light main=GetMainLight(inputData.shadowCoord,inputData.positionWS,shadowMask);
    main.shadowAttenuation=StudioDirectionalShadow(main.shadowAttenuation,inputData.positionWS);
    float3 lightSum=inputData.bakedGI+main.color*main.distanceAttenuation*main.shadowAttenuation*max(dot(inputData.normalWS,main.direction),0);
#if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount=GetAdditionalLightsCount();
#if USE_CLUSTER_LIGHT_LOOP
    [loop] for(uint lightIndex=0;lightIndex<min(URP_FP_DIRECTIONAL_LIGHTS_COUNT,MAX_VISIBLE_LIGHTS);++lightIndex)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        Light light=GetAdditionalLight(lightIndex,inputData.positionWS,shadowMask);
        light=StudioLegacyAttenuation(light,lightIndex,inputData.positionWS,true);
        lightSum+=light.color*light.distanceAttenuation*light.shadowAttenuation*max(dot(inputData.normalWS,light.direction),0);
    }
#endif
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light=GetAdditionalLight(lightIndex,inputData.positionWS,shadowMask);
        light=StudioLegacyAttenuation(light,lightIndex,inputData.positionWS,true);
        lightSum+=light.color*light.distanceAttenuation*light.shadowAttenuation*max(dot(inputData.normalWS,light.direction),0);
    LIGHT_LOOP_END
#endif
    return albedo*lightSum;
}

void StudioClothLight(inout TsukuyomiOpenPBRResponse result,Light light,InputData inputData,SurfaceData surface,bool basePass)
{
    float3 N=inputData.normalWS,V=inputData.viewDirectionWS,L=light.direction;
    float3 H=normalize(V+L);
    float nv=abs(dot(N,V)),nl=saturate(dot(N,L)),nh=saturate(dot(N,H)),lh=saturate(dot(L,H));
    float roughness=1-surface.smoothness,alpha=max(roughness*roughness,.002),alpha2=alpha*alpha;
    float fd90=2*lh*lh*roughness-.5;
    float disney=(1+fd90*StudioPow5(1-nl))*(1+fd90*StudioPow5(1-nv))*nl;
    float visibility=.5/(nl*(nv*(1-alpha)+alpha)+nv*(nl*(1-alpha)+alpha)+.00001);
    float d=nh*nh*(alpha2-1)+1;
    float spec=max(alpha2*visibility/(d*d+1e-7)*nl,0);
    float3 f0=lerp(.04.xxx,surface.albedo,surface.metallic);
    float3 radiance=light.color*light.distanceAttenuation*light.shadowAttenuation;
    result.diffuse+=surface.albedo*.96*(1-surface.metallic)*disney*radiance;
    result.specular+=lerp(f0,1,StudioPow5(1-lh))*spec*radiance;
    if(IsStudioAITCloth())
    {
        float back=pow(saturate(dot(V,-(L+N*_AITTransmission.x))),_AITTransmission.y);
        float3 transLight=basePass?light.color*lerp(1,light.shadowAttenuation,_AITTransShadow):radiance;
        result.diffuse+=surface.albedo*StudioAITTransmissionMask*back*_AITTransmission.z*_AITTransmission.w*transLight;
    }
}

TsukuyomiOpenPBRResponse StudioClothLighting(InputData inputData,SurfaceData surface)
{
    TsukuyomiOpenPBRResponse result=(TsukuyomiOpenPBRResponse)0;
    half4 shadowMask=CalculateShadowMask(inputData);
    Light main=GetMainLight(inputData.shadowCoord,inputData.positionWS,shadowMask);
    main.shadowAttenuation=StudioDirectionalShadow(main.shadowAttenuation,inputData.positionWS);
    StudioClothLight(result,main,inputData,surface,true);
#if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount=GetAdditionalLightsCount();
#if USE_CLUSTER_LIGHT_LOOP
    [loop] for(uint lightIndex=0;lightIndex<min(URP_FP_DIRECTIONAL_LIGHTS_COUNT,MAX_VISIBLE_LIGHTS);++lightIndex)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        Light light=GetAdditionalLight(lightIndex,inputData.positionWS,shadowMask);
        StudioClothLight(result,StudioLegacyAttenuation(light,lightIndex,inputData.positionWS,true),inputData,surface,false);
    }
#endif
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light=GetAdditionalLight(lightIndex,inputData.positionWS,shadowMask);
        StudioClothLight(result,StudioLegacyAttenuation(light,lightIndex,inputData.positionWS,true),inputData,surface,false);
    LIGHT_LOOP_END
#endif
    float roughness=1-surface.smoothness,alpha=max(roughness*roughness,.002);
    float nv=abs(dot(inputData.normalWS,inputData.viewDirectionWS));
    float3 f0=lerp(.04.xxx,surface.albedo,surface.metallic);
    float grazing=saturate(surface.smoothness+1-.96*(1-surface.metallic));
    float3 reflection=GlossyEnvironmentReflection(reflect(-inputData.viewDirectionWS,inputData.normalWS),inputData.positionWS,roughness,surface.occlusion,inputData.normalizedScreenSpaceUV);
    result.specular+=reflection*lerp(f0,grazing.xxx,StudioPow5(1-nv))/(1+alpha*alpha);
    result.diffuse+=inputData.bakedGI*surface.albedo*.96*(1-surface.metallic)*surface.occlusion;
    return result;
}

TsukuyomiOpenPBRPrepared PrepareSpecularPBR(SurfaceData surfaceData, InputData inputData, TsukuyomiSpecularPBRVaryings input)
{
    TsukuyomiOpenPBRMaterial m = TsukuyomiOpenPBRFromURPSurface(surfaceData);
    if (_SourceMode > 2.5 && _SourceMode < 3.5)
    {
        float nv=saturate(dot(inputData.normalWS,inputData.viewDirectionWS));
        m.specularRoughness=1-surfaceData.smoothness*saturate(2*nv-.7);
        m.metalness=max(surfaceData.metallic,.5*_WetEffect)*saturate(2*nv-.1);
    }
    m.specularIOR = 1.5;
    m.anisotropy = (_SourceMode > 2.5 && _SourceMode < 3.5) ? _HairAnisotropy : 0;
    TsukuyomiOpenPBRGeometry g;
    // input.normalWS is a smooth vertex normal, not the rasterized face normal.
    // It needs the same view-hemisphere correction as the shading normal.
    g.geometricNormalWS = SpecularPBRRasterNormal(NormalizeNormalPerPixel(input.normalWS), inputData.viewDirectionWS);
    g.normalWS = inputData.normalWS;
    g.coatNormalWS = g.normalWS;
    g.tangentWS = input.tangentWS.xyz;
    g.tangentSign = input.tangentWS.w;
    g.viewDirectionWS = inputData.viewDirectionWS;
    TsukuyomiOpenPBRPrepared prepared = TsukuyomiOpenPBRPrepare(m, g);
    // Layer weights are zero in this material. Keep the unused coat state finite
    // even if a backend evaluates a branch speculatively during optimization.
    prepared.coatSpecular = prepared.baseSpecular;
    return prepared;
}

void SetupSpecularPBR(TsukuyomiSpecularPBRVaryings input, out SurfaceData surfaceData, out InputData inputData)
{
#if defined(TSUKUYOMI_SPECULAR_PBR_SSS_LIGHTING_PASS)
    // SSS quality changes the lighting viewport, while URP screen-space inputs
    // (AO, clustered lights, APV, decals and SSGI) still use camera pixel coordinates.
    input.positionCS.xy *= _TsukuyomiSpecularPBRSSSLightingScale.xy;
#endif
    StudioOverlayUV=input.studioOverlay.xy;
    StudioVertexColor=float4(input.studioVertexColor,1);
    StudioAmbientL2=input.studioAmbientL2;
    float3 studioViewWS=GetWorldSpaceNormalizeViewDir(input.positionWS);
    float3 studioBitangent=input.studioBitangentWS;
    StudioViewTS=float3(dot(input.tangentWS.xyz,studioViewWS),dot(studioBitangent,studioViewWS),dot(input.normalWS,studioViewWS));
    if(IsStudioHair()) StudioHairCoverage(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).a,input.positionCS.xy);
    InitializeTsukuyomiSpecularPBRSurfaceData(input.uv, surfaceData);
    if(IsStudioAITCloth()) StudioAITCoverage(input.uv,input.positionCS.xy);
    else if(_SourceMode>4.5) StudioClothCoverage(input.uv,input.positionCS.xy);
    if(_StudioLashes>.5) {
        float mask=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).a;
        surfaceData.alpha=StudioLashAlpha(mask,input.positionCS.xy);
        surfaceData.albedo=mask*_BaseColor.rgb;
    }
    if (_SourceMode>.5 && _SourceMode<1.5)
    {
        half brow=StudioBrowWeight()*_FaceBrowColor.a;
        half blush=SAMPLE_TEXTURE2D(_RegionMask,sampler_LinearRepeat,input.uv).b*_FaceBlushStrength*_FaceBlushColor.a;
        surfaceData.albedo=lerp(surfaceData.albedo,_FaceBrowColor.rgb*_BaseColor.rgb,brow);
        surfaceData.albedo=lerp(surfaceData.albedo,_FaceBlushColor.rgb*_BaseColor.rgb,saturate(blush));
    }
#if defined(LOD_FADE_CROSSFADE)
    LODFadeCrossFade(input.positionCS);
#endif
    if(IsStudioSkin()) surfaceData.albedo *= _StudioAlbedoGain;
    TsukuyomiSpecularPBRInitializeInputData(input, surfaceData.normalTS, inputData);
    SETUP_DEBUG_TEXTURE_DATA(inputData, UNDO_TRANSFORM_TEX(input.uv, _BaseMap));
#if defined(_DBUFFER)
    ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
#endif
    inputData.normalWS = SpecularPBRRasterNormal(inputData.normalWS, inputData.viewDirectionWS);
    TsukuyomiSpecularPBRInitializeBakedGIData(input, inputData);
}



void TsukuyomiSpecularPBRForwardFragment(TsukuyomiSpecularPBRVaryings input, FRONT_FACE_TYPE studioFrontFace : FRONT_FACE_SEMANTIC,
    out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    SurfaceData surfaceData; InputData inputData;
    if(IsStudioHair()) input.normalWS=normalize(input.normalWS)*IS_FRONT_VFACE(studioFrontFace,1,-1);
    StudioAITFrontFace=IS_FRONT_VFACE(studioFrontFace,1,.4);
    SetupSpecularPBR(input, surfaceData, inputData);
    TsukuyomiOpenPBRPrepared p = PrepareSpecularPBR(surfaceData, inputData, input);
    float strength = saturate(SampleSpecularPBRMask(input.uv).r);
    // Studio skin decodes source textures, then uses the same OpenPBR
    // lighting and SSS composition as SpecularPBR Skin.
    TsukuyomiOpenPBRResponse lit = (TsukuyomiOpenPBRResponse)0;
    if(IsStudioSkin()) lit=SpecularPBRLightingHigh(inputData, p, strength, surfaceData.occlusion, 0.0);
    else if(IsStudioEye()) lit=StudioEyeLighting(inputData,input.uv);
    else if(IsStudioHair()) lit=StudioHairLighting(inputData,input.uv);
    else if(IsStudioAITCloth()||_SourceMode>4.5) lit=StudioClothLighting(inputData,surfaceData);
    else lit=SpecularPBRLightingHigh(inputData, p, strength, surfaceData.occlusion, 0.0);
    float3 baseTexture = surfaceData.albedo / max(_BaseColor.rgb, float3(0.0001,0.0001,0.0001));
    float3 color = SpecularPBRCompositeSkin(lit, baseTexture, saturate(SampleSpecularPBRMask(input.uv).a),
        inputData.positionWS, inputData.normalizedScreenSpaceUV, surfaceData.emission);
    if(_StudioLashes>.5) color=StudioLashLighting(inputData,surfaceData.albedo);
    float3 studioDiffuseDebug=color-lit.specular;
    if(IsStudioSkin() && _StudioDebugOutput>.5) {
        color=SAMPLE_TEXTURE2D_X_LOD(_LightingTexBlurred,sampler_LinearClamp,inputData.normalizedScreenSpaceUV,0).rgb;
        if(_StudioDebugOutput>1.5) color=surfaceData.albedo;
        if(_StudioDebugOutput>2.5) color=surfaceData.smoothness.xxx;
        if(_StudioDebugOutput>3.5) color=StudioBodyRegionWeight().xxx;
        if(_StudioDebugOutput>4.5) color=SAMPLE_TEXTURE2D_X_LOD(_SSSSkinLightingTexture,sampler_LinearClamp,inputData.normalizedScreenSpaceUV,0).rgb;
    }
    outColor = half4(MixFog(color, inputData.fogCoord), surfaceData.alpha);
    if(_StudioEyeShadow>.5) {
        float alpha=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv).a;
        float coverage=min(pow(alpha,_StudioEyeShadowScale),1)*_BaseColor.a;
        outColor=half4(lerp(1.0.xxx,_BaseColor.rgb,coverage),1);
    }
    if(_StudioDebugOutput>5.5) outColor=half4(IsStudioHair()?1.0.xxx:0.0.xxx,1);
    if(IsStudioSkin() && _StudioDebugOutput>9.5 && _StudioDebugOutput<19.5)
        outColor=half4(SAMPLE_TEXTURE2D_X_LOD(_SSSSkinLightingTexture,sampler_LinearClamp,inputData.normalizedScreenSpaceUV,0).rgb,1);
    if(IsStudioSkin() && _StudioDebugOutput>19.5) outColor=half4(lit.specular,1);
    if(IsStudioSkin() && _StudioDebugOutput>20.5) outColor=half4(studioDiffuseDebug,1);
    if(IsStudioSkin() && _StudioDebugOutput>21.5) outColor=half4(inputData.normalWS*.5+.5,1);
    if(IsStudioSkin() && _StudioDebugOutput>22.5) outColor=half4(surfaceData.smoothness.xxx,1);
    if(IsStudioSkin() && _StudioDebugOutput>23.5) {
        float2 scatterUV=inputData.normalizedScreenSpaceUV;
        float3 value=SAMPLE_TEXTURE2D_X_LOD(_LightingTexBlurred,sampler_LinearClamp,scatterUV,0).rgb;
        if(_StudioDebugOutput>24.5) value=baseTexture;
        if(_StudioDebugOutput>25.5) value=float3(scatterUV,-TransformWorldToView(inputData.positionWS).z);
        if(_StudioDebugOutput>26.5) value=float3(input.uv,0);
        outColor=half4(value,1);
    }
    
#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

half4 TsukuyomiSpecularPBRMaskFragment(TsukuyomiSpecularPBRVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
#if !defined(_SSS_ON)
    clip(-1);
    return 0;
#else
    Alpha(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a, _BaseColor, _Cutoff);
#if defined(LOD_FADE_CROSSFADE)
    LODFadeCrossFade(input.positionCS);
#endif
    return saturate(SampleSpecularPBRMask(input.uv).a);
#endif
}

half4 TsukuyomiSpecularPBRSSSLightingFragment(TsukuyomiSpecularPBRVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
#if !defined(_SSS_ON)
    clip(-1);
    return 0;
#else
    SurfaceData surfaceData; InputData inputData;
    SetupSpecularPBR(input, surfaceData, inputData);
    float4 mask = SampleSpecularPBRMask(input.uv);
    if (mask.a <= 0.0) return 0;
    SpecularPBRPrepareDiffusionSurface(surfaceData, IsStudioSkin() ? StudioDiffusionTint(input.uv) : lerp(_OcclusionColor.rgb, 1.0, surfaceData.occlusion));
    TsukuyomiOpenPBRPrepared p = PrepareSpecularPBR(surfaceData, inputData, input);
    float3 transmission = SpecularPBRSampleTransmission(input.uv, surfaceData.occlusion);
    TsukuyomiOpenPBRResponse lit = SpecularPBRLightingHigh(inputData, p, saturate(mask.r), surfaceData.occlusion, transmission);
    // Mask participates in blur rejection and final blending, never twice in radiance.
    if(IsStudioSkin() && _StudioDebugOutput>9.5 && _StudioDebugOutput<19.5) {
        float3 value=inputData.normalWS*.5+.5;
        if(_StudioDebugOutput>10.5) value=surfaceData.normalTS*.5+.5;
        if(_StudioDebugOutput>11.5) value=StudioDiffusionTint(input.uv);
        if(_StudioDebugOutput>12.5) value=SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,input.uv).wyy;
        if(_StudioDebugOutput>13.5) value=SAMPLE_TEXTURE2D(_DetailNormalMap,sampler_BumpMap,input.uv).wyy;
        if(_StudioDebugOutput>14.5) {
            float2 uv=input.uv*_PoreUV.xy+_PoreUV.zw;
            float stepUV=_SourceMode<1.5?.009:.00625;
            value=float3(SAMPLE_TEXTURE2D(_PoreMap,sampler_LinearRepeat,uv).a,
                SAMPLE_TEXTURE2D(_PoreMap,sampler_LinearRepeat,uv+float2(stepUV,0)).a,
                SAMPLE_TEXTURE2D(_PoreMap,sampler_LinearRepeat,uv+float2(0,stepUV)).a);
        }
        if(_StudioDebugOutput>15.5) value=float3(input.uv,0);
        if(_StudioDebugOutput>16.5) value=input.normalWS*.5+.5;
        if(_StudioDebugOutput>17.5) value=input.tangentWS.xyz*.5+.5;
        if(_StudioDebugOutput>18.5) value=input.studioBitangentWS*.5+.5;
        return half4(value,1);
    }
    return half4(lit.diffuse, 1.0);
#endif
}

#endif
