using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Tsukuyomi.Rendering.Editor
{
    [CustomEditor(typeof(TsukuyomiPipelineProfile))]
    public sealed class TsukuyomiPipelineProfileEditor : UnityEditor.Editor
    {
        private sealed class StageGroup
        {
            internal readonly List<int> Slots = new();
            internal readonly ReorderableList List;

            internal StageGroup(TsukuyomiPipelineProfileEditor editor)
            {
                List = new ReorderableList(Slots, typeof(int), true, false, false, false)
                {
                    headerHeight = 0,
                    footerHeight = 0,
                    elementHeightCallback = row => editor.PassRow(default, Slots, row, false),
                    drawElementCallback = (rect, row, active, focused) => editor.PassRow(rect, Slots, row, true),
                    onReorderCallback = _ => editor.ApplyStageOrder(Slots)
                };
            }
        }

        private readonly SortedDictionary<InjectionPoint, StageGroup> _groups = new();
        private readonly HashSet<InjectionPoint> _expandedStages = new();
        private readonly HashSet<long> _expandedPasses = new();
        private SerializedProperty _passes;
        private int _drawnRevision;

        private void OnEnable() => _passes = serializedObject.FindProperty(nameof(TsukuyomiPipelineProfile.Passes));

        public override void OnInspectorGUI()
        {
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
            if (_passes.arraySize == 0) EditorGUILayout.HelpBox("No effects added. Open Graph Editor and add an effect in a rendering stage.", MessageType.Info);
            // An external edit (including Graph Apply or Undo) may replace rows and their cached heights.
            if (profile.Revision != _drawnRevision) _groups.Clear();
            foreach (var group in _groups.Values) group.Slots.Clear();
            for (int i = 0; i < _passes.arraySize; i++)
            {
                var definition = profile.Passes[i];
                if (definition == null) { EditorGUILayout.HelpBox($"Node {i}: missing pass.", MessageType.Error); continue; }
                if (!_groups.TryGetValue(definition.InjectionPoint, out var group))
                    _groups.Add(definition.InjectionPoint, group = new StageGroup(this));
                group.Slots.Add(i);
            }

            foreach (var entry in _groups)
            {
                var group = entry.Value;
                if (group.Slots.Count == 0) continue;
                var label = new GUIContent($"{entry.Key} ({group.Slots.Count})",
                    "Drag passes within this stage to set their order. Render events and dependencies take precedence.");
                bool expanded = EditorGUILayout.BeginFoldoutHeaderGroup(_expandedStages.Contains(entry.Key), label);
                EditorGUILayout.EndFoldoutHeaderGroup();
                if (expanded)
                {
                    _expandedStages.Add(entry.Key);
                    group.List.DoLayoutList();
                }
                else _expandedStages.Remove(entry.Key);
            }

            if (serializedObject.ApplyModifiedProperties())
            {
                profile.NotifyChanged(); EditorUtility.SetDirty(profile);
            }
            _drawnRevision = profile.Revision;
        }

        private float PassRow(Rect rect, List<int> slots, int row, bool draw)
        {
            int slot = slots[row];
            var item = _passes.GetArrayElementAtIndex(slot);
            long id = item.managedReferenceId;
            bool expanded = _expandedPasses.Contains(id);
            float line = EditorGUIUtility.singleLineHeight;
            float gap = EditorGUIUtility.standardVerticalSpacing;
            // Leave room for the arrows of both the row and nested parameter foldouts.
            rect.xMin += 14f;
            rect.y += gap;
            rect.height = line;
            if (draw)
            {
                var header = rect;
                header.xMax -= 20f;
                var definition = ((TsukuyomiPipelineProfile)target).Passes[slot];
                expanded = EditorGUI.Foldout(header, expanded, $"{row + 1}. {definition.Name}", true);
                if (expanded) _expandedPasses.Add(id); else _expandedPasses.Remove(id);
                var toggle = new Rect(header.xMax + 2f, rect.y, 18f, line);
                EditorGUI.PropertyField(toggle, item.FindPropertyRelative(nameof(RenderPassBase.Enabled)), GUIContent.none);
            }

            float height = line + 2f * gap;
            if (!expanded) return height;
            var end = item.GetEndProperty();
            bool enterChildren = true;
            while (item.NextVisible(enterChildren) && !SerializedProperty.EqualContents(item, end))
            {
                enterChildren = false;
                if (item.name == nameof(RenderPassBase.Enabled)) continue;
                rect.y += rect.height + gap;
                rect.height = EditorGUI.GetPropertyHeight(item, true);
                if (draw) EditorGUI.PropertyField(rect, item, true);
                height += rect.height + gap;
            }
            return height;
        }

        private void ApplyStageOrder(List<int> slots)
        {
            // The list just reordered the projected slot indices. Write the same references back
            // into this stage's original slots so interleaved stages keep their exact positions.
            var orderedIds = slots.ConvertAll(slot => _passes.GetArrayElementAtIndex(slot).managedReferenceId);
            slots.Sort();
            for (int i = 0; i < slots.Count; i++)
                _passes.GetArrayElementAtIndex(slots[i]).managedReferenceId = orderedIds[i];
            Undo.SetCurrentGroupName("Reorder Tsukuyomi passes");
        }
    }
}
