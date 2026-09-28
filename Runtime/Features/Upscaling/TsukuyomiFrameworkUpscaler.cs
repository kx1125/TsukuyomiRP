#if ENABLE_UPSCALER_FRAMEWORK && UNITY_6000_6_OR_NEWER
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering
{
    /// <summary>
    /// Unity 6.6 constructs the upscaler with its serialized options.
    /// Keep backend resources with the existing options owner, outside this adapter.
    /// </summary>
    internal sealed class TsukuyomiFrameworkUpscaler : AbstractUpscaler
    {
        private readonly TsukuyomiUpscalerOptions _options;

        [UnityEngine.Scripting.Preserve]
        public TsukuyomiFrameworkUpscaler(TsukuyomiUpscalerOptions options) => _options = options;

        public override string name => TsukuyomiUpscaling.UpscalerName;
        public override UpscalerOptions options => ResolveOptions();
        public override bool isTemporal => TsukuyomiUpscaling.Active?.isTemporal ?? false;
        public override bool supportsSharpening => TsukuyomiUpscaling.Active?.supportsSharpening ?? false;

        private TsukuyomiUpscalerOptions ResolveOptions()
        {
            // URP caches this adapter when the pipeline is created. Its options may be
            // added or replaced on the asset later, so the constructor reference can be stale.
            var asset = UniversalRenderPipeline.asset;
            if (asset)
                foreach (var candidate in asset.upscalerOptions)
                    if (candidate is TsukuyomiUpscalerOptions source && source)
                        return source;
            return _options;
        }

        public override void NegotiatePreUpscaleResolution(ref Vector2Int preUpscaleResolution, Vector2Int postUpscaleResolution)
        {
            TsukuyomiUpscaling.PrepareUpscaler(options)?.NegotiatePreUpscaleResolution(
                ref preUpscaleResolution, postUpscaleResolution);
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
            TsukuyomiUpscaling.PrepareUpscaler(options)?.RecordRenderGraph(graph, frame);
        }
    }
}
#endif
