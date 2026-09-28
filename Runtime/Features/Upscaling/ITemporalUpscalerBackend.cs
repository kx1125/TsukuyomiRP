#if ENABLE_UPSCALER_FRAMEWORK
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityRhi.Dlss.Urp;

namespace Tsukuyomi.Rendering
{
    /// <summary>
    /// Both backends consume URP UpscalingIO: color/depth/motion, exposure, coordinate
    /// conventions, resolution and camera history. FSR masks remain optional frame data.
    /// Resource encoding and native dispatch belong to the selected implementation.
    /// </summary>
    internal interface ITemporalUpscalerBackend : IDisposable
    {
        void NegotiatePreUpscaleResolution(ref Vector2Int renderSize, Vector2Int displaySize);
        void CalculateJitter(int frame, out Vector2 jitter, out bool allowScaling);
        void RecordRenderGraph(RenderGraph graph, ContextContainer frame);
        void ResetHistory();
        void ReleaseCamera(ulong cameraId);
    }

    internal sealed class Fsr3TemporalBackend : ITemporalUpscalerBackend
    {
        private readonly TsukuyomiFsr3Upscaler _upscaler;
        internal Fsr3TemporalBackend(TsukuyomiFsr3Settings settings) => _upscaler = new(settings);
        public void NegotiatePreUpscaleResolution(ref Vector2Int size, Vector2Int display) => _upscaler.NegotiatePreUpscaleResolution(ref size, display);
        public void CalculateJitter(int frame, out Vector2 jitter, out bool scaling)
        {
#if UNITY_6000_6_OR_NEWER
            _upscaler.CalculateJitter(frame, TsukuyomiUpscaling.CurrentUpscaleRatio, out jitter, out scaling);
#else
            _upscaler.CalculateJitter(frame, out jitter, out scaling);
#endif
        }
        public void RecordRenderGraph(RenderGraph graph, ContextContainer frame) => _upscaler.RecordRenderGraph(graph, frame);
        public void ResetHistory() => _upscaler.ResetHistory();
        public void ReleaseCamera(ulong cameraId) => _upscaler.ReleaseCamera(cameraId);
        public void Dispose() => _upscaler.Dispose();
    }

    internal sealed class DlssTemporalBackend : ITemporalUpscalerBackend
    {
        private readonly UnityRhiDlssOptions _options;
        private readonly UnityRhiDlssIUpscaler _upscaler;
        internal DlssTemporalBackend(TsukuyomiUpscalerOptions options)
        {
            _options = ScriptableObject.CreateInstance<UnityRhiDlssOptions>();
            _options.hideFlags = HideFlags.HideAndDontSave;
            _options.qualityMode = TsukuyomiUpscaling.ToDlssQuality(options.quality);
            _options.fixedResolutionMode = true;
            _options.preset = options.dlssPreset;
            _options.motionVectorScale = options.dlssMotionVectorScale;
            if (TsukuyomiRenderPipelineResourcesProvider.TryGet(out var resources))
                _options.prepareInputsShader = resources.DlssPrepareInputsShader;
            _upscaler = new UnityRhiDlssIUpscaler(_options);
        }
        public void NegotiatePreUpscaleResolution(ref Vector2Int size, Vector2Int display) => _upscaler.NegotiatePreUpscaleResolution(ref size, display);
        public void CalculateJitter(int frame, out Vector2 jitter, out bool scaling)
        {
#if UNITY_6000_6_OR_NEWER
            _upscaler.CalculateJitter(frame, TsukuyomiUpscaling.CurrentUpscaleRatio, out jitter, out scaling);
#else
            _upscaler.CalculateJitter(frame, out jitter, out scaling);
#endif
        }
        public void RecordRenderGraph(RenderGraph graph, ContextContainer frame) => _upscaler.RecordRenderGraph(graph, frame);
        public void ResetHistory() => _upscaler.ResetHistory();
        public void ReleaseCamera(ulong cameraId) => _upscaler.ReleaseCamera(cameraId);
        public void Dispose()
        {
            _upscaler.Dispose();
            CoreUtils.Destroy(_options);
        }
    }
}
#endif
