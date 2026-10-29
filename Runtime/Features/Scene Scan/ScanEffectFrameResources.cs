using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Tsukuyomi.Rendering
{
    // These handles never cross a camera recording or render-graph execution.
    internal sealed class ScanEffectFrameResources : ContextItem
    {
        public TextureHandle OriginalColor, SampledColor, DistortedColor, ScreenMask, TargetContribution;
        public bool OpeningActive;
        public ScanCoverageParameters OpeningCoverage, XrayCoverage;
        public override void Reset()
        {
            OriginalColor = SampledColor = DistortedColor = ScreenMask = TargetContribution = TextureHandle.nullHandle;
            OpeningActive = false;
            OpeningCoverage = XrayCoverage = default;
        }
    }

    internal static class ScanEffectShaderData
    {
        public static Vector4 Origin(in ScanCoverageParameters c) => new(c.OriginWS.x, c.OriginWS.y, c.OriginWS.z, c.Progress);
        public static Vector4 Range(in ScanCoverageParameters c, float cellSize) => new(Mathf.Max(.01f, cellSize),
            Mathf.Max(.01f, c.MaxRange), Mathf.Max(.01f, c.RingWidth), Mathf.Max(0f, c.NoiseStrength));
        public static Vector4 Curves(in ScanCoverageParameters c, float fourth = 0f) => new(c.Blend, c.WorldProgress, c.ScreenProgress, fourth);

        public static TextureHandle Optional(in FeatureGraphContext graph, TextureHandle handle)
            => handle.IsValid() ? handle : graph.RenderGraph.defaultResources.blackTexture;

        public static TextureDesc ColorDesc(in FeatureGraphContext graph, TextureHandle source,
            string name, UnityEngine.Experimental.Rendering.GraphicsFormat format)
        {
            var desc = TextureDescriptors.ColorLike(graph.RenderGraph.GetTextureDesc(source), name);
            desc.colorFormat = format;
            desc.filterMode = FilterMode.Point;
            return desc;
        }
    }
}
