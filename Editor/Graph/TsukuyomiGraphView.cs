using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using System.Reflection;
using Tsukuyomi.Rendering;

namespace Tsukuyomi.Rendering.Editor
{
    public class TsukuyomiGraphView : GraphView
    {
        [Serializable]
        private sealed class ClipboardPassData
        {
            public string TypeName;
            public string PassJson;
            public Vector2 Position;
        }

        [Serializable]
        private sealed class ClipboardData
        {
            public List<ClipboardPassData> Passes = new();
        }

        public InjectionPoint? CurrentDetailPoint { get; set; }
        public event Action<InjectionPoint> OnNodeDoubleClicked;
        public event Action<IEnumerable<ISelectable>> OnSelectionChanged;
        public event Action OnGraphChanged;
        public event Action OnBeforeGraphChanged;
        public event Action<RenderPassBase> OnPassAdded;
        public Func<Type, bool> CanAddType;
        public Func<string, RenderPassBase> ResolveDefinition;
        public bool SuppressChanges { get; set; }

        private GridBackground _grid;

        internal static Vector2 AuthoredPosition(GraphElement element)
        {
            // Inline positions update during a drag before UI Toolkit's next layout calculation.
            var position = element.GetPosition().position;
            if (element.style.left.keyword == StyleKeyword.Undefined) position.x = element.style.left.value.value;
            if (element.style.top.keyword == StyleKeyword.Undefined) position.y = element.style.top.value.value;
            return position;
        }

        public TsukuyomiGraphView()
        {
            AddToClassList("tsukuyomi-graph-view");
            style.flexGrow = 1;

            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);

            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            _grid = new GridBackground();
            Insert(0, _grid);
            _grid.StretchToParentSize();

            RegisterCallback<ContextualMenuPopulateEvent>(OnContextualMenu);
            RegisterCallback<MouseDownEvent>(OnMouseDown, TrickleDown.TrickleDown);
            RegisterCallback<MouseUpEvent>(_ => OnSelectionChanged?.Invoke(selection));
            RegisterCallback<KeyUpEvent>(_ => OnSelectionChanged?.Invoke(selection));

            graphViewChanged = change =>
            {
                if (SuppressChanges) return change;
                OnBeforeGraphChanged?.Invoke();
                schedule.Execute(() => OnGraphChanged?.Invoke());
                return change;
            };

            serializeGraphElements = SerializePassElements;
            canPasteSerializedData = CanPastePassElements;
            unserializeAndPaste = PastePassElements;
        }

        public void SetBackgroundStyle(bool isDetail)
        {
            _grid.style.unityBackgroundImageTintColor = isDetail
                ? new Color(0.16f, 0.16f, 0.18f, 1.0f)
                : new Color(0.21f, 0.21f, 0.21f, 1.0f);
        }

        private void OnMouseDown(MouseDownEvent evt)
        {
            if (evt.clickCount == 2)
            {
                var target = evt.target as VisualElement;
                TsukuyomiInjectionPointNode clickedNode = null;

                while (target != null && clickedNode == null)
                {
                    if (target is TsukuyomiInjectionPointNode node)
                        clickedNode = node;
                    target = target.parent;
                }

                if (clickedNode != null)
                {
                    OnNodeDoubleClicked?.Invoke(clickedNode.Point);
                    evt.StopImmediatePropagation();
                }
            }
        }

        public void FrameAllNodes()
        {
            FrameAll();
        }

        public void ClearGraph()
        {
            ClearEdges();
            var elementsToRemove = graphElements.ToList();
            foreach (var element in elementsToRemove) RemoveElement(element);
        }

        private void ClearEdges()
        {
            foreach (var edge in edges.ToList())
            {
                edge.output?.Disconnect(edge);
                edge.input?.Disconnect(edge);
                edge.output = null;
                edge.input = null;
                RemoveElement(edge);
            }
        }

