using System.Collections.Generic;
using System.Reflection;
using Tsukuyomi.Rendering.FSR3;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering
{
    [System.Serializable]
    internal sealed class TsukuyomiFsr3Pass : UnsafePass
    {
        [Read(BuiltinTexture.ActiveColor)]
        public TextureSlot color = TextureSlot.Read("Color", BuiltinTexture.ActiveColor);

        [Read(BuiltinTexture.CameraDepthTexture)]
        public TextureSlot depth = TextureSlot.Read("Depth", BuiltinTexture.CameraDepthTexture);

        [Read(BuiltinTexture.MotionVectorColor)]
        public TextureSlot motionVectors = TextureSlot.Read("MotionVectors", BuiltinTexture.MotionVectorColor);

        private static readonly PropertyInfo ResetHistoryProperty = typeof(UniversalCameraData).GetProperty("resetHistory", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly int ScreenParamsId = Shader.PropertyToID("_ScreenParams");

        private readonly Fsr3Upscaler.DispatchDescription _dispatchDescription = new();

        // TODO: Move FSR3 persistent data into a Tsukuyomi camera history system if the pipeline grows more camera-history users.
        private readonly Dictionary<ulong, CameraContext> _cameraContexts = new();

        private TsukuyomiFsr3Settings _settings;
        private TsukuyomiFsr3Shaders _shaders;
        private bool _missingResourcesLogged;

        public override string Name => "FSR3 Upscaler";

        public bool Configure(TsukuyomiRenderPipelineResources resources)
        {
            _settings = resources != null ? resources.Fsr3Settings : null;
            _shaders = resources != null ? resources.Fsr3Shaders : null;

            if (_settings == null || !_settings.Enabled)
                return false;

            if (_shaders == null || !_shaders.IsValid)
            {
                if (!_missingResourcesLogged)
                {
                    Debug.LogError("Tsukuyomi FSR3 requires all FSR3 compute shaders in Project Settings > Tsukuyomi RP > FSR3 Upscaler.");
                    _missingResourcesLogged = true;
                }

                return false;
            }

            return SystemInfo.supportsComputeShaders;
        }

        public override bool IsActive(in FrameContext frame)
        {
            return base.IsActive(frame) && _settings != null && _settings.Enabled && _shaders != null && _shaders.IsValid;
        }

        public void SetJitter(ulong cameraId, Vector2 jitterOffset, int jitterFrame)
        {
            CameraContext cameraContext = GetCameraContext(cameraId);
            cameraContext.JitterOffset = jitterOffset;
            cameraContext.JitterFrame = jitterFrame;
        }

        public void Dispose()
        {
            DestroyAllContexts();
        }

        public override void Record(in UnsafePassContext context)
        {
            if (_settings == null || !_settings.Enabled || _shaders == null || !_shaders.IsValid)
                return;

            if (!TsukuyomiFsr3Validation.IsSupportedCamera(context.CameraData))
                return;

            TextureHandle cameraColor = context.GetTexture(color);
            TextureHandle cameraDepth = context.GetTexture(depth);
            TextureHandle motionVectorColor = context.GetTexture(motionVectors);
            if (!cameraColor.IsValid() || !cameraDepth.IsValid() || !motionVectorColor.IsValid())
                return;

            Camera camera = context.CameraData.camera;
            ulong cameraId = EntityId.ToULong(camera.GetEntityId());
            RenderTextureDescriptor cameraDescriptor = context.CameraData.cameraTargetDescriptor;
            int renderWidth = Mathf.Max(1, cameraDescriptor.width);
            int renderHeight = Mathf.Max(1, cameraDescriptor.height);
            int targetWidth = context.CameraData.targetTexture != null ? context.CameraData.targetTexture.width : camera.pixelWidth;
            int targetHeight = context.CameraData.targetTexture != null ? context.CameraData.targetTexture.height : camera.pixelHeight;
            int displayWidth = Mathf.Max(renderWidth, targetWidth);
            int displayHeight = Mathf.Max(renderHeight, targetHeight);
            Vector2Int renderSize = new(renderWidth, renderHeight);
            Vector2Int displaySize = new(displayWidth, displayHeight);

            Vector2 jitterOffset = GetJitter(cameraId);

            Fsr3Upscaler.GetRenderResolutionFromQualityMode(
                out int qualityWidth,
                out int qualityHeight,
                displayWidth,
                displayHeight,
                _settings.QualityMode);
            Vector2Int maxRenderSize = new(Mathf.Max(renderWidth, qualityWidth), Mathf.Max(renderHeight, qualityHeight));

            TextureHandle output = context.RenderGraph.CreateTexture(CreateOutputDesc(cameraDescriptor, displaySize));

            context.Builder.UseTexture(cameraColor, AccessFlags.Read);
            context.Builder.UseTexture(cameraDepth, AccessFlags.Read);
            context.Builder.UseTexture(motionVectorColor, AccessFlags.Read);
            context.Builder.UseTexture(output, AccessFlags.ReadWrite);
            context.Builder.AllowGlobalStateModification(true);

            TsukuyomiFsr3Settings settings = _settings;
            TsukuyomiFsr3Shaders shaders = _shaders;
            bool isHdr = GraphicsFormatUtility.IsHDRFormat(cameraDescriptor.graphicsFormat);
            float fieldOfView = camera.fieldOfView;
            float nearClip = camera.nearClipPlane;
            float farClip = camera.farClipPlane;
            float deltaTime = Time.unscaledDeltaTime;
            int frameCount = Time.frameCount;

            CameraContext cameraContext = GetCameraContext(cameraId);
            bool contextRecreated = EnsureContext(cameraContext, displaySize, maxRenderSize, isHdr, settings.EnableAutoExposure, settings.QualityMode, shaders);
            bool resetAccumulation = contextRecreated || ShouldReset(cameraContext, context.CameraData, frameCount);
            cameraContext.LastFrame = frameCount;
            Fsr3UpscalerContext fsrContext = cameraContext.Context;

            context.SetRenderFunc((data, graphContext) =>
            {
                if (fsrContext == null)
                    return;

                _dispatchDescription.Color = new ResourceView(cameraColor, RenderTextureSubElement.Color);
                _dispatchDescription.Depth = new ResourceView(cameraDepth, RenderTextureSubElement.Depth);
                _dispatchDescription.MotionVectors = new ResourceView(motionVectorColor, RenderTextureSubElement.Color);
                _dispatchDescription.Exposure = ResourceView.Unassigned;
                _dispatchDescription.Reactive = ResourceView.Unassigned;
                _dispatchDescription.TransparencyAndComposition = ResourceView.Unassigned;
                _dispatchDescription.Output = new ResourceView(output, RenderTextureSubElement.Color);
                _dispatchDescription.JitterOffset = jitterOffset;
                _dispatchDescription.MotionVectorScale = new Vector2(-renderSize.x, -renderSize.y);
                _dispatchDescription.RenderSize = renderSize;
                _dispatchDescription.UpscaleSize = displaySize;
                _dispatchDescription.EnableSharpening = settings.PerformSharpenPass;
                _dispatchDescription.Sharpness = settings.Sharpness;
                _dispatchDescription.FrameTimeDelta = deltaTime;
                _dispatchDescription.PreExposure = 1.0f;
                _dispatchDescription.Reset = resetAccumulation;
                _dispatchDescription.CameraNear = nearClip;
                _dispatchDescription.CameraFar = farClip;
                _dispatchDescription.CameraFovAngleVertical = fieldOfView * Mathf.Deg2Rad;
                _dispatchDescription.ViewSpaceToMetersFactor = 1.0f;
                _dispatchDescription.VelocityFactor = settings.VelocityFactor;
                _dispatchDescription.Flags = settings.EnableDebugView ? Fsr3Upscaler.DispatchFlags.DrawDebugView : 0;
                _dispatchDescription.EnableAutoReactive = false;

                if (SystemInfo.usesReversedZBuffer)
                    (_dispatchDescription.CameraNear, _dispatchDescription.CameraFar) = (_dispatchDescription.CameraFar, _dispatchDescription.CameraNear);

                fsrContext.Dispatch(_dispatchDescription, CommandBufferHelpers.GetNativeCommandBuffer(graphContext.cmd));
                graphContext.cmd.SetGlobalVector(
                    ScreenParamsId,
                    new Vector4(
                        displaySize.x,
                        displaySize.y,
                        1.0f / displaySize.x,
                        1.0f / displaySize.y));
            });

            PassRecorder.SwapActiveColor(context.Resources, output);
            UpdateCameraResolution(context.CameraData, displaySize);
        }

        private Vector2 GetJitter(ulong cameraId)
        {
            if (_cameraContexts.TryGetValue(cameraId, out CameraContext cameraContext) && cameraContext.JitterFrame == Time.frameCount)
                return cameraContext.JitterOffset;

            return Vector2.zero;
        }

        private CameraContext GetCameraContext(ulong cameraId)
        {
            if (_cameraContexts.TryGetValue(cameraId, out CameraContext cameraContext))
                return cameraContext;

            cameraContext = new CameraContext();
            _cameraContexts.Add(cameraId, cameraContext);
            return cameraContext;
        }

        private bool EnsureContext(
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

        private static bool ShouldReset(CameraContext cameraContext, UniversalCameraData cameraData, int frameCount)
        {
            if (cameraContext.LastFrame < 0)
                return true;

            if (cameraContext.LastFrame != frameCount - 1)
                return true;

            return IsHistoryResetRequested(cameraData);
        }

        private static bool IsHistoryResetRequested(UniversalCameraData cameraData)
        {
            if (ResetHistoryProperty == null)
                return false;

            return (bool)ResetHistoryProperty.GetValue(cameraData);
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
        }

        private static TextureDesc CreateOutputDesc(RenderTextureDescriptor cameraDescriptor, Vector2Int displaySize)
        {
            GraphicsFormat format = cameraDescriptor.graphicsFormat;
            if (format == GraphicsFormat.None)
                format = GraphicsFormat.R16G16B16A16_SFloat;

            return new TextureDesc(displaySize.x, displaySize.y)
            {
                name = "_TsukuyomiFsr3UpscaledColor",
                colorFormat = format,
                depthBufferBits = DepthBits.None,
                msaaSamples = MSAASamples.None,
                clearBuffer = false,
                enableRandomWrite = true,
                filterMode = FilterMode.Bilinear
            };
        }

        private static void UpdateCameraResolution(UniversalCameraData cameraData, Vector2Int displaySize)
        {
            // TODO: If FSR3 moves to URP IUpscaler, use URP's native upscaler resolution update path instead.
            cameraData.cameraTargetDescriptor.width = displaySize.x;
            cameraData.cameraTargetDescriptor.height = displaySize.y;
        }

        private sealed class CameraContext
        {
            public Fsr3UpscalerContext Context;
            public Vector2Int DisplaySize;
            public Vector2Int MaxRenderSize;
            public bool AutoExposure;
            public bool Hdr;
            public Fsr3Upscaler.QualityMode QualityMode;
            public Vector2 JitterOffset;
            public int JitterFrame = -1;
            public int LastFrame = -1;
        }
    }
}


