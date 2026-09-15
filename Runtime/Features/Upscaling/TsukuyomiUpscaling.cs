using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityRhi;
using Fsr = Tsukuyomi.Rendering.FSR3.Fsr3Upscaler;

namespace Tsukuyomi.Rendering
{
    /// <summary>Project-facing runtime API. Changes are applied before the next render context.</summary>
    public static class TsukuyomiUpscaling
    {
        public const string UpscalerName = "Tsukuyomi Upscaler";
        private static UpscalerBackend? s_Backend;
        private static UpscalerQuality? s_Quality;
        private static bool? s_NeuralRendering;
        private static UpscalingStatus s_Status;
        private static string s_DlssFailure, s_NrFailure;
        private static int s_Revision;
        private static bool s_ShuttingDown;
        internal static int Revision => s_Revision;
        internal static Camera CurrentCamera { get; private set; }
        internal static ulong CurrentCameraId => CurrentCamera ? EntityId.ToULong(CurrentCamera.GetEntityId()) : 0;
        internal static float CurrentUpscaleRatio { get; set; } = 1f;
#if ENABLE_UPSCALER_FRAMEWORK
        internal static TsukuyomiUnifiedUpscaler Active { get; private set; }
        internal static TsukuyomiFsr3Settings ActiveFsr3Settings => Active?.Fsr3Settings;
#endif
        public static void SetBackend(UpscalerBackend backend)
        {
            if (!Enum.IsDefined(typeof(UpscalerBackend), backend)) throw new ArgumentOutOfRangeException(nameof(backend));
            s_Backend = backend; Changed();
        }
        public static void SetQuality(UpscalerQuality quality)
        {
            if (!Enum.IsDefined(typeof(UpscalerQuality), quality)) throw new ArgumentOutOfRangeException(nameof(quality));
            s_Quality = quality; Changed();
        }
        public static void SetNeuralRenderingEnabled(bool enabled) { s_NeuralRendering = enabled; s_NrFailure = null; s_Revision++; }
        public static void ResetHistory() { s_Revision++; }
        public static void ClearRuntimeOverrides() { s_Backend = null; s_Quality = null; s_NeuralRendering = null; Changed(); }
        public static UpscalingStatus GetStatus() => s_Status;
        private static void Changed() { s_DlssFailure = null; s_NrFailure = null; s_Revision++; }
        internal static void ReportDlssFailure(string reason) { s_DlssFailure = reason; }
        internal static void ReportNrFailure(string reason) { s_NrFailure = reason; }
        internal static bool FailedNgxResult(int result) => (unchecked((uint)result) & 0xFFF00000u) == 0xBAD00000u || result < 0;

