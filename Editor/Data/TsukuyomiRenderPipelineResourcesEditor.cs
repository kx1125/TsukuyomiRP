using UnityEditor;
using UnityEngine;

namespace Tsukuyomi.Rendering.Editor
{
    [CustomEditor(typeof(TsukuyomiRenderPipelineResources))]
    internal sealed class TsukuyomiRenderPipelineResourcesEditor : UnityEditor.Editor
    {
        private static bool s_ShowPcssShadow = true;
        private static bool s_ShowContactShadow = true;
        private static bool s_ShowDepthPyramid = true;
        private static bool s_ShowGtao = true;
        private static bool s_ShowVolumeLight = true;
        private static bool s_ShowFsr3Resources = true;
        private static bool s_ShowDefaultTextures = true;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawFoldout(
                ref s_ShowPcssShadow,
                "PCSS Shadow",
                "screenSpacePcssShadowsShader",
                "screenSpacePcssShadowsMaterial",
                "screenSpaceShadowsShader",
                "screenSpaceShadowsMaterial");

            DrawFoldout(
                ref s_ShowContactShadow,
                "Contact Shadow",
                "contactShadowsComputeShader",
                "contactShadowDenoiserComputeShader");

            DrawFoldout(
                ref s_ShowDepthPyramid,
                "Depth Pyramid",
                "depthPyramidComputeShader");

            DrawFoldout(
                ref s_ShowGtao,
                "Ground Truth Ambient Occlusion",
                "gtaoTraceComputeShader",
                "gtaoSpatialDenoiseComputeShader",
                "gtaoBlurAndUpsampleComputeShader");

            DrawFoldout(
                ref s_ShowVolumeLight,
                "Volume Light",
                "volumetricFogShader",
                "volumetricFogMaterial",
                "downsampleDepthShader",
                "downsampleDepthMaterial",
                "volumetricFogRaymarchComputeShader",
                "volumetricFogBlurComputeShader",
                "volumetricFogUpsampleComputeShader");

            DrawChildFoldout(ref s_ShowFsr3Resources, "FSR3 Resources", "fsr3Shaders");

            DrawFoldout(
                ref s_ShowDefaultTextures,
                "Default Textures",
                "defaultWhiteTexture",
                "defaultBlackTexture",
                "defaultNormalTexture");

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawFoldout(ref bool expanded, string label, params string[] propertyNames)
        {
            expanded = EditorGUILayout.BeginFoldoutHeaderGroup(expanded, label);
            if (expanded)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < propertyNames.Length; i++)
                {
                    SerializedProperty property = serializedObject.FindProperty(propertyNames[i]);
                    if (property != null)
                        DrawObjectReference(property);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawChildFoldout(ref bool expanded, string label, string parentPropertyName)
        {
            expanded = EditorGUILayout.BeginFoldoutHeaderGroup(expanded, label);
            if (expanded)
            {
                SerializedProperty parentProperty = serializedObject.FindProperty(parentPropertyName);
                if (parentProperty != null)
                {
                    EditorGUI.indentLevel++;
                    SerializedProperty property = parentProperty.Copy();
                    SerializedProperty endProperty = property.GetEndProperty();
                    bool enterChildren = true;
                    while (property.NextVisible(enterChildren) && !SerializedProperty.EqualContents(property, endProperty))
                    {
                        EditorGUILayout.PropertyField(property, true);
                        enterChildren = false;
                    }

                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawObjectReference(SerializedProperty property)
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference)
            {
                EditorGUILayout.PropertyField(property, true);
                return;
            }

            EditorGUI.BeginChangeCheck();
            Object value = EditorGUILayout.ObjectField(
                property.displayName,
                property.objectReferenceValue,
                GetObjectReferenceType(property),
                false);

            if (EditorGUI.EndChangeCheck())
                property.objectReferenceValue = value;
        }

        private static System.Type GetObjectReferenceType(SerializedProperty property)
        {
            string typeName = property.type;
            if (typeName.StartsWith("PPtr<$"))
                typeName = typeName.Substring(6, typeName.Length - 7);

            return typeName switch
            {
                "Shader" => typeof(Shader),
                "Material" => typeof(Material),
                "ComputeShader" => typeof(ComputeShader),
                "Texture2D" => typeof(Texture2D),
                _ => typeof(Object)
            };
        }
    }
}
