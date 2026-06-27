#if ENABLE_UPSCALER_FRAMEWORK
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

        public TsukuyomiFsr3Upscaler()
        {
            Instances.Add(this);
        }

        public override string name => UpscalerName;
        public override bool isTemporal => true;
        public override bool supportsSharpening => true;
        public override bool supportsXR => false;

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

        public override void NegotiatePreUpscaleResolution(ref Vector2Int preUpscaleResolution, Vector2Int postUpscaleResolution)
        {
            if (!TryGetValidResources(out TsukuyomiRenderPipelineResources resources))
                return;

            TsukuyomiFsr3Settings settings = TsukuyomiRenderPipelineProjectSettings.Current.Fsr3Settings;
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
                TsukuyomiRenderPipelineProjectSettings.Current.Fsr3Settings.EnableAutoExposure,
                TsukuyomiRenderPipelineProjectSettings.Current.Fsr3Settings.QualityMode,
                resources.Fsr3Shaders);

            bool resetAccumulation = contextRecreated || io.resetHistory || cameraContext.LastFrame != io.frameIndex - 1;
            cameraContext.LastFrame = io.frameIndex;
            Fsr3UpscalerContext fsrContext = cameraContext.Context;
            TsukuyomiFsr3Settings settings = TsukuyomiRenderPipelineProjectSettings.Current.Fsr3Settings;
            Vector2 jitterOffset = CalculateJitter(io.frameIndex, io.preUpscaleResolution, io.postUpscaleResolution);
            Vector2Int renderSize = io.preUpscaleResolution;
            Vector2Int displaySize = io.postUpscaleResolution;
            float fieldOfView = io.fieldOfViewDegrees;
            float nearClip = io.nearClipPlane;
            float farClip = io.farClipPlane;
            float deltaTime = io.deltaTime;

            using (var builder = renderGraph.AddUnsafePass<PassData>("Tsukuyomi FSR3 Upscaler", out PassData passData))
            {
                builder.UseTexture(io.cameraColor, AccessFlags.Read);
                builder.UseTexture(io.cameraDepth, AccessFlags.Read);
                builder.UseTexture(io.motionVectorColor, AccessFlags.Read);
                builder.UseTexture(output, AccessFlags.ReadWrite);
                builder.AllowGlobalStateModification(true);

                passData.Context = fsrContext;
                passData.Color = io.cameraColor;
                passData.Depth = io.cameraDepth;
                passData.MotionVectors = io.motionVectorColor;
                passData.Output = output;
                passData.JitterOffset = jitterOffset;
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
                        Reactive = ResourceView.Unassigned,
                        TransparencyAndComposition = ResourceView.Unassigned,
                        Output = new ResourceView(data.Output, RenderTextureSubElement.Color),
                        JitterOffset = data.JitterOffset,
                        MotionVectorScale = new Vector2(-data.RenderSize.x, -data.RenderSize.y),
                        RenderSize = data.RenderSize,
                        UpscaleSize = data.DisplaySize,
                        EnableSharpening = data.EnableSharpening,
                        Sharpness = data.Sharpness,
                        FrameTimeDelta = data.FrameTimeDelta,
                        PreExposure = 1.0f,
                        Reset = data.ResetAccumulation,
                        CameraNear = data.CameraNear,
                        CameraFar = data.CameraFar,
                        CameraFovAngleVertical = data.CameraFovAngleVertical,
                        ViewSpaceToMetersFactor = 1.0f,
                        VelocityFactor = data.VelocityFactor,
                        Flags = data.Flags,
                        EnableAutoReactive = false
                    };

                    if (SystemInfo.usesReversedZBuffer)
                        (dispatchDescription.CameraNear, dispatchDescription.CameraFar) = (dispatchDescription.CameraFar, dispatchDescription.CameraNear);

                    data.Context.Dispatch(dispatchDescription, CommandBufferHelpers.GetNativeCommandBuffer(context.cmd));
                });
            }

            io.cameraColor = output;
        }

        public static void DestroyAllInstances()
        {
            for (int i = 0; i < Instances.Count; i++)
                Instances[i]?.DestroyAllContexts();
        }

        private static bool TryGetValidResources(out TsukuyomiRenderPipelineResources resources)
        {
            if (!TsukuyomiRenderPipelineResourcesProvider.TryGet(out resources))
                return false;

            TsukuyomiFsr3Settings settings = TsukuyomiRenderPipelineProjectSettings.Current.Fsr3Settings;
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
            ulong cameraId = TsukuyomiFsr3UpscalerBootstrap.CurrentCameraId;
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

            cameraContext.Context.Destroy();
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
            public TextureHandle Output;
            public Vector2 JitterOffset;
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
        }
    }

