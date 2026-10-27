Shader "TsukuyomiRP/Character/Studio EyeShadow"
{
    Properties
    {
        _StudioEyeShadowScale("Studio Eye Shadow Scale", Float) = .5
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Source Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Destination Blend", Float) = 0
        [ToggleUI] _ZWrite("Depth Write", Float) = 1
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clipping", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Render Face", Float) = 2
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.5
        [HideInInspector] _AlphaToMask("Alpha To Mask", Float) = 0
        [HideInInspector] _AddPrecomputedVelocity("Add Precomputed Velocity", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "UniversalMaterialType"="Lit" "IgnoreProjector"="True" "TsukuyomiRTShadow"="True" }
        LOD 300

        HLSLINCLUDE
        #define STUDIO_EYE_SHADOW 1
        #define TSUKUYOMI_MATERIAL_INPUT "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/Material/StudioEyeShadowInput.hlsl"
        #define TSUKUYOMI_DEPTH_NORMAL(uv) half3(0,0,1)
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull[_Cull]
            AlphaToMask[_AlphaToMask]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex StudioEyeShadowVertex
            #pragma fragment StudioEyeShadowFragment
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Character/Passes/StudioEyeShadowPass.hlsl"
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
            #include TSUKUYOMI_MATERIAL_INPUT
            #pragma target 4.5
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include TSUKUYOMI_MATERIAL_INPUT
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
            #include TSUKUYOMI_MATERIAL_INPUT
            #pragma target 4.5
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include TSUKUYOMI_MATERIAL_INPUT
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
            #include TSUKUYOMI_MATERIAL_INPUT
            #pragma target 4.5
            #pragma vertex TsukuyomiSpecularPBRDepthNormalsVertex
            #pragma fragment TsukuyomiSpecularPBRDepthNormalsFragment

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Lit/Passes/TsukuyomiSpecularPBRDepthNormalsPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "MotionVectors"
            Tags { "LightMode" = "MotionVectors" }
            Cull[_Cull]
            ColorMask RG

            HLSLPROGRAM
            #include TSUKUYOMI_MATERIAL_INPUT
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma shader_feature_local_vertex _ADD_PRECOMPUTED_VELOCITY

            #include TSUKUYOMI_MATERIAL_INPUT
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ObjectMotionVectors.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "TsukuyomiRTShadow"
            Tags { "LightMode" = "TsukuyomiRTShadow" }

            HLSLPROGRAM
            #pragma only_renderers d3d11
            #pragma raytracing surface_shader
            #pragma shader_feature_local_raytracing _ALPHATEST_ON
            #pragma shader_feature_local_raytracing _SURFACE_TYPE_TRANSPARENT
            #include TSUKUYOMI_MATERIAL_INPUT
            #include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Ray Traced Shadow/RTShadowCaster.hlsl"
            ENDHLSL
        }

    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
