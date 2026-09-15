using System;
using UnityEngine;
using UnityRhi;

namespace Tsukuyomi.Rendering
{
    public enum UpscalerBackend { Off, FSR3, DLSS }
    public enum UpscalerQuality { NativeAA, Quality, Balanced, Performance, UltraPerformance }

    public readonly struct UpscalingStatus
    {
        public UpscalerBackend RequestedBackend { get; }
        public UpscalerBackend ActiveBackend { get; }
        public bool NeuralRenderingRequested { get; }
        public bool NeuralRenderingActive { get; }
        public string Reason { get; }
        public string NeuralRenderingReason { get; }
        public Vector2Int RenderResolution { get; }
        public Vector2Int OutputResolution { get; }

        internal UpscalingStatus(UpscalerBackend requested, UpscalerBackend actual, bool nrRequested,
            bool nrActive, string reason, string nrReason, Vector2Int renderSize, Vector2Int outputSize)
        {
            RequestedBackend = requested; ActiveBackend = actual;
            NeuralRenderingRequested = nrRequested; NeuralRenderingActive = nrActive;
            Reason = reason ?? string.Empty; NeuralRenderingReason = nrReason ?? string.Empty;
            RenderResolution = renderSize; OutputResolution = outputSize;
        }

        public override string ToString() => $"{RequestedBackend} -> {ActiveBackend}, " +
            $"{RenderResolution} -> {OutputResolution}, NR={NeuralRenderingActive}; {Reason} {NeuralRenderingReason}";
    }

#if ENABLE_UPSCALER_FRAMEWORK
    /// <summary>Serialized on the URP asset; runtime changes use a separate snapshot.</summary>
    [Serializable]
    public sealed class TsukuyomiUpscalerOptions : UnityEngine.Rendering.UpscalerOptions
    {
        public UpscalerBackend backend = UpscalerBackend.Off;
        public UpscalerQuality quality = UpscalerQuality.Quality;
        [Tooltip("Run DLSS 5 Neural Rendering after post processing, only while DLSS is active. Image controls are in its Volume.")]
        public bool neuralRendering;
        public DlssPreset dlssPreset = DlssPreset.Default;
        public Vector2 dlssMotionVectorScale = Vector2.one;
        public TsukuyomiFsr3Settings fsr3 = new();
        [Tooltip("FSR3-only legacy 1.2x mode. Used when common Quality is selected.")]
        public bool fsr3UltraQuality;

        internal int BackendSettingsHash()
        {
            // NR has its own history and does not invalidate the upscaler's accumulation.
            unchecked
            {
                int h = (int)backend * 397 ^ (int)quality;
                h = h * 397 ^ (int)dlssPreset;
                h = h * 397 ^ dlssMotionVectorScale.GetHashCode();
                h = h * 397 ^ fsr3UltraQuality.GetHashCode();
                var f = fsr3 ?? new TsukuyomiFsr3Settings();
                h = h * 397 ^ f.PerformSharpenPass.GetHashCode();
                h = h * 397 ^ f.Sharpness.GetHashCode();
                h = h * 397 ^ f.VelocityFactor.GetHashCode();
                h = h * 397 ^ f.EnableAutoExposure.GetHashCode();
                h = h * 397 ^ f.EnableDebugView.GetHashCode();
                h = h * 397 ^ (int)f.ReactiveMaskMode;
                h = h * 397 ^ f.AutoTcThreshold.GetHashCode();
                h = h * 397 ^ f.AutoTcScale.GetHashCode();
                h = h * 397 ^ f.AutoReactiveScale.GetHashCode();
                return h * 397 ^ f.AutoReactiveMax.GetHashCode();
            }
        }
    }
#endif
}
