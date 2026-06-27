Shader "SSSSkin/Standard SSS"
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
        [Toggle(TRANSMISSION)] _Transmission ("Transmission", Float) = 0
        [NoScaleOffset] _TransmissionMap ("Transmission Map", 2D) = "white" {}
        _TransmissionColor ("Transmission Color", Color) = (0.2,0.2,0.2,1)
        TransmissionOcc ("Transmission Occlusion", Range(0,1)) = 1
        TransmissionShadows ("Transmission Shadows", Range(0,1)) = 1
        TransmissionRange ("Transmission Range", Range(0,5)) = 0.5
        DynamicPassTransmission ("Dynamic Pass Transmission", Range(0,1)) = 1
        SSS_shader ("SSS Shader", Float) = 1
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
            #pragma fragment ForwardFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_instancing

            #include "Packages/tsukuyomi.render-pipelines.universal/Shaders/SSS Skin/SSSSkinCommon.hlsl"

            half4 ForwardFragment(SSSSkinVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 albedo = SampleAlbedo(input.uv);
                #if defined(_ALPHATEST_ON)
                clip(albedo.a - _Cutoff);
                #endif

                half3 normalTS = half3(0.0h, 0.0h, 1.0h);
                half3 normalWS = NormalizeNormalPerPixel(SampleNormalWS(input));
                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half occlusion = SampleOcclusion(input.uv);
                half4 pbrMask = SamplePBRMask(input.uv);
                half skinMask = ResolveSkinMask(pbrMask);

                InputData inputData = BuildSSSSkinInputData(input, normalWS, viewDirWS);
                SurfaceData surfaceData = BuildSSSSkinSurfaceData(albedo, normalTS, occlusion, pbrMask);
                half4 pbrColor = UniversalFragmentPBR(inputData, surfaceData);

                if (SSS_shader != 1.0h || skinMask <= 0.0h)
                {
                    return pbrColor;
                }

                SurfaceData specularSurfaceData = surfaceData;
                specularSurfaceData.albedo = half3(0.0h, 0.0h, 0.0h);
                half3 specularLighting = UniversalFragmentPBR(inputData, specularSurfaceData).rgb;
                half3 sssDiffuse = SampleAlbedoTexture(input.uv).rgb * SampleBlurredSSSLighting(input.positionCS);
                half3 sssColor = specularLighting + sssDiffuse;

                return half4(lerp(pbrColor.rgb, sssColor, skinMask), albedo.a);
            }
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
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
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
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
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
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _PARALLAXMAP
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"
            ENDHLSL
        }


        Pass
        {
            Name "SSSSkin Mask"
            Tags { "LightMode" = "SSSSkinMask" }

            ZWrite On
            ZTest LEqual
            Cull[_Cull]
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex SSSSkinVert
            #pragma fragment MaskFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing

            #include "Packages/tsukuyomi.render-pipelines.universal/Shaders/SSS Skin/SSSSkinCommon.hlsl"

            half4 MaskFragment(SSSSkinVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 albedo = SampleAlbedo(input.uv);
                #if defined(_ALPHATEST_ON)
                clip(albedo.a - _Cutoff);
                #endif

                if (SSS_shader != 1.0h)
                    return 0.0h;

                return ResolveSkinMask(SamplePBRMask(input.uv));
            }
            ENDHLSL
        }
        Pass
        {
            Name "SSSSkin Lighting"
            Tags { "LightMode" = "SSSSkinLighting" }

            ZWrite Off
            ZTest LEqual
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex SSSSkinVert
            #pragma fragment LightingFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ TRANSMISSION
            #pragma multi_compile _ ENABLE_DETAIL_NORMALMAP
            #pragma multi_compile_instancing

            #include "Packages/tsukuyomi.render-pipelines.universal/Shaders/SSS Skin/SSSSkinCommon.hlsl"

            half3 SSSSkinDirectLight(Light light, half3 lightingAlbedo, half3 normalWS, half3 viewDirWS, half3 transmission)
            {
                half3 attenuatedLightColor = light.color * (light.distanceAttenuation * light.shadowAttenuation);
                half3 diffuse = lightingAlbedo * LightingLambert(attenuatedLightColor, light.direction, normalWS);
                #if defined(TRANSMISSION)
                diffuse += SSSSkinTransmissionDynamic(transmission, light.direction, normalWS, viewDirWS, light.shadowAttenuation) * light.color;
                #endif
                return diffuse;
            }

            SurfaceData SSSSkinSurfaceData(half alpha)
            {
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = half3(1.0h, 1.0h, 1.0h);
                surfaceData.specular = half3(0.0h, 0.0h, 0.0h);
                surfaceData.metallic = 0.0h;
                surfaceData.smoothness = 0.0h;
                surfaceData.normalTS = half3(0.0h, 0.0h, 1.0h);
                surfaceData.emission = half3(0.0h, 0.0h, 0.0h);
                surfaceData.occlusion = 1.0h;
                surfaceData.alpha = alpha;
                surfaceData.clearCoatMask = 0.0h;
                surfaceData.clearCoatSmoothness = 1.0h;
                return surfaceData;
            }

            half4 LightingFragment(SSSSkinVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half alpha = 1.0h;
                #if defined(_ALPHATEST_ON)
                alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a;
                clip(alpha - _Cutoff);
                #endif

                if (SSS_shader != 1.0h)
                    return half4(0.0h, 0.0h, 0.0h, 1.0h);

                half skinMask = ResolveSkinMask(SamplePBRMask(input.uv));
                if (skinMask <= 0.0h)
                    return half4(0.0h, 0.0h, 0.0h, 1.0h);

                half3 normalWS = NormalizeNormalPerPixel(SampleNormalWS(input));
                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = viewDirWS;
                #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #else
                inputData.shadowCoord = float4(0.0, 0.0, 0.0, 0.0);
                #endif
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.bakedGI = SampleSH(normalWS);
                inputData.shadowMask = half4(1.0h, 1.0h, 1.0h, 1.0h);
                #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                inputData.vertexLighting = VertexLighting(input.positionWS, normalWS);
                #endif

                SurfaceData surfaceData = SSSSkinSurfaceData(alpha);
                half4 shadowMask = CalculateShadowMask(inputData);
                AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(inputData, surfaceData);
                uint meshRenderingLayers = GetMeshRenderingLayer();
                half3 lightingAlbedo = SampleLightingAlbedo(input.uv);
                half3 transmission = SampleTransmission(input.uv);
                half3 lighting = inputData.bakedGI * lightingAlbedo;

                Light mainLight = GetMainLight(inputData, shadowMask, aoFactor);
                #if defined(_LIGHT_LAYERS)
                if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
                #endif
                {
                    lighting += SSSSkinDirectLight(mainLight, lightingAlbedo, normalWS, viewDirWS, transmission);
                }

                #if defined(_ADDITIONAL_LIGHTS)
                uint pixelLightCount = GetAdditionalLightsCount();

                #if USE_CLUSTER_LIGHT_LOOP
                [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                {
                    CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                    Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
                    #if defined(_LIGHT_LAYERS)
                    if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
                    #endif
                    {
                        lighting += SSSSkinDirectLight(light, lightingAlbedo, normalWS, viewDirWS, transmission);
                    }
                }
                #endif

                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
                    #if defined(_LIGHT_LAYERS)
                    if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
                    #endif
                    {
                        lighting += SSSSkinDirectLight(light, lightingAlbedo, normalWS, viewDirWS, transmission);
                    }
                LIGHT_LOOP_END
                #endif

                #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                lighting += inputData.vertexLighting * lightingAlbedo;
                #endif

                return half4(lighting * skinMask, 1.0h);
            }
            ENDHLSL
        }
    }

    Fallback Off
}


