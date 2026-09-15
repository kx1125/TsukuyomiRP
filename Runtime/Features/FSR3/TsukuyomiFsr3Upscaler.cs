#if ENABLE_UPSCALER_FRAMEWORK
#if UNITY_6000_6_OR_NEWER
#define TSUKUYOMI_UPSCALER_API_6000_6
#endif
using System.Collections.Generic;
using Tsukuyomi.Rendering.FSR3;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tsukuyomi.Rendering
{
    internal sealed class TsukuyomiFsr3Upscaler : AbstractUpscaler
    {
        public const string UpscalerName = "Tsukuyomi FSR3";

        private static readonly List<TsukuyomiFsr3Upscaler> Instances = new();

        private readonly Dictionary<ulong, CameraContext> _cameraContexts = new();
        private readonly Dictionary<ulong, ResolutionContext> _resolutionContexts = new();
        private readonly HashSet<ulong> _loggedTaaConflictCameras = new();

        private readonly TsukuyomiFsr3Settings _settings;
        public TsukuyomiFsr3Upscaler(TsukuyomiFsr3Settings settings)
        {
            _settings = settings;
            Instances.Add(this);
        }

        // The property contract and EntityId camera IDs are shared by Unity 6000.5 and 6000.6.
        public override string name => UpscalerName;
        public override bool isTemporal => true;
        public override bool supportsSharpening => true;
        public override bool supportsXR => false;

#if TSUKUYOMI_UPSCALER_API_6000_6
        public override IUpscalerContext CreateContext(UpscalerOptions options, Vector2Int displayResolution)
        {
            // The FSR3 implementation owns its native history per camera because it also
            // tracks render size, HDR and shader changes. URP still requires a framework
            // context for temporal upscalers, so provide a lightweight lifecycle bridge.
            return new Fsr3FrameworkContext(displayResolution);
        }
#endif

#if TSUKUYOMI_UPSCALER_API_6000_6
        public override void CalculateJitter(int frameIndex, float upscaleRatio, out Vector2 jitter, out bool allowScaling)
        {
            float safeUpscaleRatio = upscaleRatio > 0.0f && !float.IsNaN(upscaleRatio) && !float.IsInfinity(upscaleRatio)
                ? upscaleRatio
                : 1.0f;
            int jitterPhaseCount = Mathf.Max(1, (int)(8.0f * safeUpscaleRatio * safeUpscaleRatio));

            Fsr3Upscaler.GetJitterOffset(out float jitterX, out float jitterY, frameIndex, jitterPhaseCount);
            jitter = new Vector2(jitterX, jitterY);
            allowScaling = false;
        }
#else
        public override void CalculateJitter(int frameIndex, out Vector2 jitter, out bool allowScaling)
        {
            ResolutionContext resolution = GetCurrentResolutionContext();
            int jitterPhaseCount = Fsr3Upscaler.GetJitterPhaseCount(
                Mathf.Max(1, resolution.PreUpscaleResolution.x),
                Mathf.Max(1, resolution.PostUpscaleResolution.x));

            Fsr3Upscaler.GetJitterOffset(out float jitterX, out float jitterY, frameIndex, jitterPhaseCount);
            jitter = new Vector2(jitterX, jitterY);
            allowScaling = false;
        }
#endif

        public override void NegotiatePreUpscaleResolution(ref Vector2Int preUpscaleResolution, Vector2Int postUpscaleResolution)
        {
            if (!TryGetValidResources(out TsukuyomiRenderPipelineResources resources))
                return;

            TsukuyomiFsr3Settings settings = _settings;
            Fsr3Upscaler.GetRenderResolutionFromQualityMode(
                out int renderWidth,
                out int renderHeight,
                Mathf.Max(1, postUpscaleResolution.x),
                Mathf.Max(1, postUpscaleResolution.y),
                settings.QualityMode);

            preUpscaleResolution = new Vector2Int(Mathf.Max(1, renderWidth), Mathf.Max(1, renderHeight));
            GetCurrentResolutionContext().Set(preUpscaleResolution, postUpscaleResolution);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (!TryGetValidResources(out TsukuyomiRenderPipelineResources resources))
                return;

            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            if (!IsSupportedCamera(cameraData))
                return;

            if (HasUserTemporalAA(cameraData))
            {
                LogTaaConflict(cameraData.camera);
                return;
            }

            UpscalingIO io = frameData.Get<UpscalingIO>();
            if (!io.cameraColor.IsValid() || !io.cameraDepth.IsValid() || !io.motionVectorColor.IsValid())
                return;

            TextureHandle output = CreateOutputTexture(renderGraph, io);
            ulong cameraId = io.cameraInstanceID;
            CameraContext cameraContext = GetCameraContext(cameraId);
            bool isHdr = io.hdrInput;
            bool contextRecreated = EnsureContext(
                cameraContext,
                io.postUpscaleResolution,
                io.preUpscaleResolution,
                isHdr,
                _settings.EnableAutoExposure,
                _settings.QualityMode,
                resources.Fsr3Shaders);

            bool resetAccumulation = contextRecreated || io.resetHistory || cameraContext.LastFrame != io.frameIndex - 1;
            cameraContext.LastFrame = io.frameIndex;
            Fsr3UpscalerContext fsrContext = cameraContext.Context;
            TsukuyomiFsr3Settings settings = _settings;
            Vector2 jitterOffset = CalculateJitter(io.frameIndex, io.preUpscaleResolution, io.postUpscaleResolution);
            Vector2Int renderSize = io.preUpscaleResolution;
            Vector2Int displaySize = io.postUpscaleResolution;
            float fieldOfView = io.fieldOfViewDegrees;
            float nearClip = io.nearClipPlane;
            float farClip = io.farClipPlane;
            float deltaTime = io.deltaTime;
            TsukuyomiFsr3FrameData fsrFrameData = frameData.GetOrCreate<TsukuyomiFsr3FrameData>();
            TextureHandle colorOpaqueOnly = fsrFrameData.ColorOpaqueOnly;
            TextureHandle reactiveMask = fsrFrameData.ReactiveMask;
            TextureHandle compositionMask = fsrFrameData.TransparencyAndCompositionMask;
            bool enableAutoReactive = settings.ReactiveMaskMode != TsukuyomiFsr3ReactiveMaskMode.Disabled && colorOpaqueOnly.IsValid();

            using (var builder = renderGraph.AddUnsafePass<PassData>("Tsukuyomi FSR3 Upscaler", out PassData passData))
            {
                builder.UseTexture(io.cameraColor, AccessFlags.Read);
                builder.UseTexture(io.cameraDepth, AccessFlags.Read);
                builder.UseTexture(io.motionVectorColor, AccessFlags.Read);
                builder.UseTexture(output, AccessFlags.ReadWrite);
                if (colorOpaqueOnly.IsValid())
                    builder.UseTexture(colorOpaqueOnly, AccessFlags.Read);
                if (reactiveMask.IsValid())
                    builder.UseTexture(reactiveMask, AccessFlags.Read);
                if (compositionMask.IsValid())
                    builder.UseTexture(compositionMask, AccessFlags.Read);
                builder.AllowGlobalStateModification(true);

                passData.Context = fsrContext;
                passData.Color = io.cameraColor;
                passData.Depth = io.cameraDepth;
                passData.MotionVectors = io.motionVectorColor;
                passData.ColorOpaqueOnly = colorOpaqueOnly;
                passData.ReactiveMask = reactiveMask;
                passData.CompositionMask = compositionMask;
                passData.Output = output;
                passData.JitterOffset = jitterOffset;
                float motionSign = io.motionVectorDirection == UpscalingIO.MotionVectorDirection.PreviousFrameToCurrentFrame ? -1f : 1f;
                passData.MotionVectorScale = motionSign * (io.motionVectorDomain == UpscalingIO.MotionVectorDomain.NDC
                    ? (Vector2)io.motionVectorTextureSize : Vector2.one);
                passData.PreExposure = Mathf.Max(0.0001f, io.preExposureValue);
                passData.RenderSize = renderSize;
                passData.DisplaySize = displaySize;
                passData.ResetAccumulation = resetAccumulation;
                passData.EnableSharpening = settings.PerformSharpenPass;
                passData.Sharpness = settings.Sharpness;
                passData.FrameTimeDelta = deltaTime;
                passData.CameraNear = nearClip;
                passData.CameraFar = farClip;
                passData.CameraFovAngleVertical = fieldOfView * Mathf.Deg2Rad;
                passData.VelocityFactor = settings.VelocityFactor;
                passData.Flags = settings.EnableDebugView ? Fsr3Upscaler.DispatchFlags.DrawDebugView : 0;
                passData.EnableAutoReactive = enableAutoReactive;
                passData.AutoTcThreshold = settings.AutoTcThreshold;
                passData.AutoTcScale = settings.AutoTcScale;
                passData.AutoReactiveScale = settings.AutoReactiveScale;
                passData.AutoReactiveMax = settings.AutoReactiveMax;

                builder.SetRenderFunc(static (PassData data, UnsafeGraphContext context) =>
                {
                    if (data.Context == null)
                        return;

                    Fsr3Upscaler.DispatchDescription dispatchDescription = new()
                    {
                        Color = new ResourceView(data.Color, RenderTextureSubElement.Color),
                        Depth = new ResourceView(data.Depth, RenderTextureSubElement.Depth),
                        MotionVectors = new ResourceView(data.MotionVectors, RenderTextureSubElement.Color),
                        Exposure = ResourceView.Unassigned,
                        Reactive = data.ReactiveMask.IsValid()
                            ? new ResourceView(data.ReactiveMask, RenderTextureSubElement.Color)
                            : ResourceView.Unassigned,
                        TransparencyAndComposition = data.CompositionMask.IsValid()
                            ? new ResourceView(data.CompositionMask, RenderTextureSubElement.Color)
                            : ResourceView.Unassigned,
                        Output = new ResourceView(data.Output, RenderTextureSubElement.Color),
                        JitterOffset = data.JitterOffset,
                        MotionVectorScale = data.MotionVectorScale,
                        RenderSize = data.RenderSize,
                        UpscaleSize = data.DisplaySize,
                        EnableSharpening = data.EnableSharpening,
                        Sharpness = data.Sharpness,
                        FrameTimeDelta = data.FrameTimeDelta,
                        PreExposure = data.PreExposure,
                        Reset = data.ResetAccumulation,
                        CameraNear = data.CameraNear,
                        CameraFar = data.CameraFar,
                        CameraFovAngleVertical = data.CameraFovAngleVertical,
                        ViewSpaceToMetersFactor = 1.0f,
                        VelocityFactor = data.VelocityFactor,
                        Flags = data.Flags,
                        EnableAutoReactive = data.EnableAutoReactive,
                        ColorOpaqueOnly = data.ColorOpaqueOnly.IsValid()
                            ? new ResourceView(data.ColorOpaqueOnly, RenderTextureSubElement.Color)
                            : ResourceView.Unassigned,
                        AutoTcThreshold = data.AutoTcThreshold,
                        AutoTcScale = data.AutoTcScale,
                        AutoReactiveScale = data.AutoReactiveScale,
                        AutoReactiveMax = data.AutoReactiveMax
                    };

                    if (SystemInfo.usesReversedZBuffer)
                        (dispatchDescription.CameraNear, dispatchDescription.CameraFar) = (dispatchDescription.CameraFar, dispatchDescription.CameraNear);

                    data.Context.Dispatch(dispatchDescription, CommandBufferHelpers.GetNativeCommandBuffer(context.cmd));
                });
            }

            io.cameraColor = output;
        }

        public void ResetHistory() { foreach (var context in _cameraContexts.Values) context.LastFrame = -1; }
        public void Dispose() { DestroyAllContexts(); Instances.Remove(this); }

        public static void DestroyAllInstances()
        {
            for (int i = 0; i < Instances.Count; i++)
                Instances[i]?.DestroyAllContexts();
        }

        private bool TryGetValidResources(out TsukuyomiRenderPipelineResources resources)
        {
            if (!TsukuyomiRenderPipelineResourcesProvider.TryGet(out resources))
                return false;

            TsukuyomiFsr3Settings settings = _settings;
            return settings != null &&
                   settings.Enabled &&
                   resources.Fsr3Shaders != null &&
                   resources.Fsr3Shaders.IsValid &&
                   SystemInfo.supportsComputeShaders;
        }

        private static bool IsSupportedCamera(UniversalCameraData cameraData)
        {
            if (cameraData == null || cameraData.camera == null)
                return false;

            if (cameraData.cameraType != CameraType.Game || cameraData.isPreviewCamera || cameraData.xr.enabled)
                return false;

            if (cameraData.renderType == CameraRenderType.Overlay)
                return false;

            if (cameraData.camera.TryGetComponent(out UniversalAdditionalCameraData additionalCameraData))
            {
                if (additionalCameraData.renderType == CameraRenderType.Overlay)
                    return false;

                if (additionalCameraData.cameraStack != null && additionalCameraData.cameraStack.Count > 0)
                    return false;
            }

            return true;
        }

        private static bool HasUserTemporalAA(UniversalCameraData cameraData)
        {
            return cameraData.camera != null &&
                   cameraData.camera.TryGetComponent(out UniversalAdditionalCameraData additionalCameraData) &&
                   additionalCameraData.antialiasing == AntialiasingMode.TemporalAntiAliasing;
        }

        private void LogTaaConflict(Camera camera)
        {
            if (camera == null)
                return;

            ulong cameraId = EntityId.ToULong(camera.GetEntityId());
            if (_loggedTaaConflictCameras.Add(cameraId))
            {
                Debug.LogError($"Tsukuyomi FSR3 is enabled on camera '{camera.name}', but Unity Temporal Anti-Aliasing is also enabled. Disable TAA in the camera's Universal Additional Camera Data before using Tsukuyomi FSR3.", camera);
            }
        }

        private static Vector2 CalculateJitter(int frameIndex, Vector2Int renderSize, Vector2Int displaySize)
        {
            int jitterPhaseCount = Fsr3Upscaler.GetJitterPhaseCount(Mathf.Max(1, renderSize.x), Mathf.Max(1, displaySize.x));
            Fsr3Upscaler.GetJitterOffset(out float jitterX, out float jitterY, frameIndex, jitterPhaseCount);
            return new Vector2(jitterX, jitterY);
        }

        private static TextureHandle CreateOutputTexture(RenderGraph renderGraph, UpscalingIO io)
        {
            TextureDesc inputDesc = io.cameraColor.GetDescriptor(renderGraph);
            TextureDesc outputDesc = inputDesc;
            outputDesc.width = io.postUpscaleResolution.x;
            outputDesc.height = io.postUpscaleResolution.y;
            outputDesc.format = GraphicsFormatUtility.GetLinearFormat(inputDesc.format);
            outputDesc.msaaSamples = MSAASamples.None;
            outputDesc.useMipMap = false;
            outputDesc.autoGenerateMips = false;
            outputDesc.useDynamicScale = false;
            outputDesc.discardBuffer = false;
            outputDesc.enableRandomWrite = true;
            outputDesc.clearBuffer = false;
            outputDesc.filterMode = FilterMode.Bilinear;
            outputDesc.name = "_TsukuyomiFsr3UpscaledColor";
            return renderGraph.CreateTexture(outputDesc);
        }

        private CameraContext GetCameraContext(ulong cameraId)
        {
            if (_cameraContexts.TryGetValue(cameraId, out CameraContext cameraContext))
                return cameraContext;

            cameraContext = new CameraContext();
            _cameraContexts.Add(cameraId, cameraContext);
            return cameraContext;
        }

        private ResolutionContext GetCurrentResolutionContext()
        {
            ulong cameraId = TsukuyomiUpscaling.CurrentCameraId;
            if (cameraId == 0UL)
                cameraId = ulong.MaxValue;

            if (_resolutionContexts.TryGetValue(cameraId, out ResolutionContext resolutionContext))
                return resolutionContext;

            resolutionContext = new ResolutionContext();
            _resolutionContexts.Add(cameraId, resolutionContext);
            return resolutionContext;
        }

        private static bool EnsureContext(
            CameraContext cameraContext,
            Vector2Int displaySize,
            Vector2Int maxRenderSize,
            bool isHdr,
            bool autoExposure,
            Fsr3Upscaler.QualityMode qualityMode,
            TsukuyomiFsr3Shaders shaders)
        {
            if (cameraContext.Context != null &&
                cameraContext.DisplaySize == displaySize &&
                cameraContext.MaxRenderSize == maxRenderSize &&
                cameraContext.Hdr == isHdr &&
                cameraContext.AutoExposure == autoExposure &&
                cameraContext.QualityMode == qualityMode)
            {
                return false;
            }

            DestroyContext(cameraContext);

            Fsr3Upscaler.InitializationFlags flags = 0;
            if (isHdr)
                flags |= Fsr3Upscaler.InitializationFlags.EnableHighDynamicRange;
            if (autoExposure)
                flags |= Fsr3Upscaler.InitializationFlags.EnableAutoExposure;

            cameraContext.Context = Fsr3Upscaler.CreateContext(displaySize, maxRenderSize, shaders.ToFsr3Shaders(), flags);
            cameraContext.DisplaySize = displaySize;
            cameraContext.MaxRenderSize = maxRenderSize;
            cameraContext.Hdr = isHdr;
            cameraContext.AutoExposure = autoExposure;
            cameraContext.QualityMode = qualityMode;
            cameraContext.LastFrame = -1;
            return true;
        }

        private static void DestroyContext(CameraContext cameraContext)
        {
            if (cameraContext.Context == null)
                return;

            var old = cameraContext.Context;
            TsukuyomiUpscaling.RetireResources(old.Destroy);
            cameraContext.Context = null;
        }

        private void DestroyAllContexts()
        {
            foreach (CameraContext cameraContext in _cameraContexts.Values)
                DestroyContext(cameraContext);

            _cameraContexts.Clear();
            _resolutionContexts.Clear();
            _loggedTaaConflictCameras.Clear();
        }

        private sealed class CameraContext
        {
            public Fsr3UpscalerContext Context;
            public Vector2Int DisplaySize;
            public Vector2Int MaxRenderSize;
            public bool AutoExposure;
            public bool Hdr;
            public Fsr3Upscaler.QualityMode QualityMode;
            public int LastFrame = -1;
        }

#if TSUKUYOMI_UPSCALER_API_6000_6
        private sealed class Fsr3FrameworkContext : IUpscalerContext
        {
            public Vector2Int createdForDisplayResolution { get; }
            public int lastUsedFrame { get; set; }

            public Fsr3FrameworkContext(Vector2Int displayResolution)
            {
                createdForDisplayResolution = new Vector2Int(
                    Mathf.Max(1, displayResolution.x),
                    Mathf.Max(1, displayResolution.y));
            }

            public bool IsValidForOptions(UpscalerOptions options) => true;

            public void Cleanup(CommandBuffer cmd)
            {
                // FSR3 resources are owned and released by TsukuyomiFsr3Upscaler.
            }
        }
#endif

        private sealed class ResolutionContext
        {
            public Vector2Int PreUpscaleResolution = Vector2Int.one;
            public Vector2Int PostUpscaleResolution = Vector2Int.one;

            public void Set(Vector2Int preUpscaleResolution, Vector2Int postUpscaleResolution)
            {
                PreUpscaleResolution = new Vector2Int(Mathf.Max(1, preUpscaleResolution.x), Mathf.Max(1, preUpscaleResolution.y));
                PostUpscaleResolution = new Vector2Int(Mathf.Max(1, postUpscaleResolution.x), Mathf.Max(1, postUpscaleResolution.y));
            }
        }

        private sealed class PassData
        {
            public Fsr3UpscalerContext Context;
            public TextureHandle Color;
            public TextureHandle Depth;
            public TextureHandle MotionVectors;
            public TextureHandle ColorOpaqueOnly;
            public TextureHandle ReactiveMask;
            public TextureHandle CompositionMask;
            public TextureHandle Output;
            public Vector2 JitterOffset;
            public Vector2 MotionVectorScale;
            public float PreExposure;
            public Vector2Int RenderSize;
            public Vector2Int DisplaySize;
            public bool ResetAccumulation;
            public bool EnableSharpening;
            public float Sharpness;
            public float FrameTimeDelta;
            public float CameraNear;
            public float CameraFar;
            public float CameraFovAngleVertical;
            public float VelocityFactor;
            public Fsr3Upscaler.DispatchFlags Flags;
            public bool EnableAutoReactive;
            public float AutoTcThreshold;
            public float AutoTcScale;
            public float AutoReactiveScale;
            public float AutoReactiveMax;
        }
    }

}
#endif
