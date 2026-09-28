using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering
{
    [System.Serializable]
    internal sealed class TsukuyomiScreenSpaceGlobalIlluminationPass : UnsafePass
    {
        private const int TileSize = 8;
        private const string KeywordName = "_TSUKUYOMI_SCREEN_SPACE_GLOBAL_ILLUMINATION";

        private static readonly int CameraDepthTextureId = Shader.PropertyToID("_CameraDepthTexture");
        private static readonly int CameraNormalsTextureId = Shader.PropertyToID("_CameraNormalsTexture");
        private static readonly int MotionVectorTextureId = Shader.PropertyToID("_MotionVectorTexture");
        private static readonly int DepthPyramidId = Shader.PropertyToID("_DepthPyramid");
        private static readonly int DepthPyramidMipLevelOffsetsId = Shader.PropertyToID("_DepthPyramidMipLevelOffsets");
        private static readonly int HistoryColorTextureId = Shader.PropertyToID("_HistoryColorTexture");
        private static readonly int HistoryColorHalfTextureId = Shader.PropertyToID("_HistoryColorHalfTexture");
        private static readonly int HistoryColorHalfTextureRwId = Shader.PropertyToID("_HistoryColorHalfTextureRW");
        private static readonly int HistoryDepthTextureId = Shader.PropertyToID("_HistoryDepthTexture");
        private static readonly int HistoryNormalTextureId = Shader.PropertyToID("_HistoryNormalTexture");
        private static readonly int OutputNormalHistoryRwId = Shader.PropertyToID("_OutputNormalHistoryRW");
        private static readonly int HitPointTextureId = Shader.PropertyToID("_IndirectDiffuseHitPointTexture");
        private static readonly int HitPointTextureRwId = Shader.PropertyToID("_IndirectDiffuseHitPointTextureRW");
        private static readonly int IndirectDiffuseTextureRwId = Shader.PropertyToID("_IndirectDiffuseTextureRW");
        private static readonly int InputIndirectDiffuseTextureId = Shader.PropertyToID("_InputIndirectDiffuseTexture");
        private static readonly int HistoryIndirectDiffuseTextureId = Shader.PropertyToID("_HistoryIndirectDiffuseTexture");
        private static readonly int OutputIndirectDiffuseHistoryRwId = Shader.PropertyToID("_OutputIndirectDiffuseHistoryRW");
        private static readonly int OutputIndirectDiffuseTextureRwId = Shader.PropertyToID("_OutputIndirectDiffuseTextureRW");
        private static readonly int HistoryValidationTextureId = Shader.PropertyToID("_HistoryValidationTexture");
        private static readonly int HistoryValidationTextureRwId = Shader.PropertyToID("_HistoryValidationTextureRW");
        private static readonly int FullResolutionId = Shader.PropertyToID("_FullResolution");
        private static readonly int OutputResolutionId = Shader.PropertyToID("_OutputResolution");
        private static readonly int InputResolutionId = Shader.PropertyToID("_InputResolution");
        private static readonly int SignalResolutionId = Shader.PropertyToID("_SignalResolution");
        private static readonly int ViewProjectionId = Shader.PropertyToID("_SsgiViewProjection");
        private static readonly int InverseViewProjectionId = Shader.PropertyToID("_SsgiInverseViewProjection");
        private static readonly int PreviousInverseViewProjectionId = Shader.PropertyToID("_SsgiPreviousInverseViewProjection");
        private static readonly int JitterDeltaId = Shader.PropertyToID("_SsgiJitterDelta");
        private static readonly int AmbientProbeDataId = Shader.PropertyToID("_SsgiAmbientProbeData");
        private static readonly int RayMarchingStepsId = Shader.PropertyToID("_RayMarchingSteps");
        private static readonly int RayMarchingThicknessScaleId = Shader.PropertyToID("_RayMarchingThicknessScale");
        private static readonly int RayMarchingThicknessBiasId = Shader.PropertyToID("_RayMarchingThicknessBias");
        private static readonly int RayMarchingReflectsSkyId = Shader.PropertyToID("_RayMarchingReflectsSky");
        private static readonly int RayMarchingFallbackHierarchyId = Shader.PropertyToID("_RayMarchingFallbackHierarchy");
        private static readonly int IndirectDiffuseFrameIndexId = Shader.PropertyToID("_IndirectDiffuseFrameIndex");
        private static readonly int EnableProbeVolumesId = Shader.PropertyToID("_SsgiEnableProbeVolumes");
        private static readonly int HistoryValidId = Shader.PropertyToID("_HistoryValid");
        private static readonly int DenoiserRadiusId = Shader.PropertyToID("_DenoiserRadius");
        private static readonly int GlobalTextureId = Shader.PropertyToID("_TsukuyomiScreenSpaceGlobalIlluminationTexture");

        private static GlobalKeyword s_GlobalKeyword;
        private static bool s_KeywordInitialized;

        [Read(BuiltinTexture.CameraDepthTexture)]
        public TextureSlot depth = TextureSlot.Read("Depth", BuiltinTexture.CameraDepthTexture);

        [Read(BuiltinTexture.CameraNormals)]
        public TextureSlot normals = TextureSlot.Read("Normals", BuiltinTexture.CameraNormals);

        [Read(BuiltinTexture.MotionVectorColor)]
        public TextureSlot motionVectors = TextureSlot.Read("Motion Vectors", BuiltinTexture.MotionVectorColor);

        private static readonly ProfilingSampler _profilingSampler = new("Tsukuyomi Screen Space Global Illumination");
        private TsukuyomiPipelineProfile _profile;
        private TsukuyomiScreenSpaceGlobalIlluminationResolvedSettings _settings;
        private ComputeShader _traceCompute;
        private ComputeShader _temporalCompute;
        private ComputeShader _denoiserCompute;
        private ComputeShader _upsampleCompute;
        private int _downsampleHistoryKernel = -1;
        private int _traceKernel = -1;
        private int _traceHalfKernel = -1;
        private int _reprojectKernel = -1;
        private int _reprojectHalfKernel = -1;
        private int _copyNormalsKernel = -1;
        private int _validateHistoryKernel = -1;
        private int _temporalKernel = -1;
        private int _spatialKernel = -1;
        private int _upsampleKernel = -1;

        public override void CollectTextureSlots(System.Collections.Generic.List<TextureSlot> slots)
        {
            slots.Add(depth);
            slots.Add(normals);
            slots.Add(motionVectors);
        }

        public override string Name => "Screen Space Global Illumination";
        internal static GlobalKeyword GlobalKeyword => s_GlobalKeyword;
        internal bool DebugOutput => _settings.DebugOutput;

        internal static void InitializeKeyword()
        {
            if (s_KeywordInitialized)
                return;
            s_GlobalKeyword = UnityEngine.Rendering.GlobalKeyword.Create(KeywordName);
            s_KeywordInitialized = true;
        }

        internal static void ClearGlobals()
        {
            Shader.DisableKeyword(KeywordName);
            Shader.SetGlobalTexture(GlobalTextureId, Texture2D.blackTexture);
        }

        public bool Configure(
            TsukuyomiPipelineProfile profile,
            TsukuyomiScreenSpaceGlobalIlluminationVolume volume,
            ref RenderingData renderingData)
        {
            _profile = profile;
            if (profile == null || !SystemInfo.supportsComputeShaders || !IsSupportedCamera(ref renderingData))
                return false;

            _settings = TsukuyomiScreenSpaceGlobalIlluminationResolvedSettings.From(profile, volume);
            if (!_settings.IsActive)
                return false;

            if (!TsukuyomiRenderPipelineResourcesProvider.TryGet(out TsukuyomiRenderPipelineResources resources))
                return false;

            _traceCompute = resources.SsgiTraceComputeShader;
            _temporalCompute = resources.SsgiTemporalComputeShader;
            _denoiserCompute = resources.SsgiDiffuseDenoiserComputeShader;
            _upsampleCompute = resources.SsgiBilateralUpsampleComputeShader;
            if (_traceCompute == null || _temporalCompute == null || _denoiserCompute == null || _upsampleCompute == null)
            {
                Debug.LogError("Tsukuyomi SSGI requires trace, temporal, diffuse denoiser, and bilateral upsample compute shaders in TsukuyomiRenderPipelineResources.");
                return false;
            }

            _downsampleHistoryKernel = _traceCompute.FindKernel("DownsampleHistoryColor");
            _traceKernel = _traceCompute.FindKernel("TraceGlobalIllumination");
            _traceHalfKernel = _traceCompute.FindKernel("TraceGlobalIlluminationHalf");
            _reprojectKernel = _traceCompute.FindKernel("ReprojectGlobalIllumination");
            _reprojectHalfKernel = _traceCompute.FindKernel("ReprojectGlobalIlluminationHalf");
            _copyNormalsKernel = _temporalCompute.FindKernel("CopyNormals");
            _validateHistoryKernel = _temporalCompute.FindKernel("ValidateHistory");
            _temporalKernel = _temporalCompute.FindKernel("TemporalAccumulation");
            _spatialKernel = _denoiserCompute.FindKernel("SpatialDenoise");
            _upsampleKernel = _upsampleCompute.FindKernel("BilateralUpsample");

            renderingData.cameraData.historyManager?.RequestAccess<RawColorHistory>();
            renderingData.cameraData.historyManager?.RequestAccess<RawDepthHistory>();
            renderingData.cameraData.historyManager?.RequestAccess<TsukuyomiScreenSpaceGlobalIlluminationHistory>();
            return KernelsAreValid();
        }

        public override bool IsActive(in FrameContext frame)
        {
            return base.IsActive(frame) && _profile != null && _settings.IsActive && KernelsAreValid();
        }

        private sealed class RenderData
        {
            public TextureHandle CameraDepth;
            public TextureHandle CameraNormals;
            public TextureHandle MotionVectorTexture;
            public TextureHandle DepthPyramid;
            public BufferHandle DepthPyramidOffsets;
            public int FullWidth;
            public int FullHeight;
            public int TraceWidth;
            public int TraceHeight;
            public Matrix4x4 ViewProjection;
            public Matrix4x4 InverseViewProjection;
            public bool HistoryValid;
            public Matrix4x4 PreviousInverseViewProjection;
            public Vector2 JitterDeltaUv;
            public TextureHandle HistoryColor;
            public TextureHandle HistoryDepth;
            public TextureHandle PreviousNormalHistory;
            public TextureHandle CurrentNormalHistory;
            public TextureHandle PreviousGi0;
            public TextureHandle CurrentGi0;
            public TextureHandle PreviousGi1;
            public TextureHandle CurrentGi1;
            public TextureHandle HistoryColorHalf;
            public TextureHandle HitPoint;
            public TextureHandle RawIndirectDiffuse;
            public TextureHandle HistoryValidation;
            public TextureHandle Spatial0;
            public TextureHandle Spatial1;
            public TextureHandle Signal;
            public int SignalWidth;
            public int SignalHeight;
            public bool RequiresUpsample;
            public TextureHandle FinalIndirectDiffuse;
            public ComputeShader TraceCompute;
            public ComputeShader TemporalCompute;
            public ComputeShader DenoiserCompute;
            public ComputeShader UpsampleCompute;
            public int DownsampleHistoryKernel;
            public int TraceKernel;
            public int ReprojectKernel;
            public int CopyNormalsKernel;
            public int ValidateHistoryKernel;
            public int TemporalKernel;
            public int SpatialKernel;
            public int UpsampleKernel;
            public TsukuyomiScreenSpaceGlobalIlluminationResolvedSettings Settings;
            public Vector4 FullResolution;
            public Vector4 TraceResolution;
            public Vector4 SpatialResolution;
            public Vector4 HalfColorResolution;
            public readonly Vector4[] AmbientProbeData = new Vector4[7];
            public float ThicknessScale;
            public float ThicknessBias;
            public int FrameIndex;
        }

        public override void CollectResourceRequirements(in FrameContext frame, in ResourceRequirementCollector requirements)
        {
            if (!frame.CameraData.isPreviewCamera && frame.CameraData.historyManager != null)
                requirements.RequireDepthPyramid();
        }

        public override void Record(in UnsafePassContext context)
        {
            var passData = context.GetOrCreateData<RenderData>();
            var graphResources = context.GraphResources;
            if (!_settings.IsActive || context.CameraData.historyManager == null)
                return;

            TextureHandle cameraDepth = context.GetTexture(depth);
            TextureHandle cameraNormals = context.GetTexture(normals);
            TextureHandle motionVectorTexture = context.GetTexture(motionVectors);
            if (!cameraDepth.IsValid() || !cameraNormals.IsValid() || !motionVectorTexture.IsValid())
                return;

            RenderTextureDescriptor cameraDescriptor = context.CameraData.cameraTargetDescriptor;
            TextureHandle depthPyramid = context.GetTexture(
                TsukuyomiDepthPyramidResources.CreateDepthPyramidSlot(cameraDescriptor, ResourceAccess.Read));
            BufferHandle depthPyramidOffsets = context.GetBuffer(
                TsukuyomiDepthPyramidResources.CreateDepthPyramidMipLevelOffsetsBufferSlot(ResourceAccess.Read));
            if (!depthPyramid.IsValid() || !depthPyramidOffsets.IsValid())
                return;

            int fullWidth = Mathf.Max(1, cameraDescriptor.width);
            int fullHeight = Mathf.Max(1, cameraDescriptor.height);
            int traceWidth = _settings.HalfResolution ? Mathf.Max(1, (fullWidth + 1) / 2) : fullWidth;
            int traceHeight = _settings.HalfResolution ? Mathf.Max(1, (fullHeight + 1) / 2) : fullHeight;
            bool spatialHalfResolution = !_settings.HalfResolution && _settings.HalfResolutionDenoiser;
            int spatialWidth = spatialHalfResolution ? Mathf.Max(1, (fullWidth + 1) / 2) : traceWidth;
            int spatialHeight = spatialHalfResolution ? Mathf.Max(1, (fullHeight + 1) / 2) : traceHeight;

            Matrix4x4 jitteredGpuProjection = GL.GetGPUProjectionMatrix(context.CameraData.GetProjectionMatrix(), true);
            Matrix4x4 nonJitteredGpuProjection = GL.GetGPUProjectionMatrix(
                context.CameraData.camera.projectionMatrix,
                true);
            Matrix4x4 viewProjection = jitteredGpuProjection * context.CameraData.GetViewMatrix();
            passData.InverseViewProjection = viewProjection.inverse;
            Vector2 currentJitterUv = CalculateJitterUv(jitteredGpuProjection, nonJitteredGpuProjection);
            UniversalCameraHistory historyManager = context.CameraData.historyManager;
            RawColorHistory rawColorHistory = historyManager.GetHistoryForRead<RawColorHistory>();
            RawDepthHistory rawDepthHistory = historyManager.GetHistoryForRead<RawDepthHistory>();
            TsukuyomiScreenSpaceGlobalIlluminationHistory ssgiHistory =
                historyManager.GetHistoryForWrite<TsukuyomiScreenSpaceGlobalIlluminationHistory>();
            if (ssgiHistory == null)
                return;

            RTHandle previousRawColor = rawColorHistory?.GetPreviousTexture();
            RTHandle previousRawDepth = rawDepthHistory?.GetPreviousTexture();
            bool historyValid = ssgiHistory.Update(
                ref cameraDescriptor,
                traceWidth,
                traceHeight,
                spatialWidth,
                spatialHeight,
                _settings.HistorySignature,
                viewProjection,
                currentJitterUv,
                Time.frameCount);
            historyValid &= previousRawColor != null && previousRawDepth != null;
            passData.PreviousInverseViewProjection = ssgiHistory.PreviousViewProjection.inverse;
            passData.JitterDeltaUv = ssgiHistory.PreviousJitterUv - currentJitterUv;

            TextureHandle historyColor = previousRawColor != null
                ? graphResources.ImportTexture(previousRawColor, AccessFlags.Read)
                : graphResources.UseTexture(context.RenderGraph.defaultResources.blackTexture, AccessFlags.Read);
            TextureHandle historyDepth = previousRawDepth != null
                ? graphResources.ImportTexture(previousRawDepth, AccessFlags.Read)
                : graphResources.UseTexture(cameraDepth, AccessFlags.Read);
            var normalHistory = HistoryTextureHelper.ImportPair(graphResources, ssgiHistory.GetPreviousNormal(), ssgiHistory.GetCurrentNormal());
            TextureHandle previousNormalHistory = normalHistory.Previous;
            TextureHandle currentNormalHistory = normalHistory.Current;
            var gi0History = HistoryTextureHelper.ImportPair(graphResources, ssgiHistory.GetPreviousGi0(), ssgiHistory.GetCurrentGi0());
            TextureHandle previousGi0 = gi0History.Previous;
            TextureHandle currentGi0 = gi0History.Current;
            var gi1History = HistoryTextureHelper.ImportPair(graphResources, ssgiHistory.GetPreviousGi1(), ssgiHistory.GetCurrentGi1());
            TextureHandle previousGi1 = gi1History.Previous;
            TextureHandle currentGi1 = gi1History.Current;

            GraphicsFormat giFormat = SystemInfo.IsFormatSupported(
                GraphicsFormat.B10G11R11_UFloatPack32,
                GraphicsFormatUsage.LoadStore)
                ? GraphicsFormat.B10G11R11_UFloatPack32
                : GraphicsFormat.R16G16B16A16_SFloat;
            TextureHandle historyColorHalf = graphResources.CreateTexture(TextureDescriptors.Color2D(
                Mathf.Max(1, (fullWidth + 1) / 2),
                Mathf.Max(1, (fullHeight + 1) / 2),
                giFormat,
                "_TsukuyomiSsgiHistoryColorHalf",
                FilterMode.Bilinear, randomWrite: true), AccessFlags.ReadWrite);
            TextureHandle hitPoint = graphResources.CreateTexture(TextureDescriptors.Color2D(
                traceWidth,
                traceHeight,
                GraphicsFormat.R16G16_SFloat,
                "_TsukuyomiSsgiHitPoint",
                FilterMode.Point, randomWrite: true), AccessFlags.ReadWrite);
            TextureHandle rawIndirectDiffuse = graphResources.CreateTexture(TextureDescriptors.Color2D(
                traceWidth,
                traceHeight,
                giFormat,
                "_TsukuyomiSsgiRawIndirectDiffuse",
                FilterMode.Bilinear, randomWrite: true), AccessFlags.ReadWrite);
            TextureHandle historyValidation = graphResources.CreateTexture(TextureDescriptors.Color2D(
                fullWidth,
                fullHeight,
                GraphicsFormat.R8_UInt,
                "_TsukuyomiSsgiHistoryValidation",
                FilterMode.Point, randomWrite: true), AccessFlags.ReadWrite);
            TextureHandle spatial0 = graphResources.CreateTexture(TextureDescriptors.Color2D(
                spatialWidth,
                spatialHeight,
                giFormat,
                "_TsukuyomiSsgiSpatial0",
                FilterMode.Bilinear, randomWrite: true), AccessFlags.ReadWrite);
            TextureHandle spatial1 = graphResources.CreateTexture(TextureDescriptors.Color2D(
                spatialWidth,
                spatialHeight,
                giFormat,
                "_TsukuyomiSsgiSpatial1",
                FilterMode.Bilinear, randomWrite: true), AccessFlags.ReadWrite);

            TextureHandle signal = rawIndirectDiffuse;
            int signalWidth = traceWidth;
            int signalHeight = traceHeight;
            if (_settings.Denoise)
            {
                signal = spatial0;
                signalWidth = spatialWidth;
                signalHeight = spatialHeight;
                if (_settings.SecondDenoiser)
                    signal = spatial1;
            }

            bool requiresUpsample = signalWidth != fullWidth || signalHeight != fullHeight;
            TextureHandle finalIndirectDiffuse = requiresUpsample
                ? graphResources.CreateTexture(TextureDescriptors.Color2D(
                    fullWidth,
                    fullHeight,
                    giFormat,
                    "_TsukuyomiScreenSpaceGlobalIlluminationTexture",
                    FilterMode.Bilinear, randomWrite: true), AccessFlags.Write)
                : signal;

            graphResources.UseTexture(cameraDepth, AccessFlags.Read);
            graphResources.UseTexture(cameraNormals, AccessFlags.Read);
            graphResources.UseTexture(motionVectorTexture, AccessFlags.Read);
            graphResources.UseTexture(depthPyramid, AccessFlags.Read);
            graphResources.UseBuffer(depthPyramidOffsets, AccessFlags.Read);
            context.Builder.UseAllGlobalTextures(true);
            context.Builder.AllowPassCulling(false);
            context.Builder.AllowGlobalStateModification(true);
            context.Builder.SetGlobalTextureAfterPass(finalIndirectDiffuse, GlobalTextureId);

            passData.TraceCompute = _traceCompute;
            passData.TemporalCompute = _temporalCompute;
            passData.DenoiserCompute = _denoiserCompute;
            passData.UpsampleCompute = _upsampleCompute;
            passData.DownsampleHistoryKernel = _downsampleHistoryKernel;
            passData.TraceKernel = _settings.HalfResolution ? _traceHalfKernel : _traceKernel;
            passData.ReprojectKernel = _settings.HalfResolution ? _reprojectHalfKernel : _reprojectKernel;
            passData.CopyNormalsKernel = _copyNormalsKernel;
            passData.ValidateHistoryKernel = _validateHistoryKernel;
            passData.TemporalKernel = _temporalKernel;
            passData.SpatialKernel = _spatialKernel;
            passData.UpsampleKernel = _upsampleKernel;
            TsukuyomiScreenSpaceGlobalIlluminationResolvedSettings settings = _settings;
            passData.FullResolution = ResolutionVector(fullWidth, fullHeight);
            passData.TraceResolution = ResolutionVector(traceWidth, traceHeight);
            passData.SpatialResolution = ResolutionVector(spatialWidth, spatialHeight);
            passData.HalfColorResolution = ResolutionVector(
                Mathf.Max(1, (fullWidth + 1) / 2),
                Mathf.Max(1, (fullHeight + 1) / 2));
            FillAmbientProbeData(RenderSettings.ambientProbe, passData.AmbientProbeData);
            float near = context.CameraData.camera.nearClipPlane;
            float far = context.CameraData.camera.farClipPlane;
            float thicknessScale = 1.0f / (1.0f + settings.DepthBufferThickness);
            passData.ThicknessBias = -near / Mathf.Max(0.0001f, far - near)
                * (settings.DepthBufferThickness * thicknessScale);
            passData.FrameIndex = Time.frameCount & 15;

            passData.CameraDepth = cameraDepth;
            passData.CameraNormals = cameraNormals;
            passData.MotionVectorTexture = motionVectorTexture;
            passData.DepthPyramid = depthPyramid;
            passData.DepthPyramidOffsets = depthPyramidOffsets;
            passData.FullWidth = fullWidth;
            passData.FullHeight = fullHeight;
            passData.TraceWidth = traceWidth;
            passData.TraceHeight = traceHeight;
            passData.ViewProjection = viewProjection;
            passData.HistoryValid = historyValid;
            passData.HistoryColor = historyColor;
            passData.HistoryDepth = historyDepth;
            passData.PreviousNormalHistory = previousNormalHistory;
            passData.CurrentNormalHistory = currentNormalHistory;
            passData.PreviousGi0 = previousGi0;
            passData.CurrentGi0 = currentGi0;
            passData.PreviousGi1 = previousGi1;
            passData.CurrentGi1 = currentGi1;
            passData.HistoryColorHalf = historyColorHalf;
            passData.HitPoint = hitPoint;
            passData.RawIndirectDiffuse = rawIndirectDiffuse;
            passData.HistoryValidation = historyValidation;
            passData.Spatial0 = spatial0;
            passData.Spatial1 = spatial1;
            passData.Signal = signal;
            passData.SignalWidth = signalWidth;
            passData.SignalHeight = signalHeight;
            passData.RequiresUpsample = requiresUpsample;
            passData.FinalIndirectDiffuse = finalIndirectDiffuse;
            passData.Settings = settings;
            passData.ThicknessScale = thicknessScale;

            context.SetRenderFunc(passData, static (state, graphContext) =>
            {
                graphContext.cmd.SetKeyword(s_GlobalKeyword, true);

                SetResolution(graphContext.cmd, state.TraceCompute, state.FullResolution, state.HalfColorResolution);
                graphContext.cmd.SetComputeTextureParam(state.TraceCompute, state.DownsampleHistoryKernel, HistoryColorTextureId, state.HistoryColor);
                graphContext.cmd.SetComputeTextureParam(state.TraceCompute, state.DownsampleHistoryKernel, HistoryColorHalfTextureRwId, state.HistoryColorHalf);
                Dispatch(graphContext.cmd, state.TraceCompute, state.DownsampleHistoryKernel, (int)state.HalfColorResolution.x, (int)state.HalfColorResolution.y);

                SetTraceParameters(
                    graphContext.cmd,
                    state.TraceCompute,
                    state.FullResolution,
                    state.TraceResolution,
                    state.ViewProjection,
                    state.InverseViewProjection,
                    state.Settings,
                    state.ThicknessScale,
                    state.ThicknessBias,
                    state.JitterDeltaUv,
                    state.FrameIndex);
                graphContext.cmd.SetComputeTextureParam(state.TraceCompute, state.TraceKernel, CameraNormalsTextureId, state.CameraNormals);
                graphContext.cmd.SetComputeTextureParam(state.TraceCompute, state.TraceKernel, DepthPyramidId, state.DepthPyramid);
                graphContext.cmd.SetComputeBufferParam(state.TraceCompute, state.TraceKernel, DepthPyramidMipLevelOffsetsId, state.DepthPyramidOffsets);
                graphContext.cmd.SetComputeTextureParam(state.TraceCompute, state.TraceKernel, HitPointTextureRwId, state.HitPoint);
                Dispatch(graphContext.cmd, state.TraceCompute, state.TraceKernel, state.TraceWidth, state.TraceHeight);

                SetTraceParameters(
                    graphContext.cmd,
                    state.TraceCompute,
                    state.FullResolution,
                    state.TraceResolution,
                    state.ViewProjection,
                    state.InverseViewProjection,
                    state.Settings,
                    state.ThicknessScale,
                    state.ThicknessBias,
                    state.JitterDeltaUv,
                    state.FrameIndex);
                graphContext.cmd.SetComputeTextureParam(state.TraceCompute, state.ReprojectKernel, CameraNormalsTextureId, state.CameraNormals);
                graphContext.cmd.SetComputeTextureParam(state.TraceCompute, state.ReprojectKernel, MotionVectorTextureId, state.MotionVectorTexture);
                graphContext.cmd.SetComputeTextureParam(state.TraceCompute, state.ReprojectKernel, DepthPyramidId, state.DepthPyramid);
                graphContext.cmd.SetComputeTextureParam(state.TraceCompute, state.ReprojectKernel, HistoryDepthTextureId, state.HistoryDepth);
                graphContext.cmd.SetComputeTextureParam(state.TraceCompute, state.ReprojectKernel, HistoryColorHalfTextureId, state.HistoryColorHalf);
                graphContext.cmd.SetComputeTextureParam(state.TraceCompute, state.ReprojectKernel, HitPointTextureId, state.HitPoint);
                graphContext.cmd.SetComputeTextureParam(state.TraceCompute, state.ReprojectKernel, IndirectDiffuseTextureRwId, state.RawIndirectDiffuse);
                graphContext.cmd.SetComputeVectorArrayParam(state.TraceCompute, AmbientProbeDataId, state.AmbientProbeData);
                Dispatch(graphContext.cmd, state.TraceCompute, state.ReprojectKernel, state.TraceWidth, state.TraceHeight);

                graphContext.cmd.SetComputeVectorParam(state.TemporalCompute, FullResolutionId, state.FullResolution);
                graphContext.cmd.SetComputeTextureParam(state.TemporalCompute, state.CopyNormalsKernel, CameraNormalsTextureId, state.CameraNormals);
                graphContext.cmd.SetComputeTextureParam(state.TemporalCompute, state.CopyNormalsKernel, OutputNormalHistoryRwId, state.CurrentNormalHistory);
                Dispatch(graphContext.cmd, state.TemporalCompute, state.CopyNormalsKernel, state.FullWidth, state.FullHeight);

                if (state.Settings.Denoise)
                {
                    graphContext.cmd.SetComputeVectorParam(state.TemporalCompute, FullResolutionId, state.FullResolution);
                    graphContext.cmd.SetComputeMatrixParam(state.TemporalCompute, InverseViewProjectionId, state.InverseViewProjection);
                    graphContext.cmd.SetComputeMatrixParam(state.TemporalCompute, PreviousInverseViewProjectionId, state.PreviousInverseViewProjection);
                    graphContext.cmd.SetComputeVectorParam(state.TemporalCompute, JitterDeltaId, state.JitterDeltaUv);
                    graphContext.cmd.SetComputeIntParam(state.TemporalCompute, HistoryValidId, state.HistoryValid ? 1 : 0);
                    graphContext.cmd.SetComputeTextureParam(state.TemporalCompute, state.ValidateHistoryKernel, CameraDepthTextureId, state.CameraDepth);
                    graphContext.cmd.SetComputeTextureParam(state.TemporalCompute, state.ValidateHistoryKernel, CameraNormalsTextureId, state.CameraNormals);
                    graphContext.cmd.SetComputeTextureParam(state.TemporalCompute, state.ValidateHistoryKernel, MotionVectorTextureId, state.MotionVectorTexture);
                    graphContext.cmd.SetComputeTextureParam(state.TemporalCompute, state.ValidateHistoryKernel, HistoryDepthTextureId, state.HistoryDepth);
                    graphContext.cmd.SetComputeTextureParam(state.TemporalCompute, state.ValidateHistoryKernel, HistoryNormalTextureId, state.PreviousNormalHistory);
                    graphContext.cmd.SetComputeTextureParam(state.TemporalCompute, state.ValidateHistoryKernel, HistoryValidationTextureRwId, state.HistoryValidation);
                    Dispatch(graphContext.cmd, state.TemporalCompute, state.ValidateHistoryKernel, state.FullWidth, state.FullHeight);

                    DispatchTemporal(
                        graphContext.cmd,
                        state.TemporalCompute,
                        state.TemporalKernel,
                        state.RawIndirectDiffuse,
                        state.PreviousGi0,
                        state.CurrentGi0,
                        state.HistoryValidation,
                        state.MotionVectorTexture,
                        state.FullResolution,
                        state.TraceResolution,
                        state.HistoryValid);
                    DispatchSpatial(
                        graphContext.cmd,
                        state.DenoiserCompute,
                        state.SpatialKernel,
                        state.CurrentGi0,
                        state.Spatial0,
                        state.CameraDepth,
                        state.CameraNormals,
                        state.FullResolution,
                        state.TraceResolution,
                        state.SpatialResolution,
                        state.Settings.DenoiserRadius);

                    if (state.Settings.SecondDenoiser)
                    {
                        DispatchTemporal(
                            graphContext.cmd,
                            state.TemporalCompute,
                            state.TemporalKernel,
                            state.Spatial0,
                            state.PreviousGi1,
                            state.CurrentGi1,
                            state.HistoryValidation,
                            state.MotionVectorTexture,
                            state.FullResolution,
                            state.SpatialResolution,
                            state.HistoryValid);
                        DispatchSpatial(
                            graphContext.cmd,
                            state.DenoiserCompute,
                            state.SpatialKernel,
                            state.CurrentGi1,
                            state.Spatial1,
                            state.CameraDepth,
                            state.CameraNormals,
                            state.FullResolution,
                            state.SpatialResolution,
                            state.SpatialResolution,
                            state.Settings.DenoiserRadius);
                    }
                }

                if (state.RequiresUpsample)
                {
                    graphContext.cmd.SetComputeVectorParam(state.UpsampleCompute, FullResolutionId, state.FullResolution);
                    graphContext.cmd.SetComputeVectorParam(state.UpsampleCompute, InputResolutionId, ResolutionVector(state.SignalWidth, state.SignalHeight));
                    graphContext.cmd.SetComputeTextureParam(state.UpsampleCompute, state.UpsampleKernel, InputIndirectDiffuseTextureId, state.Signal);
                    graphContext.cmd.SetComputeTextureParam(state.UpsampleCompute, state.UpsampleKernel, CameraDepthTextureId, state.CameraDepth);
                    graphContext.cmd.SetComputeTextureParam(state.UpsampleCompute, state.UpsampleKernel, OutputIndirectDiffuseTextureRwId, state.FinalIndirectDiffuse);
                    Dispatch(graphContext.cmd, state.UpsampleCompute, state.UpsampleKernel, state.FullWidth, state.FullHeight);
                }
            }, _profilingSampler);
        }

        private bool KernelsAreValid()
        {
            return _traceCompute != null
                && _temporalCompute != null
                && _denoiserCompute != null
                && _upsampleCompute != null
                && _downsampleHistoryKernel >= 0
                && _traceKernel >= 0
                && _traceHalfKernel >= 0
                && _reprojectKernel >= 0
                && _reprojectHalfKernel >= 0
                && _copyNormalsKernel >= 0
                && _validateHistoryKernel >= 0
                && _temporalKernel >= 0
                && _spatialKernel >= 0
                && _upsampleKernel >= 0;
        }

        private static bool IsSupportedCamera(ref RenderingData renderingData)
        {
            CameraData cameraData = renderingData.cameraData;
            Camera camera = cameraData.camera;
            return camera != null
                && camera.cameraType == CameraType.Game
                && cameraData.renderType == CameraRenderType.Base
                && !cameraData.isPreviewCamera
                && !cameraData.xr.enabled
                && !camera.stereoEnabled;
        }

        private static Vector4 ResolutionVector(int width, int height)
        {
            return new Vector4(width, height, 1.0f / Mathf.Max(1, width), 1.0f / Mathf.Max(1, height));
        }

        private static Vector2 CalculateJitterUv(
            Matrix4x4 jitteredGpuProjection,
            Matrix4x4 nonJitteredGpuProjection)
        {
            // Motion vectors are generated from URP's non-jittered matrices. Recover the
            // current projection offset in the same normalized screen-UV convention used
            // by ComputeWorldSpacePosition before sampling any jittered history texture.
            Vector4 referenceViewPosition = new(0.0f, 0.0f, -1.0f, 1.0f);
            Vector4 jitteredClip = jitteredGpuProjection * referenceViewPosition;
            Vector4 nonJitteredClip = nonJitteredGpuProjection * referenceViewPosition;
            if (Mathf.Abs(jitteredClip.w) < 0.000001f || Mathf.Abs(nonJitteredClip.w) < 0.000001f)
                return Vector2.zero;

            Vector2 jitterNdc = new(
                jitteredClip.x / jitteredClip.w - nonJitteredClip.x / nonJitteredClip.w,
                jitteredClip.y / jitteredClip.w - nonJitteredClip.y / nonJitteredClip.w);
            float yScale = SystemInfo.graphicsUVStartsAtTop ? -0.5f : 0.5f;
            return new Vector2(jitterNdc.x * 0.5f, jitterNdc.y * yScale);
        }

        private static void FillAmbientProbeData(SphericalHarmonicsL2 ambientProbe, Vector4[] data)
        {
            data[0] = new Vector4(ambientProbe[0, 3], ambientProbe[0, 1], ambientProbe[0, 2], ambientProbe[0, 0] - ambientProbe[0, 6]);
            data[1] = new Vector4(ambientProbe[1, 3], ambientProbe[1, 1], ambientProbe[1, 2], ambientProbe[1, 0] - ambientProbe[1, 6]);
            data[2] = new Vector4(ambientProbe[2, 3], ambientProbe[2, 1], ambientProbe[2, 2], ambientProbe[2, 0] - ambientProbe[2, 6]);
            data[3] = new Vector4(ambientProbe[0, 4], ambientProbe[0, 5], ambientProbe[0, 6] * 3.0f, ambientProbe[0, 7]);
            data[4] = new Vector4(ambientProbe[1, 4], ambientProbe[1, 5], ambientProbe[1, 6] * 3.0f, ambientProbe[1, 7]);
            data[5] = new Vector4(ambientProbe[2, 4], ambientProbe[2, 5], ambientProbe[2, 6] * 3.0f, ambientProbe[2, 7]);
            data[6] = new Vector4(ambientProbe[0, 8], ambientProbe[1, 8], ambientProbe[2, 8], 1.0f);
        }

        private static void SetResolution(UnsafeCommandBuffer cmd, ComputeShader compute, Vector4 fullResolution, Vector4 outputResolution)
        {
            cmd.SetComputeVectorParam(compute, FullResolutionId, fullResolution);
            cmd.SetComputeVectorParam(compute, OutputResolutionId, outputResolution);
        }

        private static void SetTraceParameters(
            UnsafeCommandBuffer cmd,
            ComputeShader compute,
            Vector4 fullResolution,
            Vector4 outputResolution,
            Matrix4x4 viewProjection,
            Matrix4x4 inverseViewProjection,
            TsukuyomiScreenSpaceGlobalIlluminationResolvedSettings settings,
            float thicknessScale,
            float thicknessBias,
            Vector2 jitterDeltaUv,
            int frameIndex)
        {
            SetResolution(cmd, compute, fullResolution, outputResolution);
            cmd.SetComputeMatrixParam(compute, ViewProjectionId, viewProjection);
            cmd.SetComputeMatrixParam(compute, InverseViewProjectionId, inverseViewProjection);
            cmd.SetComputeVectorParam(compute, JitterDeltaId, jitterDeltaUv);
            cmd.SetComputeIntParam(compute, RayMarchingStepsId, settings.MaxRaySteps);
            cmd.SetComputeFloatParam(compute, RayMarchingThicknessScaleId, thicknessScale);
            cmd.SetComputeFloatParam(compute, RayMarchingThicknessBiasId, thicknessBias);
            cmd.SetComputeIntParam(compute, RayMarchingReflectsSkyId, 1);
            cmd.SetComputeIntParam(compute, RayMarchingFallbackHierarchyId, (int)settings.RayMiss);
            cmd.SetComputeIntParam(compute, IndirectDiffuseFrameIndexId, frameIndex);
            cmd.SetComputeIntParam(compute, EnableProbeVolumesId, settings.EnableProbeVolumes ? 1 : 0);
        }

        private static void DispatchTemporal(
            UnsafeCommandBuffer cmd,
            ComputeShader compute,
            int kernel,
            TextureHandle input,
            TextureHandle previousHistory,
            TextureHandle outputHistory,
            TextureHandle validation,
            TextureHandle motionVectors,
            Vector4 fullResolution,
            Vector4 signalResolution,
            bool historyValid)
        {
            cmd.SetComputeVectorParam(compute, FullResolutionId, fullResolution);
            cmd.SetComputeVectorParam(compute, SignalResolutionId, signalResolution);
            cmd.SetComputeIntParam(compute, HistoryValidId, historyValid ? 1 : 0);
            cmd.SetComputeTextureParam(compute, kernel, InputIndirectDiffuseTextureId, input);
            cmd.SetComputeTextureParam(compute, kernel, HistoryIndirectDiffuseTextureId, previousHistory);
            cmd.SetComputeTextureParam(compute, kernel, HistoryValidationTextureId, validation);
            cmd.SetComputeTextureParam(compute, kernel, MotionVectorTextureId, motionVectors);
            cmd.SetComputeTextureParam(compute, kernel, OutputIndirectDiffuseHistoryRwId, outputHistory);
            Dispatch(cmd, compute, kernel, (int)signalResolution.x, (int)signalResolution.y);
        }

        private static void DispatchSpatial(
            UnsafeCommandBuffer cmd,
            ComputeShader compute,
            int kernel,
            TextureHandle input,
            TextureHandle output,
            TextureHandle cameraDepth,
            TextureHandle cameraNormals,
            Vector4 fullResolution,
            Vector4 inputResolution,
            Vector4 outputResolution,
            float radius)
        {
            cmd.SetComputeVectorParam(compute, FullResolutionId, fullResolution);
            cmd.SetComputeVectorParam(compute, InputResolutionId, inputResolution);
            cmd.SetComputeVectorParam(compute, OutputResolutionId, outputResolution);
            cmd.SetComputeFloatParam(compute, DenoiserRadiusId, radius);
            cmd.SetComputeTextureParam(compute, kernel, InputIndirectDiffuseTextureId, input);
            cmd.SetComputeTextureParam(compute, kernel, CameraDepthTextureId, cameraDepth);
            cmd.SetComputeTextureParam(compute, kernel, CameraNormalsTextureId, cameraNormals);
            cmd.SetComputeTextureParam(compute, kernel, OutputIndirectDiffuseTextureRwId, output);
            Dispatch(cmd, compute, kernel, (int)outputResolution.x, (int)outputResolution.y);
        }

        private static void Dispatch(UnsafeCommandBuffer cmd, ComputeShader compute, int kernel, int width, int height)
        {
            cmd.DispatchCompute(compute, kernel, DivRoundUp(width, TileSize), DivRoundUp(height, TileSize), 1);
        }

        private static int DivRoundUp(int value, int divisor)
        {
            return (value + divisor - 1) / divisor;
        }
    }
}
