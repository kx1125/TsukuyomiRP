Shader "TsukuyomiRP/Lit/Character/CharacterHair"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Render Face", Float) = 2.0

        [Header(Character Hair Base)]
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)
        _HairBaseTintColor ("Hair Base Tint Color", Color) = (1, 1, 1, 1)
        _HairAddTintColor ("Hair Add Tint Color", Color) = (1, 1, 1, 1)

        [NoScaleOffset] _MetallicGlossMap("Metallic/Spec/Shadow/Smoothness", 2D) = "white" {}
        _OcclusionStrength("Occlusion Strength", Range(0.0, 1.0)) = 1.0

        _SplitNormalMap("Split Diffuse / Specular Normal", 2D) = "bump" {}
        _BumpScale("Normal Scale", Range(0.0, 1.0)) = 1.0
        _SpecBumpScale("Spec Normal Scale", Range(0.0, 1.0)) = 1.0

        [NoScaleOffset] _DiffuseRampMap("Diffuse Ramp", 2D) = "white" {}
        [NoScaleOffset] _SpecRampMap("Specular Ramp", 2D) = "white" {}
        [NoScaleOffset] _EmissionMap("Emission Map", 2D) = "black" {}
        _EmissionBrightness("Emission Brightness", Float) = 1.0

        [Header(Character Hair Lighting)]
        _DiffuseOffset("Diffuse Offset", Range(-1.0, 1.0)) = 0.0
        _ShadowColorBrightness ("Shadow Color Brightness", Range(0, 1)) = 0.5
        _ShadowColorSaturation ("Shadow Color Saturation", Range(0, 2)) = 1
        _SpecularScale("Specular Scale", Range(0.0, 4.0)) = 1.0
        _AnisotropyValue("Anisotropy Value", Range(0.0, 1.0)) = 0.35
        _AnisotropyDirX("Anisotropy Direction X", Range(-1.0, 1.0)) = 0.0
        _AnisotropyIntensity("Anisotropy Intensity", Range(0.0, 3.0)) = 1.0
        _AnisotropyEdgeFade("Anisotropy Edge Fade", Range(0.01, 10.0)) = 1.0
        _AnisotropyValue2("Anisotropy Value 2", Range(0.0, 1.0)) = 0.4
        _AnisotropyRange2("Anisotropy Range 2", Range(-1.0, 1.0)) = 0.0
        _AnisotropyColor2("Anisotropy Color 2", Color) = (0, 0, 0, 1)

        [Header(Character Hair Stroke And Line)]
        _StrokeMap("Stroke Map (R: Anisotropy Offset)", 2D) = "gray" {}
        _StrokeScale("Stroke Scale", Float) = 1.0
        _LineMap("Line Map", 2D) = "black" {}
        [ToggleUI] _UseLineMap("Use Line Map", Float) = 0.0
        _LineAmount("Line Amount", Float) = 300.0
        _LineValue("Line Value", Range(0.0, 1.0)) = 0.0
        _LineRange("Line Range", Range(-1.0, 1.0)) = 0.0
        _LineIntensity("Line Intensity", Range(0.0, 1.0)) = 0.0
        _LineSaturation("Line Saturation", Range(0.0, 10.0)) = 1.0

        [Toggle(DITHER)] _EnableDither("Dither", Float) = 0.0

        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 1)

        [HideInInspector] _Cutoff("Alpha Cutoff", Float) = 0.5
        [HideInInspector] _AddPrecomputedVelocity("_AddPrecomputedVelocity", Float) = 0.0
        [HideInInspector] _XRMotionVectorsPass("_XRMotionVectorsPass", Float) = 1.0
        [HideInInspector] _RMOE("RMOE", 2D) = "white" {}
        [HideInInspector] _Fsr3ReactiveScale("Reactive Scale", Range(0.0, 1.0)) = 0.9
        [HideInInspector] _Fsr3CompositionScale("Composition Scale", Range(0.0, 1.0)) = 0.0
        [HideInInspector] _QueueOffset("Queue Offset", Float) = 0.0

        [HideInInspector] _MainTex("BaseMap", 2D) = "white" {}
        [HideInInspector] _Color("Base Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _GlossMapScale("Smoothness", Float) = 0.0
        [HideInInspector] _Glossiness("Smoothness", Float) = 0.0
        [HideInInspector] _GlossyReflections("EnvironmentReflections", Float) = 0.0
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
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend One Zero
            ZWrite On
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 4.5

            #pragma vertex CharacterHairForwardVertex
            #pragma fragment CharacterHairForwardFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
            #pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
            #pragma multi_compile_fragment _ _TSUKUYOMI_SCREEN_SPACE_GLOBAL_ILLUMINATION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma shader_feature_local_fragment _ DITHER
            #pragma multi_compile_instancing
            #include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Character/Passes/CharacterHairForwardPass.hlsl"
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
            #include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Material/TsukuyomiPBRInput.hlsl"
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
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Material/TsukuyomiPBRInput.hlsl"
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
            #pragma multi_compile_instancing
            #include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Material/TsukuyomiPBRInput.hlsl"
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
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing
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
            #pragma fragment TsukuyomiPBRUniversalFragmentMeta
            #include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Lit/Passes/TsukuyomiPBRMetaPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "MotionVectors"
            Tags { "LightMode" = "MotionVectors" }
            ColorMask RG

            HLSLPROGRAM
            #pragma target 3.5
            #pragma shader_feature_local_vertex _ADD_PRECOMPUTED_VELOCITY
            #include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Material/TsukuyomiPBRInput.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ObjectMotionVectors.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
