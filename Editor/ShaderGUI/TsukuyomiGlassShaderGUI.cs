using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering.Editor.ShaderGUI
{
    public sealed class TsukuyomiGlassShaderGUI : UnityEditor.ShaderGUI
    {
        private static bool transmissionFoldout = true;
        private static bool refractionFoldout = true;
        private static bool matCapFoldout = true;
        private static bool lightingFoldout = true;
        private static bool advancedFoldout;

        private MaterialEditor materialEditor;
        private MaterialProperty[] properties;

        public override void OnGUI(MaterialEditor editor, MaterialProperty[] materialProperties)
        {
            materialEditor = editor;
            properties = materialProperties;

            EditorGUI.BeginChangeCheck();
            DrawTransmission();
            DrawRefraction();
            DrawMatCap();
            DrawLighting();
            DrawAdvanced();

            if (EditorGUI.EndChangeCheck())
            {
                foreach (Object target in materialEditor.targets)
                {
                    if (target is Material material)
                        ValidateMaterial(material);
                }
            }
        }

        private void DrawTransmission()
        {
            transmissionFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(transmissionFoldout, "Glass Surface");
            if (transmissionFoldout)
            {
                MaterialProperty baseMap = Find("_BaseMap");
                MaterialProperty baseColor = Find("_BaseColor");
                if (baseMap != null)
                {
                    materialEditor.TexturePropertySingleLine(new GUIContent("Transmission"), baseMap, baseColor);
                    materialEditor.TextureScaleOffsetProperty(baseMap);
                }

                DrawProperty("_Roughness", "Roughness");

                MaterialProperty normalMap = Find("_BumpMap");
                MaterialProperty normalScale = Find("_BumpScale");
                if (normalMap != null)
                {
                    materialEditor.TexturePropertySingleLine(
                        new GUIContent("Normal Map"),
                        normalMap,
                        normalMap.textureValue != null ? normalScale : null);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawRefraction()
        {
            refractionFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(refractionFoldout, "Refraction");
            if (refractionFoldout)
            {
                MaterialProperty refraction = Find("_RefractionEnabled");
                if (refraction != null)
                    materialEditor.ShaderProperty(refraction, "Enable Refraction");

                bool disableRefractionSettings = refraction != null
                    && !refraction.hasMixedValue
                    && refraction.floatValue < 0.5f;
                using (new EditorGUI.DisabledScope(disableRefractionSettings))
                {
                    DrawProperty("_IndexOfRefraction", "Index Of Refraction");
                    DrawProperty("_Thickness", "Sphere Thickness (World Units)");
                    DrawProperty("_Absorption", "Absorption");
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawMatCap()
        {
            matCapFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(matCapFoldout, "MatCap");
            if (matCapFoldout)
            {
                MaterialProperty matCap = Find("_MatCapMap");
                MaterialProperty intensity = Find("_MatCapIntensity");
                if (matCap != null)
                    materialEditor.TexturePropertySingleLine(new GUIContent("MatCap"), matCap, intensity);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawLighting()
        {
            lightingFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(lightingFoldout, "Tsukuyomi Lighting");
            if (lightingFoldout)
            {
                DrawProperty("_MicroShadowOpacity", "Micro Shadow Opacity");
                DrawProperty("_IndirectSpecularIntensity", "Environment Reflection Intensity");
                DrawProperty("_EnvironmentReflectionRange", "Environment Reflection Range");
                DrawProperty("_EnvironmentReflectionSharpness", "Environment Reflection Sharpness");
                DrawProperty("_HorizonOcclusionPower", "Horizon Occlusion Power");
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawAdvanced()
        {
            advancedFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(advancedFoldout, "Advanced Options");
            if (advancedFoldout)
            {
                DrawProperty("_SpecularHighlights", "Specular Highlights");
                DrawProperty("_EnvironmentReflections", "Environment Reflections");
                DrawProperty("_ReceiveShadows", "Receive Shadows");
                DrawPopup("Render Face", "_Cull", new[] { "Both", "Front", "Back" });
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
                return;

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
                materialEditor.ShaderProperty(property, label);
        }

        private MaterialProperty Find(string name)
        {
            foreach (MaterialProperty property in properties)
            {
                if (property.name == name)
                    return property;
            }
            return null;
        }

        public override void ValidateMaterial(Material material)
        {
            bool refractionEnabled = material.GetFloat("_RefractionEnabled") > 0.5f;
            SetKeyword(material, "_NORMALMAP", material.GetTexture("_BumpMap") != null);
            SetKeyword(material, "_REFRACTION_OFF", !refractionEnabled);
            SetKeyword(material, "_MATCAP_ON",
                material.GetTexture("_MatCapMap") != null && material.GetFloat("_MatCapIntensity") > 0.0f);
            SetKeyword(material, "_SPECULARHIGHLIGHTS_OFF", material.GetFloat("_SpecularHighlights") == 0.0f);
            SetKeyword(material, "_ENVIRONMENTREFLECTIONS_OFF", material.GetFloat("_EnvironmentReflections") == 0.0f);
            SetKeyword(material, "_RECEIVE_SHADOWS_OFF", material.GetFloat("_ReceiveShadows") == 0.0f);

            material.SetOverrideTag("RenderType", "Transparent");
            material.SetFloat("_SrcBlend", refractionEnabled ? (float)BlendMode.One : (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", refractionEnabled ? (float)BlendMode.Zero : (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0.0f);
            material.renderQueue = (int)RenderQueue.Transparent + Mathf.Clamp((int)material.GetFloat("_QueueOffset"), -50, 50);

            material.SetTexture("_MainTex", material.GetTexture("_BaseMap"));
            material.SetTextureScale("_MainTex", material.GetTextureScale("_BaseMap"));
            material.SetTextureOffset("_MainTex", material.GetTextureOffset("_BaseMap"));
            material.SetColor("_Color", material.GetColor("_BaseColor"));
        }

        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        {
            base.AssignNewShaderToMaterial(material, oldShader, newShader);
            ValidateMaterial(material);
        }

        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled)
                material.EnableKeyword(keyword);
            else
                material.DisableKeyword(keyword);
        }
    }
}
