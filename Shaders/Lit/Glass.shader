Shader "TsukuyomiRP/Lit/Glass"
{
    Properties
    {
        [MainTexture] _BaseMap("Transmission Map", 2D) = "white" {}
        [MainColor] _BaseColor("Transmission Color", Color) = (1, 1, 1, 0.25)
        _Roughness("Roughness", Range(0.0, 1.0)) = 0.08

        _BumpScale("Normal Scale", Float) = 1.0
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}

        [Toggle] _RefractionEnabled("Enable Refraction", Float) = 1.0
        _IndexOfRefraction("Index Of Refraction", Range(1.0, 2.5)) = 1.5
        _Thickness("Sphere Thickness (World Units)", Range(0.0, 10.0)) = 0.25
        _Absorption("Absorption", Range(0.0, 1.0)) = 0.35

        [NoScaleOffset] _MatCapMap("MatCap", 2D) = "black" {}
        _MatCapIntensity("MatCap Intensity", Range(0.0, 8.0)) = 0.0

        _MicroShadowOpacity("Micro Shadow Opacity", Range(0.0, 1.0)) = 1.0
        _RoughDiffuseStrength("Rough Diffuse Strength", Range(0.0, 1.0)) = 1.0
        _IndirectSpecularFGDStrength("Indirect Specular FGD Strength", Range(0.0, 1.0)) = 1.0
        _IndirectDiffuseIntensity("Indirect Diffuse Intensity", Range(0.0, 2.0)) = 0.0
        _IndirectSpecularIntensity("Indirect Specular Intensity", Range(0.0, 2.0)) = 1.0
        _EnvironmentReflectionRange("Environment Reflection Range", Range(0.001, 1.0)) = 0.35
        _EnvironmentReflectionSharpness("Environment Reflection Sharpness", Range(0.25, 8.0)) = 2.0
        _HorizonOcclusionPower("Horizon Occlusion Power", Range(0.0, 4.0)) = 2.0

        [ToggleOff(_SPECULARHIGHLIGHTS_OFF)] _SpecularHighlights("Specular Highlights", Float) = 1.0
        [ToggleOff(_ENVIRONMENTREFLECTIONS_OFF)] _EnvironmentReflections("Environment Reflections", Float) = 1.0
        [ToggleOff(_RECEIVE_SHADOWS_OFF)] _ReceiveShadows("Receive Shadows", Float) = 1.0

        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Render Face", Float) = 2.0
        _QueueOffset("Queue Offset", Range(-50.0, 50.0)) = 0.0

        [Header(FSR3 Reactivity)]
        _Fsr3ReactiveScale("Reactive Scale", Range(0.0, 1.0)) = 0.9
        _Fsr3CompositionScale("Composition Scale", Range(0.0, 1.0)) = 0.0

        [HideInInspector] _ClearCoatMask("_ClearCoatMask", Float) = 0.0
        [HideInInspector] _ClearCoatSmoothness("_ClearCoatSmoothness", Float) = 0.0
        [HideInInspector] _SrcBlend("_SrcBlend", Float) = 1.0
        [HideInInspector] _DstBlend("_DstBlend", Float) = 0.0
        [HideInInspector] _ZWrite("_ZWrite", Float) = 0.0
        [HideInInspector] _MainTex("BaseMap", 2D) = "white" {}
        [HideInInspector] _Color("Base Color", Color) = (1, 1, 1, 0.25)

        [HideInInspector][NoScaleOffset] unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }
        LOD 300

        Pass
        {
            Name "GlassForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex GlassForwardVertex
            #pragma fragment GlassForwardFragment

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _REFRACTION_OFF
            #pragma shader_feature_local_fragment _MATCAP_ON
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fragment _ LIGHTMAP_BICUBIC_SAMPLING
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ DEBUG_DISPLAY

            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Lit/Passes/TsukuyomiGlassForwardPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Fsr3Mask"
            Tags { "LightMode" = "TsukuyomiFsr3Mask" }

            Blend One One
            BlendOp Max
            ZWrite Off
            ZTest LEqual
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex TsukuyomiFsr3MaskVertex
            #pragma fragment TsukuyomiFsr3MaskFragment
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Material/TsukuyomiGlassInput.hlsl"
            #include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Lit/Passes/TsukuyomiFsr3MaskPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "Tsukuyomi.Rendering.Editor.ShaderGUI.TsukuyomiGlassShaderGUI"
}
