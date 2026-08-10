Shader "TsukuyomiRP/Lit/Water"
{
    Properties
    {
        [Header(Water)]
        [Normal][NoScaleOffset] _WaterNormal("Normal", 2D) = "bump" {}
        _NormalScale("Normal Scale", Range(0, 2)) = 1
        _WaterColor("Shallow Water Color", Color) = (0.08, 0.45, 0.55, 1)
        _WaterColorDeep("Deep Water Color", Color) = (0.01, 0.08, 0.13, 1)
        _Alpha("Water Opacity", Range(0, 1)) = 1
        _DepthMaxDistance("Depth Color Distance", Range(0.01, 50)) = 4
        [HDR] _Absorption("Absorption (RGB per meter)", Color) = (0.35, 0.12, 0.06, 1)
        _ScatteringStrength("Scattering Strength", Range(0, 2)) = 0.65
        _RefractionStrength("Refraction Strength", Range(0, 0.1)) = 0.02
        _RefractionDepth("Refraction Depth Fade", Range(0.01, 20)) = 2
        _ShadowCol("Shadow Color", Color) = (0.2, 0.2, 0.2, 1)

        [Header(Reflection)]
        _ReflectDistortion("Reflection Distortion", Range(0, 0.1)) = 0.015
        _ReflectIndensity("Reflection Intensity", Range(0, 2)) = 1
        _ReflectionRoughness("Reflection Roughness", Range(0, 1)) = 0.08
        _FresnelF0("Water Fresnel F0", Range(0, 0.2)) = 0.02
        _FresnelRange("Fresnel Range", Range(0, 1)) = 0.5
        _FresnelFade("Fresnel Fade", Range(0.001, 1)) = 0.1
        _SunSpecularIntensity("Sun Specular Intensity", Range(0, 4)) = 1
        _SunSpecularPower("Sun Specular Power", Range(8, 512)) = 128

        [Header(Wave)]
        _WaveScroll("Wave Scroll (xy Tiling, zw Speed)", Vector) = (1, 1, 0.04, 0.03)
        _WaveScale("Diffuse Wave Strength", Range(0, 1)) = 0.25
        [Toggle(_WATER_VERTEX_WAVES)] _VertexWaves("Enable Vertex Gerstner Waves", Float) = 0
        _GerstnerWaveA("Gerstner A (direction xy, amplitude, wavelength)", Vector) = (1, 0, 0.12, 6)
        _GerstnerWaveB("Gerstner B (direction xy, amplitude, wavelength)", Vector) = (0.4, 0.8, 0.06, 2.5)
        _GerstnerSpeed("Gerstner Speed (A, B)", Vector) = (1.2, 1.8, 0, 0)
        _GerstnerSteepness("Gerstner Steepness", Range(0, 1)) = 0.35

        [Header(Foam)]
        [HDR] _FoamColor("Foam Color", Color) = (1, 1, 1, 1)
        [NoScaleOffset] _FoamNoise("Foam Noise", 2D) = "white" {}
        _FoamScroll("Foam Scroll (xy Tiling, zw Speed)", Vector) = (1, 1, 0.03, 0.02)
        _FoamMaxDistance("Foam Max Distance", Range(0.01, 5)) = 0.5
        _FoamMinDistance("Foam Min Distance", Range(0.01, 5)) = 0.1
        _FoamNoiseCutoff("Foam Noise Threshold", Range(0, 10)) = 1
        _FoamNoiseSmooth("Foam Noise Smooth", Range(0.001, 1)) = 0.1
        _CrestFoamThreshold("Crest Foam Threshold", Range(0, 1)) = 0.35
        _CrestFoamSmooth("Crest Foam Smooth", Range(0.001, 1)) = 0.15
        _CrestFoamIntensity("Crest Foam Intensity", Range(0, 2)) = 0.5

        [Header(Caustics)]
        [Toggle(_CAUSTICS_ON)] _CausticsEnabled("Enable Surface Caustics", Float) = 1
        [NoScaleOffset] _CausticsTex("Caustics Texture", 2D) = "white" {}
        [HDR] _CausticsCol("Caustics Color", Color) = (1, 1, 1, 1)
        _CausticsIndensity("Caustics Intensity", Range(0, 2)) = 1
        _CausticsScroll("Caustics Scroll (xy Tiling, zw Speed)", Vector) = (1, 1, 0.1, 0.1)
        _CausticsDistortion("Caustics Distortion", Range(0, 0.1)) = 0.02

        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Render Face", Float) = 2
        _QueueOffset("Queue Offset", Range(-50, 50)) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "UniversalMaterialType"="Lit" "IgnoreProjector"="True" }
        LOD 300

        Pass
        {
            Name "WaterForward"
            Tags { "LightMode"="UniversalForward" }

            // The shader composites the opaque scene colour itself, so normal alpha blending would blend it twice.
            Blend One Zero
            ZWrite Off
            ZTest LEqual
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex WaterForwardVertex
            #pragma fragment WaterForwardFragment
            #pragma shader_feature_local_vertex _WATER_VERTEX_WAVES
            #pragma shader_feature_local_fragment _CAUSTICS_ON
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
            #pragma multi_compile_local_fragment _ _TSUKUYOMI_PLANAR_REFLECTION
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Lit/Passes/WaterForwardPass.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
