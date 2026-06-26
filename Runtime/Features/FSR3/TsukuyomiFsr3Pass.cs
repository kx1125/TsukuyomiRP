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

        private static readonly MethodInfo SetViewProjectionAndJitterMatrixMethod = typeof(CameraData).GetMethod("SetViewProjectionAndJitterMatrix", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo GetProjectionMatrixNoJitterMethod = typeof(CameraData).GetMethod("GetProjectionMatrixNoJitter", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly Fsr3Upscaler.DispatchDescription _dispatchDescription = new();
        private Fsr3UpscalerContext _context;
        private Vector2Int _contextDisplaySize;
        private Vector2Int _contextMaxRenderSize;
        private bool _contextAutoExposure;
        private bool _contextHdr;
        private Fsr3Upscaler.QualityMode _contextQualityMode;
        private TsukuyomiFsr3Settings _settings;
        private TsukuyomiFsr3Shaders _shaders;
        private bool _missingResourcesLogged;
        private Vector2 _preparedJitterOffset;
        private int _preparedJitterFrame = -1;
        private CameraData _jitteredCameraData;
        private Camera _jitteredCamera;
        private Matrix4x4 _originalViewMatrix;
        private Matrix4x4 _originalProjectionMatrix;
        private Matrix4x4 _originalCameraProjectionMatrix;
        private Matrix4x4 _originalNonJitteredProjectionMatrix;
        private bool _originalUseJitteredProjectionForTransparentRendering;
        private bool _cameraJitterApplied;

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

        public void PrepareCameraJitter(ref RenderingData renderingData)
        {
            RestoreCameraJitter();

            if (_settings == null || !_settings.Enabled || _shaders == null || !_shaders.IsValid)
                return;

            if (renderingData.cameraData.isPreviewCamera || renderingData.cameraData.xr.enabled)
                return;

            Camera camera = renderingData.cameraData.camera;
            if (camera == null)
                return;

            RenderTextureDescriptor cameraDescriptor = renderingData.cameraData.cameraTargetDescriptor;
            int renderWidth = Mathf.Max(1, cameraDescriptor.width);
            int renderHeight = Mathf.Max(1, cameraDescriptor.height);
            int targetWidth = renderingData.cameraData.targetTexture != null ? renderingData.cameraData.targetTexture.width : camera.pixelWidth;
            int displayWidth = Mathf.Max(renderWidth, targetWidth);

            int jitterPhaseCount = Fsr3Upscaler.GetJitterPhaseCount(renderWidth, displayWidth);
            Fsr3Upscaler.GetJitterOffset(out float jitterX, out float jitterY, Time.frameCount, jitterPhaseCount);
            _preparedJitterOffset = new Vector2(jitterX, jitterY);
            _preparedJitterFrame = Time.frameCount;

            CameraData cameraData = renderingData.cameraData;
            _jitteredCameraData = cameraData;
            _jitteredCamera = camera;
            _originalViewMatrix = cameraData.GetViewMatrix();
            _originalProjectionMatrix = GetProjectionMatrixNoJitter(cameraData, camera.projectionMatrix);
            _originalCameraProjectionMatrix = camera.projectionMatrix;
            _originalNonJitteredProjectionMatrix = camera.nonJitteredProjectionMatrix;
            _originalUseJitteredProjectionForTransparentRendering = camera.useJitteredProjectionMatrixForTransparentRendering;
            _cameraJitterApplied = true;

            float projectionJitterX = 2.0f * jitterX / renderWidth;
            float projectionJitterY = 2.0f * jitterY / renderHeight;
            Matrix4x4 jitterTranslationMatrix = Matrix4x4.Translate(new Vector3(projectionJitterX, projectionJitterY, 0.0f));
            SetViewProjectionAndJitterMatrix(cameraData, _originalViewMatrix, _originalProjectionMatrix, jitterTranslationMatrix);

            camera.nonJitteredProjectionMatrix = _originalProjectionMatrix;
            camera.projectionMatrix = jitterTranslationMatrix * _originalProjectionMatrix;
            camera.useJitteredProjectionMatrixForTransparentRendering = true;
        }

        public void Dispose()
        {
            RestoreCameraJitter();
            DestroyContext();
        }

        public override void Record(in UnsafePassContext context)
        {
            if (_settings == null || !_settings.Enabled || _shaders == null || !_shaders.IsValid)
            {
                RestoreCameraJitter();
                return;
            }

            if (context.CameraData.isPreviewCamera || context.CameraData.xr.enabled)
            {
                RestoreCameraJitter();
                return;
            }

            TextureHandle cameraColor = context.GetTexture(color);
            TextureHandle cameraDepth = context.GetTexture(depth);
            TextureHandle motionVectorColor = context.GetTexture(motionVectors);
            if (!cameraColor.IsValid() || !cameraDepth.IsValid() || !motionVectorColor.IsValid())
            {
                RestoreCameraJitter();
                return;
            }

            RenderTextureDescriptor cameraDescriptor = context.CameraData.cameraTargetDescriptor;
            Camera camera = context.CameraData.camera;
            int renderWidth = Mathf.Max(1, cameraDescriptor.width);
            int renderHeight = Mathf.Max(1, cameraDescriptor.height);
            int targetWidth = context.CameraData.targetTexture != null ? context.CameraData.targetTexture.width : camera != null ? camera.pixelWidth : renderWidth;
            int targetHeight = context.CameraData.targetTexture != null ? context.CameraData.targetTexture.height : camera != null ? camera.pixelHeight : renderHeight;
            int displayWidth = Mathf.Max(renderWidth, targetWidth);
            int displayHeight = Mathf.Max(renderHeight, targetHeight);
            Vector2Int renderSize = new(renderWidth, renderHeight);
            Vector2Int displaySize = new(displayWidth, displayHeight);
            Vector2 jitterOffset = _preparedJitterFrame == Time.frameCount ? _preparedJitterOffset : Vector2.zero;

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
            float fieldOfView = camera != null ? camera.fieldOfView : 60.0f;
            float nearClip = camera != null ? camera.nearClipPlane : 0.1f;
            float farClip = camera != null ? camera.farClipPlane : 1000.0f;
            float deltaTime = Time.unscaledDeltaTime;

            EnsureContext(displaySize, maxRenderSize, isHdr, settings.EnableAutoExposure, settings.QualityMode, shaders);
            Fsr3UpscalerContext fsrContext = _context;

            context.SetRenderFunc((data, graphContext) =>
            {
                if (fsrContext == null)
                {
                    RestoreCameraJitter();
                    return;
                }

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
                _dispatchDescription.Reset = false;
                _dispatchDescription.CameraNear = nearClip;
                _dispatchDescription.CameraFar = farClip;
                _dispatchDescription.CameraFovAngleVertical = fieldOfView * Mathf.Deg2Rad;
                _dispatchDescription.ViewSpaceToMetersFactor = 1.0f;
                _dispatchDescription.VelocityFactor = settings.VelocityFactor;
                _dispatchDescription.Flags = settings.EnableDebugView ? Fsr3Upscaler.DispatchFlags.DrawDebugView : 0;
                _dispatchDescription.EnableAutoReactive = false;

                if (SystemInfo.usesReversedZBuffer)
                    (_dispatchDescription.CameraNear, _dispatchDescription.CameraFar) = (_dispatchDescription.CameraFar, _dispatchDescription.CameraNear);

                try
                {
                    fsrContext.Dispatch(_dispatchDescription, CommandBufferHelpers.GetNativeCommandBuffer(graphContext.cmd));
                }
                finally
                {
                    RestoreCameraJitter();
                }
            });

            PassRecorder.SwapActiveColor(context.Resources, output);
        }

        private void EnsureContext(
            Vector2Int displaySize,
            Vector2Int maxRenderSize,
            bool isHdr,
            bool autoExposure,
            Fsr3Upscaler.QualityMode qualityMode,
            TsukuyomiFsr3Shaders shaders)
        {
            if (_context != null &&
                _contextDisplaySize == displaySize &&
                _contextMaxRenderSize == maxRenderSize &&
                _contextHdr == isHdr &&
                _contextAutoExposure == autoExposure &&
                _contextQualityMode == qualityMode)
            {
                return;
            }

            DestroyContext();

            Fsr3Upscaler.InitializationFlags flags = 0;
            if (isHdr)
                flags |= Fsr3Upscaler.InitializationFlags.EnableHighDynamicRange;
            if (autoExposure)
                flags |= Fsr3Upscaler.InitializationFlags.EnableAutoExposure;

            _context = Fsr3Upscaler.CreateContext(displaySize, maxRenderSize, shaders.ToFsr3Shaders(), flags);
            _contextDisplaySize = displaySize;
            _contextMaxRenderSize = maxRenderSize;
            _contextHdr = isHdr;
            _contextAutoExposure = autoExposure;
            _contextQualityMode = qualityMode;
        }

        private static Matrix4x4 GetProjectionMatrixNoJitter(CameraData cameraData, Matrix4x4 fallbackProjectionMatrix)
        {
            if (GetProjectionMatrixNoJitterMethod == null)
                return fallbackProjectionMatrix;

            object boxedCameraData = cameraData;
            return (Matrix4x4)GetProjectionMatrixNoJitterMethod.Invoke(boxedCameraData, new object[] { 0 });
        }

        private static void SetViewProjectionAndJitterMatrix(CameraData cameraData, Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix, Matrix4x4 jitterMatrix)
        {
            if (SetViewProjectionAndJitterMatrixMethod == null)
                return;

            object boxedCameraData = cameraData;
            SetViewProjectionAndJitterMatrixMethod.Invoke(boxedCameraData, new object[] { viewMatrix, projectionMatrix, jitterMatrix });
        }

        private void RestoreCameraJitter()
        {
            if (!_cameraJitterApplied)
                return;
            SetViewProjectionAndJitterMatrix(_jitteredCameraData, _originalViewMatrix, _originalProjectionMatrix, Matrix4x4.identity);

            if (_jitteredCamera != null)
            {
                _jitteredCamera.projectionMatrix = _originalCameraProjectionMatrix;
                _jitteredCamera.nonJitteredProjectionMatrix = _originalNonJitteredProjectionMatrix;
                _jitteredCamera.useJitteredProjectionMatrixForTransparentRendering = _originalUseJitteredProjectionForTransparentRendering;
            }

            _cameraJitterApplied = false;
            _jitteredCameraData = default;
            _jitteredCamera = null;
            _preparedJitterOffset = Vector2.zero;
            _preparedJitterFrame = -1;
        }

        private void DestroyContext()
        {
            if (_context == null)
                return;

            _context.Destroy();
            _context = null;
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
    }
}





