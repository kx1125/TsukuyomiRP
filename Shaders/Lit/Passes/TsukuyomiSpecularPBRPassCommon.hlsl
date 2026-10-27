#ifndef TSUKUYOMI_SPECULAR_PBR_PASS_COMMON_INCLUDED
#define TSUKUYOMI_SPECULAR_PBR_PASS_COMMON_INCLUDED

#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Lighting/TsukuyomiSpecularPBRLighting.hlsl"
#include "Packages/tsukuyomi.render-pipelines.universal/Shaders/SSGI/TsukuyomiScreenSpaceGlobalIllumination.hlsl"

#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Lighting/TsukuyomiVertexLighting.hlsl"

struct TsukuyomiSpecularPBRAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 texcoord : TEXCOORD0;
#if defined(_STUDIO_SKIN)
    half4 vertexColor : COLOR;
#endif
    float2 staticLightmapUV : TEXCOORD1;
    float2 dynamicLightmapUV : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct TsukuyomiSpecularPBRVaryings
{
    float2 uv : TEXCOORD0;
#if defined(_STUDIO_SKIN)
    float2 studioOverlay : TEXCOORD7;
    float3 studioVertexColor : TEXCOORD11;
#endif
#if defined(_STUDIO_SKIN)
    float3 studioBitangentWS : TEXCOORD12;
#endif
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
#if defined(_STUDIO_SKIN)
    float3 bitangent = input.studioBitangentWS;
#else
    float3 bitangent = tangentSign * cross(input.normalWS.xyz, input.tangentWS.xyz);
#endif
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
#if defined(_STUDIO_SKIN)
    output.studioOverlay = input.staticLightmapUV;
    output.studioVertexColor = input.vertexColor.rgb;
#endif
    output.positionWS = vertexInput.positionWS;
    output.normalWS = normalInput.normalWS;

    real sign = input.tangentOS.w * GetOddNegativeScale();
#if defined(_STUDIO_SKIN)
    output.tangentWS = half4(normalize(normalInput.tangentWS.xyz), sign);
    output.studioBitangentWS=sign*cross(normalize(normalInput.normalWS),normalize(normalInput.tangentWS));
#else
    output.tangentWS = half4(normalInput.tangentWS.xyz, sign);
#endif

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



TsukuyomiOpenPBRPrepared PrepareSpecularPBR(SurfaceData surfaceData, InputData inputData, TsukuyomiSpecularPBRVaryings input)
{
    TsukuyomiOpenPBRMaterial m = TsukuyomiOpenPBRFromURPSurface(surfaceData);
    m.specularIOR = 1.5;
    m.anisotropy = 0;
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

#endif