#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    internal static class TsukuyomiFsr3UpscalerBootstrap
    {
        private const string FallbackUpscalerName = "Bilinear";
        private static readonly HashSet<ulong> LoggedTaaConflictCameras = new();
        private static readonly HashSet<ulong> LoggedCameraStackCameras = new();
        private static string s_contextPreviousUpscalerName;
        private static bool s_contextOverrideActive;
        private static bool s_initialized;

        public static ulong CurrentCameraId { get; private set; }

#if UNITY_EDITOR
        static TsukuyomiFsr3UpscalerBootstrap()
        {
            Initialize();
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RuntimeInitialize()
        {
            Initialize();
        }

        private static void Initialize()
        {
            if (s_initialized)
                return;

            UpscalerRegistry.Register<TsukuyomiFsr3Upscaler>(TsukuyomiFsr3Upscaler.UpscalerName);
            RenderPipelineManager.beginContextRendering -= OnBeginContextRendering;
            RenderPipelineManager.beginContextRendering += OnBeginContextRendering;
            RenderPipelineManager.endContextRendering -= OnEndContextRendering;
            RenderPipelineManager.endContextRendering += OnEndContextRendering;
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
#if UNITY_EDITOR
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
#endif
            s_initialized = true;
        }

        private static void OnBeginContextRendering(ScriptableRenderContext context, List<Camera> cameras)
        {
            UniversalRenderPipelineAsset asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (asset == null)
            {
                TsukuyomiFsr3Upscaler.DestroyAllInstances();
                return;
            }

            if (ShouldUseFsr3(cameras))
            {
                s_contextPreviousUpscalerName = asset.upscalerName;
                s_contextOverrideActive = true;
                asset.upscalerName = TsukuyomiFsr3Upscaler.UpscalerName;
            }
            else
            {
                s_contextOverrideActive = false;
                s_contextPreviousUpscalerName = null;
                TsukuyomiFsr3Upscaler.DestroyAllInstances();
            }
        }

        private static void OnEndContextRendering(ScriptableRenderContext context, List<Camera> cameras)
        {
            UniversalRenderPipelineAsset asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (asset != null && s_contextOverrideActive)
            {
                asset.upscalerName = string.IsNullOrEmpty(s_contextPreviousUpscalerName)
                    ? FallbackUpscalerName
                    : s_contextPreviousUpscalerName;
            }

            s_contextOverrideActive = false;
            s_contextPreviousUpscalerName = null;
            CurrentCameraId = 0UL;
        }

        private static void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            CurrentCameraId = camera != null ? EntityId.ToULong(camera.GetEntityId()) : 0UL;
        }

        private static void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            CurrentCameraId = 0UL;
        }

#if UNITY_EDITOR
        private static void OnBeforeAssemblyReload()
        {
            TsukuyomiFsr3Upscaler.DestroyAllInstances();
        }
#endif

        private static bool ShouldUseFsr3(List<Camera> cameras)
        {
            TsukuyomiFsr3Settings settings = TsukuyomiRenderPipelineProjectSettings.Current.Fsr3Settings;
            if (settings == null ||
                !settings.Enabled ||
                !TsukuyomiRenderPipelineResourcesProvider.TryGet(out TsukuyomiRenderPipelineResources resources) ||
                resources.Fsr3Shaders == null ||
                !resources.Fsr3Shaders.IsValid ||
                !SystemInfo.supportsComputeShaders)
            {
                return false;
            }

            if (cameras == null)
                return true;

            bool hasSupportedGameCamera = false;
            for (int i = 0; i < cameras.Count; i++)
            {
                Camera camera = cameras[i];
                if (camera == null || camera.cameraType == CameraType.Preview)
                    continue;

                if (!camera.TryGetComponent(out UniversalAdditionalCameraData additionalCameraData))
                {
                    if (camera.cameraType == CameraType.Game)
                        hasSupportedGameCamera = true;

                    continue;
                }

                ulong cameraId = EntityId.ToULong(camera.GetEntityId());
                if (additionalCameraData.antialiasing == AntialiasingMode.TemporalAntiAliasing)
                {
                    if (LoggedTaaConflictCameras.Add(cameraId))
                    {
                        Debug.LogError($"Tsukuyomi FSR3 is enabled on camera '{camera.name}', but Unity Temporal Anti-Aliasing is also enabled. Disable TAA in the camera's Universal Additional Camera Data before using Tsukuyomi FSR3.", camera);
                    }

                    return false;
                }

                LoggedTaaConflictCameras.Remove(cameraId);

                if (additionalCameraData.cameraStack != null && additionalCameraData.cameraStack.Count > 0)
                {
                    if (LoggedCameraStackCameras.Add(cameraId))
                    {
                        Debug.LogWarning($"Tsukuyomi FSR3 is skipped because camera '{camera.name}' uses a camera stack, which is not supported by the current FSR3 integration.", camera);
                    }

                    return false;
                }

                LoggedCameraStackCameras.Remove(cameraId);

                if (camera.cameraType == CameraType.Game && additionalCameraData.renderType == CameraRenderType.Base)
                    hasSupportedGameCamera = true;
            }

            // URP's IUpscaler path does not reliably execute for Scene View at 100% render scale.
            // Keep this integration runtime/GameView-only until a dedicated editor preview path exists.
            return hasSupportedGameCamera;
        }
    }
}
#endif
