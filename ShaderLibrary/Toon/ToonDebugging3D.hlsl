#ifndef TOON_DEBUGGING_3D_INCLUDED
#define TOON_DEBUGGING_3D_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Debug/Debugging3D.hlsl"
#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Toon/ToonBRDFData.hlsl"

#if defined(DEBUG_DISPLAY)

// URP debug-display overloads operating directly on Toon material data.
bool UpdateSurfaceAndInputDataForDebug(inout ToonSurfaceData surfaceData, inout InputData inputData)
{
    bool changed = false;

    if (_DebugLightingMode == DEBUGLIGHTINGMODE_LIGHTING_WITHOUT_NORMAL_MAPS || _DebugLightingMode == DEBUGLIGHTINGMODE_LIGHTING_WITH_NORMAL_MAPS)
    {
        surfaceData.albedo = 1;
        surfaceData.emission = 0;
        surfaceData.specular = 0;
        surfaceData.occlusion = 1;
        surfaceData.clearCoatMask = 0;
        surfaceData.clearCoatSmoothness = 1;
        surfaceData.metallic = 0;
        surfaceData.smoothness = 0;
        changed = true;
    }
    else if (_DebugLightingMode == DEBUGLIGHTINGMODE_REFLECTIONS || _DebugLightingMode == DEBUGLIGHTINGMODE_REFLECTIONS_WITH_SMOOTHNESS)
    {
        surfaceData.albedo = 0;
        surfaceData.emission = 0;
        surfaceData.occlusion = 1;
        surfaceData.clearCoatMask = 0;
        surfaceData.clearCoatSmoothness = 1;
        surfaceData.specular = 1;
        surfaceData.metallic = 0;
        if (_DebugLightingMode == DEBUGLIGHTINGMODE_REFLECTIONS)
        {
            surfaceData.smoothness = 1;
        }
        changed = true;
    }

    if (_DebugLightingMode == DEBUGLIGHTINGMODE_LIGHTING_WITHOUT_NORMAL_MAPS || _DebugLightingMode == DEBUGLIGHTINGMODE_REFLECTIONS)
    {
        const half3 normalTS = half3(0, 0, 1);
        #if defined(_NORMALMAP)
        inputData.normalWS = TransformTangentToWorld(normalTS, inputData.tangentToWorld);
        #else
        inputData.normalWS = inputData.normalWS;
        #endif
        surfaceData.normalTS = normalTS;
        changed = true;
    }

    return changed;
}

bool CalculateValidationColorForDebug(in InputData inputData, in ToonSurfaceData surfaceData, inout half4 debugColor)
{
    switch(_DebugMaterialValidationMode)
    {
        case DEBUGMATERIALVALIDATIONMODE_NONE:
            return false;

        case DEBUGMATERIALVALIDATIONMODE_ALBEDO:
            return CalculateValidationAlbedo(surfaceData.albedo, debugColor);

        case DEBUGMATERIALVALIDATIONMODE_METALLIC:
            return CalculateValidationMetallic(surfaceData.albedo, surfaceData.metallic, debugColor);

        default:
            return TryGetDebugColorInvalidMode(debugColor);
    }
}

bool CalculateColorForDebugMaterial(in InputData inputData, in ToonSurfaceData surfaceData, inout half4 debugColor)
{
    // Debug materials...
    switch(_DebugMaterialMode)
    {
        case DEBUGMATERIALMODE_NONE:
            return false;

        case DEBUGMATERIALMODE_ALBEDO:
            debugColor = half4(surfaceData.albedo, 1);
            return true;

        case DEBUGMATERIALMODE_SPECULAR:
            debugColor = half4(surfaceData.specular, 1);
            return true;

        case DEBUGMATERIALMODE_ALPHA:
            debugColor = half4(surfaceData.alpha.rrr, 1);
            return true;

        case DEBUGMATERIALMODE_SMOOTHNESS:
            debugColor = half4(surfaceData.smoothness.rrr, 1);
            return true;

        case DEBUGMATERIALMODE_AMBIENT_OCCLUSION:
            debugColor = half4(surfaceData.occlusion.rrr, 1);
            return true;

        case DEBUGMATERIALMODE_EMISSION:
            debugColor = half4(surfaceData.emission, 1);
            return true;

        case DEBUGMATERIALMODE_NORMAL_WORLD_SPACE:
            debugColor = half4(inputData.normalWS.xyz * 0.5 + 0.5, 1);
            return true;

        case DEBUGMATERIALMODE_NORMAL_TANGENT_SPACE:
            debugColor = half4(surfaceData.normalTS.xyz * 0.5 + 0.5, 1);
            return true;

        case DEBUGMATERIALMODE_METALLIC:
            debugColor = half4(surfaceData.metallic.rrr, 1);
            return true;

        case DEBUGMATERIALMODE_RENDERING_LAYER_MASKS:
            debugColor.xyz = GetRenderingLayerMasksDebugColor(inputData.positionCS, inputData.normalWS).xyz;
            return true;

        default:
            return TryGetDebugColorInvalidMode(debugColor);
    }
}

