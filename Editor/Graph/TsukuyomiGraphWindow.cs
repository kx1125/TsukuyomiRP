using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections.Generic;
using System.Linq;
using Tsukuyomi.Rendering;

namespace Tsukuyomi.Rendering.Editor
{
    public class TsukuyomiGraphWindow : EditorWindow
    {
        private const float SidebarWidth = 280f;
        private const float InspectorWidth = 340f;

        [System.Serializable]
        public class PassNodeData
        {
            public string TypeName;
            public Vector2 Position;

            [NonSerialized]
            public RenderPassBase PassInstance;
        }

        private sealed class PassInspectorHost : ScriptableObject
        {
            [SerializeReference]
            public RenderPassBase Pass;
        }

        [System.Serializable]
        public class InjectionPointNodeData
        {
            public InjectionPoint Point;
            public Vector2 Position;
            public List<PassNodeData> ChildPasses = new();
        }

        [System.Serializable]
        public class GraphData
        {
            public List<InjectionPointNodeData> InjectionPoints = new();
        }

        public static void Open(TsukuyomiPipelineProfile profile)
        {
            var window = GetWindow<TsukuyomiGraphWindow>();
            window.titleContent = new GUIContent("Tsukuyomi Graph");
            window.minSize = new Vector2(900, 520);
            window.SetProfile(profile);
        }

        private TsukuyomiGraphView _graphView;
        private TsukuyomiPipelineProfile _profile;

        private ObjectField _profileField;
        private Label _breadcrumbLabel;
        private Label _selectionLabel;
        private Label _statsLabel;
        private Label _statusLabel;
        private ToolbarButton _backButton;
        private ToolbarButton _saveButton;
        private ToolbarButton _inspectorButton;
        private TwoPaneSplitView _graphInspectorSplitView;
        private VisualElement _inspectorPanel;
        private ScrollView _inspectorContent;
        private PassInspectorHost _inspectorHost;
        private SerializedObject _inspectorObject;
        private TsukuyomiPassNode _selectedPassNode;

        private GraphData _graphData = new GraphData();
        private InjectionPoint? _currentDetailPoint;
        private bool _suppressGraphChanges;
        private bool _inspectorVisible = true;

        public void SetProfile(TsukuyomiPipelineProfile profile)
        {
            if (_profile == profile)
                return;

            if (!ResolveUnsavedChanges())
            {
                _profileField?.SetValueWithoutNotify(_profile);
                return;
            }

            _profile = profile;
            if (_profileField != null)
                _profileField.SetValueWithoutNotify(profile);

            LoadGraph();
            UpdateChrome();
        }

        public void CreateGUI()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;

            if (_inspectorHost == null)
            {
                _inspectorHost = CreateInstance<PassInspectorHost>();
                _inspectorHost.hideFlags = HideFlags.DontSave;
                _inspectorObject = new SerializedObject(_inspectorHost);
            }

            saveChangesMessage = "The Tsukuyomi graph contains changes that have not been baked to the selected profile.";
            rootVisualElement.Clear();
            rootVisualElement.style.flexGrow = 1;

            BuildToolbar();
            BuildBody();
            BuildStatusBar();

            rootVisualElement.RegisterCallback<KeyDownEvent>(evt =>
            {
                if ((evt.ctrlKey || evt.commandKey) && evt.keyCode == KeyCode.S)
                {
                    BakeGraph();
                    evt.StopPropagation();
                }
            });

            LoadGraph();
            UpdateChrome();

            rootVisualElement.schedule.Execute(() => _graphView?.FrameAllNodes()).ExecuteLater(50);
        }

        private void BuildToolbar()
        {
            var toolbar = new Toolbar();
            toolbar.style.height = 22;

            _profileField = new ObjectField
            {
                objectType = typeof(TsukuyomiPipelineProfile),
                allowSceneObjects = false,
                label = "Profile"
            };
            _profileField.style.width = 320;
            _profileField.RegisterValueChangedCallback(evt => SetProfile(evt.newValue as TsukuyomiPipelineProfile));
            toolbar.Add(_profileField);

            _backButton = new ToolbarButton(SwitchToMainView) { text = "Back" };
            toolbar.Add(_backButton);

            toolbar.Add(new ToolbarSpacer());

            var frameButton = new ToolbarButton(() => _graphView?.FrameAllNodes()) { text = "Frame All" };
            toolbar.Add(frameButton);

            _inspectorButton = new ToolbarButton(ToggleInspector) { text = "Graph Inspector" };
            toolbar.Add(_inspectorButton);

            _saveButton = new ToolbarButton(BakeGraph) { text = "Save & Bake" };
            toolbar.Add(_saveButton);

            rootVisualElement.Add(toolbar);
        }

