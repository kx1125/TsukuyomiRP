using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

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
        private static readonly ProfilingSampler _profilingSampler = new("Ground Truth Ambient Occlusion");

        private TsukuyomiPipelineProfile _profile;
        private TsukuyomiGroundTruthAmbientOcclusionResolvedSettings _settings;
        private ComputeShader _tracingComputeShader;
        private ComputeShader _spatialDenoiseComputeShader;
        private ComputeShader _upsampleComputeShader;
        private int _tracingKernel = -1;
        private int _spatialDenoiseKernel = -1;
        private int _upsampleKernel = -1;

        public override void CollectTextureSlots(System.Collections.Generic.List<TextureSlot> slots)
        {
            slots.Add(depth);
            slots.Add(normals);
        }

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

        private sealed class RenderData
        {
            public TextureHandle CameraDepth;
            public TextureHandle CameraNormals;
            public TextureHandle DepthPyramid;
            public int AoWidth;
            public int AoHeight;
            public TextureHandle AoPackedData;
            public TextureHandle FinalAO;
            public PreparedAOParameters AoParameters;
            public Vector4 AmbientOcclusionParam;
            public ComputeShader Tracing;
            public ComputeShader Denoise;
            public ComputeShader Upsample;
            public int TracingKernel;
            public int DenoiseKernel;
            public int UpsampleKernel;
            public bool DownSample;
            public int FullWidth;
            public int FullHeight;
            public readonly Vector4[] TopLeftCorner = new Vector4[2];
            public readonly Vector4[] XExtent = new Vector4[2];
            public readonly Vector4[] YExtent = new Vector4[2];
            public readonly Vector4[] ZExtent = new Vector4[2];
            public readonly Matrix4x4[] ViewProjections = new Matrix4x4[2];
        }

        public override void CollectResourceRequirements(in FrameContext frame, in ResourceRequirementCollector requirements)
        {
            if (!frame.CameraData.isPreviewCamera) requirements.RequireDepthPyramid();
        }

        public override void Record(in UnsafePassContext context)
        {
            var passData = context.GetOrCreateData<RenderData>();
            var graphResources = context.GraphResources;
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

            TextureHandle aoPackedData = graphResources.CreateTexture(
                TextureDescriptors.Color2D(aoWidth, aoHeight, GraphicsFormat.R32_SFloat,
                    "_GTAOPackedData", FilterMode.Bilinear, randomWrite: true, clearColor: Color.white), AccessFlags.ReadWrite);
            TextureHandle finalAO = graphResources.CreateTexture(
                TextureDescriptors.Color2D(cameraDescriptor.width, cameraDescriptor.height, GraphicsFormat.R8_UNorm,
                    "_ScreenSpaceOcclusionTexture", FilterMode.Bilinear, randomWrite: true, clearColor: Color.white), AccessFlags.ReadWrite);
            passData.AoParameters = PrepareVariables(context, aoWidth, aoHeight, downsampleDivider);
            passData.AmbientOcclusionParam = new(1.0f, 0.0f, 0.0f, _settings.DirectLightingStrength);

            passData.Tracing = _tracingComputeShader;
            passData.Denoise = _spatialDenoiseComputeShader;
            passData.Upsample = _upsampleComputeShader;
            passData.TracingKernel = _tracingKernel;
            passData.DenoiseKernel = _spatialDenoiseKernel;
            passData.UpsampleKernel = _upsampleKernel;
            passData.DownSample = _settings.DownSample;
            passData.FullWidth = cameraDescriptor.width;
            passData.FullHeight = cameraDescriptor.height;
            System.Array.Copy(_cameraTopLeftCorner, passData.TopLeftCorner, 2);
            System.Array.Copy(_cameraXExtent, passData.XExtent, 2);
            System.Array.Copy(_cameraYExtent, passData.YExtent, 2);
            System.Array.Copy(_cameraZExtent, passData.ZExtent, 2);
            System.Array.Copy(_cameraViewProjections, passData.ViewProjections, 2);

            context.Builder.UseTexture(cameraDepth, AccessFlags.Read);
            context.Builder.UseTexture(cameraNormals, AccessFlags.Read);
            context.Builder.UseTexture(depthPyramid, AccessFlags.Read);
            context.Builder.SetGlobalTextureAfterPass(finalAO, ScreenSpaceOcclusionTextureId);
            context.FrameData.Get<UniversalResourceData>().ssaoTexture = finalAO;
            context.Builder.AllowPassCulling(false);
            context.Builder.AllowGlobalStateModification(true);

            passData.CameraDepth = cameraDepth;
            passData.CameraNormals = cameraNormals;
            passData.DepthPyramid = depthPyramid;
            passData.AoWidth = aoWidth;
            passData.AoHeight = aoHeight;
            passData.AoPackedData = aoPackedData;
            passData.FinalAO = finalAO;

            context.SetRenderFunc(passData, static (state, graphContext) =>
            {
                if (s_KeywordsInitialized)
                    graphContext.cmd.SetKeyword(ScreenSpaceOcclusionKeyword, true);

                SetKeyword(state.Tracing, HalfResolutionKeyword, state.DownSample);
                SetKeyword(state.Tracing, FullResolutionKeyword, !state.DownSample);
                SetKeyword(state.Tracing, PackAODepthKeyword, true);

                PushAOParameters(graphContext.cmd, state.Tracing, state.AoParameters, state.TopLeftCorner, state.XExtent, state.YExtent, state.ZExtent, state.ViewProjections);
                graphContext.cmd.SetComputeTextureParam(state.Tracing, state.TracingKernel, AOPackedDataId, state.AoPackedData);
                graphContext.cmd.SetComputeTextureParam(state.Tracing, state.TracingKernel, CameraDepthTextureId, state.CameraDepth);
                graphContext.cmd.SetComputeTextureParam(state.Tracing, state.TracingKernel, DepthPyramidId, state.DepthPyramid);
                graphContext.cmd.SetComputeTextureParam(state.Tracing, state.TracingKernel, CameraNormalsTextureId, state.CameraNormals);
                graphContext.cmd.DispatchCompute(state.Tracing, state.TracingKernel, DivRoundUp(state.AoWidth, TileSize), DivRoundUp(state.AoHeight, TileSize), 1);

                if (state.DownSample)
                {
                    PushAOParameters(graphContext.cmd, state.Upsample, state.AoParameters, state.TopLeftCorner, state.XExtent, state.YExtent, state.ZExtent, state.ViewProjections);
                    graphContext.cmd.SetComputeTextureParam(state.Upsample, state.UpsampleKernel, AOPackedDataId, state.AoPackedData);
                    graphContext.cmd.SetComputeTextureParam(state.Upsample, state.UpsampleKernel, OcclusionTextureId, state.FinalAO);
                    graphContext.cmd.SetComputeTextureParam(state.Upsample, state.UpsampleKernel, DepthPyramidId, state.DepthPyramid);
                    // Each thread writes 2*p and 2*p-1. Include the final half-grid
                    // point so the last row/column is covered at every resolution.
                    graphContext.cmd.DispatchCompute(state.Upsample, state.UpsampleKernel, DivRoundUp(state.FullWidth / 2 + 1, TileSize), DivRoundUp(state.FullHeight / 2 + 1, TileSize), 1);
                }
                else
                {
                    PushAOParameters(graphContext.cmd, state.Denoise, state.AoParameters, state.TopLeftCorner, state.XExtent, state.YExtent, state.ZExtent, state.ViewProjections);
                    graphContext.cmd.SetComputeTextureParam(state.Denoise, state.DenoiseKernel, AOPackedDataId, state.AoPackedData);
                    graphContext.cmd.SetComputeTextureParam(state.Denoise, state.DenoiseKernel, OcclusionTextureId, state.FinalAO);
                    graphContext.cmd.DispatchCompute(state.Denoise, state.DenoiseKernel, DivRoundUp(state.AoWidth, TileSize), DivRoundUp(state.AoHeight, TileSize), 1);
                }

                graphContext.cmd.SetGlobalVector(AmbientOcclusionParamId, state.AmbientOcclusionParam);
            }, _profilingSampler);
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

    }
}