        private void OnContextualMenu(ContextualMenuPopulateEvent evt)
        {
            if (CurrentDetailPoint.HasValue)
            {
                var point = CurrentDetailPoint.Value;
                evt.menu.AppendAction($"Add Pass/{point}", (a) => { }, DropdownMenuAction.Status.Disabled);
                evt.menu.AppendSeparator();

                var types = UnityEditor.TypeCache.GetTypesDerivedFrom<RenderPassBase>()
                    .Where(p => p.IsPublic && !p.IsAbstract && !p.ContainsGenericParameters && p.IsSerializable
                        && p.GetConstructor(Type.EmptyTypes) != null && PassEditorTypes.CanRunInPlayer(p))
                    .OrderBy(p => p.Name);

                foreach (var type in types)
                {
                    var attributes = type.GetCustomAttributes<InjectionPointAttribute>(false).ToList();
                    if (attributes.Count == 0)
                        attributes = type.GetCustomAttributes<InjectionPointAttribute>(true).ToList();

                    if (attributes.Any(a => a.Point == point))
                    {
                        var definition = (RenderPassBase)Activator.CreateInstance(type);
                        evt.menu.AppendAction($"Add Pass/{definition.MenuPath}", (action) => {
                            CreatePassNode(type, evt.mousePosition, true);
                        }, _ => CanAddType == null || CanAddType(type) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
                    }
                }
            }
            else
            {
                evt.menu.AppendAction("Info: Double-click an Injection Point to enter and add passes", (a) => { }, DropdownMenuAction.Status.Disabled);
            }
        }

        public TsukuyomiPassNode CreatePassNode(
            Type type,
            Vector2 position,
            bool convertMousePosition,
            RenderPassBase passInstance = null, bool notifyAdded = false)
        {
            bool adding = convertMousePosition || notifyAdded;
            if (adding && CanAddType != null && !CanAddType(type))
                return null;
            if (adding)
                OnBeforeGraphChanged?.Invoke();
            var node = new TsukuyomiPassNode(type, passInstance);
            if (CurrentDetailPoint.HasValue) node.PassInstance.InjectionPoint = CurrentDetailPoint.Value;
            if (convertMousePosition || notifyAdded) OnPassAdded?.Invoke(node.PassInstance);
            var finalPosition = convertMousePosition ? contentViewContainer.WorldToLocal(position) : position;
            node.SetPosition(new Rect(finalPosition, Vector2.zero));
            AddElement(node);
            ClearSelection();
            AddToSelection(node);
            OnSelectionChanged?.Invoke(selection);
            if (convertMousePosition || notifyAdded) OnGraphChanged?.Invoke();
            return node;
        }

        public TsukuyomiInjectionPointNode CreateInjectionPointNode(InjectionPoint point, Vector2 position)
        {
            var node = new TsukuyomiInjectionPointNode(point);
            node.SetPosition(new Rect(position, Vector2.zero)); 
            AddElement(node);
            return node;
        }

        public void ConnectInjectionPoints()
        {
            var ipNodes = graphElements.OfType<TsukuyomiInjectionPointNode>().OrderBy(n => (int)n.Point).ToList();
            for (int i = 0; i < ipNodes.Count - 1; i++)
            {
                var startNode = ipNodes[i];
                var endNode = ipNodes[i+1];
                
                var edge = startNode.OutputPort.ConnectTo(endNode.InputPort);
                AddElement(edge);
            }
        }

        internal void ConnectPassesInOrder(IReadOnlyList<TsukuyomiPassNode> ordered)
        {
            ClearEdges();
            for (int i = 0; i < ordered.Count; i++)
            {
                var node = ordered[i];
                node.title = $"{i + 1}. {node.PassInstance.Name}";
                if (i == 0) continue;
                var edge = ordered[i - 1].OrderOutput.ConnectTo(node.OrderInput);
                edge.capabilities &= ~(Capabilities.Selectable | Capabilities.Deletable);
                edge.pickingMode = edge.edgeControl.pickingMode = PickingMode.Ignore;
                AddElement(edge);
            }
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatiblePorts = new List<Port>();
            if (CurrentDetailPoint.HasValue) return compatiblePorts;
            ports.ForEach((port) =>
            {
                if (startPort != port && startPort.node != port.node && startPort.direction != port.direction)
                    compatiblePorts.Add(port);
            });
            return compatiblePorts;
        }

        private string SerializePassElements(IEnumerable<GraphElement> elements)
        {
            var clipboard = new ClipboardData();
            foreach (TsukuyomiPassNode node in elements.OfType<TsukuyomiPassNode>())
            {
                var definition = ResolveDefinition?.Invoke(node.PassInstance.NodeId) ?? node.PassInstance;
                clipboard.Passes.Add(new ClipboardPassData
                {
                    TypeName = definition.GetType().AssemblyQualifiedName,
                    PassJson = JsonUtility.ToJson(definition),
                    Position = AuthoredPosition(node)
                });
            }

            return clipboard.Passes.Count > 0 ? JsonUtility.ToJson(clipboard) : string.Empty;
        }

        private static ClipboardData ReadClipboard(string data)
        {
            if (string.IsNullOrWhiteSpace(data) || !data.TrimStart().StartsWith("{", StringComparison.Ordinal))
                return null;
            try
            {
                return JsonUtility.FromJson<ClipboardData>(data);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private Type PasteableType(ClipboardPassData entry)
        {
            if (!CurrentDetailPoint.HasValue || string.IsNullOrEmpty(entry?.TypeName))
                return null;

            Type type = Type.GetType(entry.TypeName);
            if (type == null || !typeof(RenderPassBase).IsAssignableFrom(type) || type.IsAbstract
                || type.ContainsGenericParameters || type.GetConstructor(Type.EmptyTypes) == null
                || !PassEditorTypes.CanRunInPlayer(type))
                return null;

            bool supportsPoint = type.GetCustomAttributes<InjectionPointAttribute>(true)
                .Any(attribute => attribute.Point == CurrentDetailPoint.Value);
            return supportsPoint && (CanAddType == null || CanAddType(type)) ? type : null;
        }

        private bool CanPastePassElements(string data)
        {
            return CurrentDetailPoint.HasValue && ReadClipboard(data)?.Passes?.Any(p => PasteableType(p) != null) == true;
        }

        private void PastePassElements(string operationName, string data)
        {
            var clipboard = ReadClipboard(data);
            if (!CurrentDetailPoint.HasValue || clipboard?.Passes == null)
                return;

            foreach (ClipboardPassData passData in clipboard.Passes)
            {
                Type type = PasteableType(passData);
                if (type == null)
                    continue;
                var pass = (RenderPassBase)Activator.CreateInstance(type);
                JsonUtility.FromJsonOverwrite(passData.PassJson, pass);
                pass.RenewNodeId();
                pass.InjectionPoint = CurrentDetailPoint.Value;
                TsukuyomiPassNode node = CreatePassNode(
                    type,
                    passData.Position + new Vector2(30.0f, 30.0f),
                    false,
                    pass, true);
                if (node != null)
                    AddToSelection(node);
            }
        }

        private static string GetPassCategory(Type type)
        {
            if (typeof(RenderPassDefinition).IsAssignableFrom(type)) return "Effects";
            if (typeof(PostPass).IsAssignableFrom(type)) return "Post";
            if (typeof(RasterPass).IsAssignableFrom(type)) return "Raster";
            if (typeof(GraphFeaturePass).IsAssignableFrom(type)) return "Graph Feature";
            if (typeof(ComputePass).IsAssignableFrom(type)) return "Compute";
            if (typeof(UnsafePass).IsAssignableFrom(type)) return "Unsafe";
            return "Other";
        }
    }
}
