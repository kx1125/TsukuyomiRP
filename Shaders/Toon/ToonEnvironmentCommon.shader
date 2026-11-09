Shader "TsukuyomiRP/Toon/EnvironmentCommon"
{
    Properties
    {
        [Toggle(_ALPHATEST_ON)] _AlphaClip("AlphaClip", Float) = 0.0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Render Face", Float) = 2.0
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        _Roughness("Roughness", Range(0.0, 1.0)) = 0.5
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _OcclusionStrength("Occlusion Strength", Range(0.0, 1.0)) = 1.0
        [NoScaleOffset] _RMOE("RMOE (R Roughness, G Metallic, B AO, A Emission Mask)", 2D) = "white" {}

        _BumpScale("Normal Scale", Float) = 1.0
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}

        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 1)

        _CurveMap("Curvature Map", 2D) = "black" {}
        _NoiseMap("Noise Map", 2D) = "black" {}

        [Header(Toon Lighting)]
        _ShadowColor("Shadow Color", Color) = (0,0,0,0)
        _ShadowPosition("Shadow Position", Range(0.0 , 1.0)) = 0.5
        _ShadowSmoothness("Shadow Smoothness", Range(0.0 , 0.5)) = 0.1

        [Toggle(SPECULAR_REMAP)] _SpecularRemap("Specular Remap", Float) = 0.0
        [HDR] _SpecularColor("Specular Color", Color) = (1, 1, 1, 1)
        _SpecularPosition("Specular Position", Range(0.0, 1.0)) = 0.5
        _SpecularSmoothness("Specular Smoothness", Range(0.0, 0.5)) = 0.05

        [HDR] _EdgeLightColor("Edge Color", Color) = (1,1,1,1)
        _EdgeLightPosition("EdgeLight Position", Range(0.0, 1.0)) = 0.5
        _EdgeLightSmoothness("EdgeLight Smoothness", Range(0.0, 0.5)) = 0.05

        _RimOriginBias("RimOrigin Bias", Vector) = (0.0,0.0,0.0,0.0)
        [HDR] _RimColor("Rim Color", Color) = (1, 1, 1, 1)
        _RimWidth("Rim Width", Range(0, 10.0)) = 2.0
        _RimNoiseStrength("Rim Noise Strength", Range(0.0, 1.0)) = 0.0
        _RimReferenceDistance("Rim Reference Distance", Range(0.1, 50.0)) = 3.0
        _RimDistanceFade("Rim Distance Fade", Range(0.0, 1.0)) = 1.0
        _RimPosition("Rim Position", Range(0.0, 1.0)) = 0.5
        _RimSmoothness("Rim Smoothness", Range(0.0, 1.0)) = 0.05
        _FresnelColor("Fresnel Color", Color) = (0.5,0.5,0.5,1)
        _FresnelPosition("Fresnel Position", Range(0.0, 1.0)) = 0.5
        _FresnelSmoothness("Fresnel Smoothness", Range(0.0, 1.0)) = 0.1

        [Header(Outline)]
        [Enum(Mesh Normal, 0, Vertex Color, 1)] _OutlineNormalSource("Outline Normal Source", Float) = 0
        _OutlineColor("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth("Outline Width (px at 1080p)", Range(0, 16)) = 1

        [Header(Tsukuyomi Lighting)]
        _RoughDiffuseStrength("Rough Diffuse Strength", Range(0.0, 1.0)) = 1.0
        [NoScaleOffset] _EnvironmentMap("Environment Map (Lat-Long)", 2D) = "black" {}
        _IndirectSpecularFGDStrength("Indirect Specular FGD Strength", Range(0.0, 1.0)) = 1.0
        _IndirectDiffuseIntensity("Indirect Diffuse Intensity", Range(0.0, 2.0)) = 1.0
        _IndirectSpecularIntensity("Indirect Specular Intensity", Range(0.0, 2.0)) = 1.0
        _HorizonOcclusionPower("Horizon Occlusion Power", Range(0.0, 4.0)) = 2.0

        [ToggleOff(_SPECULARHIGHLIGHTS_OFF)] _SpecularHighlights("Specular Highlights", Float) = 1.0
        [ToggleOff(_ENVIRONMENTREFLECTIONS_OFF)] _EnvironmentReflections("Environment Reflections", Float) = 1.0

        [HideInInspector] _ClearCoatMask("_ClearCoatMask", Float) = 0.0
        [HideInInspector] _ClearCoatSmoothness("_ClearCoatSmoothness", Float) = 0.0

        [HideInInspector]_Surface("__surface", Float) = 0.0
        [HideInInspector]_Blend("__blend", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _SrcBlendAlpha("__srcA", Float) = 1.0
        [HideInInspector] _DstBlendAlpha("__dstA", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _BlendModePreserveSpecular("_BlendModePreserveSpecular", Float) = 1.0
        [HideInInspector] _AlphaToMask("__alphaToMask", Float) = 0.0
        [HideInInspector] _AddPrecomputedVelocity("_AddPrecomputedVelocity", Float) = 0.0
        [HideInInspector] _XRMotionVectorsPass("_XRMotionVectorsPass", Float) = 1.0

        [ToggleOff(_RECEIVE_SHADOWS_OFF)] _ReceiveShadows("Receive Shadows", Float) = 1.0
        _QueueOffset("Queue Offset", Float) = 0.0

        [Header(FSR3 Reactivity)]
        _Fsr3ReactiveScale("Reactive Scale", Range(0.0, 1.0)) = 0.9
        _Fsr3CompositionScale("Composition Scale", Range(0.0, 1.0)) = 0.0

        [HideInInspector] _MainTex("BaseMap", 2D) = "white" {}
        [HideInInspector] _Color("Base Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _GlossMapScale("Smoothness", Float) = 0.0
        [HideInInspector] _Glossiness("Smoothness", Float) = 0.0
        [HideInInspector] _GlossyReflections("EnvironmentReflections", Float) = 0.0

        [HideInInspector][NoScaleOffset] unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }
        LOD 300

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "ObjectOutline" }

            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ToonOutlineVertex
            #pragma fragment ToonOutlineFragment
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Toon/Passes/ToonEnvironmentCommonOutlinePass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend[_SrcBlend][_DstBlend], [_SrcBlendAlpha][_DstBlendAlpha]
            ZWrite[_ZWrite]
            Cull[_Cull]
            AlphaToMask[_AlphaToMask]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex TsukuyomiPBRForwardVertex
            #pragma fragment TsukuyomiPBRForwardFragment

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ _ALPHAPREMULTIPLY_ON _ALPHAMODULATE_ON
            #pragma shader_feature_local_fragment _EMISSION
            #pragma multi_compile_local_fragment _ SPECULAR_REMAP
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
            #pragma shader_feature_local_fragment _DISNEY_DIFFUSE_BURLEY

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
            #pragma multi_compile_fragment _ _TSUKUYOMI_SCREEN_SPACE_GLOBAL_ILLUMINATION
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

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
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Toon/Passes/ToonEnvironmentCommonForwardPass.hlsl"
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
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Toon/ToonEnvironmentCommonInput.hlsl"
            #include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Lit/Passes/TsukuyomiFsr3MaskPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Toon/ToonEnvironmentCommonInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Toon/ToonEnvironmentCommonInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex TsukuyomiPBRDepthNormalsVertex
            #pragma fragment TsukuyomiPBRDepthNormalsFragment

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Toon/ToonEnvironmentCommonInput.hlsl"
            #include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Lit/Passes/TsukuyomiPBRDepthNormalsPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }

            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex UniversalVertexMeta
            #pragma fragment ToonEnvironmentCommonUniversalFragmentMeta

            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature EDITOR_VISUALIZATION

            #include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Toon/ToonEnvironmentCommonInput.hlsl"
            #include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Toon/Passes/ToonEnvironmentCommonMetaPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "MotionVectors"
            Tags { "LightMode" = "MotionVectors" }
            ColorMask RG

            HLSLPROGRAM
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma shader_feature_local_vertex _ADD_PRECOMPUTED_VELOCITY

            #include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Toon/ToonEnvironmentCommonInput.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ObjectMotionVectors.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    //CustomEditor "Tsukuyomi.Rendering.Editor.ShaderGUI.TsukuyomiPBRShaderGUI"
}
