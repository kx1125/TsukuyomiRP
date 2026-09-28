#if ENABLE_UPSCALER_FRAMEWORK && UNITY_6000_6_OR_NEWER
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Tsukuyomi.Rendering
{
    /// <summary>
    /// Unity 6.6 creates a parameterless, shared upscaler and supplies its options per call.
    /// Keep backend resources with the existing options owners, outside that shared instance.
    /// </summary>
    internal sealed class TsukuyomiFrameworkUpscaler : AbstractUpscaler
    {
        [UnityEngine.Scripting.Preserve]
        public TsukuyomiFrameworkUpscaler() { }

        public override string name => TsukuyomiUpscaling.UpscalerName;
        public override bool hasQualityMode => true;
        public override bool isTemporal => TsukuyomiUpscaling.Active?.isTemporal ?? false;
        public override bool supportsSharpening => TsukuyomiUpscaling.Active?.supportsSharpening ?? false;

        public override UpscalerResolutionInfo GetResolutionInfo(Vector2Int displayResolution, UpscalerOptions options)
        {
            var owner = TsukuyomiUpscaling.PrepareUpscaler(options);
            var renderResolution = displayResolution;
            owner?.NegotiatePreUpscaleResolution(ref renderResolution, displayResolution);
            return UpscalerResolutionInfo.Fixed(renderResolution);
        }

        public override void CalculateJitter(int frameIndex, float upscaleRatio, out Vector2 jitter, out bool allowScaling)
        {
            jitter = Vector2.zero;
            allowScaling = false;
            TsukuyomiUpscaling.Active?.CalculateJitter(frameIndex, upscaleRatio, out jitter, out allowScaling);
        }

        public override IUpscalerContext CreateContext(UpscalerOptions options, Vector2Int displayResolution)
            => TsukuyomiUpscaling.PrepareUpscaler(options)?.CreateContext(options, displayResolution);

        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frame)
        {
            var io = frame.Get<UpscalingIO>();
            TsukuyomiUpscaling.PrepareUpscaler(io.options)?.RecordRenderGraph(graph, frame);
        }
    }
}
#endif
