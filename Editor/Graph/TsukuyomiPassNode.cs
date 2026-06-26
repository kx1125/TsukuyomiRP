using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Reflection;
using Tsukuyomi.Rendering;

namespace Tsukuyomi.Rendering.Editor
{
    public class TsukuyomiPassNode : Node
    {
        public Type PassType { get; private set; }

        public TsukuyomiPassNode(Type passType)
        {
            PassType = passType;
            title = passType.Name;
            AddToClassList("tsukuyomi-pass-node");
            style.minWidth = 220;
            style.borderTopWidth = 1;
            style.borderBottomWidth = 1;
            style.borderLeftWidth = 1;
            style.borderRightWidth = 1;
            style.borderTopColor = new Color(0.36f, 0.36f, 0.36f);
            style.borderBottomColor = new Color(0.11f, 0.11f, 0.11f);
            style.borderLeftColor = new Color(0.22f, 0.22f, 0.22f);
            style.borderRightColor = new Color(0.22f, 0.22f, 0.22f);
            
            var inExec = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            inExec.portName = "In";
            inputContainer.Add(inExec);

            var outExec = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
            outExec.portName = "Out";
            outputContainer.Add(outExec);

            GenerateResourcePorts();
        }

        private void GenerateResourcePorts()
        {
            var passKind = new Label(GetPassKind(PassType));
            passKind.style.fontSize = 10;
            passKind.style.color = new Color(0.62f, 0.72f, 0.9f);
            passKind.style.marginLeft = 5;
            passKind.style.marginRight = 5;
            passKind.style.marginBottom = 3;
            extensionContainer.Add(passKind);

            var fields = PassType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (field.FieldType == typeof(TextureSlot))
                {
                    var readAttr = field.GetCustomAttribute<ReadAttribute>();
                    var writeAttr = field.GetCustomAttribute<WriteAttribute>();
                    var rwAttr = field.GetCustomAttribute<ReadWriteAttribute>();

                    string resourceInfo = " [None]";
                    string accessInfo = "";

                    if (readAttr != null)
                    {
                        resourceInfo = readAttr.Builtin != BuiltinTexture.None ? $" [{readAttr.Builtin}]" : $" [{readAttr.CustomName}]";
                        accessInfo = "(Read)";
                    }
                    else if (writeAttr != null)
                    {
                        resourceInfo = writeAttr.Builtin != BuiltinTexture.None ? $" [{writeAttr.Builtin}]" : $" [{writeAttr.CustomName}]";
                        accessInfo = "(Write)";
                    }
                    else if (rwAttr != null)
                    {
                        resourceInfo = rwAttr.Builtin != BuiltinTexture.None ? $" [{rwAttr.Builtin}]" : $" [{rwAttr.CustomName}]";
                        accessInfo = "(ReadWrite)";
                    }

                    var label = new Label($"{field.Name}: {resourceInfo} {accessInfo}");
                    label.style.fontSize = 11;
                    label.style.color = new Color(0.78f, 0.78f, 0.78f);
                    label.style.paddingLeft = 5;
                    label.style.paddingRight = 5;
                    label.style.paddingTop = 1;
                    label.style.paddingBottom = 1;
                    
                    extensionContainer.Add(label);
                }
            }
            RefreshExpandedState();
        }

        private static string GetPassKind(Type type)
        {
            if (typeof(PostPass).IsAssignableFrom(type)) return "Post Pass";
            if (typeof(RasterPass).IsAssignableFrom(type)) return "Raster Pass";
            if (typeof(ComputePass).IsAssignableFrom(type)) return "Compute Pass";
            if (typeof(UnsafePass).IsAssignableFrom(type)) return "Unsafe Pass";
            return "Render Pass";
        }
    }
}
