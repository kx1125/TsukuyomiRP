Shader "SSSSkin/Standard PBR"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1,1,1,1)
        [MainTexture] _BaseMap ("Albedo", 2D) = "white" {}
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clipping", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        [NoScaleOffset] _OcclusionMap ("Occlusion", 2D) = "white" {}
        _OcclusionColor ("Occlusion Color", Color) = (0,0,0,1)
        [NoScaleOffset] _PBRMask ("PBR Mask (R Specular G Roughness B Metallic A Skin)", 2D) = "white" {}
        _Metallic ("Metallic", Range(0,1)) = 0
        _SpecColor ("Specular Color", Color) = (0.2,0.2,0.2,1)
        _Roughness ("Roughness", Range(0,1)) = 0.5
        [NoScaleOffset] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Range(0,2)) = 1
        _BumpTile ("Normal Tile", Range(1,20)) = 1
        [Toggle(ENABLE_DETAIL_NORMALMAP)] _DetailNormal ("Detail Normal", Float) = 0
        [NoScaleOffset] _DetailNormalMap ("Detail Normal Map", 2D) = "bump" {}
        _DetailNormalMapScale ("Detail Normal Scale", Range(0,2)) = 1
        _DetailNormalMapTile ("Detail Normal Tile", Float) = 1

        [Header(Tsukuyomi Lighting)]
        _MicroShadowOpacity("Micro Shadow Opacity", Range(0.0, 1.0)) = 1.0
        _RoughDiffuseStrength("Rough Diffuse Strength", Range(0.0, 1.0)) = 1.0
        _IndirectSpecularFGDStrength("Indirect Specular FGD Strength", Range(0.0, 1.0)) = 1.0
        _IndirectDiffuseIntensity("Indirect Diffuse Intensity", Range(0.0, 2.0)) = 1.0
        _IndirectSpecularIntensity("Indirect Specular Intensity", Range(0.0, 2.0)) = 1.0
        _HorizonOcclusionPower("Horizon Occlusion Power", Range(0.0, 4.0)) = 2.0

        [HideInInspector] _Surface ("Surface", Float) = 0.0
        [HideInInspector] _Cull ("Cull", Float) = 2.0
        [HideInInspector] _AlphaToMask ("Alpha To Mask", Float) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex SSSSkinVert
            #pragma fragment SSSSkinPBRForwardFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ ENABLE_DETAIL_NORMALMAP
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            #include "Packages/tsukuyomi.render-pipelines.universal/Shaders/SSS Skin/SSSSkinPass.hlsl"
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
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/tsukuyomi.render-pipelines.universal/Shaders/SSS Skin/SSSSkinInput.hlsl"
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

            #include "Packages/tsukuyomi.render-pipelines.universal/Shaders/SSS Skin/SSSSkinInput.hlsl"
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
            #pragma vertex SSSSkinDepthNormalsVertex
            #pragma fragment SSSSkinDepthNormalsFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile _ ENABLE_DETAIL_NORMALMAP
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/tsukuyomi.render-pipelines.universal/Shaders/SSS Skin/SSSSkinPass.hlsl"
            ENDHLSL
        }
    }

    Fallback Off
}
