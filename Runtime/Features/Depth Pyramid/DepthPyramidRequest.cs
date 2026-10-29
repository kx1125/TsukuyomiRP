using System;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering
{
    /// <summary>Pass-owned packed min-depth variant. Compatible consumers share its producer.</summary>
    public readonly struct DepthPyramidRequest : IEquatable<DepthPyramidRequest>
    {
        public readonly BuiltinTexture Source;
        public readonly RenderPassEvent GenerationEvent;
        public readonly GraphicsFormat Format;
        public readonly int CheckerboardMipCount;
        public readonly int MipCount;
        public DepthPyramidRequest(int mipCount, BuiltinTexture source = BuiltinTexture.CameraDepthTexture,
            RenderPassEvent generationEvent = RenderPassEvent.AfterRenderingPrePasses,
            GraphicsFormat format = GraphicsFormat.R32_SFloat, int checkerboardMipCount = 0)
        { Source = source; GenerationEvent = generationEvent; Format = format; CheckerboardMipCount = checkerboardMipCount; MipCount = mipCount; }
        public bool Equals(DepthPyramidRequest other) => Source == other.Source && GenerationEvent == other.GenerationEvent
            && Format == other.Format && CheckerboardMipCount == other.CheckerboardMipCount;
        public override bool Equals(object obj) => obj is DepthPyramidRequest other && Equals(other);
        public override int GetHashCode() => HashCode.Combine((int)Source, (int)GenerationEvent, (int)Format, CheckerboardMipCount);
        internal bool Supported => Source == BuiltinTexture.CameraDepthTexture
            && GenerationEvent == RenderPassEvent.AfterRenderingPrePasses && Format == GraphicsFormat.R32_SFloat
            && CheckerboardMipCount == 0 && MipCount > 0 && MipCount <= 15;
    }

    public static class DepthPyramidRequirements
    {
        public static void RequireDepthPyramid(this ResourceRequirementCollector collector)
            => collector.Require(new DepthPyramidRequest(15));
        public static void RequireDepthPyramid(this ResourceRequirementCollector collector, in DepthPyramidRequest request)
            => collector.Require(request);
    }
}