bool CalculateColorForDebugMipmapStreaming(in InputData inputData, in ToonSurfaceData surfaceData, inout half4 debugColor)
{
    return CalculateColorForDebugMipmapStreaming(inputData.mipCount, inputData.positionCS.xy, inputData.texelSize, inputData.uv, inputData.mipInfo, inputData.streamInfo, inputData.originalColor, debugColor);
}

bool CalculateColorForDebug(in InputData inputData, in ToonSurfaceData surfaceData, inout half4 debugColor)
{
    if (CalculateColorForDebugSceneOverride(debugColor))
    {
        return true;
    }
    else if (CalculateColorForDebugMipmapStreaming(inputData, surfaceData, debugColor))
    {
        return true;
    }
    else if (CalculateColorForDebugMaterial(inputData, surfaceData, debugColor))
    {
        return true;
    }
    else if (CalculateValidationColorForDebug(inputData, surfaceData, debugColor))
    {
        return true;
    }
    else
    {
        return false;
    }
}

half4 CalculateDebugLightingComplexityColor(in InputData inputData, in ToonSurfaceData surfaceData)
{
#if USE_CLUSTER_LIGHT_LOOP
    int numLights = URP_FP_DIRECTIONAL_LIGHTS_COUNT;
    uint entityIndex;
    ClusterIterator it = ClusterInit(inputData.normalizedScreenSpaceUV, inputData.positionWS, 0);
    [loop] while (ClusterNext(it, entityIndex))
    {
        numLights++;
    }
    it = ClusterInit(inputData.normalizedScreenSpaceUV, inputData.positionWS, 1);
    [loop] while (ClusterNext(it, entityIndex))
    {
        numLights++;
    }
#else
    // Assume a main light and add 1 to the additional lights.
    int numLights = GetAdditionalLightsCount() + 1;
#endif

    const uint2 tileSize = uint2(32,32);
    const uint maxLights = 9;
    const float opacity = 0.8f;

    uint2 pixelCoord = uint2(inputData.normalizedScreenSpaceUV * _ScreenParams.xy);
    half3 base = surfaceData.albedo;
    half4 overlay = half4(OverlayHeatMap(pixelCoord, tileSize, numLights, maxLights, opacity));

    uint2 tileCoord = (float2)pixelCoord / tileSize;
    uint2 offsetInTile = pixelCoord - tileCoord * tileSize;
    bool border = any(offsetInTile == 0 || offsetInTile == tileSize.x - 1);
    if (border)
        overlay = half4(1, 1, 1, 0.4f);

    return half4(lerp(base.rgb, overlay.rgb, overlay.a), 1);
}

bool CanDebugOverrideOutputColor(inout InputData inputData, inout ToonSurfaceData surfaceData, inout ToonBRDFData brdfData, inout half4 debugColor)
{
    if (_DebugMaterialMode == DEBUGMATERIALMODE_LIGHTING_COMPLEXITY)
    {
        debugColor = CalculateDebugLightingComplexityColor(inputData, surfaceData);
        return true;
    }
    else if (_DebugLightingMode == DEBUGLIGHTINGMODE_GLOBAL_ILLUMINATION)
    {
        debugColor = half4(inputData.bakedGI, surfaceData.alpha);
        return true;
    }
    else
    {
        debugColor = half4(0, 0, 0, 1);

        if (_DebugLightingMode == DEBUGLIGHTINGMODE_SHADOW_CASCADES)
        {
            surfaceData.albedo = CalculateDebugShadowCascadeColor(inputData);
        }
        else
        {
            if (UpdateSurfaceAndInputDataForDebug(surfaceData, inputData))
            {
                // If we've modified any data we'll need to re-sample the GI to ensure that everything works correctly...

                #if defined(_SCREEN_SPACE_IRRADIANCE)
                // In screen space irradiance mode the final pixel values have already been resolved so we cannot reevaluate here.
                #elif defined(DYNAMICLIGHTMAP_ON)
                inputData.bakedGI = SAMPLE_GI(inputData.staticLightmapUV, inputData.dynamicLightmapUV.xy, inputData.vertexSH, inputData.normalWS);
                #elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
                inputData.bakedGI = SAMPLE_GI(inputData.vertexSH,
                    GetAbsolutePositionWS(inputData.positionWS),
                    inputData.normalWS,
                    inputData.viewDirectionWS,
                    inputData.positionCS.xy,
                    inputData.probeOcclusion,
                    inputData.shadowMask);
                #else
                inputData.bakedGI = SAMPLE_GI(inputData.staticLightmapUV, inputData.vertexSH, inputData.normalWS);
                #endif
            }
        }

        // Update the BRDF data following any changes to the input/surface above...
        InitializeToonBRDFData(surfaceData, brdfData);

        return CalculateColorForDebug(inputData, surfaceData, debugColor);
    }
}

#endif

#endif
