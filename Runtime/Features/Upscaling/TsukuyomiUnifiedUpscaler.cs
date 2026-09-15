#if ENABLE_UPSCALER_FRAMEWORK
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering
{
    /// <summary>URP-facing owner of backend selection, options snapshots and temporal history.</summary>
    public sealed class TsukuyomiUnifiedUpscaler : AbstractUpscaler
    {
        private static readonly List<TsukuyomiUnifiedUpscaler> Instances = new();
        private readonly TsukuyomiUpscalerOptions _source;
        private TsukuyomiUpscalerOptions _snapshot;
        private ITemporalUpscalerBackend _backend;
        private int _hash, _revision = -1;
        private int _sourceHash, _snapshotRevision = -1, _dlssFirstDispatch = -1;
        private bool _sourceNr;
        private readonly Dictionary<ulong, CameraHistory> _cameras = new();
        private sealed class CameraHistory
        {
            internal Camera Camera;
            internal Vector3 Position;
            internal Quaternion Rotation;
            internal float Fov;
            internal int Frame;
        }
        private string _reason;
        internal UpscalerBackend ActualBackend { get; private set; }
        internal TsukuyomiFsr3Settings Fsr3Settings => _snapshot?.fsr3;
        internal bool NeuralRenderingRequested => _snapshot && _snapshot.neuralRendering;
        [UnityEngine.Scripting.Preserve]
        public TsukuyomiUnifiedUpscaler(TsukuyomiUpscalerOptions options)
        {
            _source = options;
            for (int i = Instances.Count - 1; i >= 0; --i)
                if (!Instances[i]._source || ReferenceEquals(Instances[i]._source, options))
                {
                    Instances[i].Deactivate();
                    CoreUtils.Destroy(Instances[i]._snapshot);
                    Instances.RemoveAt(i);
                }
            Instances.Add(this);
        }
        public override string name => TsukuyomiUpscaling.UpscalerName;
        public override UpscalerOptions options => _source;
        public override bool supportsXR => false;
        public override bool supportsSharpening => ActualBackend == UpscalerBackend.FSR3;
        public override bool isTemporal => _backend != null && TsukuyomiUpscaling.IsSupportedCamera(TsukuyomiUpscaling.CurrentCamera, out _);

        internal static TsukuyomiUnifiedUpscaler FindFor(UniversalRenderPipelineAsset asset)
        {
            for (int i = Instances.Count - 1; i >= 0; --i)
                if (Instances[i]._source && asset.upscalerOptions.Contains(Instances[i]._source)) return Instances[i];
            return null;
        }
        internal static void BeginContextForAll(TsukuyomiUnifiedUpscaler active)
        {
            foreach (var instance in Instances)
                if (ReferenceEquals(instance, active)) instance.ApplySettings();
                else instance.Deactivate();
        }
        private void ApplySettings()
        {
            if (!_source) { Deactivate(); return; }
            if (!_snapshot)
            {
                _snapshot = ScriptableObject.CreateInstance<TsukuyomiUpscalerOptions>();
                _snapshot.hideFlags = HideFlags.HideAndDontSave;
            }
            int sourceHash = _source.BackendSettingsHash();
            if (_snapshotRevision != TsukuyomiUpscaling.Revision || _sourceHash != sourceHash || _sourceNr != _source.neuralRendering)
            {
                // Copy only when settings change; no per-frame JSON allocations.
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(_source), _snapshot);
                TsukuyomiUpscaling.ApplyOverrides(_snapshot);
                _snapshot.fsr3 ??= new TsukuyomiFsr3Settings();
                _snapshot.fsr3.Enabled = true;
                _snapshot.fsr3.QualityMode = TsukuyomiUpscaling.ToFsrQuality(_snapshot.quality, _snapshot.fsr3UltraQuality);
                _sourceHash = sourceHash; _sourceNr = _source.neuralRendering; _snapshotRevision = TsukuyomiUpscaling.Revision;
            }
            if (ActualBackend == UpscalerBackend.DLSS && _dlssFirstDispatch >= 0 && Time.frameCount > _dlssFirstDispatch + 3 &&
                (TsukuyomiUpscaling.FailedNgxResult(UnityRhi.RhiCore.DlssLastCreateResult) || TsukuyomiUpscaling.FailedNgxResult(UnityRhi.RhiCore.DlssLastEvaluateResult)))
                TsukuyomiUpscaling.ReportDlssFailure($"NGX SR failed: create=0x{UnityRhi.RhiCore.DlssLastCreateResult:X8}, evaluate=0x{UnityRhi.RhiCore.DlssLastEvaluateResult:X8}.");
            var selected = _snapshot.backend;
            _reason = null;
            if (selected == UpscalerBackend.DLSS && !TsukuyomiUpscaling.IsDlssAvailable(out _reason)) selected = UpscalerBackend.FSR3;
            if (selected == UpscalerBackend.FSR3 && !TsukuyomiUpscaling.IsFsr3Available(out var fsrReason))
            { selected = UpscalerBackend.Off; _reason = string.IsNullOrEmpty(_reason) ? fsrReason : _reason + " " + fsrReason; }
            int hash = _snapshot.BackendSettingsHash();
            if (selected != ActualBackend || hash != _hash || (selected != UpscalerBackend.Off && _backend == null))
            {
                Deactivate();
                ActualBackend = selected;
                try
                {
                    _backend = selected switch
                    {
                        UpscalerBackend.DLSS => new DlssTemporalBackend(_snapshot),
                        UpscalerBackend.FSR3 => new Fsr3TemporalBackend(_snapshot.fsr3),
                        _ => null
                    };
                }
                catch (Exception e) when (selected == UpscalerBackend.DLSS)
                {
                    TsukuyomiUpscaling.ReportDlssFailure(e.Message);
                    _reason = e.Message;
                    ActualBackend = TsukuyomiUpscaling.IsFsr3Available(out _) ? UpscalerBackend.FSR3 : UpscalerBackend.Off;
                    if (ActualBackend == UpscalerBackend.FSR3) _backend = new Fsr3TemporalBackend(_snapshot.fsr3);
                }
                _hash = hash;
            }
            if (_revision != TsukuyomiUpscaling.Revision) { _backend?.ResetHistory(); _revision = TsukuyomiUpscaling.Revision; }
        }
        private void Deactivate()
        {
            if (_backend != null) { var old = _backend; _backend = null; TsukuyomiUpscaling.RetireResources(old.Dispose); }
            ActualBackend = UpscalerBackend.Off;
            _dlssFirstDispatch = -1;
            _cameras.Clear();
        }
        public override void NegotiatePreUpscaleResolution(ref Vector2Int renderSize, Vector2Int displaySize)
        {
            renderSize = displaySize;
            bool supported = TsukuyomiUpscaling.IsSupportedCamera(TsukuyomiUpscaling.CurrentCamera, out var cameraReason);
            if (supported) _backend?.NegotiatePreUpscaleResolution(ref renderSize, displaySize);
            TsukuyomiUpscaling.CurrentUpscaleRatio = (float)displaySize.x / Mathf.Max(1, renderSize.x);
            if (TsukuyomiUpscaling.CurrentCamera && TsukuyomiUpscaling.CurrentCamera.cameraType == CameraType.Game)
                TsukuyomiUpscaling.SetFrameStatus(_snapshot ? _snapshot.backend : UpscalerBackend.Off,
                    supported ? ActualBackend : UpscalerBackend.Off, NeuralRenderingRequested,
                    supported ? _reason : cameraReason, renderSize, displaySize);
        }
#if UNITY_6000_6_OR_NEWER
        public override void CalculateJitter(int frameIndex, float upscaleRatio, out Vector2 jitter, out bool allowScaling)
#else
        public override void CalculateJitter(int frameIndex, out Vector2 jitter, out bool allowScaling)
#endif
        {
            jitter = Vector2.zero; allowScaling = false;
            if (isTemporal) _backend.CalculateJitter(frameIndex, out jitter, out allowScaling);
        }
        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frame)
        {
            if (isTemporal)
            {
                var io = frame.Get<UpscalingIO>();
                var camera = TsukuyomiUpscaling.CurrentCamera;
                if (!_cameras.TryGetValue(io.cameraInstanceID, out var history))
                {
                    history = new CameraHistory { Camera = camera, Frame = -1 };
                    _cameras[io.cameraInstanceID] = history;
                    io.resetHistory = true;
                }
                io.resetHistory |= history.Frame != io.frameIndex - 1 ||
                    Vector3.Distance(history.Position, camera.transform.position) > 5f ||
                    Quaternion.Angle(history.Rotation, camera.transform.rotation) > 45f ||
                    Mathf.Abs(history.Fov - camera.fieldOfView) > .01f;
                history.Position = camera.transform.position; history.Rotation = camera.transform.rotation;
                history.Fov = camera.fieldOfView; history.Frame = io.frameIndex;
                _backend.RecordRenderGraph(graph, frame);
                if (ActualBackend == UpscalerBackend.DLSS && _dlssFirstDispatch < 0) _dlssFirstDispatch = Time.frameCount;
            }
        }
        internal static void ShutdownAll()
        {
            foreach (var instance in Instances)
            { instance.Deactivate(); CoreUtils.Destroy(instance._snapshot); instance._snapshot = null; }
            // Keep the lightweight registry until URP replaces its instances. In
            // the Editor, leaving Play Mode can reuse the same framework owner.
        }
#if UNITY_6000_6_OR_NEWER
        public override IUpscalerContext CreateContext(UpscalerOptions options, Vector2Int displayResolution) => new FrameworkContext(displayResolution);
        private sealed class FrameworkContext : IUpscalerContext
        {
            public Vector2Int createdForDisplayResolution { get; }
            public int lastUsedFrame { get; set; }
            internal FrameworkContext(Vector2Int resolution) => createdForDisplayResolution = resolution;
            public bool IsValidForOptions(UpscalerOptions options) => true;
            public void Cleanup(CommandBuffer cmd) { }
        }
#endif
    }
}
#endif
