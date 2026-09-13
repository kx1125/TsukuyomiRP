Shader "TsukuyomiRP/Lit/Character/CharacterFace"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Render Face", Float) = 2
        [Enum(Flip, 0, Keep, 1)] _BackFaceNormalFlip("Back Face Normal", Float) = 0
        [Toggle(_ALPHATEST_ON)] _EnableAlphaTest("Alpha Test", Float) = 0
        _AlphaClipThreshold("Clip Threshold", Range(0, 1)) = 0.5

        [Header(Albedo)]
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)
        [Toggle(_EMOTION_MAP)] _UseEmotionMap("Use Emotion Map", Float) = 0
        [NoScaleOffset] _EmotionMap("Emotion Map (2 x 2)", 2D) = "black" {}
        [IntRange] _EmotionIndex("Emotion Index", Range(0, 3)) = 0
        _EmotionBlend("Emotion Blend", Range(0, 1)) = 1

        [Header(Face Lighting)]
        [Toggle(_SDFLIGHTMAP)] _UseSDFLightmap("Use SDF Lightmap", Float) = 1
        [NoScaleOffset] _SDFLightmap("SDF Lightmap", 2D) = "black" {}
        [NoScaleOffset] _SDFMask("Rim / Ordinary Skin / Rim Scale / Global Rim", 2D) = "black" {}
        [Toggle(_DIFF_RAMP_ON)] _UseDiffRampMap("Use Diffuse Ramp", Float) = 1
        [NoScaleOffset] _DiffRampMap("Diffuse Ramp", 2D) = "white" {}
        _ShadowColorBrightness("Shadow Color Brightness", Range(0, 1)) = 0.55
        _ShadowColorSaturation("Shadow Color Saturation", Range(0, 2)) = 1.2
        _SDFRimColor("Skin Rim Color", Color) = (1, 1, 1, 1)
        _FaceRimOffScale("Face Rim Scale (SDF Area)", Range(0, 1.5)) = 1
        _SkinRimOffScale("Skin Rim Scale", Range(0, 1.5)) = 0.5

        [Header(Skin Specular)]
        [Gamma] _Metallic("Metallic", Range(0, 1)) = 0
        _Specular("Specular Scale", Range(0, 1)) = 1
        _Smoothness("Smoothness", Range(0, 1)) = 0.5
        [Toggle(_NORMALMAP)] _UseBumpMap("Use Normal Map", Float) = 0
        [Normal] [NoScaleOffset] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Float) = 1
        [Toggle(_HIGHLIGHT_MAP)] _FaceHighlightMap("Use Face Highlight Map", Float) = 0
        [NoScaleOffset] _HighlightMap("Highlight Map", 2D) = "black" {}
        _HighlightMapVector("Highlight Offset (XY)", Vector) = (0.04, -0.01, 0, 0)

        [Header(Emission)]
        [Toggle(_EMISSION)] _UseEmission("Use Emission", Float) = 0
        [NoScaleOffset] _EmissionMap("Emission Map", 2D) = "black" {}
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 1)
        _EmissionBrightness("Emission Brightness", Float) = 1

        [Header(URP Lighting)]
        _DiffuseOffset("Diffuse Offset", Range(-1, 1)) = 0
        [ToggleOff(_RECEIVE_SHADOWS_OFF)] _ReceiveShadows("Receive Shadows", Float) = 1

        [Header(FSR3 Reactivity)]
        _Fsr3ReactiveScale("Reactive Scale", Range(0, 1)) = 0.9
        _Fsr3CompositionScale("Composition Scale", Range(0, 1)) = 0
        [HideInInspector] _AddPrecomputedVelocity("Add Precomputed Velocity", Float) = 0
        [HideInInspector] _XRMotionVectorsPass("XR Motion Vectors", Float) = 1
        [HideInInspector] _QueueOffset("Queue Offset", Float) = 0
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
            #pragma vertex TsukuyomiFaceForwardVertex
            #pragma fragment TsukuyomiFaceForwardFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SDFLIGHTMAP
            #pragma shader_feature_local_fragment _DIFF_RAMP_ON
            #pragma shader_feature_local_fragment _EMOTION_MAP
            #pragma shader_feature_local_fragment _HIGHLIGHT_MAP
            #pragma shader_feature_local_fragment _NORMALMAP
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
            #pragma multi_compile_fragment _ _TSUKUYOMI_SCREEN_SPACE_GLOBAL_ILLUMINATION
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
            #pragma multi_compile_instancing
            #include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Character/Passes/CharacterFaceForwardPass.hlsl"
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
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing
            #include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Material/CharacterFaceInput.hlsl"
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
            #pragma target 3.5
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Material/CharacterFaceInput.hlsl"
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
            #pragma target 3.5
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Material/CharacterFaceInput.hlsl"
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
            #pragma target 3.5
            #pragma vertex TsukuyomiFaceDepthNormalsVertex
            #pragma fragment TsukuyomiFaceDepthNormalsFragment
            #pragma shader_feature_local_fragment _NORMALMAP
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_instancing
            #include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Character/Passes/CharacterFaceDepthNormalsPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex UniversalVertexMeta
            #pragma fragment TsukuyomiFaceMetaFragment
            #pragma shader_feature_local_fragment _SDFLIGHTMAP
            #pragma shader_feature_local_fragment _EMOTION_MAP
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature EDITOR_VISUALIZATION
            #include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Character/Passes/CharacterFaceMetaPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "MotionVectors"
            Tags { "LightMode" = "MotionVectors" }
            ColorMask RG
            Cull[_Cull]

            HLSLPROGRAM
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma shader_feature_local_vertex _ADD_PRECOMPUTED_VELOCITY
            #include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Material/CharacterFaceInput.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ObjectMotionVectors.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
