using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Tsukuyomi.Rendering
{
    [System.Serializable]
    internal sealed class TsukuyomiGroundTruthAmbientOcclusionPass : UnsafePass
    {
        private const int TileSize = 8;
        private const string TracingKernelName = "GTAOMain";
        private const string SpatialDenoiseKernelName = "SpatialDenoise";
        private const string UpsampleKernelName = "BlurUpsample";
        private const string FullResolutionKeyword = "FULL_RES";
        private const string HalfResolutionKeyword = "HALF_RES";
        private const string PackAODepthKeyword = "PACK_AO_DEPTH";
        private const string ScreenSpaceOcclusionKeywordName = "_SCREEN_SPACE_OCCLUSION";

        private static readonly int CameraDepthTextureId = Shader.PropertyToID("_CameraDepthTexture");
        private static readonly int CameraNormalsTextureId = Shader.PropertyToID("_CameraNormalsTexture");
        private static readonly int DepthPyramidId = Shader.PropertyToID("_DepthPyramid");
        private static readonly int AOPackedDataId = Shader.PropertyToID("_AOPackedData");
        private static readonly int OcclusionTextureId = Shader.PropertyToID("_OcclusionTexture");
        private static readonly int ScreenSpaceOcclusionTextureId = Shader.PropertyToID("_ScreenSpaceOcclusionTexture");
        private static readonly int AmbientOcclusionParamId = Shader.PropertyToID("_AmbientOcclusionParam");
        private static readonly int AOBufferSizeId = Shader.PropertyToID("_AOBufferSize");
        private static readonly int AOParams0Id = Shader.PropertyToID("_AOParams0");
        private static readonly int AOParams1Id = Shader.PropertyToID("_AOParams1");
        private static readonly int AOParams2Id = Shader.PropertyToID("_AOParams2");
        private static readonly int AOParams3Id = Shader.PropertyToID("_AOParams3");
        private static readonly int AOParams4Id = Shader.PropertyToID("_AOParams4");
        private static readonly int FirstTwoDepthMipOffsetsId = Shader.PropertyToID("_FirstTwoDepthMipOffsets");
        private static readonly int AODepthToViewParamsId = Shader.PropertyToID("_AODepthToViewParams");
        private static readonly int SSAOUVToViewId = Shader.PropertyToID("_SSAO_UVToView");
        private static readonly int ProjectionParams2Id = Shader.PropertyToID("_ProjectionParams2");
        private static readonly int CameraViewProjectionsId = Shader.PropertyToID("_CameraViewProjections");
        private static readonly int CameraViewTopLeftCornerId = Shader.PropertyToID("_CameraViewTopLeftCorner");
        private static readonly int CameraViewXExtentId = Shader.PropertyToID("_CameraViewXExtent");
        private static readonly int CameraViewYExtentId = Shader.PropertyToID("_CameraViewYExtent");
        private static readonly int CameraViewZExtentId = Shader.PropertyToID("_CameraViewZExtent");
        private static GlobalKeyword ScreenSpaceOcclusionKeyword;
        private static bool s_KeywordsInitialized;

        [Read(BuiltinTexture.CameraDepthTexture)]
        public TextureSlot depth = TextureSlot.Read("Depth", BuiltinTexture.CameraDepthTexture);

        [Read(BuiltinTexture.CameraNormals)]
        public TextureSlot normals = TextureSlot.Read("Normals", BuiltinTexture.CameraNormals);

        private readonly Vector4[] _cameraTopLeftCorner = new Vector4[2];
        private readonly Vector4[] _cameraXExtent = new Vector4[2];
        private readonly Vector4[] _cameraYExtent = new Vector4[2];
        private readonly Vector4[] _cameraZExtent = new Vector4[2];
        private readonly Matrix4x4[] _cameraViewProjections = new Matrix4x4[2];
        private readonly ProfilingSampler _profilingSampler = new("Ground Truth Ambient Occlusion");

        private TsukuyomiPipelineProfile _profile;
        private TsukuyomiGroundTruthAmbientOcclusionResolvedSettings _settings;
        private ComputeShader _tracingComputeShader;
        private ComputeShader _spatialDenoiseComputeShader;
        private ComputeShader _upsampleComputeShader;
        private int _tracingKernel = -1;
        private int _spatialDenoiseKernel = -1;
        private int _upsampleKernel = -1;

        public override string Name => "Ground Truth Ambient Occlusion";

        internal static GlobalKeyword ScreenSpaceOcclusionGlobalKeyword => ScreenSpaceOcclusionKeyword;

        internal static void InitializeKeywords()
        {
            if (s_KeywordsInitialized)
                return;

            ScreenSpaceOcclusionKeyword = GlobalKeyword.Create(ScreenSpaceOcclusionKeywordName);
            s_KeywordsInitialized = true;
        }

        private struct ShaderVariablesAmbientOcclusion
        {
            public Vector4 BufferSize;
            public Vector4 Params0;
            public Vector4 Params1;
            public Vector4 Params2;
            public Vector4 Params3;
            public Vector4 Params4;
            public Vector4 FirstTwoDepthMipOffsets;
            public Vector4 DepthToViewParams;
        }

        private readonly struct PreparedAOParameters
        {
            public readonly ShaderVariablesAmbientOcclusion Variables;
            public readonly Vector4 SSAOUVToView;
            public readonly Vector4 ProjectionParams2;

            public PreparedAOParameters(ShaderVariablesAmbientOcclusion variables, Vector4 ssaoUVToView, Vector4 projectionParams2)
            {
                Variables = variables;
                SSAOUVToView = ssaoUVToView;
                ProjectionParams2 = projectionParams2;
            }
        }

        public bool Configure(TsukuyomiPipelineProfile profile, TsukuyomiGroundTruthAmbientOcclusionVolume volume)
        {
            _profile = profile;
            if (profile == null)
                return false;

            _settings = TsukuyomiGroundTruthAmbientOcclusionResolvedSettings.From(profile, volume);
            if (!_settings.IsActive)
                return false;

            if (!TsukuyomiRenderPipelineResourcesProvider.TryGet(out TsukuyomiRenderPipelineResources resources))
                return false;

            _tracingComputeShader = resources.GtaoTraceComputeShader;
            _spatialDenoiseComputeShader = resources.GtaoSpatialDenoiseComputeShader;
            _upsampleComputeShader = resources.GtaoBlurAndUpsampleComputeShader;

            if (_tracingComputeShader == null || _spatialDenoiseComputeShader == null || _upsampleComputeShader == null)
            {
                Debug.LogError("Tsukuyomi GTAO requires trace, spatial denoise, and blur/upsample compute shaders in TsukuyomiRenderPipelineResources.");
                return false;
            }

            _tracingKernel = _tracingComputeShader.FindKernel(TracingKernelName);
            _spatialDenoiseKernel = _spatialDenoiseComputeShader.FindKernel(SpatialDenoiseKernelName);
            _upsampleKernel = _upsampleComputeShader.FindKernel(UpsampleKernelName);
            return _tracingKernel >= 0 && _spatialDenoiseKernel >= 0 && _upsampleKernel >= 0;
        }

        public override bool IsActive(in FrameContext frame)
        {
            return base.IsActive(frame)
                && _profile != null
                && _settings.IsActive
                && _tracingComputeShader != null
                && _spatialDenoiseComputeShader != null
                && _upsampleComputeShader != null;
        }

        public override void Record(in UnsafePassContext context)
        {
            if (_profile == null || !_settings.IsActive || context.CameraData.isPreviewCamera)
                return;

            TextureHandle cameraDepth = context.GetTexture(depth);
            TextureHandle cameraNormals = context.GetTexture(normals);
            if (!cameraDepth.IsValid() || !cameraNormals.IsValid())
                return;

            RenderTextureDescriptor cameraDescriptor = context.CameraData.cameraTargetDescriptor;
            TextureSlot depthPyramidSlot = TsukuyomiDepthPyramidResources.CreateDepthPyramidSlot(cameraDescriptor, ResourceAccess.Read);
            TextureHandle depthPyramid = context.GetTexture(depthPyramidSlot);
            if (!depthPyramid.IsValid())
                return;

            int downsampleDivider = _settings.DownSample ? 2 : 1;
            int aoWidth = Mathf.Max(1, cameraDescriptor.width / downsampleDivider);
            int aoHeight = Mathf.Max(1, cameraDescriptor.height / downsampleDivider);

            TextureHandle aoPackedData = context.RenderGraph.CreateTexture(CreateAODesc(aoWidth, aoHeight, GraphicsFormat.R32_SFloat, "_GTAOPackedData"));
            TextureHandle finalAO = context.RenderGraph.CreateTexture(CreateAODesc(cameraDescriptor.width, cameraDescriptor.height, GraphicsFormat.R8_UNorm, "_ScreenSpaceOcclusionTexture"));
            PreparedAOParameters aoParameters = PrepareVariables(context, aoWidth, aoHeight, downsampleDivider);
            Vector4 ambientOcclusionParam = new(1.0f, 0.0f, 0.0f, _settings.DirectLightingStrength);

            ComputeShader tracing = _tracingComputeShader;
            ComputeShader denoise = _spatialDenoiseComputeShader;
            ComputeShader upsample = _upsampleComputeShader;
            int tracingKernel = _tracingKernel;
            int denoiseKernel = _spatialDenoiseKernel;
            int upsampleKernel = _upsampleKernel;
            bool downSample = _settings.DownSample;
            int fullWidth = cameraDescriptor.width;
            int fullHeight = cameraDescriptor.height;
            Vector4[] topLeftCorner = CopyArray(_cameraTopLeftCorner);
            Vector4[] xExtent = CopyArray(_cameraXExtent);
            Vector4[] yExtent = CopyArray(_cameraYExtent);
            Vector4[] zExtent = CopyArray(_cameraZExtent);
            Matrix4x4[] viewProjections = CopyArray(_cameraViewProjections);
            ProfilingSampler profilingSampler = _profilingSampler;

            context.Builder.UseTexture(cameraDepth, AccessFlags.Read);
            context.Builder.UseTexture(cameraNormals, AccessFlags.Read);
            context.Builder.UseTexture(depthPyramid, AccessFlags.Read);
            context.Builder.UseTexture(aoPackedData, AccessFlags.ReadWrite);
            context.Builder.UseTexture(finalAO, AccessFlags.ReadWrite);
            context.Builder.AllowPassCulling(false);
            context.Builder.AllowGlobalStateModification(true);

            context.SetRenderFunc((data, graphContext) =>
            {
                using (new ProfilingScope(graphContext.cmd, profilingSampler))
                {
                    if (s_KeywordsInitialized)
                        graphContext.cmd.SetKeyword(ScreenSpaceOcclusionKeyword, true);

                    SetKeyword(tracing, HalfResolutionKeyword, downSample);
                    SetKeyword(tracing, FullResolutionKeyword, !downSample);
                    SetKeyword(tracing, PackAODepthKeyword, true);

                    PushAOParameters(graphContext.cmd, tracing, aoParameters, topLeftCorner, xExtent, yExtent, zExtent, viewProjections);
                    graphContext.cmd.SetComputeTextureParam(tracing, tracingKernel, AOPackedDataId, aoPackedData);
                    graphContext.cmd.SetComputeTextureParam(tracing, tracingKernel, CameraDepthTextureId, cameraDepth);
                    graphContext.cmd.SetComputeTextureParam(tracing, tracingKernel, DepthPyramidId, depthPyramid);
                    graphContext.cmd.SetComputeTextureParam(tracing, tracingKernel, CameraNormalsTextureId, cameraNormals);
                    graphContext.cmd.DispatchCompute(tracing, tracingKernel, DivRoundUp(aoWidth, TileSize), DivRoundUp(aoHeight, TileSize), 1);

                    if (downSample)
                    {
                        PushAOParameters(graphContext.cmd, upsample, aoParameters, topLeftCorner, xExtent, yExtent, zExtent, viewProjections);
                        graphContext.cmd.SetComputeTextureParam(upsample, upsampleKernel, AOPackedDataId, aoPackedData);
                        graphContext.cmd.SetComputeTextureParam(upsample, upsampleKernel, OcclusionTextureId, finalAO);
                        graphContext.cmd.SetComputeTextureParam(upsample, upsampleKernel, DepthPyramidId, depthPyramid);
                        graphContext.cmd.DispatchCompute(upsample, upsampleKernel, DivRoundUp(aoWidth, TileSize), DivRoundUp(aoHeight, TileSize), 1);
                    }
                    else
                    {
                        PushAOParameters(graphContext.cmd, denoise, aoParameters, topLeftCorner, xExtent, yExtent, zExtent, viewProjections);
                        graphContext.cmd.SetComputeTextureParam(denoise, denoiseKernel, AOPackedDataId, aoPackedData);
                        graphContext.cmd.SetComputeTextureParam(denoise, denoiseKernel, OcclusionTextureId, finalAO);
                        graphContext.cmd.DispatchCompute(denoise, denoiseKernel, DivRoundUp(aoWidth, TileSize), DivRoundUp(aoHeight, TileSize), 1);
                    }

                    graphContext.cmd.SetGlobalVector(AmbientOcclusionParamId, ambientOcclusionParam);
                    graphContext.cmd.SetGlobalTexture(ScreenSpaceOcclusionTextureId, finalAO);
                }
            });
        }

        private PreparedAOParameters PrepareVariables(in UnsafePassContext context, int width, int height, int downsampleDivider)
        {
            for (int eyeIndex = 0; eyeIndex < 2; eyeIndex++)
            {
                Matrix4x4 view = context.CameraData.GetViewMatrix(eyeIndex);
                Matrix4x4 proj = context.CameraData.GetProjectionMatrix(eyeIndex);
                _cameraViewProjections[eyeIndex] = proj * view;

                Matrix4x4 cview = view;
                cview.SetColumn(3, new Vector4(0.0f, 0.0f, 0.0f, 1.0f));
                Matrix4x4 cviewProjInv = (proj * cview).inverse;

                Vector4 topLeftCorner = cviewProjInv.MultiplyPoint(new Vector4(-1, 1, -1, 1));
                Vector4 topRightCorner = cviewProjInv.MultiplyPoint(new Vector4(1, 1, -1, 1));
                Vector4 bottomLeftCorner = cviewProjInv.MultiplyPoint(new Vector4(-1, -1, -1, 1));
                Vector4 farCentre = cviewProjInv.MultiplyPoint(new Vector4(0, 0, 1, 1));
                _cameraTopLeftCorner[eyeIndex] = topLeftCorner;
                _cameraXExtent[eyeIndex] = topRightCorner - topLeftCorner;
                _cameraYExtent[eyeIndex] = bottomLeftCorner - topLeftCorner;
                _cameraZExtent[eyeIndex] = farCentre;
            }

            Camera camera = context.CameraData.camera;
            float fovRad = camera.fieldOfView * Mathf.Deg2Rad;
            float invHalfTanFov = 1.0f / Mathf.Tan(fovRad * 0.5f);
            float aspect = (camera.pixelHeight / (float)downsampleDivider) / (camera.pixelWidth / (float)downsampleDivider);
            Vector2 focalLen = new(invHalfTanFov * aspect, invHalfTanFov);
            Vector2 invFocalLen = new(1.0f / focalLen.x, 1.0f / focalLen.y);

            float scaleFactor = (float)width * height / (540.0f * 960.0f);
            float radiusInPixels = Mathf.Max(16.0f, _settings.MaximumRadiusInPixels * Mathf.Sqrt(scaleFactor));
            float stepSize = _settings.DownSample ? 0.5f : 1.0f;
            float blurTolerance = 1.0f - _settings.BlurSharpness;
            blurTolerance = -2.5f + blurTolerance * (0.25f + 2.5f);
            float bilateralTolerance = 1.0f - Mathf.Pow(10.0f, blurTolerance) * stepSize;
            bilateralTolerance *= bilateralTolerance;
            const float upsampleTolerance = -7.0f;
            float upsampleToleranceValue = Mathf.Pow(10.0f, upsampleTolerance);
            float noiseFilterWeight = 1.0f / (Mathf.Pow(10.0f, 0.0f) + upsampleToleranceValue);
            float aspectRatio = (float)height / width;
            TsukuyomiDepthPyramidResources.PackedMipChainInfo depthMipInfo =
                TsukuyomiDepthPyramidResources.ComputePackedMipChainInfo(context.CameraData.cameraTargetDescriptor.width, context.CameraData.cameraTargetDescriptor.height);

            ShaderVariablesAmbientOcclusion variables = new()
            {
                BufferSize = new Vector4(width, height, 1.0f / width, 1.0f / height),
                Params0 = new Vector4(
                    Mathf.Clamp(_settings.Thickness * _settings.Thickness, 0.0f, 0.99f),
                    height * invHalfTanFov * 0.25f,
                    _settings.Radius,
                    _settings.StepCount),
                Params1 = new Vector4(
                    _settings.Intensity,
                    1.0f / (_settings.Radius * _settings.Radius),
                    (Time.renderedFrameCount / 6) % 4,
                    Time.renderedFrameCount % 6),
                Params2 = new Vector4(
                    _settings.DirectionCount,
                    1.0f / downsampleDivider,
                    1.0f / (_settings.StepCount + 1.0f),
                    radiusInPixels),
                Params3 = new Vector4(
                    bilateralTolerance,
                    upsampleToleranceValue,
                    noiseFilterWeight,
                    stepSize),
                Params4 = new Vector4(
                    0.0f,
                    5.0f,
                    0.25f,
                    _settings.SpatialBilateralAggressiveness * 15.0f),
                FirstTwoDepthMipOffsets = depthMipInfo.FirstTwoDepthMipOffsets,
                DepthToViewParams = new Vector4(
                    2.0f / (invHalfTanFov * aspectRatio * width),
                    2.0f / (invHalfTanFov * height),
                    1.0f / (invHalfTanFov * aspectRatio),
                    1.0f / invHalfTanFov)
            };

            Vector4 ssaoUVToView = new(2.0f * invFocalLen.x, 2.0f * invFocalLen.y, -invFocalLen.x, -invFocalLen.y);
            Vector4 projectionParams2 = new(1.0f / Mathf.Max(0.0001f, camera.nearClipPlane), 0.0f, 0.0f, 0.0f);
            return new PreparedAOParameters(variables, ssaoUVToView, projectionParams2);
        }

        private static TextureDesc CreateAODesc(int width, int height, GraphicsFormat format, string name)
        {
            return new TextureDesc(width, height)
            {
                name = name,
                colorFormat = format,
                depthBufferBits = DepthBits.None,
                msaaSamples = MSAASamples.None,
                enableRandomWrite = true,
                clearBuffer = false,
                clearColor = Color.white,
                filterMode = FilterMode.Bilinear
            };
        }

        private static void PushAOParameters(
            UnsafeCommandBuffer cmd,
            ComputeShader compute,
            PreparedAOParameters aoParameters,
            Vector4[] topLeftCorner,
            Vector4[] xExtent,
            Vector4[] yExtent,
            Vector4[] zExtent,
            Matrix4x4[] viewProjections)
        {
            ShaderVariablesAmbientOcclusion variables = aoParameters.Variables;
            cmd.SetComputeVectorParam(compute, AOBufferSizeId, variables.BufferSize);
            cmd.SetComputeVectorParam(compute, AOParams0Id, variables.Params0);
            cmd.SetComputeVectorParam(compute, AOParams1Id, variables.Params1);
            cmd.SetComputeVectorParam(compute, AOParams2Id, variables.Params2);
            cmd.SetComputeVectorParam(compute, AOParams3Id, variables.Params3);
            cmd.SetComputeVectorParam(compute, AOParams4Id, variables.Params4);
            cmd.SetComputeVectorParam(compute, FirstTwoDepthMipOffsetsId, variables.FirstTwoDepthMipOffsets);
            cmd.SetComputeVectorParam(compute, AODepthToViewParamsId, variables.DepthToViewParams);
            cmd.SetComputeVectorParam(compute, SSAOUVToViewId, aoParameters.SSAOUVToView);
            cmd.SetComputeVectorParam(compute, ProjectionParams2Id, aoParameters.ProjectionParams2);
            cmd.SetComputeMatrixArrayParam(compute, CameraViewProjectionsId, viewProjections);
            cmd.SetComputeVectorArrayParam(compute, CameraViewTopLeftCornerId, topLeftCorner);
            cmd.SetComputeVectorArrayParam(compute, CameraViewXExtentId, xExtent);
            cmd.SetComputeVectorArrayParam(compute, CameraViewYExtentId, yExtent);
            cmd.SetComputeVectorArrayParam(compute, CameraViewZExtentId, zExtent);
        }

        private static void SetKeyword(ComputeShader compute, string keyword, bool enabled)
        {
            if (enabled)
                compute.EnableKeyword(keyword);
            else
                compute.DisableKeyword(keyword);
        }

        private static int DivRoundUp(int value, int divisor)
        {
            return (value + divisor - 1) / divisor;
        }

        private static Vector4[] CopyArray(Vector4[] source)
        {
            Vector4[] result = new Vector4[source.Length];
            source.CopyTo(result, 0);
            return result;
        }

        private static Matrix4x4[] CopyArray(Matrix4x4[] source)
        {
            Matrix4x4[] result = new Matrix4x4[source.Length];
            source.CopyTo(result, 0);
            return result;
        }
    }
}