        private void BuildBody()
        {
            var splitView = new TwoPaneSplitView(0, SidebarWidth, TwoPaneSplitViewOrientation.Horizontal);
            splitView.style.flexGrow = 1;

            splitView.Add(BuildSidebar());

            _graphInspectorSplitView = new TwoPaneSplitView(1, InspectorWidth, TwoPaneSplitViewOrientation.Horizontal);
            _graphInspectorSplitView.style.flexGrow = 1;
            _graphInspectorSplitView.Add(BuildGraphPanel());
            _graphInspectorSplitView.Add(BuildGraphInspector());
            splitView.Add(_graphInspectorSplitView);
            rootVisualElement.Add(splitView);
        }

        private VisualElement BuildGraphInspector()
        {
            _inspectorPanel = new VisualElement { name = "TsukuyomiGraphInspector" };
            _inspectorPanel.style.flexGrow = 1;
            _inspectorPanel.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);
            _inspectorPanel.style.borderLeftWidth = 1;
            _inspectorPanel.style.borderLeftColor = new Color(0.08f, 0.08f, 0.08f);

            var title = MakeSectionTitle("Graph Inspector");
            title.style.marginLeft = 10;
            title.style.marginTop = 10;
            _inspectorPanel.Add(title);

            _inspectorContent = new ScrollView(ScrollViewMode.Vertical);
            _inspectorContent.style.flexGrow = 1;
            _inspectorContent.style.paddingLeft = 10;
            _inspectorContent.style.paddingRight = 10;
            _inspectorContent.style.paddingBottom = 10;
            _inspectorContent.RegisterCallback<SerializedPropertyChangeEvent>(OnInspectorPropertyChanged);
            _inspectorPanel.Add(_inspectorContent);

