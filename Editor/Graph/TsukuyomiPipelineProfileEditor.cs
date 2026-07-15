using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using Tsukuyomi.Rendering;

namespace Tsukuyomi.Rendering.Editor
{
    [CustomEditor(typeof(TsukuyomiPipelineProfile))]
    public class TsukuyomiPipelineProfileEditor : UnityEditor.Editor
    {
        private static bool s_RenderingFeaturesExpanded = true;
        private static bool s_PlanarReflectionExpanded = true;
        private static bool s_PcssExpanded = true;
        private static bool s_PerObjectShadowExpanded = true;
        private static bool s_ContactShadowExpanded = true;
        private static bool s_GtaoExpanded = true;
        private static bool s_VolumeLightExpanded = true;
        private static bool s_PostProcessingExpanded = true;
        private static bool s_SssSkinExpanded = true;
        private static bool s_PassListExpanded = true;

        public override void OnInspectorGUI()
        {
            var profile = (TsukuyomiPipelineProfile)target;
            serializedObject.Update();

            EditorGUILayout.Space();
            if (GUILayout.Button("Open Graph Editor", GUILayout.Height(40)))
            {
                TsukuyomiGraphWindow.Open(profile);
            }
            EditorGUILayout.Space();

            DrawPreloadedResourcesStatus();
            EditorGUILayout.Space();
            DrawRenderingFeatures();
            EditorGUILayout.Space();
            DrawPassList(profile);

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawPreloadedResourcesStatus()
        {
            if (TsukuyomiRenderPipelineResourcesPreloader.LoadDefaultResources() == null)
            {
                EditorGUILayout.HelpBox("Tsukuyomi RP default resources asset is missing.", MessageType.Error);
                return;
            }

            if (TsukuyomiRenderPipelineResourcesPreloader.IsDefaultResourcesPreloaded())
                return;

            EditorGUILayout.HelpBox("Tsukuyomi RP default resources are not registered in Player Settings > Preloaded Assets.", MessageType.Warning);
            if (GUILayout.Button("Preload Tsukuyomi RP Default Resources"))
            {
                TsukuyomiRenderPipelineResourcesPreloader.RegisterDefaultResources();
            }
        }

        private void DrawRenderingFeatures()
        {
            s_RenderingFeaturesExpanded = CoreEditorUtils.DrawHeaderFoldout("Rendering Features", s_RenderingFeaturesExpanded);

            if (!s_RenderingFeaturesExpanded)
            {
                return;
            }

            EditorGUI.indentLevel++;
            DrawPlanarReflectionSettings();
            DrawPcssSettings();
            DrawPerObjectShadowSettings();
            DrawContactShadowSettings();
            DrawGtaoSettings();
            DrawVolumeLightSettings();
            DrawPostProcessingSettings();
            DrawSssSkinSettings();
            EditorGUI.indentLevel--;
        }

        private void DrawPlanarReflectionSettings()
        {
            SerializedProperty enablePlanarReflection = serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.EnablePlanarReflection));

            CoreEditorUtils.DrawSplitter();
            s_PlanarReflectionExpanded = CoreEditorUtils.DrawHeaderToggleFoldout(
                EditorGUIUtility.TrTextContent("Planar Reflection"),
                s_PlanarReflectionExpanded,
                enablePlanarReflection,
                null,
                null,
                null,
                null);

