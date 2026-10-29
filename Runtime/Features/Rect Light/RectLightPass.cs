using System;
using UnityEngine;

namespace Tsukuyomi.Rendering
{
    [Serializable, InjectionPoint(InjectionPoint.BeforeOpaque)]
    public sealed class RectLightPass : RenderPassDefinition
    {
        public RectLightPass() => InjectionPoint = InjectionPoint.BeforeOpaque;
        public override string Name => "Rect Lights";
        [Range(1, 64)] public int MaxRectLights = 16;

        public override IRenderPassRuntime CreateRuntime() => new TsukuyomiRectLightPass(this);
    }
}
