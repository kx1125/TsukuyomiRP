using System;
using UnityEngine;

namespace Tsukuyomi.Rendering
{
    [Serializable, InjectionPoint(InjectionPoint.BeforeOpaque)]
    public sealed class RayTracedShadowPass : RenderPassDefinition
    {
        public RayTracedShadowPass() => InjectionPoint = InjectionPoint.BeforeOpaque;
        public override string Name => "Ray Traced Shadows";
        [Range(1, 64)] public int RayTracedShadowSamples = 8;
        [Range(1, 16)] public int MaxRayTracedShadowLights = 4;
        public LayerMask RayTracedShadowCasterMask = -1;
        [Min(0.01f)] public float RayTracedShadowDirectionalDistance = 100;


        public override IRenderPassRuntime CreateRuntime() => new TsukuyomiRayTracedShadowPass(this);
    }
}
