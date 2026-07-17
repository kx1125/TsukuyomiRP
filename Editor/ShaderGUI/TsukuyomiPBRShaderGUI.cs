using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering.Editor.ShaderGUI
{
    public sealed class TsukuyomiPBRShaderGUI : UnityEditor.ShaderGUI
    {
        private const string AlphaTestKeyword = "_ALPHATEST_ON";
        private const string NormalMapKeyword = "_NORMALMAP";
        private const string EmissionKeyword = "_EMISSION";
        private const string SurfaceTransparentKeyword = "_SURFACE_TYPE_TRANSPARENT";
        private const string AlphaPremultiplyKeyword = "_ALPHAPREMULTIPLY_ON";
        private const string AlphaModulateKeyword = "_ALPHAMODULATE_ON";
        private const string SpecularHighlightsOffKeyword = "_SPECULARHIGHLIGHTS_OFF";
        private const string EnvironmentReflectionsOffKeyword = "_ENVIRONMENTREFLECTIONS_OFF";
        private const string ReceiveShadowsOffKeyword = "_RECEIVE_SHADOWS_OFF";

        private static bool surfaceOptionsFoldout = true;
        private static bool surfaceInputsFoldout = true;
        private static bool lightingFoldout = true;
        private static bool advancedFoldout;

        private MaterialEditor materialEditor;
        private MaterialProperty[] properties;

        public override void OnGUI(MaterialEditor editor, MaterialProperty[] materialProperties)
        {
            materialEditor = editor;
            properties = materialProperties;

            EditorGUI.BeginChangeCheck();
            DrawSurfaceOptions();
            DrawSurfaceInputs();
            DrawTsukuyomiLighting();
            DrawAdvancedOptions();

            if (EditorGUI.EndChangeCheck())
            {
                foreach (Object target in materialEditor.targets)
                {
                    if (target is Material material)
                    {
                        ValidateMaterial(material);
                    }
                }
            }
        }

        private void DrawSurfaceOptions()
        {
            surfaceOptionsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(surfaceOptionsFoldout, "Surface Options");
            if (surfaceOptionsFoldout)
            {
                DrawPopup("Surface Type", "_Surface", new[] { "Opaque", "Transparent" });

                MaterialProperty surface = Find("_Surface");
                if (surface != null && surface.floatValue > 0.5f)
                {
                    DrawPopup("Blending Mode", "_Blend", new[] { "Alpha", "Premultiply", "Additive", "Multiply" });
                }

                DrawPopup("Render Face", "_Cull", new[] { "Both", "Front", "Back" });
                DrawProperty("_AlphaClip", "Alpha Clipping");

                MaterialProperty alphaClip = Find("_AlphaClip");
                if (alphaClip != null && alphaClip.floatValue > 0.5f)
                {
                    EditorGUI.indentLevel++;
                    DrawProperty("_Cutoff", "Threshold");
                    EditorGUI.indentLevel--;
                }

                DrawProperty("_ReceiveShadows", "Receive Shadows");
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawSurfaceInputs()
        {
            surfaceInputsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(surfaceInputsFoldout, "Surface Inputs");
            if (surfaceInputsFoldout)
            {
                MaterialProperty baseMap = Find("_BaseMap");
                MaterialProperty baseColor = Find("_BaseColor");
                if (baseMap != null)
                {
                    materialEditor.TexturePropertySingleLine(new GUIContent("Base Map"), baseMap, baseColor);
                    materialEditor.TextureScaleOffsetProperty(baseMap);
                }

                MaterialProperty rmoe = Find("_RMOE");
                if (rmoe != null)
                {
                    materialEditor.TexturePropertySingleLine(
                        new GUIContent("RMOE Map", "R: Roughness, G: Metallic, B: Ambient Occlusion, A: Emission Mask"),
                        rmoe);
                }

                DrawProperty("_Roughness", "Roughness");
                DrawProperty("_Metallic", "Metallic");
                DrawProperty("_OcclusionStrength", "Occlusion Strength");

                MaterialProperty normalMap = Find("_BumpMap");
                MaterialProperty normalScale = Find("_BumpScale");
                if (normalMap != null)
                {
                    materialEditor.TexturePropertySingleLine(new GUIContent("Normal Map"), normalMap,
                        normalMap.textureValue != null ? normalScale : null);
                }

                MaterialProperty emissionColor = Find("_EmissionColor");
                if (emissionColor != null)
                {
                    materialEditor.ShaderProperty(emissionColor,
                        new GUIContent("Emission", "Emission Color multiplied by the alpha channel of the RMOE map."));
                    materialEditor.LightmapEmissionProperty();
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawTsukuyomiLighting()
        {
            lightingFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(lightingFoldout, "Tsukuyomi Lighting");
            if (lightingFoldout)
            {
                DrawProperty("_MicroShadowOpacity", "Micro Shadow Opacity");
                DrawProperty("_RoughDiffuseStrength", "Rough Diffuse Strength");
                DrawProperty("_IndirectDiffuseIntensity", "Indirect Diffuse Intensity");
                DrawProperty("_IndirectSpecularIntensity", "Indirect Specular Intensity");
                DrawProperty("_IndirectSpecularFGDStrength", "Indirect Specular FGD Strength");
                DrawProperty("_HorizonOcclusionPower", "Horizon Occlusion Power");
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawAdvancedOptions()
        {
            advancedFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(advancedFoldout, "Advanced Options");
            if (advancedFoldout)
            {
                DrawProperty("_SpecularHighlights", "Specular Highlights");
                DrawProperty("_EnvironmentReflections", "Environment Reflections");
                DrawProperty("_QueueOffset", "Sorting Priority");
                materialEditor.EnableInstancingField();
                materialEditor.DoubleSidedGIField();
                materialEditor.RenderQueueField();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawPopup(string label, string propertyName, string[] options)
        {
            MaterialProperty property = Find(propertyName);
            if (property == null)
            {
                return;
            }

            EditorGUI.showMixedValue = property.hasMixedValue;
            EditorGUI.BeginChangeCheck();
            int value = EditorGUILayout.Popup(label, Mathf.RoundToInt(property.floatValue), options);
            if (EditorGUI.EndChangeCheck())
            {
                materialEditor.RegisterPropertyChangeUndo(label);
                property.floatValue = value;
            }
            EditorGUI.showMixedValue = false;
        }

        private void DrawProperty(string propertyName, string label)
        {
            MaterialProperty property = Find(propertyName);
            if (property != null)
            {
                materialEditor.ShaderProperty(property, label);
            }
        }

        private MaterialProperty Find(string name)
        {
            foreach (MaterialProperty property in properties)
            {
                if (property.name == name)
                {
                    return property;
                }
            }
            return null;
        }

        public override void ValidateMaterial(Material material)
        {
            MaterialEditor.FixupEmissiveFlag(material);
            SetupKeywords(material);
            SetupSurface(material);
        }

        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        {
            base.AssignNewShaderToMaterial(material, oldShader, newShader);

            if (material.HasProperty("_EmissionColor") && material.GetColor("_EmissionColor").maxColorComponent > 0.0f)
            {
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            }

            ValidateMaterial(material);
        }

        private static void SetupKeywords(Material material)
        {
            SetKeyword(material, AlphaTestKeyword, GetFloat(material, "_AlphaClip") > 0.5f);
            SetKeyword(material, NormalMapKeyword, material.HasProperty("_BumpMap") && material.GetTexture("_BumpMap") != null);
            SetKeyword(material, EmissionKeyword,
                (material.globalIlluminationFlags & MaterialGlobalIlluminationFlags.AnyEmissive) != 0);
            SetKeyword(material, SpecularHighlightsOffKeyword, material.HasProperty("_SpecularHighlights") && material.GetFloat("_SpecularHighlights") == 0.0f);
            SetKeyword(material, EnvironmentReflectionsOffKeyword, material.HasProperty("_EnvironmentReflections") && material.GetFloat("_EnvironmentReflections") == 0.0f);
            SetKeyword(material, ReceiveShadowsOffKeyword, material.HasProperty("_ReceiveShadows") && material.GetFloat("_ReceiveShadows") == 0.0f);

            if (material.HasProperty("_MainTex") && material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_MainTex", material.GetTexture("_BaseMap"));
                material.SetTextureScale("_MainTex", material.GetTextureScale("_BaseMap"));
                material.SetTextureOffset("_MainTex", material.GetTextureOffset("_BaseMap"));
            }

            if (material.HasProperty("_Color") && material.HasProperty("_BaseColor"))
            {
                material.SetColor("_Color", material.GetColor("_BaseColor"));
            }
        }

        private static void SetupSurface(Material material)
        {
            bool alphaClip = GetFloat(material, "_AlphaClip") > 0.5f;
            bool transparent = GetFloat(material, "_Surface") > 0.5f;
            int queueOffset = Mathf.Clamp((int)GetFloat(material, "_QueueOffset"), -50, 50);

            SetKeyword(material, SurfaceTransparentKeyword, transparent);

            if (transparent)
            {
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = (int)RenderQueue.Transparent + queueOffset;
                SetFloat(material, "_ZWrite", 0.0f);
                SetupTransparentBlend(material);
            }
            else
            {
                material.SetOverrideTag("RenderType", alphaClip ? "TransparentCutout" : "Opaque");
                material.renderQueue = (alphaClip ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry) + queueOffset;
                SetFloat(material, "_SrcBlend", (float)BlendMode.One);
                SetFloat(material, "_DstBlend", (float)BlendMode.Zero);
                SetFloat(material, "_SrcBlendAlpha", (float)BlendMode.One);
                SetFloat(material, "_DstBlendAlpha", (float)BlendMode.Zero);
                SetFloat(material, "_ZWrite", 1.0f);
                SetKeyword(material, AlphaPremultiplyKeyword, false);
                SetKeyword(material, AlphaModulateKeyword, false);
            }
        }

        private static void SetupTransparentBlend(Material material)
        {
            int blendMode = Mathf.RoundToInt(GetFloat(material, "_Blend"));
            SetKeyword(material, AlphaPremultiplyKeyword, blendMode == 1);
            SetKeyword(material, AlphaModulateKeyword, blendMode == 3);

            switch (blendMode)
            {
                case 1:
                    SetBlend(material, BlendMode.One, BlendMode.OneMinusSrcAlpha, BlendMode.One, BlendMode.OneMinusSrcAlpha);
                    break;
                case 2:
                    SetBlend(material, BlendMode.SrcAlpha, BlendMode.One, BlendMode.One, BlendMode.One);
                    break;
                case 3:
                    SetBlend(material, BlendMode.DstColor, BlendMode.Zero, BlendMode.DstAlpha, BlendMode.Zero);
                    break;
                default:
                    SetBlend(material, BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha, BlendMode.One, BlendMode.OneMinusSrcAlpha);
                    break;
            }
        }

        private static void SetBlend(Material material, BlendMode src, BlendMode dst, BlendMode srcAlpha, BlendMode dstAlpha)
        {
            SetFloat(material, "_SrcBlend", (float)src);
            SetFloat(material, "_DstBlend", (float)dst);
            SetFloat(material, "_SrcBlendAlpha", (float)srcAlpha);
            SetFloat(material, "_DstBlendAlpha", (float)dstAlpha);
        }

        private static float GetFloat(Material material, string name)
        {
            return material.HasProperty(name) ? material.GetFloat(name) : 0.0f;
        }

        private static void SetFloat(Material material, string name, float value)
        {
            if (material.HasProperty(name))
            {
                material.SetFloat(name, value);
            }
        }

        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled)
            {
                material.EnableKeyword(keyword);
            }
            else
            {
                material.DisableKeyword(keyword);
            }
        }
    }
}
