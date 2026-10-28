using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tsukuyomi.Rendering.Editor
{
    public sealed class TsukuyomiGraphWindow : EditorWindow
    {
        [Serializable] public sealed class NodeLayout { public string NodeId; public Vector2 Position; }
        [Serializable] public sealed class StageLayout { public InjectionPoint Point; public Vector2 Position; }
        [Serializable] public sealed class GraphLayout
        { public List<NodeLayout> Nodes = new(); public List<StageLayout> Stages = new(); }
        [SerializeField] private TsukuyomiPipelineProfile _profile;
        [SerializeField] private TsukuyomiPipelineProfile _draft;
        private SerializedObject _serializedDraft;
        private SerializedObject _serializedProfile;
        private TsukuyomiGraphView _view;
        private VisualElement _inspector;
        private Label _status;
        private InjectionPoint? _point;
        [SerializeField] private string _selectedId;
        [SerializeField] private int _loadedRevision;
        [SerializeField] private string _loadedProfileState;
        [SerializeField] private string _savedDraftState;
        [SerializeField] private string _observedDraftState;
        private bool _profileChanged;
        private bool _rebuilding;

        public static void Open(TsukuyomiPipelineProfile profile)
        {
            var window = GetWindow<TsukuyomiGraphWindow>(); window.titleContent = new GUIContent("Tsukuyomi Graph");
            window.SetProfile(profile); window.Show();
        }
        public void SetProfile(TsukuyomiPipelineProfile profile)
        {
            if (_profile == profile && _draft != null) return;
            if (!ResolveUnsavedChanges()) return;
            _profile = profile; LoadDraft();
        }
        public void CreateGUI()
        {
            Undo.undoRedoPerformed -= OnUndoRedo; Undo.undoRedoPerformed += OnUndoRedo;
            rootVisualElement.Clear();
            var toolbar = new Toolbar();
            var picker = new ObjectField { objectType = typeof(TsukuyomiPipelineProfile), value = _profile };
            picker.style.width = 280;
            picker.RegisterValueChangedCallback(e =>
            {
                SetProfile(e.newValue as TsukuyomiPipelineProfile);
                picker.SetValueWithoutNotify(_profile);
            });
            toolbar.Add(picker);
            toolbar.Add(new ToolbarButton(BakeGraph) { text = "Save & Apply" });
            toolbar.Add(new ToolbarButton(() => { if (ResolveUnsavedChanges()) LoadDraft(); }) { text = "Reload" });
            toolbar.Add(new ToolbarButton(() => { CommitPendingEdits(); SynchronizeVisiblePasses(); CaptureLayout(); _point = null; Rebuild(); }) { text = "All Stages" });
            toolbar.Add(new ToolbarButton(() => _view?.FrameAllNodes()) { text = "Frame All" });
            rootVisualElement.Add(toolbar);
            _status = new Label(); _status.style.paddingLeft = 8; rootVisualElement.Add(_status);
            var body = new VisualElement(); body.style.flexDirection = FlexDirection.Row; body.style.flexGrow = 1;
            _view = new TsukuyomiGraphView(); _view.style.flexGrow = 1;
            _view.OnNodeDoubleClicked += point => { CommitPendingEdits(); CaptureLayout(); _point = point; Rebuild(); };
            _view.OnBeforeGraphChanged += BeforeGraphChange;
            _view.OnGraphChanged += OnGraphChange;
            _view.OnPassAdded += pass => { if (!_rebuilding && _draft) { pass.EnsureNodeId(); _draft.Passes.Add(pass); } };
            _view.OnSelectionChanged += SelectionChanged; _view.CanAddType = CanAdd;
            _view.ResolveDefinition = id => _draft?.Passes.Find(p => p?.NodeId == id);
            body.Add(_view);
            var scroll = new ScrollView(); scroll.style.width = 350; scroll.style.paddingLeft = 10; scroll.style.paddingRight = 10;
            _inspector = scroll.contentContainer; body.Add(scroll); rootVisualElement.Add(body);
            if (!_draft && _profile) LoadDraft();
            else
            {
                // Existing windows may survive an assembly reload.
                if (_draft)
                {
                    bool wasReadOnly = (_draft.hideFlags & HideFlags.NotEditable) != 0;
                    _draft.hideFlags &= ~HideFlags.NotEditable;
                    if (wasReadOnly || _observedDraftState == null) _observedDraftState = EditorJsonUtility.ToJson(_draft);
                    if (!hasUnsavedChanges && (wasReadOnly || _savedDraftState == null)) _savedDraftState = _observedDraftState;
                }
                if (_profile && _loadedProfileState == null) _loadedProfileState = EditorJsonUtility.ToJson(_profile);
                Rebuild();
            }
        }
        private bool CanAdd(Type type)
        {
            var prototype = (RenderPassBase)Activator.CreateInstance(type);
            return prototype.AllowMultiple || !_draft.Passes.Any(p => p?.GetType() == type);
        }
        private void LoadDraft()
        {
            ReleaseDraftBinding();
            _serializedProfile?.Dispose(); _serializedProfile = null;
            if (_draft) DestroyImmediate(_draft);
            _draft = null; hasUnsavedChanges = false; _profileChanged = false;
            _loadedProfileState = _savedDraftState = _observedDraftState = null;
            if (_profile && PassProfileSchema.IsSupported(_profile))
            {
                _draft = Instantiate(_profile);
                // HideAndDontSave includes NotEditable, which disables bound parameter controls.
                _draft.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
                _serializedDraft = new SerializedObject(_draft); _loadedRevision = _profile.Revision;
                _loadedProfileState = EditorJsonUtility.ToJson(_profile);
                _savedDraftState = _observedDraftState = EditorJsonUtility.ToJson(_draft);
                _serializedProfile = new SerializedObject(_profile);
            }
            saveChangesMessage = "The graph has unapplied changes."; Rebuild();
        }
        private GraphLayout ReadLayout()
        {
            try { return string.IsNullOrEmpty(_draft.GraphLayoutData) ? new GraphLayout() : JsonUtility.FromJson<GraphLayout>(_draft.GraphLayoutData) ?? new GraphLayout(); }
            catch { return new GraphLayout(); }
        }
        private void Rebuild()
        {
            if (_view == null) return;
            _rebuilding = true; _view.SuppressChanges = true;
            try
            {
                _view.ClearGraph(); _inspector.Unbind(); _inspector.Clear(); _view.CurrentDetailPoint = _point;
                if (!_draft)
                {
                    _status.text = _profile ? "Unsupported Profile schema. Restore a migrated Profile before editing." : "Select a Profile.";
                    return;
                }
                if (SerializationUtility.HasManagedReferencesWithMissingTypes(_draft))
                { _status.text = "Missing pass type. Restore its assembly; saving is disabled."; return; }
                var layout = ReadLayout();
                if (_point.HasValue)
                {
                    int i = 0;
                    foreach (var pass in _draft.Passes)
                    {
                        if (pass == null || pass.InjectionPoint != _point.Value) continue;
                        var position = layout.Nodes.FirstOrDefault(n => n.NodeId == pass.NodeId)?.Position ?? new Vector2(20, i * 180);
                        _view.CreatePassNode(pass.GetType(), position, false, pass); i++;
                    }
                }
                else
                {
                    int i = 0;
                    foreach (InjectionPoint point in Enum.GetValues(typeof(InjectionPoint)))
                    {
                        var position = layout.Stages.FirstOrDefault(n => n.Point == point)?.Position ?? new Vector2(i * 310, 20);
                        var node = _view.CreateInjectionPointNode(point, position);
                        node.extensionContainer.Add(new Label($"{_draft.Passes.Count(p => p != null && p.InjectionPoint == point)} effects"));
                        node.RefreshExpandedState(); i++;
                    }
                    _view.ConnectInjectionPoints();
                }
                if (_selectedId != null) ShowInspector(_selectedId);
                UpdateStatus();
            }
            finally { _rebuilding = false; _view.SuppressChanges = false; }
        }
        private void BeforeGraphChange()
        {
            if (_rebuilding || !_draft) return;
            CommitPendingEdits();
            Undo.RegisterCompleteObjectUndo(_draft, "Edit Tsukuyomi graph");
        }
        private void OnGraphChange()
        {
            if (_rebuilding || !_draft) return;
            CommitPendingEdits();
            SynchronizeVisiblePasses();
            CaptureLayout(); TrackDraftChanges();
            // Serialized property paths use array indices; rebind the selected stable ID after sorting/deletion.
            if (_selectedId != null) ShowInspector(_selectedId);
            UpdateStatus();
        }
        private void SynchronizeVisiblePasses()
        {
            if (_rebuilding || !_draft || _view == null) return;
            if (_point.HasValue)
            {
                // The draft is authoritative even when Unity replaces a managed reference during serialization.
                if (_draft.Passes.Where(p => p != null).GroupBy(p => p.NodeId).Any(g => g.Count() > 1)) return;
                var nodes = _view.nodes.OfType<TsukuyomiPassNode>().OrderBy(n => TsukuyomiGraphView.AuthoredPosition(n).y).ToList();
                var ids = nodes.Select(n => n.PassInstance.NodeId).ToHashSet();
                var visible = nodes.Select(n => _draft.Passes.Find(p => p?.NodeId == n.PassInstance.NodeId))
                    .Where(p => p != null && p.InjectionPoint == _point).ToList();
                _draft.Passes.RemoveAll(p => p != null && p.InjectionPoint == _point && !ids.Contains(p.NodeId));
                int index = 0;
                for (int i = 0; i < _draft.Passes.Count; i++)
                    if (_draft.Passes[i]?.InjectionPoint == _point) _draft.Passes[i] = visible[index++];
                foreach (var node in nodes)
                {
                    var definition = _draft.Passes.Find(p => p?.NodeId == node.PassInstance.NodeId);
                    if (definition != null) node.BindDefinition(definition);
                }
            }
        }
        private void CaptureLayout()
        {
            if (_rebuilding || !_draft || _view == null) return;
            var layout = ReadLayout();
            foreach (var node in _view.nodes.OfType<TsukuyomiPassNode>())
            {
                var entry = layout.Nodes.FirstOrDefault(n => n.NodeId == node.PassInstance.NodeId);
                if (entry == null) { entry = new NodeLayout { NodeId = node.PassInstance.NodeId }; layout.Nodes.Add(entry); }
                entry.Position = TsukuyomiGraphView.AuthoredPosition(node);
            }
            foreach (var node in _view.nodes.OfType<TsukuyomiInjectionPointNode>())
            {
                var entry = layout.Stages.FirstOrDefault(n => n.Point == node.Point);
                if (entry == null) { entry = new StageLayout { Point = node.Point }; layout.Stages.Add(entry); }
                entry.Position = TsukuyomiGraphView.AuthoredPosition(node);
            }
            layout.Nodes.RemoveAll(n => !_draft.Passes.Any(p => p?.NodeId == n.NodeId));
            _draft.GraphLayoutData = JsonUtility.ToJson(layout);
        }
        private void SelectionChanged(IEnumerable<ISelectable> selection)
        {
            if (_rebuilding) return;
            var node = selection.OfType<TsukuyomiPassNode>().LastOrDefault();
            if (node != null) { _selectedId = node.PassInstance.NodeId; ShowInspector(_selectedId); }
        }
        private void ShowInspector(string nodeId)
        {
            CommitPendingEdits();
            _inspector.Unbind(); _inspector.Clear();
            int index = _draft.Passes.FindIndex(p => p?.NodeId == nodeId);
            if (index < 0) return;
            var definition = _draft.Passes[index];
            _inspector.Add(new Label(definition.Name));
            _serializedDraft ??= new SerializedObject(_draft); _serializedDraft.Update();
            var element = _serializedDraft.FindProperty(nameof(TsukuyomiPipelineProfile.Passes)).GetArrayElementAtIndex(index);
            element.isExpanded = true;
            var field = new PropertyField(element, "Parameters"); field.Bind(_serializedDraft);
            field.TrackSerializedObjectValue(_serializedDraft, _ =>
            {
                if (_rebuilding) return;
                // Observe applied object changes, including nested controls. Ignore initial binding notifications.
                if (EditorJsonUtility.ToJson(_draft) == _observedDraftState) return;
                _draft.Passes.Find(p => p?.NodeId == nodeId)?.ValidateSettings();
                TrackDraftChanges(); UpdateStatus();
            });
            _inspector.Add(field);
            var stages = definition.GetType().GetCustomAttributes<InjectionPointAttribute>(true).Select(a => a.Point).Distinct().ToList();
            if (stages.Count > 1)
            {
                var stage = new PopupField<string>("Stage", stages.Select(s => s.ToString()).ToList(), Mathf.Max(0, stages.IndexOf(definition.InjectionPoint)));
                stage.RegisterValueChangedCallback(e =>
                {
                    CommitPendingEdits();
                    Undo.RegisterCompleteObjectUndo(_draft, "Move effect stage");
                    _draft.Passes.Find(p => p?.NodeId == nodeId).InjectionPoint = (InjectionPoint)Enum.Parse(typeof(InjectionPoint), e.newValue);
                    TrackDraftChanges(); Rebuild();
                });
                _inspector.Add(stage);
            }
            var details = new Foldout { text = "Compiled passes and dependencies", value = false };
            using (var plan = new PassSession(_draft, initialize: false))
            {
                foreach (var error in plan.Errors) details.Add(new HelpBox(error, HelpBoxMessageType.Error));
                foreach (var pass in plan.Passes)
                {
                    if (!pass.ContributesTo(nodeId)) continue;
                    var label = new Label($"{(int)pass.Event}: {pass.Name}"); label.style.whiteSpace = WhiteSpace.Normal;
                    details.Add(label);
                    foreach (var dependency in pass.DependencyDescriptions)
                        details.Add(new Label("    ← " + dependency));
                }
            }
            _inspector.Add(details);
        }
        public bool TryApply(out string error)
        {
            error = null;
            if (!_profile || !_draft) { error = "Select a migrated Profile."; return false; }
            CommitPendingEdits();
            if (ProfileChanged(force: true)) { error = "Profile changed outside this graph. Reload before applying; the asset has not been overwritten."; return false; }
            if (SerializationUtility.HasManagedReferencesWithMissingTypes(_draft)) { error = "Restore missing pass types before saving."; return false; }
            SynchronizeVisiblePasses();
            CaptureLayout();
            foreach (var pass in _draft.Passes) { pass?.EnsureNodeId(); pass?.ValidateSettings(); }
            using (var plan = new PassSession(_draft, initialize: false))
                if (!plan.Valid) { error = string.Join("\n", plan.Errors); return false; }
            Undo.RegisterCompleteObjectUndo(_profile, "Apply Tsukuyomi graph");
            string assetName = _profile.name;
            EditorUtility.CopySerialized(_draft, _profile);
            _profile.name = assetName;
            _profile.hideFlags = HideFlags.None; _profile.NotifyChanged();
            EditorUtility.SetDirty(_profile); AssetDatabase.SaveAssetIfDirty(_profile);
            _loadedRevision = _profile.Revision; _loadedProfileState = EditorJsonUtility.ToJson(_profile);
            _savedDraftState = _observedDraftState = EditorJsonUtility.ToJson(_draft);
            _profileChanged = false; hasUnsavedChanges = false; UpdateStatus(); return true;
        }
        private void BakeGraph()
        {
            if (!TryApply(out var error)) { if (_status != null) _status.text = error; Debug.LogWarning(error, _profile); }
        }
        private bool ResolveUnsavedChanges()
        {
            CommitPendingEdits();
            if (!hasUnsavedChanges) return true;
            int action = EditorUtility.DisplayDialogComplex("Unapplied graph changes", saveChangesMessage, "Save & Apply", "Cancel", "Discard");
            if (action == 1) return false;
            if (action == 0) { BakeGraph(); return !hasUnsavedChanges; }
            hasUnsavedChanges = false; return true;
        }
        public override void SaveChanges() { BakeGraph(); if (!hasUnsavedChanges) base.SaveChanges(); }
        public override void DiscardChanges() { LoadDraft(); base.DiscardChanges(); }
        private void OnUndoRedo()
        {
            if (!_draft) return;
            // Undo in another Inspector must not make this graph dirty or replace its controls.
            if (EditorJsonUtility.ToJson(_draft) == _observedDraftState) { UpdateStatus(); return; }
            ReleaseDraftBinding();
            _serializedDraft = new SerializedObject(_draft); TrackDraftChanges(); Rebuild();
        }
        private void CommitPendingEdits()
        {
            rootVisualElement.focusController?.focusedElement?.Blur();
            if (_serializedDraft != null && _serializedDraft.ApplyModifiedProperties()) TrackDraftChanges();
        }
        private void TrackDraftChanges()
        {
            if (!_draft) return;
            _observedDraftState = EditorJsonUtility.ToJson(_draft);
            hasUnsavedChanges = _observedDraftState != _savedDraftState;
        }
        private bool ProfileChanged(bool force = false)
        {
            if (!_profile) return false;
            _serializedProfile ??= new SerializedObject(_profile);
            // Serialize only on actual source changes (or the final apply check), not every editor tick.
            if (_serializedProfile.UpdateIfRequiredOrScript() || force)
                _profileChanged = _profile.Revision != _loadedRevision || EditorJsonUtility.ToJson(_profile) != _loadedProfileState;
            return _profileChanged;
        }
        private void ReleaseDraftBinding()
        {
            _inspector?.Unbind();
            _serializedDraft?.Dispose(); _serializedDraft = null;
        }
        private void UpdateStatus()
        {
            if (_status == null || !_profile) return;
            _status.text = ProfileChanged() ? "Profile changed externally — reload before applying."
                : $"{_profile.name} · {(_point.HasValue ? _point.Value.ToString() : "All Stages")} · {(hasUnsavedChanges ? "Unapplied changes" : "Saved")}";
        }
        private void OnInspectorUpdate() => UpdateStatus();
        private void OnDestroy()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            ReleaseDraftBinding();
            _serializedProfile?.Dispose(); _serializedProfile = null;
            if (_draft) DestroyImmediate(_draft);
        }
    }
}
