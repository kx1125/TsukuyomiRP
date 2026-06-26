using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using Tsukuyomi.Rendering;

namespace Tsukuyomi.Rendering.Editor
{
    public class TsukuyomiGraphWindow : EditorWindow
    {
        private const float SidebarWidth = 280f;

        [System.Serializable]
        public class PassNodeData
        {
            public string TypeName;
            public Vector2 Position;
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

        private GraphData _graphData = new GraphData();
        private InjectionPoint? _currentDetailPoint;

        public void SetProfile(TsukuyomiPipelineProfile profile)
        {
            _profile = profile;
            if (_profileField != null)
                _profileField.SetValueWithoutNotify(profile);

            LoadGraph();
            UpdateChrome();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.flexGrow = 1;

            BuildToolbar();
            BuildBody();
            BuildStatusBar();

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

            _saveButton = new ToolbarButton(BakeGraph) { text = "Save & Bake" };
            toolbar.Add(_saveButton);

            rootVisualElement.Add(toolbar);
        }

        private void BuildBody()
        {
            var splitView = new TwoPaneSplitView(0, SidebarWidth, TwoPaneSplitViewOrientation.Horizontal);
            splitView.style.flexGrow = 1;

            splitView.Add(BuildSidebar());
            splitView.Add(BuildGraphPanel());
            rootVisualElement.Add(splitView);
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
                            Position = passNode.GetPosition().position
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

            _currentDetailPoint = null;
            _graphView.CurrentDetailPoint = null;
            _graphView.SetBackgroundStyle(false);
            _graphView.ClearGraph();

            foreach (var pointData in _graphData.InjectionPoints)
            {
                _graphView.CreateInjectionPointNode(pointData.Point, pointData.Position);
            }

            _graphView.ConnectInjectionPoints();
            _graphView.FrameAllNodes();
            UpdateChrome();
        }

        private void SwitchToDetailView(InjectionPoint point)
        {
            SaveCurrentViewToData();

            _currentDetailPoint = point;
            _graphView.CurrentDetailPoint = point;
            _graphView.SetBackgroundStyle(true);
            _graphView.ClearGraph();

            var pointData = _graphData.InjectionPoints.FirstOrDefault(p => p.Point == point);
            if (pointData != null)
            {
                foreach (var passData in pointData.ChildPasses)
                {
                    var type = System.Type.GetType(passData.TypeName);
                    if (type != null)
                    {
                        _graphView.CreatePassNode(type, passData.Position, false);
                    }
                }
            }

            _graphView.FrameAllNodes();
            UpdateChrome();
        }

        private void LoadGraph()
        {
            if (_profile == null || _graphView == null)
            {
                UpdateChrome();
                return;
            }

            EnsureGraphData();
            SwitchToMainView();
        }

        private void UpdateSelection(IEnumerable<ISelectable> selection)
        {
            var selected = selection?.FirstOrDefault();
            _selectionLabel.text = selected switch
            {
                TsukuyomiInjectionPointNode pointNode => $"Injection Point\n{pointNode.Point}",
                TsukuyomiPassNode passNode => $"Pass\n{passNode.PassType.Name}",
                null => "Nothing selected",
                _ => selected.GetType().Name
            };
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

            if (_breadcrumbLabel != null)
                _breadcrumbLabel.text = isDetail ? $"Pipeline / {_currentDetailPoint.Value}" : "Pipeline / Injection Points";

            if (_selectionLabel != null)
                _selectionLabel.text = "Nothing selected";

            if (_statsLabel != null)
            {
                int pointCount = _graphData?.InjectionPoints?.Count ?? 0;
                int passCount = _graphData?.InjectionPoints?.Sum(p => p.ChildPasses.Count) ?? 0;
                _statsLabel.text = hasProfile
                    ? $"Profile: {_profile.name}\nInjection Points: {pointCount}\nPasses: {passCount}"
                    : "No profile assigned";
            }

            if (_statusLabel != null)
            {
                _statusLabel.text = hasProfile
                    ? (isDetail ? $"Editing {_currentDetailPoint.Value}" : "Editing injection point overview")
                    : "Assign a Tsukuyomi Pipeline Profile to edit";
            }
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
            var reusablePasses = BuildReusablePassMap(_profile.Passes);
            var sortedPoints = _graphData.InjectionPoints.OrderBy(p => (int)p.Point).ToList();

            foreach (var ipData in sortedPoints)
            {
                ipData.ChildPasses.Sort((a, b) => a.Position.y.CompareTo(b.Position.y));

                foreach (var passData in ipData.ChildPasses)
                {
                    var type = System.Type.GetType(passData.TypeName);
                    if (type == null)
                        continue;

                    var passInstance = TakeReusablePass(reusablePasses, ipData.Point, type);
                    passInstance ??= (RenderPassBase)System.Activator.CreateInstance(type);
                    passInstance.InjectionPoint = ipData.Point;
                    bakedPasses.Add(passInstance);
                }
            }

            Undo.RecordObject(_profile, "Bake Tsukuyomi Graph");
            _profile.Passes = bakedPasses;
            _profile.GraphLayoutData = JsonUtility.ToJson(_graphData);

            EditorUtility.SetDirty(_profile);
            AssetDatabase.SaveAssets();
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
