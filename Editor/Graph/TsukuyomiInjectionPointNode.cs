using UnityEditor.Experimental.GraphView;
using Tsukuyomi.Rendering;

namespace Tsukuyomi.Rendering.Editor
{
    public class TsukuyomiInjectionPointNode : Node
    {
        public InjectionPoint Point { get; private set; }
        public Port InputPort { get; private set; }
        public Port OutputPort { get; private set; }

        public TsukuyomiInjectionPointNode(InjectionPoint point)
        {
            Point = point;
            title = point.ToString();
            AddToClassList("tsukuyomi-injection-point-node");
            style.minWidth = 220;
            
            InputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(bool));
            InputPort.portName = "In";
            inputContainer.Add(InputPort);

            OutputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
            OutputPort.portName = "Out";
            outputContainer.Add(OutputPort);

            RefreshExpandedState();
            RefreshPorts();
        }
    }
}