            ShowInspectorMessage("Select a pass node to edit its settings.");
            return _inspectorPanel;
        }

        private VisualElement BuildSidebar()
        {
            var sidebar = new VisualElement { name = "TsukuyomiGraphSidebar" };
            sidebar.style.flexGrow = 1;
            sidebar.style.paddingLeft = 8;
            sidebar.style.paddingRight = 8;
            sidebar.style.paddingTop = 8;
            sidebar.style.paddingBottom = 8;
            sidebar.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);
            sidebar.style.borderRightWidth = 1;
            sidebar.style.borderRightColor = new Color(0.08f, 0.08f, 0.08f);

            sidebar.Add(MakeSectionTitle("Navigation"));
            _breadcrumbLabel = MakeInfoLabel();
            sidebar.Add(_breadcrumbLabel);

            sidebar.Add(MakeSectionTitle("Selection"));
            _selectionLabel = MakeInfoLabel();
            sidebar.Add(_selectionLabel);

            sidebar.Add(MakeSectionTitle("Graph"));
            _statsLabel = MakeInfoLabel();
            sidebar.Add(_statsLabel);

            var help = MakeInfoLabel();
            help.text = "Double-click an injection point to edit its passes. Right-click in a detail view to add a pass.";
            help.style.whiteSpace = WhiteSpace.Normal;
            sidebar.Add(MakeSectionTitle("Hints"));
            sidebar.Add(help);

            return sidebar;
        }

        private VisualElement BuildGraphPanel()
        {
            var graphPanel = new VisualElement { name = "TsukuyomiGraphPanel" };
            graphPanel.style.flexGrow = 1;
            graphPanel.style.backgroundColor = new Color(0.13f, 0.13f, 0.13f);

            _graphView = new TsukuyomiGraphView
            {
                name = "Tsukuyomi Graph"
            };
            _graphView.StretchToParentSize();
            _graphView.OnNodeDoubleClicked += SwitchToDetailView;
            _graphView.OnSelectionChanged += UpdateSelection;
            _graphView.OnGraphChanged += MarkGraphDirty;
            graphPanel.Add(_graphView);

            return graphPanel;
        }

        private void BuildStatusBar()
        {
            var statusBar = new VisualElement();
            statusBar.style.height = 20;
            statusBar.style.flexDirection = FlexDirection.Row;
            statusBar.style.alignItems = Align.Center;
            statusBar.style.paddingLeft = 8;
            statusBar.style.paddingRight = 8;
            statusBar.style.backgroundColor = new Color(0.16f, 0.16f, 0.16f);
            statusBar.style.borderTopWidth = 1;
            statusBar.style.borderTopColor = new Color(0.08f, 0.08f, 0.08f);

            _statusLabel = new Label();
            _statusLabel.style.unityFontStyleAndWeight = FontStyle.Normal;
            _statusLabel.style.fontSize = 11;
            _statusLabel.style.color = new Color(0.72f, 0.72f, 0.72f);
            statusBar.Add(_statusLabel);

            rootVisualElement.Add(statusBar);
        }

        private static Label MakeSectionTitle(string text)
        {
            var label = new Label(text);
            label.style.marginTop = 8;
            label.style.marginBottom = 4;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize = 11;
            label.style.color = new Color(0.82f, 0.82f, 0.82f);
            return label;
        }

        private static Label MakeInfoLabel()
        {
            var label = new Label();
            label.style.fontSize = 11;
            label.style.color = new Color(0.68f, 0.68f, 0.68f);
            label.style.marginBottom = 4;
            return label;
        }

        private void ToggleInspector()
        {
            _inspectorVisible = !_inspectorVisible;
            if (_graphInspectorSplitView != null)
            {
                if (_inspectorVisible)
                    _graphInspectorSplitView.UnCollapse();
                else
                    _graphInspectorSplitView.CollapseChild(1);
            }
            if (_inspectorButton != null)
                _inspectorButton.text = _inspectorVisible ? "Hide Inspector" : "Graph Inspector";
        }

        private void ShowInspectorMessage(string message)
        {
            if (_inspectorContent == null)
                return;

            _selectedPassNode = null;
            _inspectorContent.Unbind();
            _inspectorContent.Clear();
            var label = MakeInfoLabel();
            label.text = message;
            label.style.whiteSpace = WhiteSpace.Normal;
            _inspectorContent.Add(label);
        }

        private void ShowPassInspector(TsukuyomiPassNode node)
        {
            if (_inspectorContent == null || _inspectorHost == null || node?.PassInstance == null)
                return;

            _selectedPassNode = node;
            _inspectorContent.Unbind();
            _inspectorContent.Clear();

            var passName = new Label(node.PassInstance.Name);
            passName.style.unityFontStyleAndWeight = FontStyle.Bold;
            passName.style.fontSize = 13;
            passName.style.marginBottom = 3;
            _inspectorContent.Add(passName);

            var typeName = MakeInfoLabel();
            typeName.text = node.PassType.FullName;
            typeName.style.whiteSpace = WhiteSpace.Normal;
            _inspectorContent.Add(typeName);

            var injectionPoint = new TextField("Injection Point")
            {
                value = _currentDetailPoint?.ToString() ?? node.PassInstance.InjectionPoint.ToString(),
                isReadOnly = true
            };
            injectionPoint.SetEnabled(false);
            injectionPoint.style.marginBottom = 8;
            _inspectorContent.Add(injectionPoint);

            _inspectorHost.Pass = node.PassInstance;
            _inspectorObject.Update();
            SerializedProperty passProperty = _inspectorObject.FindProperty(nameof(PassInspectorHost.Pass));
            SerializedProperty iterator = passProperty.Copy();
            SerializedProperty end = iterator.GetEndProperty();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;
                _inspectorContent.Add(new PropertyField(iterator.Copy()));
            }

            _inspectorContent.Bind(_inspectorObject);
        }

        private void OnInspectorPropertyChanged(SerializedPropertyChangeEvent evt)
        {
            if (_selectedPassNode?.PassInstance == null || _inspectorObject == null)
                return;

            _inspectorObject.ApplyModifiedProperties();
            _selectedPassNode.PassInstance.ValidateSettings();
            _inspectorObject.Update();
            MarkGraphDirty();
        }

        private void OnUndoRedo()
        {
            if (_selectedPassNode?.PassInstance == null || _inspectorObject == null)
                return;

            _inspectorObject.Update();
            _selectedPassNode.PassInstance.ValidateSettings();
            MarkGraphDirty();
            Repaint();
        }

        private void MarkGraphDirty()
        {
            if (_suppressGraphChanges || _profile == null)
                return;

            hasUnsavedChanges = true;
            UpdateStatusLabel();
        }

        private bool ResolveUnsavedChanges()
        {
            if (!hasUnsavedChanges)
                return true;

            int result = EditorUtility.DisplayDialogComplex(
                "Unsaved Tsukuyomi Graph",
                saveChangesMessage,
                "Save & Bake",
                "Cancel",
                "Discard");

            if (result == 1)
                return false;
            if (result == 0)
                BakeGraph();
            else
                hasUnsavedChanges = false;

            return true;
        }

        public override void SaveChanges()
        {
            BakeGraph();
            base.SaveChanges();
        }

        public override void DiscardChanges()
        {
            hasUnsavedChanges = false;
            LoadGraph();
            base.DiscardChanges();
        }

        private void OnDestroy()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            if (_inspectorHost != null)
                DestroyImmediate(_inspectorHost);
        }

        private void EnsureGraphData()
        {
            if (_profile == null) return;

            _graphData = new GraphData();
            if (!string.IsNullOrEmpty(_profile.GraphLayoutData))
            {
                try
                {
                    _graphData = JsonUtility.FromJson<GraphData>(_profile.GraphLayoutData) ?? new GraphData();
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to load graph layout: {e.Message}");
                }
            }

            var points = (InjectionPoint[])System.Enum.GetValues(typeof(InjectionPoint));
            float currentX = 0;
            foreach (var point in points)
            {
                if (!_graphData.InjectionPoints.Any(p => p.Point == point))
                {
                    _graphData.InjectionPoints.Add(new InjectionPointNodeData
                    {
                        Point = point,
                        Position = new Vector2(currentX, 0)
                    });
                    currentX += 300;
                }
            }

            BindPassInstances();
        }

        private void BindPassInstances()
        {
            var reusablePasses = BuildReusablePassMap(_profile.Passes);
            var assignedPasses = new HashSet<RenderPassBase>();

            foreach (var pointData in _graphData.InjectionPoints)
            {
                foreach (var passData in pointData.ChildPasses)
                {
                    Type type = Type.GetType(passData.TypeName);
                    if (type == null)
                        continue;

                    RenderPassBase sourcePass = TakeReusablePass(reusablePasses, pointData.Point, type);
                    passData.PassInstance = sourcePass != null
                        ? ClonePass(sourcePass)
                        : (RenderPassBase)Activator.CreateInstance(type);
                    passData.PassInstance.InjectionPoint = pointData.Point;
                    if (sourcePass != null)
                        assignedPasses.Add(sourcePass);
                }
            }

            if (_profile.Passes == null)
                return;

            foreach (var pass in _profile.Passes)
            {
                if (pass == null || assignedPasses.Contains(pass))
                    continue;

                var pointData = _graphData.InjectionPoints.First(p => p.Point == pass.InjectionPoint);
                pointData.ChildPasses.Add(new PassNodeData
                {
                    TypeName = pass.GetType().AssemblyQualifiedName,
                    Position = new Vector2(0.0f, pointData.ChildPasses.Count * 180.0f),
                    PassInstance = ClonePass(pass)
                });
            }
        }

        private static RenderPassBase ClonePass(RenderPassBase source)
        {
            var clone = (RenderPassBase)Activator.CreateInstance(source.GetType());
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), clone);
            return clone;
        }

        private void SaveCurrentViewToData()
        {
            if (_graphData == null || _graphView == null) return;

            if (_currentDetailPoint.HasValue)
            {
                var pointData = _graphData.InjectionPoints.FirstOrDefault(p => p.Point == _currentDetailPoint.Value);
                if (pointData != null)
                {
                    pointData.ChildPasses.Clear();
                    foreach (var passNode in _graphView.nodes.ToList().OfType<TsukuyomiPassNode>())
                    {
                        pointData.ChildPasses.Add(new PassNodeData
                        {
                            TypeName = passNode.PassType.AssemblyQualifiedName,
                            Position = passNode.GetPosition().position,
                            PassInstance = passNode.PassInstance
                        });
                    }
                }
            }
            else
            {
                foreach (var ipNode in _graphView.nodes.ToList().OfType<TsukuyomiInjectionPointNode>())
                {
                    var pointData = _graphData.InjectionPoints.FirstOrDefault(p => p.Point == ipNode.Point);
                    if (pointData != null)
                    {
                        pointData.Position = ipNode.GetPosition().position;
                    }
                }
            }
        }

        private void SwitchToMainView()
        {
            SaveCurrentViewToData();

            bool previousSuppress = _suppressGraphChanges;
            _suppressGraphChanges = true;
            try
            {
                _currentDetailPoint = null;
                _graphView.CurrentDetailPoint = null;
                _graphView.SetBackgroundStyle(false);
                _graphView.ClearGraph();

                foreach (var pointData in _graphData.InjectionPoints)
                    _graphView.CreateInjectionPointNode(pointData.Point, pointData.Position);

                _graphView.ConnectInjectionPoints();
                _graphView.FrameAllNodes();
                UpdateChrome();
            }
            finally
            {
                _suppressGraphChanges = previousSuppress;
            }
        }

        private void SwitchToDetailView(InjectionPoint point)
        {
            SaveCurrentViewToData();

            bool previousSuppress = _suppressGraphChanges;
            _suppressGraphChanges = true;
            try
            {
                _currentDetailPoint = point;
                _graphView.CurrentDetailPoint = point;
                _graphView.SetBackgroundStyle(true);
                _graphView.ClearGraph();

                var pointData = _graphData.InjectionPoints.FirstOrDefault(p => p.Point == point);
                if (pointData != null)
                {
                    foreach (var passData in pointData.ChildPasses)
                    {
                        var type = Type.GetType(passData.TypeName);
                        if (type != null)
                        {
                            passData.PassInstance ??= (RenderPassBase)Activator.CreateInstance(type);
                            passData.PassInstance.InjectionPoint = point;
                            _graphView.CreatePassNode(type, passData.Position, false, passData.PassInstance);
                        }
                    }
                }

                _graphView.FrameAllNodes();
                UpdateChrome();
            }
            finally
            {
                _suppressGraphChanges = previousSuppress;
            }
        }

        private void LoadGraph()
        {
            if (_profile == null || _graphView == null)
            {
                UpdateChrome();
                return;
            }

            _suppressGraphChanges = true;
            try
            {
                EnsureGraphData();
                SwitchToMainView();
                hasUnsavedChanges = false;
            }
            finally
            {
                _suppressGraphChanges = false;
            }
        }

        private void UpdateSelection(IEnumerable<ISelectable> selection)
        {
            var selectedItems = selection?.ToList() ?? new List<ISelectable>();
            var selected = selectedItems.Count == 1 ? selectedItems[0] : null;
            _selectionLabel.text = selected switch
            {
                TsukuyomiInjectionPointNode pointNode => $"Injection Point\n{pointNode.Point}",
                TsukuyomiPassNode passNode => $"Pass\n{passNode.PassType.Name}",
                null => "Nothing selected",
                _ => selected.GetType().Name
            };

            if (selectedItems.Count > 1)
                ShowInspectorMessage("Select a single pass node to edit its settings.");
            else if (selected is TsukuyomiPassNode passNode)
                ShowPassInspector(passNode);
            else if (selected is TsukuyomiInjectionPointNode pointNode)
                ShowInspectorMessage($"Injection Point: {pointNode.Point}\nDouble-click to edit its passes.");
            else
                ShowInspectorMessage("Select a pass node to edit its settings.");
        }

        private void UpdateChrome()
        {
            if (_profileField != null)
                _profileField.SetValueWithoutNotify(_profile);

            bool hasProfile = _profile != null;
            bool isDetail = _currentDetailPoint.HasValue;

            if (_backButton != null)
                _backButton.SetEnabled(hasProfile && isDetail);
            if (_saveButton != null)
                _saveButton.SetEnabled(hasProfile);
            if (_inspectorButton != null)
                _inspectorButton.text = _inspectorVisible ? "Hide Inspector" : "Graph Inspector";

            if (_breadcrumbLabel != null)
                _breadcrumbLabel.text = isDetail ? $"Pipeline / {_currentDetailPoint.Value}" : "Pipeline / Injection Points";

            if (_selectionLabel != null)
                _selectionLabel.text = "Nothing selected";

            ShowInspectorMessage("Select a pass node to edit its settings.");

            if (_statsLabel != null)
            {
                int pointCount = _graphData?.InjectionPoints?.Count ?? 0;
                int passCount = _graphData?.InjectionPoints?.Sum(p => p.ChildPasses.Count) ?? 0;
                _statsLabel.text = hasProfile
                    ? $"Profile: {_profile.name}\nInjection Points: {pointCount}\nPasses: {passCount}"
                    : "No profile assigned";
            }

            UpdateStatusLabel();
        }

        private void UpdateStatusLabel()
        {
            if (_statusLabel == null)
                return;

            if (_profile == null)
            {
                _statusLabel.text = "Assign a Tsukuyomi Pipeline Profile to edit";
                return;
            }

            string view = _currentDetailPoint.HasValue
                ? $"Editing {_currentDetailPoint.Value}"
                : "Editing injection point overview";
            _statusLabel.text = hasUnsavedChanges ? $"{view}  •  Unsaved changes" : view;
        }

        private void BakeGraph()
        {
            if (_profile == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign a Pipeline Profile asset first.", "OK");
                return;
            }

            SaveCurrentViewToData();

            var bakedPasses = new List<RenderPassBase>();
            var sortedPoints = _graphData.InjectionPoints.OrderBy(p => (int)p.Point).ToList();

            foreach (var ipData in sortedPoints)
            {
                ipData.ChildPasses.Sort((a, b) => a.Position.y.CompareTo(b.Position.y));

                foreach (var passData in ipData.ChildPasses)
                {
                    var type = System.Type.GetType(passData.TypeName);
                    if (type == null)
                        continue;

                    var passInstance = passData.PassInstance ?? (RenderPassBase)Activator.CreateInstance(type);
                    passInstance.InjectionPoint = ipData.Point;
                    passInstance.ValidateSettings();
                    passData.PassInstance = passInstance;
                    bakedPasses.Add(passInstance);
                }
            }

            Undo.RecordObject(_profile, "Bake Tsukuyomi Graph");
            _profile.Passes = bakedPasses;
            _profile.GraphLayoutData = JsonUtility.ToJson(_graphData);

            EditorUtility.SetDirty(_profile);
            AssetDatabase.SaveAssets();
            hasUnsavedChanges = false;
            UpdateChrome();

            Debug.Log($"Baked {bakedPasses.Count} passes to {_profile.name} across {_graphData.InjectionPoints.Count} injection points.");
        }

        private static Dictionary<string, Queue<RenderPassBase>> BuildReusablePassMap(List<RenderPassBase> passes)
        {
            var map = new Dictionary<string, Queue<RenderPassBase>>();
            if (passes == null)
                return map;

            foreach (var pass in passes)
            {
                if (pass == null)
                    continue;

                var key = GetReusablePassKey(pass.InjectionPoint, pass.GetType());
                if (!map.TryGetValue(key, out var queue))
                {
                    queue = new Queue<RenderPassBase>();
                    map.Add(key, queue);
                }

                queue.Enqueue(pass);
            }

            return map;
        }

        private static RenderPassBase TakeReusablePass(
            Dictionary<string, Queue<RenderPassBase>> reusablePasses,
            InjectionPoint injectionPoint,
            System.Type type)
        {
            var key = GetReusablePassKey(injectionPoint, type);
            if (reusablePasses.TryGetValue(key, out var queue) && queue.Count > 0)
                return queue.Dequeue();

            return null;
        }

        private static string GetReusablePassKey(InjectionPoint injectionPoint, System.Type type)
        {
            return $"{(int)injectionPoint}:{type.AssemblyQualifiedName}";
        }
    }
}
