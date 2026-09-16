using UnityEditor;
using UnityEngine;

namespace Tsukuyomi.Rendering.Editor.ShaderGUI
{
    public sealed class TsukuyomiSpecularPBRShaderGUI : UnityEditor.ShaderGUI
    {
        public override void OnGUI(MaterialEditor editor, MaterialProperty[] properties)
        {
            MaterialProperty Find(string name) => FindProperty(name, properties);
            void Property(string name, string label = null) => editor.ShaderProperty(Find(name), label ?? Find(name).displayName);
            void Texture(string name, string label, string extra = null) => editor.TexturePropertySingleLine(
                new GUIContent(label), Find(name), extra == null ? null : Find(extra));

            EditorGUI.BeginChangeCheck();
            var mode = Find("_MaterialType");
            EditorGUI.showMixedValue = mode.hasMixedValue;
            EditorGUI.BeginChangeCheck();
            int selected = EditorGUILayout.Popup("Material Type", Mathf.RoundToInt(mode.floatValue), new[] { "Common", "Skin" });
            if (EditorGUI.EndChangeCheck())
            {
                editor.RegisterPropertyChangeUndo("Material Type");
                mode.floatValue = selected;
            }
            EditorGUI.showMixedValue = false;
            Property("_Cull"); Property("_AlphaClip");
            if (Find("_AlphaClip").floatValue > 0.5f || Find("_AlphaClip").hasMixedValue) Property("_Cutoff");
            EditorGUILayout.Space();
            Texture("_BaseMap", "Base Map", "_BaseColor");
            editor.TextureScaleOffsetProperty(Find("_BaseMap"));
            Texture("_PBRMask", "PBR Mask (R Specular / G Roughness / B Metallic / A Skin)");
            Property("_Roughness"); Property("_Metallic");
            Texture("_OcclusionMap", "Occlusion (R)");
            Texture("_BumpMap", "Normal Map", "_BumpScale"); Property("_BumpTile");
            Property("_DetailNormal");
            if (Find("_DetailNormal").floatValue > 0.5f || Find("_DetailNormal").hasMixedValue)
            {
                Texture("_DetailNormalMap", "Detail Normal", "_DetailNormalMapScale");
                Property("_DetailNormalMapTile");
            }
            EditorGUILayout.Space();
            Texture("_EmissionMap", "Emission Map", "_EmissionColor");
            editor.LightmapEmissionProperty();
            if (mode.floatValue > 0.5f || mode.hasMixedValue)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Skin", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Requires SSS Skin enabled in the Tsukuyomi profile and an object in its SSS Layer Mask. PBR Mask alpha controls skin coverage.", MessageType.Info);
                Property("_OcclusionColor"); Property("_Transmission");
                if (Find("_Transmission").floatValue > 0.5f || Find("_Transmission").hasMixedValue)
                {
                    Texture("_TransmissionMap", "Transmission Map", "_TransmissionColor");
                    Property("TransmissionOcc"); Property("TransmissionShadows");
                    Property("TransmissionRange"); Property("DynamicPassTransmission");
                }
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Lighting", EditorStyles.boldLabel);
            foreach (string name in new[] { "_MicroShadowOpacity", "_IndirectDiffuseIntensity", "_IndirectSpecularIntensity",
                "_HorizonOcclusionPower", "_SpecularHighlights", "_EnvironmentReflections", "_ReceiveShadows", "_QueueOffset" }) Property(name);
            editor.EnableInstancingField(); editor.DoubleSidedGIField();
            EditorGUI.EndChangeCheck();
            // Includes Undo/Redo and newly created materials whose default pass state is enabled.
            foreach (Object target in editor.targets) ValidateMaterial((Material)target);
        }

        public override void ValidateMaterial(Material material) => TsukuyomiSpecularPBRMaterial.Validate(material);

        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        {
            bool skin = oldShader != null && oldShader.name == "SSSSkin/Standard SSS";
            base.AssignNewShaderToMaterial(material, oldShader, newShader);
            TsukuyomiSpecularPBRMaterial.SetMaterialType(material,
                skin ? TsukuyomiSpecularPBRMaterialType.Skin : TsukuyomiSpecularPBRMaterialType.Common);
        }
    }
}
