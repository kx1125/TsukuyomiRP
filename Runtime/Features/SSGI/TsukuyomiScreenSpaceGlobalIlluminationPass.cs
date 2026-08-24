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

        private readonly ProfilingSampler _profilingSampler = new("Tsukuyomi Screen Space Global Illumination");
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

        public override void Record(in UnsafePassContext context)
        {
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
            Matrix4x4 inverseViewProjection = viewProjection.inverse;
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
            Matrix4x4 previousInverseViewProjection = ssgiHistory.PreviousViewProjection.inverse;
            Vector2 jitterDeltaUv = ssgiHistory.PreviousJitterUv - currentJitterUv;

            TextureHandle historyColor = previousRawColor != null
                ? context.RenderGraph.ImportTexture(previousRawColor)
                : context.RenderGraph.defaultResources.blackTexture;
            TextureHandle historyDepth = previousRawDepth != null
                ? context.RenderGraph.ImportTexture(previousRawDepth)
                : cameraDepth;
            TextureHandle previousNormalHistory = context.RenderGraph.ImportTexture(ssgiHistory.GetPreviousNormal());
            TextureHandle currentNormalHistory = context.RenderGraph.ImportTexture(ssgiHistory.GetCurrentNormal());
            TextureHandle previousGi0 = context.RenderGraph.ImportTexture(ssgiHistory.GetPreviousGi0());
            TextureHandle currentGi0 = context.RenderGraph.ImportTexture(ssgiHistory.GetCurrentGi0());
            TextureHandle previousGi1 = context.RenderGraph.ImportTexture(ssgiHistory.GetPreviousGi1());
            TextureHandle currentGi1 = context.RenderGraph.ImportTexture(ssgiHistory.GetCurrentGi1());

            GraphicsFormat giFormat = SystemInfo.IsFormatSupported(
                GraphicsFormat.B10G11R11_UFloatPack32,
                GraphicsFormatUsage.LoadStore)
                ? GraphicsFormat.B10G11R11_UFloatPack32
                : GraphicsFormat.R16G16B16A16_SFloat;
            TextureHandle historyColorHalf = context.RenderGraph.CreateTexture(CreateTextureDesc(
                Mathf.Max(1, (fullWidth + 1) / 2),
                Mathf.Max(1, (fullHeight + 1) / 2),
                giFormat,
                "_TsukuyomiSsgiHistoryColorHalf",
                FilterMode.Bilinear));
            TextureHandle hitPoint = context.RenderGraph.CreateTexture(CreateTextureDesc(
                traceWidth,
                traceHeight,
                GraphicsFormat.R16G16_SFloat,
                "_TsukuyomiSsgiHitPoint",
                FilterMode.Point));
            TextureHandle rawIndirectDiffuse = context.RenderGraph.CreateTexture(CreateTextureDesc(
                traceWidth,
                traceHeight,
                giFormat,
                "_TsukuyomiSsgiRawIndirectDiffuse",
                FilterMode.Bilinear));
            TextureHandle historyValidation = context.RenderGraph.CreateTexture(CreateTextureDesc(
                fullWidth,
                fullHeight,
                GraphicsFormat.R8_UInt,
                "_TsukuyomiSsgiHistoryValidation",
                FilterMode.Point));
            TextureHandle spatial0 = context.RenderGraph.CreateTexture(CreateTextureDesc(
                spatialWidth,
                spatialHeight,
                giFormat,
                "_TsukuyomiSsgiSpatial0",
                FilterMode.Bilinear));
            TextureHandle spatial1 = context.RenderGraph.CreateTexture(CreateTextureDesc(
                spatialWidth,
                spatialHeight,
                giFormat,
                "_TsukuyomiSsgiSpatial1",
                FilterMode.Bilinear));

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
                ? context.RenderGraph.CreateTexture(CreateTextureDesc(
                    fullWidth,
                    fullHeight,
                    giFormat,
                    "_TsukuyomiScreenSpaceGlobalIlluminationTexture",
                    FilterMode.Bilinear))
                : signal;

            UseTexture(context.Builder, cameraDepth, AccessFlags.Read);
            UseTexture(context.Builder, cameraNormals, AccessFlags.Read);
            UseTexture(context.Builder, motionVectorTexture, AccessFlags.Read);
            UseTexture(context.Builder, depthPyramid, AccessFlags.Read);
            context.Builder.UseBuffer(depthPyramidOffsets, AccessFlags.Read);
            UseTexture(context.Builder, historyColor, AccessFlags.Read);
            UseTexture(context.Builder, historyDepth, AccessFlags.Read);
            UseTexture(context.Builder, historyColorHalf, AccessFlags.ReadWrite);
            UseTexture(context.Builder, hitPoint, AccessFlags.ReadWrite);
            UseTexture(context.Builder, rawIndirectDiffuse, AccessFlags.ReadWrite);
            UseTexture(context.Builder, previousNormalHistory, AccessFlags.Read);
            UseTexture(context.Builder, currentNormalHistory, AccessFlags.Write);
            UseTexture(context.Builder, previousGi0, AccessFlags.Read);
            UseTexture(context.Builder, currentGi0, AccessFlags.Write);
            UseTexture(context.Builder, previousGi1, AccessFlags.Read);
            UseTexture(context.Builder, currentGi1, AccessFlags.Write);
            UseTexture(context.Builder, historyValidation, AccessFlags.ReadWrite);
            UseTexture(context.Builder, spatial0, AccessFlags.ReadWrite);
            UseTexture(context.Builder, spatial1, AccessFlags.ReadWrite);
            if (requiresUpsample)
                UseTexture(context.Builder, finalIndirectDiffuse, AccessFlags.Write);
            context.Builder.UseAllGlobalTextures(true);
            context.Builder.AllowPassCulling(false);
            context.Builder.AllowGlobalStateModification(true);
            context.Builder.SetGlobalTextureAfterPass(finalIndirectDiffuse, GlobalTextureId);

            ComputeShader traceCompute = _traceCompute;
            ComputeShader temporalCompute = _temporalCompute;
            ComputeShader denoiserCompute = _denoiserCompute;
            ComputeShader upsampleCompute = _upsampleCompute;
            int downsampleHistoryKernel = _downsampleHistoryKernel;
            int traceKernel = _settings.HalfResolution ? _traceHalfKernel : _traceKernel;
            int reprojectKernel = _settings.HalfResolution ? _reprojectHalfKernel : _reprojectKernel;
            int copyNormalsKernel = _copyNormalsKernel;
            int validateHistoryKernel = _validateHistoryKernel;
            int temporalKernel = _temporalKernel;
            int spatialKernel = _spatialKernel;
            int upsampleKernel = _upsampleKernel;
            TsukuyomiScreenSpaceGlobalIlluminationResolvedSettings settings = _settings;
            Vector4 fullResolution = ResolutionVector(fullWidth, fullHeight);
            Vector4 traceResolution = ResolutionVector(traceWidth, traceHeight);
            Vector4 spatialResolution = ResolutionVector(spatialWidth, spatialHeight);
            Vector4 halfColorResolution = ResolutionVector(
                Mathf.Max(1, (fullWidth + 1) / 2),
                Mathf.Max(1, (fullHeight + 1) / 2));
            Vector4[] ambientProbeData = CreateAmbientProbeData(RenderSettings.ambientProbe);
            float near = context.CameraData.camera.nearClipPlane;
            float far = context.CameraData.camera.farClipPlane;
            float thicknessScale = 1.0f / (1.0f + settings.DepthBufferThickness);
            float thicknessBias = -near / Mathf.Max(0.0001f, far - near)
                * (settings.DepthBufferThickness * thicknessScale);
            int frameIndex = Time.frameCount & 15;
            ProfilingSampler profilingSampler = _profilingSampler;

            context.SetRenderFunc((data, graphContext) =>
            {
                using (new ProfilingScope(graphContext.cmd, profilingSampler))
                {
                    graphContext.cmd.SetKeyword(s_GlobalKeyword, true);

                    SetResolution(graphContext.cmd, traceCompute, fullResolution, halfColorResolution);
                    graphContext.cmd.SetComputeTextureParam(traceCompute, downsampleHistoryKernel, HistoryColorTextureId, historyColor);
                    graphContext.cmd.SetComputeTextureParam(traceCompute, downsampleHistoryKernel, HistoryColorHalfTextureRwId, historyColorHalf);
                    Dispatch(graphContext.cmd, traceCompute, downsampleHistoryKernel, (int)halfColorResolution.x, (int)halfColorResolution.y);

                    SetTraceParameters(
                        graphContext.cmd,
                        traceCompute,
                        fullResolution,
                        traceResolution,
                        viewProjection,
                        inverseViewProjection,
                        settings,
                        thicknessScale,
                        thicknessBias,
                        jitterDeltaUv,
                        frameIndex);
                    graphContext.cmd.SetComputeTextureParam(traceCompute, traceKernel, CameraNormalsTextureId, cameraNormals);
                    graphContext.cmd.SetComputeTextureParam(traceCompute, traceKernel, DepthPyramidId, depthPyramid);
                    graphContext.cmd.SetComputeBufferParam(traceCompute, traceKernel, DepthPyramidMipLevelOffsetsId, depthPyramidOffsets);
                    graphContext.cmd.SetComputeTextureParam(traceCompute, traceKernel, HitPointTextureRwId, hitPoint);
                    Dispatch(graphContext.cmd, traceCompute, traceKernel, traceWidth, traceHeight);

                    SetTraceParameters(
                        graphContext.cmd,
                        traceCompute,
                        fullResolution,
                        traceResolution,
                        viewProjection,
                        inverseViewProjection,
                        settings,
                        thicknessScale,
                        thicknessBias,
                        jitterDeltaUv,
                        frameIndex);
                    graphContext.cmd.SetComputeTextureParam(traceCompute, reprojectKernel, CameraNormalsTextureId, cameraNormals);
                    graphContext.cmd.SetComputeTextureParam(traceCompute, reprojectKernel, MotionVectorTextureId, motionVectorTexture);
                    graphContext.cmd.SetComputeTextureParam(traceCompute, reprojectKernel, DepthPyramidId, depthPyramid);
                    graphContext.cmd.SetComputeTextureParam(traceCompute, reprojectKernel, HistoryDepthTextureId, historyDepth);
                    graphContext.cmd.SetComputeTextureParam(traceCompute, reprojectKernel, HistoryColorHalfTextureId, historyColorHalf);
                    graphContext.cmd.SetComputeTextureParam(traceCompute, reprojectKernel, HitPointTextureId, hitPoint);
                    graphContext.cmd.SetComputeTextureParam(traceCompute, reprojectKernel, IndirectDiffuseTextureRwId, rawIndirectDiffuse);
                    graphContext.cmd.SetComputeVectorArrayParam(traceCompute, AmbientProbeDataId, ambientProbeData);
                    Dispatch(graphContext.cmd, traceCompute, reprojectKernel, traceWidth, traceHeight);

                    graphContext.cmd.SetComputeVectorParam(temporalCompute, FullResolutionId, fullResolution);
                    graphContext.cmd.SetComputeTextureParam(temporalCompute, copyNormalsKernel, CameraNormalsTextureId, cameraNormals);
                    graphContext.cmd.SetComputeTextureParam(temporalCompute, copyNormalsKernel, OutputNormalHistoryRwId, currentNormalHistory);
                    Dispatch(graphContext.cmd, temporalCompute, copyNormalsKernel, fullWidth, fullHeight);

                    if (settings.Denoise)
                    {
                        graphContext.cmd.SetComputeVectorParam(temporalCompute, FullResolutionId, fullResolution);
                        graphContext.cmd.SetComputeMatrixParam(temporalCompute, InverseViewProjectionId, inverseViewProjection);
                        graphContext.cmd.SetComputeMatrixParam(temporalCompute, PreviousInverseViewProjectionId, previousInverseViewProjection);
                        graphContext.cmd.SetComputeVectorParam(temporalCompute, JitterDeltaId, jitterDeltaUv);
                        graphContext.cmd.SetComputeIntParam(temporalCompute, HistoryValidId, historyValid ? 1 : 0);
                        graphContext.cmd.SetComputeTextureParam(temporalCompute, validateHistoryKernel, CameraDepthTextureId, cameraDepth);
                        graphContext.cmd.SetComputeTextureParam(temporalCompute, validateHistoryKernel, CameraNormalsTextureId, cameraNormals);
                        graphContext.cmd.SetComputeTextureParam(temporalCompute, validateHistoryKernel, MotionVectorTextureId, motionVectorTexture);
                        graphContext.cmd.SetComputeTextureParam(temporalCompute, validateHistoryKernel, HistoryDepthTextureId, historyDepth);
                        graphContext.cmd.SetComputeTextureParam(temporalCompute, validateHistoryKernel, HistoryNormalTextureId, previousNormalHistory);
                        graphContext.cmd.SetComputeTextureParam(temporalCompute, validateHistoryKernel, HistoryValidationTextureRwId, historyValidation);
                        Dispatch(graphContext.cmd, temporalCompute, validateHistoryKernel, fullWidth, fullHeight);

                        DispatchTemporal(
                            graphContext.cmd,
                            temporalCompute,
                            temporalKernel,
                            rawIndirectDiffuse,
                            previousGi0,
                            currentGi0,
                            historyValidation,
                            motionVectorTexture,
                            fullResolution,
                            traceResolution,
                            historyValid);
                        DispatchSpatial(
                            graphContext.cmd,
                            denoiserCompute,
                            spatialKernel,
                            currentGi0,
                            spatial0,
                            cameraDepth,
                            cameraNormals,
                            fullResolution,
                            traceResolution,
                            spatialResolution,
                            settings.DenoiserRadius);

                        if (settings.SecondDenoiser)
                        {
                            DispatchTemporal(
                                graphContext.cmd,
                                temporalCompute,
                                temporalKernel,
                                spatial0,
                                previousGi1,
                                currentGi1,
                                historyValidation,
                                motionVectorTexture,
                                fullResolution,
                                spatialResolution,
                                historyValid);
                            DispatchSpatial(
                                graphContext.cmd,
                                denoiserCompute,
                                spatialKernel,
                                currentGi1,
                                spatial1,
                                cameraDepth,
                                cameraNormals,
                                fullResolution,
                                spatialResolution,
                                spatialResolution,
                                settings.DenoiserRadius);
                        }
                    }

                    if (requiresUpsample)
                    {
                        graphContext.cmd.SetComputeVectorParam(upsampleCompute, FullResolutionId, fullResolution);
                        graphContext.cmd.SetComputeVectorParam(upsampleCompute, InputResolutionId, ResolutionVector(signalWidth, signalHeight));
                        graphContext.cmd.SetComputeTextureParam(upsampleCompute, upsampleKernel, InputIndirectDiffuseTextureId, signal);
                        graphContext.cmd.SetComputeTextureParam(upsampleCompute, upsampleKernel, CameraDepthTextureId, cameraDepth);
                        graphContext.cmd.SetComputeTextureParam(upsampleCompute, upsampleKernel, OutputIndirectDiffuseTextureRwId, finalIndirectDiffuse);
                        Dispatch(graphContext.cmd, upsampleCompute, upsampleKernel, fullWidth, fullHeight);
                    }
                }
            });
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

        private static TextureDesc CreateTextureDesc(int width, int height, GraphicsFormat format, string name, FilterMode filterMode)
        {
            return new TextureDesc(Mathf.Max(1, width), Mathf.Max(1, height))
            {
                name = name,
                colorFormat = format,
                depthBufferBits = DepthBits.None,
                msaaSamples = MSAASamples.None,
                enableRandomWrite = true,
                clearBuffer = false,
                filterMode = filterMode
            };
        }

        private static void UseTexture(IUnsafeRenderGraphBuilder builder, TextureHandle texture, AccessFlags access)
        {
            if (texture.IsValid())
                builder.UseTexture(texture, access);
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

        private static Vector4[] CreateAmbientProbeData(SphericalHarmonicsL2 ambientProbe)
        {
            return new[]
            {
                new Vector4(ambientProbe[0, 3], ambientProbe[0, 1], ambientProbe[0, 2], ambientProbe[0, 0] - ambientProbe[0, 6]),
                new Vector4(ambientProbe[1, 3], ambientProbe[1, 1], ambientProbe[1, 2], ambientProbe[1, 0] - ambientProbe[1, 6]),
                new Vector4(ambientProbe[2, 3], ambientProbe[2, 1], ambientProbe[2, 2], ambientProbe[2, 0] - ambientProbe[2, 6]),
                new Vector4(ambientProbe[0, 4], ambientProbe[0, 5], ambientProbe[0, 6] * 3.0f, ambientProbe[0, 7]),
                new Vector4(ambientProbe[1, 4], ambientProbe[1, 5], ambientProbe[1, 6] * 3.0f, ambientProbe[1, 7]),
                new Vector4(ambientProbe[2, 4], ambientProbe[2, 5], ambientProbe[2, 6] * 3.0f, ambientProbe[2, 7]),
                new Vector4(ambientProbe[0, 8], ambientProbe[1, 8], ambientProbe[2, 8], 1.0f)
            };
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
