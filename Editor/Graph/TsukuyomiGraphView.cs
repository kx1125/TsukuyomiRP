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
        public InjectionPoint? CurrentDetailPoint { get; set; }
        public event Action<InjectionPoint> OnNodeDoubleClicked;
        public event Action<IEnumerable<ISelectable>> OnSelectionChanged;

        private GridBackground _grid;

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
            var edgesToRemove = edges.ToList();
            foreach (var edge in edgesToRemove) RemoveElement(edge);

            var elementsToRemove = graphElements.ToList();
            foreach (var element in elementsToRemove) RemoveElement(element);
        }

        private void OnContextualMenu(ContextualMenuPopulateEvent evt)
        {
            if (CurrentDetailPoint.HasValue)
            {
                var point = CurrentDetailPoint.Value;
                evt.menu.AppendAction($"Add Pass/{point}", (a) => { }, DropdownMenuAction.Status.Disabled);
                evt.menu.AppendSeparator();

                var types = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(s => s.GetTypes())
                    .Where(p => p.IsPublic && typeof(RenderPassBase).IsAssignableFrom(p) && !p.IsAbstract)
                    .OrderBy(p => p.Name);

                foreach (var type in types)
                {
                    var attributes = type.GetCustomAttributes<InjectionPointAttribute>(false).ToList();
                    if (attributes.Count == 0)
                        attributes = type.GetCustomAttributes<InjectionPointAttribute>(true).ToList();

                    if (attributes.Any(a => a.Point == point))
                    {
                        evt.menu.AppendAction($"Add Pass/{GetPassCategory(type)}/{type.Name}", (action) => {
                            CreatePassNode(type, evt.mousePosition, true);
                        });
                    }
                }
            }
            else
            {
                evt.menu.AppendAction("Info: Double-click an Injection Point to enter and add passes", (a) => { }, DropdownMenuAction.Status.Disabled);
            }
        }

        public TsukuyomiPassNode CreatePassNode(Type type, Vector2 position, bool convertMousePosition)
        {
            var node = new TsukuyomiPassNode(type);
            var finalPosition = convertMousePosition ? contentViewContainer.WorldToLocal(position) : position;
            node.SetPosition(new Rect(finalPosition, Vector2.zero));
            AddElement(node);
            OnSelectionChanged?.Invoke(selection);
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

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatiblePorts = new List<Port>();
            ports.ForEach((port) =>
            {
                if (startPort != port && startPort.node != port.node && startPort.direction != port.direction)
                    compatiblePorts.Add(port);
            });
            return compatiblePorts;
        }

        private static string GetPassCategory(Type type)
        {
            if (typeof(PostPass).IsAssignableFrom(type)) return "Post";
            if (typeof(RasterPass).IsAssignableFrom(type)) return "Raster";
            if (typeof(ComputePass).IsAssignableFrom(type)) return "Compute";
            if (typeof(UnsafePass).IsAssignableFrom(type)) return "Unsafe";
            return "Other";
        }
    }
}