            if (s_PlanarReflectionExpanded)
            {
                EditorGUI.indentLevel++;
                using (new EditorGUI.DisabledScope(!enablePlanarReflection.boolValue))
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.PlanarReflectionRenderTextureScale)));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.PlanarReflectionLayerMask)));
                    EditorGUILayout.HelpBox("Add a TsukuyomiPlanarReflectionPlane component in the scene to define the active reflection plane. The reflection pass renders the scene skybox before opaque objects.", MessageType.Info);
                }
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
            CoreEditorUtils.DrawSplitter();
        }

        private void DrawPcssSettings()
        {
            SerializedProperty enablePcss = serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.EnablePCSS));

            CoreEditorUtils.DrawSplitter();
            s_PcssExpanded = CoreEditorUtils.DrawHeaderToggleFoldout(
                EditorGUIUtility.TrTextContent("PCSS Screen Space Shadows"),
                s_PcssExpanded,
                enablePcss,
                null,
                null,
                null,
                null);

            if (s_PcssExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.PcssFindBlockerSampleCount)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.PcssPcfSampleCount)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.PcssAngularDiameter)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.PcssBlockerSearchAngularDiameter)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.PcssMinFilterMaxAngularDiameter)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.PcssMaxPenumbraSize)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.PcssMaxSamplingDistance)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.PcssMinFilterSizeTexels)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.PcssPenumbraMaskScale)));
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
            CoreEditorUtils.DrawSplitter();
        }

        private void DrawPerObjectShadowSettings()
        {
            SerializedProperty enablePerObjectShadow = serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.EnablePerObjectShadow));

            s_PerObjectShadowExpanded = CoreEditorUtils.DrawHeaderToggleFoldout(
                EditorGUIUtility.TrTextContent("Per Object Shadows"),
                s_PerObjectShadowExpanded,
                enablePerObjectShadow,
                null,
                null,
                null,
                null);

            if (s_PerObjectShadowExpanded)
            {
                EditorGUI.indentLevel++;
                using (new EditorGUI.DisabledScope(!enablePerObjectShadow.boolValue))
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.PerObjectShadowRenderingLayer)));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.PerObjectShadowDepthBits)));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.PerObjectShadowTileResolution)));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.PerObjectShadowLengthOffset)));
                }
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
            CoreEditorUtils.DrawSplitter();
        }
        private void DrawVolumeLightSettings()
        {
            SerializedProperty enableVolumeLight = serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.EnableVolumeLight));

            s_VolumeLightExpanded = CoreEditorUtils.DrawHeaderToggleFoldout(
                EditorGUIUtility.TrTextContent("Volume Light"),
                s_VolumeLightExpanded,
                enableVolumeLight,
                null,
                null,
                null,
                null);

            if (s_VolumeLightExpanded)
            {
                EditorGUI.indentLevel++;
                using (new EditorGUI.DisabledScope(!enableVolumeLight.boolValue))
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.VolumeLightDistance)));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.VolumeLightBaseHeight)));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.VolumeLightMaximumHeight)));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.VolumeLightEnableGround)));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.VolumeLightGroundHeight)));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.VolumeLightDensity)));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.VolumeLightAttenuationDistance)));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.VolumeLightEnableProbeVolumeContribution)));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.VolumeLightProbeVolumeContributionWeight)));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.VolumeLightEnableMainLightContribution)));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.VolumeLightAnisotropy)));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.VolumeLightScattering)));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.VolumeLightTint)));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.VolumeLightEnableAdditionalLightsContribution)));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.VolumeLightMaxSteps)));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.VolumeLightBlurIterations)));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.VolumeLightTransmittanceThreshold)));
                }
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
            CoreEditorUtils.DrawSplitter();
        }

        private void DrawPostProcessingSettings()
        {
            SerializedProperty enablePostProcessing = serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.EnableTsukuyomiPostProcessing));
            SerializedProperty enableCustomBloom = serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.EnableCustomBloom));
            SerializedProperty enableTonemapping = serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.EnableTonemapping));

            s_PostProcessingExpanded = CoreEditorUtils.DrawHeaderToggleFoldout(
                EditorGUIUtility.TrTextContent("Post Processing"),
                s_PostProcessingExpanded,
                enablePostProcessing,
                null,
                null,
                null,
                null);

            if (s_PostProcessingExpanded)
            {
                EditorGUI.indentLevel++;
                using (new EditorGUI.DisabledScope(!enablePostProcessing.boolValue))
                {
                    EditorGUILayout.PropertyField(enableCustomBloom, EditorGUIUtility.TrTextContent("Custom Bloom"));
                    using (new EditorGUI.DisabledScope(!enableCustomBloom.boolValue))
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.CustomBloomThreshold)));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.CustomBloomIntensity)));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.CustomBloomLumRangeScale)));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.CustomBloomPreFilterScale)));
                        DrawClampedVector4(
                            serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.CustomBloomBlurCompositeWeight)),
                            EditorGUIUtility.TrTextContent("Custom Bloom Blur Composite Weight"));
                        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.CustomBloomTint)));
                        EditorGUI.indentLevel--;
                    }

                    EditorGUILayout.PropertyField(enableTonemapping, EditorGUIUtility.TrTextContent("Tonemapping"));
                    using (new EditorGUI.DisabledScope(!enableTonemapping.boolValue))
                    {
                        EditorGUI.indentLevel++;
                        SerializedProperty tonemappingMode = serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.TonemappingMode));
                        EditorGUILayout.PropertyField(tonemappingMode);

                        if ((TsukuyomiTonemappingMode)tonemappingMode.enumValueIndex == TsukuyomiTonemappingMode.GranTurismo)
                        {
                            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.TonemappingMaxBrightness)));
                            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.TonemappingContrast)));
                            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.TonemappingLinearSectionStart)));
                            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.TonemappingLinearSectionLength)));
                            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.TonemappingBlackPow)));
                            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.TonemappingBlackMin)));
                        }

                        EditorGUI.indentLevel--;
                    }
                }
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
            CoreEditorUtils.DrawSplitter();
        }

        private static void DrawClampedVector4(SerializedProperty property, GUIContent label)
        {
            if (property == null)
                return;

            Vector4 value = property.vector4Value;
            value.x = Mathf.Clamp01(value.x);
            value.y = Mathf.Clamp01(value.y);
            value.z = Mathf.Clamp01(value.z);
            value.w = Mathf.Clamp01(value.w);

            EditorGUILayout.LabelField(label);
            EditorGUI.indentLevel++;
            value.x = EditorGUILayout.Slider("X", value.x, 0.0f, 1.0f);
            value.y = EditorGUILayout.Slider("Y", value.y, 0.0f, 1.0f);
            value.z = EditorGUILayout.Slider("Z", value.z, 0.0f, 1.0f);
            value.w = EditorGUILayout.Slider("W", value.w, 0.0f, 1.0f);
            EditorGUI.indentLevel--;

            property.vector4Value = value;
        }

        private void DrawGtaoSettings()
        {
            SerializedProperty enableGtao = serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.EnableGTAO));

            s_GtaoExpanded = CoreEditorUtils.DrawHeaderToggleFoldout(
                EditorGUIUtility.TrTextContent("Ground Truth Ambient Occlusion"),
                s_GtaoExpanded,
                enableGtao,
                null,
                null,
                null,
                null);

            if (s_GtaoExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.GtaoDownSample)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.GtaoIntensity)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.GtaoDirectLightingStrength)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.GtaoRadius)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.GtaoThickness)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.GtaoSpatialBilateralAggressiveness)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.GtaoBlurSharpness)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.GtaoStepCount)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.GtaoMaximumRadiusInPixels)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.GtaoDirectionCount)));
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
            CoreEditorUtils.DrawSplitter();
        }

        private void DrawContactShadowSettings()
        {
            SerializedProperty enableContactShadow = serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.EnableContactShadow));

            s_ContactShadowExpanded = CoreEditorUtils.DrawHeaderToggleFoldout(
                EditorGUIUtility.TrTextContent("Contact Shadows"),
                s_ContactShadowExpanded,
                enableContactShadow,
                null,
                null,
                null,
                null);

            if (s_ContactShadowExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.ContactShadowLength)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.ContactShadowDistanceScaleFactor)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.ContactShadowMaxDistance)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.ContactShadowMinDistance)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.ContactShadowFadeDistance)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.ContactShadowFadeInDistance)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.ContactShadowRayBias)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.ContactShadowThicknessScale)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.ContactShadowDenoiser)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.ContactShadowSampleCount)));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.ContactShadowFilterSize)));
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
            CoreEditorUtils.DrawSplitter();
        }

        private void DrawSssSkinSettings()
        {
            SerializedProperty enableSssSkin = serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.EnableSssSkin));

            s_SssSkinExpanded = CoreEditorUtils.DrawHeaderToggleFoldout(
                EditorGUIUtility.TrTextContent("SSS Skin"),
                s_SssSkinExpanded,
                enableSssSkin,
                null,
                null,
                null,
                null);

            if (s_SssSkinExpanded)
            {
                EditorGUI.indentLevel++;
                using (new EditorGUI.DisabledScope(!enableSssSkin.boolValue))
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.SssSkinLayerMask)));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.SssSkinQuality)));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.SssSkinScatteringRadius)));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.SssSkinScatteringIterations)));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.SssSkinShaderIterations)));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.SssSkinDepthTest)));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.SssSkinNormalTest)));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.SssSkinMaxDistance)));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.SssSkinColor)));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.SssSkinRandomizedRotation)));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.SssSkinDitherScale)));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.SssSkinDitherIntensity)));
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.SssSkinNoiseTexture)));
                }
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
            CoreEditorUtils.DrawSplitter();
        }
        private void DrawPassList(TsukuyomiPipelineProfile profile)
        {
            s_PassListExpanded = CoreEditorUtils.DrawHeaderFoldout("Pass List", s_PassListExpanded);

            if (!s_PassListExpanded)
            {
                return;
            }

            EditorGUI.indentLevel++;
            if (profile.Passes != null && profile.Passes.Count > 0)
            {
                for (int i = 0; i < profile.Passes.Count; i++)
                {
                    var pass = profile.Passes[i];
                    if (pass == null) continue;

                    CoreEditorUtils.DrawSplitter();
                    EditorGUILayout.BeginHorizontal();
                    bool newEnabled = EditorGUILayout.ToggleLeft($"{pass.Name} ({pass.InjectionPoint})", pass.Enabled);
                    if (newEnabled != pass.Enabled)
                    {
                        Undo.RecordObject(profile, "Toggle Render Pass");
                        pass.Enabled = newEnabled;
                        EditorUtility.SetDirty(profile);
                    }
                    EditorGUILayout.EndHorizontal();
                }
                CoreEditorUtils.DrawSplitter();
            }
            else
            {
                EditorGUILayout.HelpBox("No configurable render passes. Use the Graph Editor to add passes.", MessageType.Info);
            }
            EditorGUI.indentLevel--;
        }
    }
}

