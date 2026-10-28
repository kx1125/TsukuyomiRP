using UnityEditor;
using UnityEngine;
namespace Tsukuyomi.Rendering.Editor
{
    [CustomEditor(typeof(TsukuyomiPipelineProfile))]
    public sealed class TsukuyomiPipelineProfileEditor : UnityEditor.Editor
    {
        private GUIStyle _nodeBox;
        public override void OnInspectorGUI()
        {
            // IMGUI foldout arrows extend left of their label rect. Reserve space inside the border,
            // including for nested parameter foldouts, without indenting the Enabled toggle.
            _nodeBox ??= new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(18, 6, 4, 6) };
            var profile = (TsukuyomiPipelineProfile)target;
            if (GUILayout.Button("Open Graph Editor", GUILayout.Height(32))) TsukuyomiGraphWindow.Open(profile);
            if (!PassProfileSchema.IsSupported(profile))
            {
                EditorGUILayout.HelpBox("Unsupported Profile schema. Restore a migrated Profile before editing; the serialized asset is retained.", MessageType.Error);
                return;
            }
            serializedObject.Update();
            if (SerializationUtility.HasManagedReferencesWithMissingTypes(profile))
            {
                EditorGUILayout.HelpBox("A pass type is missing. Restore its assembly before saving. Serialized data is retained.", MessageType.Error);
                return;
            }
            var passes = serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.Passes));
            if (passes.arraySize == 0) EditorGUILayout.HelpBox("No effects added. Open Graph Editor and add an effect in a rendering stage.", MessageType.Info);
            for (int i = 0; i < passes.arraySize; i++)
            {
                var item = passes.GetArrayElementAtIndex(i);
                var definition = profile.Passes[i];
                if (definition == null) { EditorGUILayout.HelpBox($"Node {i}: missing pass.", MessageType.Error); continue; }
                EditorGUILayout.BeginVertical(_nodeBox);
                EditorGUILayout.BeginHorizontal();
                item.isExpanded = EditorGUILayout.Foldout(item.isExpanded, definition.Name + " · " + definition.InjectionPoint, true);
                EditorGUILayout.PropertyField(item.FindPropertyRelative(nameof(RenderPassBase.Enabled)), GUIContent.none, GUILayout.Width(20));
                EditorGUILayout.EndHorizontal();
                if (item.isExpanded)
                {
                    var iterator = item.Copy(); var end = iterator.GetEndProperty(); bool children = true;
                    while (iterator.NextVisible(children) && !SerializedProperty.EqualContents(iterator, end))
                    {
                        children = false;
                        if (iterator.name != nameof(RenderPassBase.Enabled)) EditorGUILayout.PropertyField(iterator, true);
                    }
                }
                EditorGUILayout.EndVertical();
            }
            // Foldout expansion changes GUI state but not the Profile's serialized configuration.
            if (serializedObject.ApplyModifiedProperties())
            {
                profile.NotifyChanged(); EditorUtility.SetDirty(profile);
            }
        }
    }
}