        public static bool IsDlssAvailable(out string reason)
        {
            reason = s_DlssFailure;
            if (!string.IsNullOrEmpty(reason)) return false;
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D12 ||
                (Application.platform != RuntimePlatform.WindowsEditor && Application.platform != RuntimePlatform.WindowsPlayer))
            { reason = "DLSS requires Windows x64 and Direct3D12."; return false; }
            try
            {
                if (!RhiCore.IsD3D12Active || !RhiCore.IsNgxDlssAvailable)
                { reason = "NGX Super Resolution is unavailable on this device/runtime."; return false; }
                if (RhiCore.NativeApiVersion != 10)
                { reason = $"UnityRHI ABI mismatch: expected 10, found {RhiCore.NativeApiVersion}."; return false; }
                var resources = TsukuyomiRenderPipelineResourcesProvider.Current;
                if (!resources || !resources.DlssPrepareInputsShader || !resources.DlssPrepareInputsShader.isSupported)
                { reason = "DLSS input preparation shader is missing or unsupported."; return false; }
                return true;
            }
            catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException || e is BadImageFormatException)
            { reason = e.Message; return false; }
        }

        internal static bool IsFsr3Available(out string reason)
        {
            reason = null;
            if (!SystemInfo.supportsComputeShaders) { reason = "FSR3 requires compute shaders."; return false; }
            var resources = TsukuyomiRenderPipelineResourcesProvider.Current;
            if (resources == null || resources.Fsr3Shaders == null || !resources.Fsr3Shaders.IsValid)
            { reason = "FSR3 shader resources are not preloaded."; return false; }
            return true;
        }

        internal static bool IsSupportedCamera(Camera camera, out string reason)
        {
            reason = null;
            if (!camera || camera.cameraType != CameraType.Game) { reason = "Only Game cameras use temporal upscaling."; return false; }
            if (camera.stereoEnabled) { reason = "XR is not supported by the unified upscaler."; return false; }
            if (camera.TryGetComponent<UniversalAdditionalCameraData>(out var data))
            {
                if (data.renderType == CameraRenderType.Overlay || (data.cameraStack != null && data.cameraStack.Count != 0))
                { reason = "Camera stacks are not supported by the unified upscaler."; return false; }
                if (data.antialiasing == AntialiasingMode.TemporalAntiAliasing)
                { reason = "Disable camera TAA before selecting temporal upscaling."; return false; }
                if (!data.renderPostProcessing) { reason = "Enable camera post processing to execute URP's upscaler."; return false; }
            }
            if (UniversalRenderPipeline.asset && UniversalRenderPipeline.asset.msaaSampleCount > 1)
            { reason = "Disable MSAA before selecting temporal upscaling."; return false; }
            return true;
        }

        internal static bool CanRunNeuralRendering(Camera camera, bool hdrOutput, out string reason)
        {
            reason = "Neural Rendering is disabled.";
#if ENABLE_UPSCALER_FRAMEWORK
            if (Active == null || !Active.NeuralRenderingRequested) return false;
            if (Active.ActualBackend != UpscalerBackend.DLSS) { reason = "Neural Rendering requires the active DLSS backend."; return false; }
            if (!IsSupportedCamera(camera, out reason)) return false;
            if (hdrOutput) { reason = "Neural Rendering currently supports SDR display output only."; return false; }
            if (s_NrFailure != null) { reason = s_NrFailure; return false; }
            if (!IsDlssAvailable(out reason)) return false;
            try
            {
                if (!RhiCore.IsDlssNrAvailable) { reason = "DLSS Neural Rendering runtime is unavailable."; return false; }
                reason = null; return true;
            }
            catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException || e is BadImageFormatException)
            { reason = e.Message; return false; }
#else
            return false;
#endif
        }

        internal static void SetNrStatus(bool active, string reason)
        {
            s_Status = new UpscalingStatus(s_Status.RequestedBackend, s_Status.ActiveBackend,
                s_Status.NeuralRenderingRequested, active, s_Status.Reason, reason, s_Status.RenderResolution, s_Status.OutputResolution);
        }
        internal static void SetFrameStatus(UpscalerBackend requested, UpscalerBackend actual, bool nrRequested,
            string reason, Vector2Int render, Vector2Int output)
        {
            s_Status = new UpscalingStatus(requested, actual, nrRequested, s_Status.NeuralRenderingActive,
                reason, s_Status.NeuralRenderingReason, render, output);
        }

        private sealed class RetiredResources
        {
            internal Action Release;
            internal GraphicsFence Fence;
            internal bool HasFence;
            internal AsyncGPUReadbackRequest Readback;
            internal ComputeBuffer ReadbackBuffer;
        }
        private static readonly List<RetiredResources> s_Retired = new();
        private static bool s_DrainingAll;
        internal static void RetireResources(Action release)
        {
            if (release == null) return;
            if (s_DrainingAll || s_ShuttingDown) { release(); return; }
            var retired = new RetiredResources { Release = release };
            if (SystemInfo.supportsGraphicsFence && SystemInfo.supportsAsyncCompute)
            {
                var cmd = CommandBufferPool.Get("Tsukuyomi retire upscaler resources");
                try
                {
                    retired.Fence = cmd.CreateGraphicsFence(GraphicsFenceType.AsyncQueueSynchronisation, SynchronisationStageFlags.AllGPUOperations);
                    Graphics.ExecuteCommandBuffer(cmd);
                    retired.HasFence = true;
                }
                finally { CommandBufferPool.Release(cmd); }
            }
            else if (SystemInfo.supportsAsyncGPUReadback)
            {
                // Async-queue fence polling requires async-compute support. A GPU readback is
                // another completion marker for commands submitted before retirement.
                retired.ReadbackBuffer = new ComputeBuffer(1, sizeof(int));
                retired.Readback = AsyncGPUReadback.Request(retired.ReadbackBuffer);
            }
            s_Retired.Add(retired);
        }
        internal static void DrainRetiredResources(bool force = false)
        {
            s_DrainingAll = force;
            try
            {
            for (int i = s_Retired.Count - 1; i >= 0; --i)
                if (force || (s_Retired[i].HasFence && s_Retired[i].Fence.passed) ||
                    (s_Retired[i].ReadbackBuffer != null && s_Retired[i].Readback.done))
                {
                    var entry = s_Retired[i]; s_Retired.RemoveAt(i);
                    if (force && entry.ReadbackBuffer != null && !entry.Readback.done) entry.Readback.WaitForCompletion();
                    entry.ReadbackBuffer?.Dispose(); entry.Release();
                }
            }
            finally { s_DrainingAll = false; }
        }

