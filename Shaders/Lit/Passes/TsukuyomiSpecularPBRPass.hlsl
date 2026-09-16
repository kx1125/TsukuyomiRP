#ifndef TSUKUYOMI_SPECULAR_PBR_PASS_INCLUDED
#define TSUKUYOMI_SPECULAR_PBR_PASS_INCLUDED

#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Material/TsukuyomiSpecularPBRInput.hlsl"
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
    float2 staticLightmapUV : TEXCOORD1;
    float2 dynamicLightmapUV : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct TsukuyomiSpecularPBRVaryings
{
    float2 uv : TEXCOORD0;
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
    float3 bitangent = tangentSign * cross(input.normalWS.xyz, input.tangentWS.xyz);
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

#if defined(UNITY_PRETRANSFORM_TO_DISPLAY_ORIENTATION)
    float2 preRotatedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    switch (UNITY_DISPLAY_ORIENTATION_PRETRANSFORM)
    {
        default:
        case UNITY_DISPLAY_ORIENTATION_PRETRANSFORM_0:
            inputData.normalizedScreenSpaceUV = preRotatedScreenSpaceUV;
            break;
        case UNITY_DISPLAY_ORIENTATION_PRETRANSFORM_90:
            inputData.normalizedScreenSpaceUV = float2(1 - preRotatedScreenSpaceUV.y, preRotatedScreenSpaceUV.x);
            break;
        case UNITY_DISPLAY_ORIENTATION_PRETRANSFORM_180:
            inputData.normalizedScreenSpaceUV = float2(1 - preRotatedScreenSpaceUV.x, 1 - preRotatedScreenSpaceUV.y);
            break;
        case UNITY_DISPLAY_ORIENTATION_PRETRANSFORM_270:
            inputData.normalizedScreenSpaceUV = float2(preRotatedScreenSpaceUV.y, 1 - preRotatedScreenSpaceUV.x);
            break;
    }
#else
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
#endif

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
    output.positionWS = vertexInput.positionWS;
    output.normalWS = normalInput.normalWS;

    real sign = input.tangentOS.w * GetOddNegativeScale();
    output.tangentWS = half4(normalInput.tangentWS.xyz, sign);

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


TEXTURE2D_X(_LightingTexBlurred);
TEXTURE2D_X(_SSSSkinMaskTexture);
TEXTURE2D_X_FLOAT(_TsukuyomiSpecularPBRSSSDepth);
float _TsukuyomiSpecularPBRSSSReady;
float4 _TsukuyomiSpecularPBRSSSLightingScale;

TsukuyomiOpenPBRPrepared PrepareSpecularPBR(SurfaceData surfaceData, InputData inputData, TsukuyomiSpecularPBRVaryings input)
{
    TsukuyomiOpenPBRMaterial m = TsukuyomiOpenPBRFromURPSurface(surfaceData);
    m.specularIOR = 1.5;
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
    InitializeTsukuyomiSpecularPBRSurfaceData(input.uv, surfaceData);
#if defined(LOD_FADE_CROSSFADE)
    LODFadeCrossFade(input.positionCS);
#endif
    TsukuyomiSpecularPBRInitializeInputData(input, surfaceData.normalTS, inputData);
    SETUP_DEBUG_TEXTURE_DATA(inputData, UNDO_TRANSFORM_TEX(input.uv, _BaseMap));
#if defined(_DBUFFER)
    ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
#endif
    inputData.normalWS = SpecularPBRRasterNormal(inputData.normalWS, inputData.viewDirectionWS);
    TsukuyomiSpecularPBRInitializeBakedGIData(input, inputData);
}

bool SpecularPBRHasSkinLighting(TsukuyomiSpecularPBRVaryings input, float2 screenUV)
{
    if (_TsukuyomiSpecularPBRSSSReady < 0.5) return false;
    float coverage = SAMPLE_TEXTURE2D_X_LOD(_SSSSkinMaskTexture, sampler_PointClamp, screenUV, 0).r;
    if (coverage <= 0.0) return false;
    float rawDepth = SAMPLE_TEXTURE2D_X_LOD(_TsukuyomiSpecularPBRSSSDepth, sampler_PointClamp, screenUV, 0).r;
    float skinDepth = IsPerspectiveProjection() ? LinearEyeDepth(rawDepth, _ZBufferParams) : LinearDepthToEyeDepth(rawDepth);
    float surfaceDepth = -TransformWorldToView(input.positionWS).z;
    // Reject SSS data belonging to a surface behind a renderer outside the skin layer.
    return abs(skinDepth - surfaceDepth) <= max(0.002, surfaceDepth * 0.002);
}

void TsukuyomiSpecularPBRForwardFragment(TsukuyomiSpecularPBRVaryings input,
    out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    SurfaceData surfaceData; InputData inputData;
    SetupSpecularPBR(input, surfaceData, inputData);
    TsukuyomiOpenPBRPrepared p = PrepareSpecularPBR(surfaceData, inputData, input);
    float strength = saturate(SampleSpecularPBRMask(input.uv).r);
    TsukuyomiOpenPBRResponse lit = SpecularPBRLightingHigh(inputData, p, strength, surfaceData.occlusion, 0.0);
    float3 color = lit.diffuse + lit.specular + surfaceData.emission;
#if defined(_SSS_ON)
    float skinMask = saturate(SampleSpecularPBRMask(input.uv).a);
    if (skinMask > 0.0 && SpecularPBRHasSkinLighting(input, inputData.normalizedScreenSpaceUV))
    {
        float3 baseTexture = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
        float3 scattered = SAMPLE_TEXTURE2D_X_LOD(_LightingTexBlurred, sampler_LinearClamp,
            inputData.normalizedScreenSpaceUV, 0).rgb * baseTexture;
        color = SpecularPBRComposite(lit.diffuse, lit.specular, scattered, skinMask, surfaceData.emission);
    }
#endif
    outColor = half4(MixFog(color, inputData.fogCoord), 1.0);
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
    // Preserve sharp albedo texture details: evaluate the lighting/tint here,
    // and apply the base texture after the screen-space diffusion.
    surfaceData.albedo = _BaseColor.rgb * lerp(_OcclusionColor.rgb, 1.0, surfaceData.occlusion);
    surfaceData.emission = 0;
    TsukuyomiOpenPBRPrepared p = PrepareSpecularPBR(surfaceData, inputData, input);
    float3 transmission = 0;
#if defined(TRANSMISSION)
    transmission = SAMPLE_TEXTURE2D(_TransmissionMap, sampler_TransmissionMap, input.uv).rgb
        * _TransmissionColor.rgb * lerp(1.0, surfaceData.occlusion, TransmissionOcc);
#endif
    TsukuyomiOpenPBRResponse lit = SpecularPBRLightingHigh(inputData, p, saturate(mask.r), surfaceData.occlusion, transmission);
    // Mask participates in blur rejection and final blending, never twice in radiance.
    return half4(lit.diffuse, 1.0);
#endif
}

#endif
