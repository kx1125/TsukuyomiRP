using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering.Editor
{
    public sealed class TsukuyomiStudioHairShaderGUI : UnityEditor.ShaderGUI
    {
        private static readonly string[] Models = { "OpenPBR", "Studio 旧版" };
        private readonly TsukuyomiOpenPBRShaderGUI openPbrGUI = new TsukuyomiOpenPBRShaderGUI();

        public override void OnGUI(MaterialEditor editor, MaterialProperty[] properties)
        {
            var model = FindProperty("_HairLightingModel", properties);
            bool previousMixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = model.hasMixedValue;
            EditorGUI.BeginChangeCheck();
            int selection = EditorGUILayout.Popup("光照模型", Mathf.Clamp(Mathf.RoundToInt(model.floatValue), 0, 1), Models);
            if (EditorGUI.EndChangeCheck())
            {
                editor.RegisterPropertyChangeUndo("Hair Lighting Model");
                model.floatValue = selection;
                foreach (Material material in editor.targets) ValidateMaterial(material);
            }
            EditorGUI.showMixedValue = previousMixed;

            bool mixed = model.hasMixedValue;
            bool legacy = model.floatValue > 0.5f;
            if (legacy || mixed)
                EditorGUILayout.HelpBox("旧版输入控制发色与高光；透明裁剪和阴影仍使用通用 BaseMap。", MessageType.Info);
            var visible = properties.Where(p => p.name != "_HairLightingModel"
                && (mixed || (legacy ? !IsOpenPbrOnly(p.name) : !p.name.StartsWith("_Hair")))).ToArray();
            EditorGUI.BeginChangeCheck();
            editor.PropertiesDefaultGUI(visible);
            if (EditorGUI.EndChangeCheck())
                foreach (Material material in editor.targets) ValidateMaterial(material);
        }

        private static bool IsOpenPbrOnly(string name)
        {
            return name.StartsWith("_OpenPBR") || name == "_RMOE" || name == "_Roughness"
                || name == "_Metallic" || name == "_OcclusionStrength" || name == "_MicroShadowOpacity"
                || name == "_HorizonOcclusionPower";
        }

        public override void ValidateMaterial(Material material)
        {
            openPbrGUI.ValidateMaterial(material);
            CoreUtils.SetKeyword(material, "_STUDIO_HAIR_LIGHTING", material.GetFloat("_HairLightingModel") > 0.5f);
        }
    }
}
