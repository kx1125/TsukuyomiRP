Shader "TsukuyomiRP/Character/Studio Character"
{
    Properties
    {
        [Header(Studio Source Texture Adapter)]
        [Enum(Standard,0,Face,1,Body,2,Hair,3,Eye,4,Clothes,5,AITClothes,6)] _SourceMode("Source Material", Float) = 0
        _AITDetailMap2("AIT Second Cloth Detail", 2D) = "white" {}
        _AITWeatherMask("AIT Weather UV Rotation", 2D) = "black" {}
        _AITPattern("AIT Pattern UV Scale", Vector) = (1,1,0,0)
        _AITDetailUV1("AIT Detail UV 1", Vector) = (1,1,0,0)
        _AITDetailUV2("AIT Detail UV 2", Vector) = (1,1,0,0)
        _AITRotation("AIT Detail Rotation", Vector) = (0,0,0,0)
        _AITDetail1("AIT Detail 1 Normal Gloss Metal Occlusion", Vector) = (3,1,1,.5)
        _AITDetail2("AIT Detail 2 Normal Gloss Metal Occlusion", Vector) = (3,1,1,.5)
        _AITWeatherUV("AIT Weather UV", Vector) = (3,3,0,0)
        _AITDither("AIT Dither Alpha", Float) = 0
        _AITAlphaEx("AIT Tear Coverage", Float) = 1
        _AITCurvature("AIT Curvature", Float) = 0
        _AITOcclusion("AIT Occlusion Strength", Float) = 1
        _AITTransmission("AIT Transmission Distortion Power Direct Strength", Vector) = (.55,3,.1,6)
        _AITTransShadow("AIT Transmission Shadow", Float) = .9
        _ClothColorMask("Clothing Color Regions", 2D) = "black" {}
        _ClothPackedMap("Clothing (R Gloss G Emission B Metallic)", 2D) = "white" {}
        _ClothBaseTint("Clothing Base Tint", Color) = (1,1,1,1)
        _ClothColor2("Clothing Color 2", Color) = (1,1,1,1)
        _ClothColor3("Clothing Color 3", Color) = (1,1,1,1)
        _ClothColor4("Clothing Color 4", Color) = (1,1,1,1)
        _ClothGloss("Clothing Region Smoothness", Vector) = (0.9,1,1,1)
        _ClothGlossWeight("Clothing Gloss Map Weights", Vector) = (1,1,1,1)
        _ClothMetal("Clothing Region Metallic", Vector) = (1,1,1,1)
        _ClothMetalWeight("Clothing Metal Map Weights", Vector) = (1,1,1,1)
        _SourceGloss("Source Smoothness", Range(0,1)) = 0
        [HideInInspector] _StudioSssOccluder("Captured SSS Replacement Occluder", Float) = 0
        _SourceGlossWeight("Source Gloss Map Weight", Range(0,1)) = 1
        _SourceDetailMap("Local Material Mask (R Metal B Gloss A Coverage)", 2D) = "black" {}
        _SourceGlossMap("Source Gloss Modulation (R)", 2D) = "white" {}
        _RegionMask("Local Skin Mask", 2D) = "black" {}
        _PoreMap("Pore Height (A)", 2D) = "gray" {}
        _PoreUV("Pore Tiling and Offset", Vector) = (100,100,0,0)
        _PoreStrength("Pore Normal Strength", Float) = 0
        _NailGloss("Nail Smoothness", Range(0,1)) = 0.65
        _BodyRegionMap("Body Region (R Color G Height B Coverage)", 2D) = "black" {}
        _BodyRegionColor("Body Region Color", Color) = (1,1,1,0)
        _BodyRegionSubColor("Body Region Subsurface Color", Color) = (.72,.72,.72,1)
        _BodyRegionScale("Body Region Scale", Float) = 1
        _BodyRegionGloss("Body Region Smoothness", Range(0,1)) = 0.7
        _BodyRegionBump("Body Region Height Strength", Float) = 0
        _NailColor("Nail Color", Color) = (1,1,1,0)
        _SubsurfaceAlbedo("Subsurface Albedo and Vein Mask", 2D) = "white" {}
        _SubsurfaceWeight("Subsurface Albedo Weight", Range(0,1)) = 0.35
        _StudioSubColor("Subsurface Base Color", Color) = (0.96,1,1,1)
        _StudioSubAdjust("Subsurface Hue Contrast Brightness Saturation", Vector) = (0,.5,.5,.5)
        _StudioTransColor("Source Transmission Color", Color) = (.75,.35,0,1)
        _StudioTransParams("Transmission Distortion Rim Power Strength", Vector) = (.58,.01,2,30)
        _StudioTransShadow("Transmission Shadow", Float) = 1
        _StudioTransAmbient("Source Ambient Transmission", Float) = 0
        _StudioAlbedoGain("Source Scattered Albedo Gain", Float) = 1.3
        _StudioUnitsPerWorldUnit("Studio Units Per World Unit", Float) = 10
        _StudioPunctualShadowBias("Studio Punctual Shadow Bias", Float) = .0015
        _StudioSpotShadowNear("Studio Spot Shadow Near Plane", Float) = .1
        _StudioShadowSlopeOffset("Studio Shadow Slope Offset", Float) = -1.5
        _SubsurfaceSaturation("Subsurface Albedo Saturation", Range(0,2)) = 1
        _VeinColor("Vein Color", Color) = (0.5686,0.9294,0.949,1)
        _VeinOpacity("Vein Opacity", Range(0,1)) = 0
        _WetMap("Wet Streaks (R), Region (G), Splats (B)", 2D) = "black" {}
        _WetEffect("Wet Effect", Range(0,1)) = 0
        _WetSkinGloss("Wet Skin Smoothness", Range(0,1)) = 0.65
        _WetGlossLevel("Wet Surface Smoothness", Range(0,0.83)) = 0.83
        _WetSplatOpacity("Wet Splat Coverage", Range(0,1)) = 1
        _WetStreakGloss("Wet Streak Smoothness", Range(0,1)) = 1
        _WetStreakBump("Wet Streak Normal Strength", Float) = 0.1
        _WetRegion("Wet Region Coverage", Range(0,1)) = 1
        _WetDropsAmount("Wet Drops Amount", Range(0,1)) = 0
        _WetDropsScale("Wet Drops Scale", Float) = 58
        _WetDropOpacity("Wet Drop Coverage", Range(0,1)) = 0
        _WetDropBump("Wet Drop Height Strength", Float) = .4
        _WetVoronoi("Wet Drop Distribution Amount Angle Scale", Vector) = (.03,.28,11,0)
        _HairAnisotropy("Hair Anisotropy", Range(0,1)) = 0.65
        _HairGradeMap("Hair Gradient Mask", 2D) = "white" {}
        _HairSpecular("Source Hair Specular Color", Color) = (.415,.4085,.4014,1)
        _HairRingParams("Source Hair Rim Power Highlight Ring Off Control", Vector) = (3.4,8,0,1)
        _HairNoiseMap("Hair Fine Normal", 2D) = "bump" {}
        _HairNoiseStrength("Hair Fine Normal Blend", Range(0,1)) = 0.42
        _FaceBrowMap("Eyebrow Mask (B) and Height (G)", 2D) = "black" {}
        _FaceBrowColor("Eyebrow Color", Color) = (0.05,0.05,0.05,1)
        _FaceBrowUV("Eyebrow Layout", Vector) = (0,0,1,1)
        _FaceBrowRotation("Eyebrow Rotation", Float) = 0
        _FaceBrowGloss("Eyebrow Smoothness", Float) = .5
        _StudioLashes("Studio Eyelashes", Float) = 0
        [HideInInspector] _StudioSourceAlphaMap("Original replacement-pass alpha", 2D) = "white" {}
        _StudioLashCutout("Studio Lash Cutout Scale", Float) = .75
        _StudioEyeShadow("Studio Eye Shadow", Float) = 0
        _StudioEyeShadowScale("Studio Eye Shadow Scale", Float) = .5
        _FaceBlushColor("Blush Color", Color) = (1,1,1,0)
        _FaceBlushStrength("Blush Strength", Float) = 0
        _HairTopColor("Hair Root Color", Color) = (0.1,0.1,0.1,1)
        _HairTipColor("Hair Tip Color", Color) = (0.65,0.65,0.65,1)
        _HairTopPower("Hair Root Gradient", Float) = 2
        _HairTipPower("Hair Tip Gradient", Float) = 2
        _IrisMap("Iris Mask", 2D) = "black" {}
        _PupilMap("Pupil Mask", 2D) = "black" {}
        _HighlightMap("Eye Highlight", 2D) = "black" {}
        _IrisColor("Iris Color", Color) = (0.35,0.35,0.35,1)
        _PupilColor("Pupil Color", Color) = (0.03,0.03,0.03,1)
        _HighlightColor("Eye Highlight Color", Color) = (1,1,1,0.75)
        _EyeParallax("Eye Parallax", Float) = 0.2
        _EyeBaseOffset("Eye Base Offset", Vector) = (0,0,0,0)
        _EyeHighlightUV("Eye Highlight UV", Vector) = (1,1,0,0)
        _EyeHighlightRotation("Eye Highlight Rotation", Float) = 0
        _EyeEmission("Eye Emission", Float) = 0
        _EyeShininess("Eye Iris Shininess", Float) = 0.1
        _EyeOcclusionStrength("Eye Occlusion Strength", Float) = 1
        _EyeTransmission("Eye Transmission Distortion Power Direct Strength", Vector) = (.9,2,.1,18)
        _EyeTransShadow("Eye Transmission Shadows", Float) = .85
        _IrisUV("Iris UV", Vector) = (1,1,0,0)
        _PupilUV("Pupil UV", Vector) = (1,1,0,0)
        _Opacity("Opacity", Range(0,1)) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Source Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Destination Blend", Float) = 0
        [Toggle] _ZWrite("Depth Write", Float) = 1

        [Enum(Common,0,Skin,1)] _MaterialType("Material Type", Float) = 0
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clipping", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Render Face", Float) = 2
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.5
        [NoScaleOffset] _PBRMask("PBR Mask (R Specular G Roughness B Metallic A Skin)", 2D) = "white" {}
        _Roughness("Roughness", Range(0,1)) = 0.5
        _Metallic("Metallic", Range(0,1)) = 0
        [NoScaleOffset] _OcclusionMap("Occlusion", 2D) = "white" {}
        _OcclusionColor("Occlusion Color", Color) = (0,0,0,1)
        [Normal][NoScaleOffset] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Range(0,2)) = 1
        _BumpTile("Normal Tile", Range(1,20)) = 1
        [Toggle(ENABLE_DETAIL_NORMALMAP)] _DetailNormal("Detail Normal", Float) = 0
        [Normal][NoScaleOffset] _DetailNormalMap("Detail Normal Map", 2D) = "bump" {}
        _DetailNormalMapScale("Detail Normal Scale", Range(0,2)) = 1
        _DetailNormalMapTile("Detail Normal Tile", Float) = 1
        [NoScaleOffset] _EmissionMap("Emission Map", 2D) = "white" {}
        [HDR] _EmissionColor("Emission Color", Color) = (0,0,0,1)
        [Toggle(TRANSMISSION)] _Transmission("Transmission", Float) = 0
        [NoScaleOffset] _TransmissionMap("Transmission Map", 2D) = "white" {}
        _TransmissionColor("Transmission Color", Color) = (0.2,0.2,0.2,1)
        TransmissionOcc("Transmission Occlusion", Range(0,1)) = 1
        TransmissionShadows("Transmission Shadows", Range(0,1)) = 1
        TransmissionRange("Transmission Range", Range(0,5)) = 0.5
        DynamicPassTransmission("Dynamic Pass Transmission", Range(0,1)) = 1
        _MicroShadowOpacity("Micro Shadow Opacity", Range(0,1)) = 1
        _IndirectDiffuseIntensity("Indirect Diffuse Intensity", Range(0,2)) = 1
        _IndirectSpecularIntensity("Indirect Specular Intensity", Range(0,2)) = 1
        _HorizonOcclusionPower("Horizon Occlusion Power", Range(0,4)) = 2
        [ToggleOff(_SPECULARHIGHLIGHTS_OFF)] _SpecularHighlights("Specular Highlights", Float) = 1
        [ToggleOff(_ENVIRONMENTREFLECTIONS_OFF)] _EnvironmentReflections("Environment Reflections", Float) = 1
        [ToggleOff(_RECEIVE_SHADOWS_OFF)] _ReceiveShadows("Receive Shadows", Float) = 1
        _QueueOffset("Queue Offset", Float) = 0
        [HideInInspector] _Surface("Surface", Float) = 0
        [HideInInspector] _AlphaToMask("Alpha To Mask", Float) = 0
        [HideInInspector] _AddPrecomputedVelocity("Add Precomputed Velocity", Float) = 0
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
            "TsukuyomiRTShadow" = "True"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull[_Cull]
            AlphaToMask[_AlphaToMask]

            HLSLPROGRAM
            #pragma shader_feature_local _STUDIO_AIT_CLOTH
            #pragma shader_feature_local _STUDIO_SKIN
            #include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Character/StudioCharacterInput.hlsl"
            #pragma target 4.5
            #pragma vertex TsukuyomiSpecularPBRForwardVertex
            #pragma fragment TsukuyomiSpecularPBRForwardFragment
            #pragma multi_compile_local_fragment _ _SSS_ON

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local ENABLE_DETAIL_NORMALMAP
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF

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

            #include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Character/StudioCharacterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            // URP supplies (constant=1, slope=2.5); Studio's GPU state is (1,1).
            // Per-shader Offset is additive to CommandBuffer.SetGlobalDepthBias.
            Offset [_StudioShadowSlopeOffset], 0

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma shader_feature_local _STUDIO_AIT_CLOTH
            #pragma shader_feature_local _STUDIO_SKIN
            #include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Character/StudioCharacterInput.hlsl"
            #pragma target 4.5
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Character/StudioCharacterInput.hlsl"
            #include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Character/StudioCharacterShadowCaster.hlsl"
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
            #pragma shader_feature_local _STUDIO_AIT_CLOTH
            #pragma shader_feature_local _STUDIO_SKIN
            #include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Character/StudioCharacterInput.hlsl"
            #pragma target 4.5
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Character/StudioCharacterInput.hlsl"
            #include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Character/StudioCharacterDepthOnly.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull[_Cull]

            HLSLPROGRAM
            #define STUDIO_GEOMETRIC_SKIN_DEPTH 1
            #pragma shader_feature_local _STUDIO_AIT_CLOTH
            #pragma shader_feature_local _STUDIO_SKIN
            #include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Character/StudioCharacterInput.hlsl"
            #pragma target 4.5
            #pragma vertex TsukuyomiSpecularPBRDepthNormalsVertex
            #pragma fragment TsukuyomiSpecularPBRDepthNormalsFragment

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local ENABLE_DETAIL_NORMALMAP
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Character/StudioCharacterDepthNormals.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }

            Cull Off

            HLSLPROGRAM
            #pragma shader_feature_local _STUDIO_AIT_CLOTH
            #pragma shader_feature_local _STUDIO_SKIN
            #include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Character/StudioCharacterInput.hlsl"
            #pragma target 4.5
            #pragma vertex UniversalVertexMeta
            #pragma fragment TsukuyomiSpecularPBRMetaFragment

            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature EDITOR_VISUALIZATION

            #include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Lit/Passes/TsukuyomiSpecularPBRMetaPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "MotionVectors"
            Tags { "LightMode" = "MotionVectors" }
            Cull[_Cull]
            ColorMask RG

            HLSLPROGRAM
            #pragma shader_feature_local _STUDIO_AIT_CLOTH
            #pragma shader_feature_local _STUDIO_SKIN
            #include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Character/StudioCharacterInput.hlsl"
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma shader_feature_local_vertex _ADD_PRECOMPUTED_VELOCITY

            #include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Character/StudioCharacterInput.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ObjectMotionVectors.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "SSSSkinMask"
            Tags { "LightMode" = "SSSSkinMask" }
            ZWrite On
            ZTest LEqual
            Cull[_Cull]
            ColorMask R
            HLSLPROGRAM
            #pragma shader_feature_local _STUDIO_AIT_CLOTH
            #pragma shader_feature_local _STUDIO_SKIN
            #include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Character/StudioCharacterInput.hlsl"
            #pragma target 4.5
            #pragma vertex TsukuyomiSpecularPBRForwardVertex
            #pragma fragment TsukuyomiSpecularPBRMaskFragment
            #pragma multi_compile_local_fragment _ _SSS_ON

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local ENABLE_DETAIL_NORMALMAP
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF

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
            #pragma multi_compile_fragment _ _TSUKUYOMI_SCREEN_SPACE_GLOBAL_ILLUMINATION
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"


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

            #include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Character/StudioCharacterPass.hlsl"

            ENDHLSL
        }

        Pass
        {
            Name "SSSSkinLighting"
            Tags { "LightMode" = "SSSSkinLighting" }
            ZWrite Off
            ZTest LEqual
            Cull[_Cull]

            HLSLPROGRAM
            #pragma shader_feature_local _STUDIO_AIT_CLOTH
            #pragma shader_feature_local _STUDIO_SKIN
            #define TSUKUYOMI_SPECULAR_PBR_SSS_LIGHTING_PASS 1
            #pragma shader_feature_local_fragment TRANSMISSION
            #include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Character/StudioCharacterInput.hlsl"
            #pragma target 4.5
            #pragma vertex TsukuyomiSpecularPBRForwardVertex
            #pragma fragment TsukuyomiSpecularPBRSSSLightingFragment
            #pragma multi_compile_local_fragment _ _SSS_ON

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local ENABLE_DETAIL_NORMALMAP
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF

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
            #pragma multi_compile_fragment _ _TSUKUYOMI_SCREEN_SPACE_GLOBAL_ILLUMINATION
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"


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

            #include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Character/StudioCharacterPass.hlsl"

            ENDHLSL
        }

        Pass
        {
            Name "TsukuyomiRTShadow"
            Tags { "LightMode" = "TsukuyomiRTShadow" }

            HLSLPROGRAM
            #pragma shader_feature_local _STUDIO_AIT_CLOTH
            #pragma shader_feature_local _STUDIO_SKIN
            #pragma only_renderers d3d11
            #pragma raytracing surface_shader
            #pragma shader_feature_local_raytracing _ALPHATEST_ON
            #pragma shader_feature_local_raytracing _SURFACE_TYPE_TRANSPARENT
            #include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Character/StudioCharacterInput.hlsl"
            #include "Packages/tsukuyomi.render-pipelines.universal/Shaders/Ray Traced Shadow/RTShadowCaster.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"

}