#if ENABLE_UPSCALER_FRAMEWORK
        internal static void ApplyOverrides(TsukuyomiUpscalerOptions copy)
        {
            if (s_Backend.HasValue) copy.backend = s_Backend.Value;
            if (s_Quality.HasValue) { copy.quality = s_Quality.Value; copy.fsr3UltraQuality = false; }
            if (s_NeuralRendering.HasValue) copy.neuralRendering = s_NeuralRendering.Value;
        }
        internal static UpscalerMode ToDlssQuality(UpscalerQuality quality) => quality switch
        {
            UpscalerQuality.NativeAA => UpscalerMode.NATIVE, UpscalerQuality.Balanced => UpscalerMode.BALANCED,
            UpscalerQuality.Performance => UpscalerMode.PERFORMANCE, UpscalerQuality.UltraPerformance => UpscalerMode.ULTRA_PERFORMANCE,
            _ => UpscalerMode.QUALITY
        };
        internal static Fsr.QualityMode ToFsrQuality(UpscalerQuality quality, bool ultraQuality) => quality switch
        {
            UpscalerQuality.NativeAA => Fsr.QualityMode.NativeAA, UpscalerQuality.Balanced => Fsr.QualityMode.Balanced,
            UpscalerQuality.Performance => Fsr.QualityMode.Performance, UpscalerQuality.UltraPerformance => Fsr.QualityMode.UltraPerformance,
            _ => ultraQuality ? Fsr.QualityMode.UltraQuality : Fsr.QualityMode.Quality
        };

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize()
        {
            s_Backend = null; s_Quality = null; s_NeuralRendering = null; s_Status = default;
            s_ShuttingDown = false;
            s_DlssFailure = null; s_NrFailure = null; s_Revision++;
            UpscalerRegistry.Register<TsukuyomiUnifiedUpscaler, TsukuyomiUpscalerOptions>(UpscalerName);
            RenderPipelineManager.beginContextRendering -= BeginContext;
            RenderPipelineManager.beginContextRendering += BeginContext;
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
            UnityEditor.EditorApplication.quitting -= Shutdown;
            UnityEditor.EditorApplication.quitting += Shutdown;
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
            Application.quitting -= Shutdown; Application.quitting += Shutdown;
        }
#if UNITY_EDITOR
        private static void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            // Application.quitting also fires when leaving Play Mode. The Editor
            // can reuse its URP instance without reloading managed assemblies.
            if (state == UnityEditor.PlayModeStateChange.EnteredEditMode) Initialize();
        }
#endif
        private static void BeginContext(ScriptableRenderContext context, List<Camera> cameras)
        {
            if (s_ShuttingDown) return;
            DrainRetiredResources();
            var asset = UniversalRenderPipeline.asset;
            Active = asset && asset.upscalerName == UpscalerName ? TsukuyomiUnifiedUpscaler.FindFor(asset) : null;
            if (Active == null) { SetFrameStatus(UpscalerBackend.Off, UpscalerBackend.Off, false, "Tsukuyomi Upscaler is not selected on the URP asset.", default, default); SetNrStatus(false, ""); }
            TsukuyomiUnifiedUpscaler.BeginContextForAll(Active);
        }
        private static void BeginCamera(ScriptableRenderContext context, Camera camera) { CurrentCamera = camera; }
        private static void EndCamera(ScriptableRenderContext context, Camera camera) { CurrentCamera = null; }
        private static void Shutdown()
        {
            s_ShuttingDown = true;
            UnityRhi.Dlss.Urp.DlssNrRenderFeature.ShutdownManagedFeatures();
            TsukuyomiUnifiedUpscaler.ShutdownAll();
            DrainRetiredResources(true); Active = null; CurrentCamera = null;
        }
#endif
    }
}
