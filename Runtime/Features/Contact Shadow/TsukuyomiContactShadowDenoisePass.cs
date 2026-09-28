using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Tsukuyomi.Rendering
{
    [System.Serializable]
    internal sealed class TsukuyomiContactShadowDenoisePass : ComputePass
    {
        private const int TileSize = 8;
        private const string HorizontalKernelName = "BilateralFilterHSingleDirectional";
        private const string VerticalKernelName = "BilateralFilterVSingleDirectional";

        private static readonly int DepthTextureId = Shader.PropertyToID("_DepthTexture");
        private static readonly int NormalBufferTextureId = Shader.PropertyToID("_NormalBufferTexture");
        private static readonly int DenoiseInputTextureId = Shader.PropertyToID("_DenoiseInputTexture");
        private static readonly int DenoiseOutputTextureRwId = Shader.PropertyToID("_DenoiseOutputTextureRW");
        private static readonly int RaytracingLightAngleId = Shader.PropertyToID("_RaytracingLightAngle");
        private static readonly int CameraFovId = Shader.PropertyToID("_CameraFOV");
        private static readonly int DenoiserFilterRadiusId = Shader.PropertyToID("_DenoiserFilterRadius");

        [Read(BuiltinTexture.CameraDepthTexture)]
        public TextureSlot depth = TextureSlot.Read("Depth", BuiltinTexture.CameraDepthTexture);

        [Read(BuiltinTexture.CameraNormals)]
        public TextureSlot normals = TextureSlot.Read("Normals", BuiltinTexture.CameraNormals);

        private TsukuyomiPipelineProfile _profile;
        private TsukuyomiContactShadowResolvedSettings _settings;
        private ComputeShader _computeShader;
        private int _horizontalKernel = -1;
        private int _verticalKernel = -1;
        private static readonly ProfilingSampler _profilingSampler = new("Diffuse Shadow Denoise");

        public override void CollectTextureSlots(System.Collections.Generic.List<TextureSlot> slots)
        {
            slots.Add(depth);
            slots.Add(normals);
        }

        public override string Name => "Diffuse Shadow Denoise";

        public bool Configure(TsukuyomiPipelineProfile profile, TsukuyomiContactShadowVolume volume)
        {
            _profile = profile;

            if (profile == null)
                return false;

            _settings = TsukuyomiContactShadowResolvedSettings.From(profile, volume);

            if (!_settings.Enabled || _settings.Denoiser != TsukuyomiShadowDenoiser.Spatial)
                return false;

            if (!TsukuyomiRenderPipelineResourcesProvider.TryGet(out TsukuyomiRenderPipelineResources resources))
                return false;

            _computeShader = resources.ContactShadowDenoiserComputeShader;
            if (_computeShader == null)
            {
                Debug.LogError("Tsukuyomi Contact Shadow spatial denoise requires a denoiser compute shader in TsukuyomiRenderPipelineResources.");
                return false;
            }

            _horizontalKernel = _computeShader.FindKernel(HorizontalKernelName);
            _verticalKernel = _computeShader.FindKernel(VerticalKernelName);
            return _horizontalKernel >= 0 && _verticalKernel >= 0;
        }

        public override bool IsActive(in FrameContext frame)
        {
            return base.IsActive(frame)
                && _profile != null
                && _settings.Enabled
                && _settings.Denoiser == TsukuyomiShadowDenoiser.Spatial
                && _computeShader != null
                && _horizontalKernel >= 0
                && _verticalKernel >= 0;
        }

        private sealed class RenderData
        {
            public TextureHandle DepthTexture;
            public TextureHandle NormalsTexture;
            public TextureHandle NoisyMap;
            public TextureHandle Intermediate;
            public TextureHandle Output;
            public ComputeShader ComputeShader;
            public int HorizontalKernel;
            public int VerticalKernel;
            public int Width;
            public int Height;
            public int FilterRadius;
            public float CameraFov;
            public float LightAngle;
        }

        public override void Record(in ComputePassContext context)
        {
            var passData = context.GetOrCreateData<RenderData>();
            if (_profile == null || !_settings.Enabled || _settings.Denoiser != TsukuyomiShadowDenoiser.Spatial)
                return;

            TextureHandle depthTexture = context.GetTexture(depth);
            TextureHandle normalsTexture = context.GetTexture(normals);
            if (!depthTexture.IsValid() || !normalsTexture.IsValid())
                return;

            TextureDesc contactDesc = TsukuyomiContactShadowPass.CreateContactShadowDesc(context.CameraData.cameraTargetDescriptor);
            TextureSlot noisySlot = TextureSlot.Read(TsukuyomiContactShadowResources.ContactShadowMap, contactDesc);
            TextureHandle noisyMap = context.GetTexture(noisySlot);
            if (!noisyMap.IsValid())
                return;

            TextureDesc denoiseDesc = CreateDenoiseDesc(context.CameraData.cameraTargetDescriptor);
            TextureSlot intermediateSlot = TextureSlot.Write(TsukuyomiContactShadowResources.ContactShadowDenoiseIntermediate, denoiseDesc);
            TextureSlot outputSlot = TextureSlot.Write(TsukuyomiContactShadowResources.ContactShadowDenoisedMap, denoiseDesc);
            TextureHandle intermediate = context.GetTexture(intermediateSlot);
            TextureHandle output = context.GetTexture(outputSlot);
            if (!intermediate.IsValid() || !output.IsValid())
                return;

            context.BindTexture(depthTexture, depth);
            context.BindTexture(normalsTexture, normals);
            context.BindTexture(noisyMap, noisySlot);
            context.BindTexture(intermediate, intermediateSlot);
            context.BindTexture(output, outputSlot);

            passData.ComputeShader = _computeShader;
            passData.HorizontalKernel = _horizontalKernel;
            passData.VerticalKernel = _verticalKernel;
            passData.Width = context.CameraData.cameraTargetDescriptor.width;
            passData.Height = context.CameraData.cameraTargetDescriptor.height;
            passData.FilterRadius = Mathf.Clamp(_settings.FilterSize, 1, 32);
            passData.CameraFov = context.CameraData.camera.fieldOfView * Mathf.Deg2Rad;
            passData.LightAngle = 2.5f * Mathf.Deg2Rad;

            passData.DepthTexture = depthTexture;
            passData.NormalsTexture = normalsTexture;
            passData.NoisyMap = noisyMap;
            passData.Intermediate = intermediate;
            passData.Output = output;

            context.SetRenderFunc(passData, static (state, graphContext) =>
            {
                int dispatchX = Mathf.CeilToInt(state.Width / (float)TileSize);
                int dispatchY = Mathf.CeilToInt(state.Height / (float)TileSize);

                graphContext.cmd.SetComputeFloatParam(state.ComputeShader, RaytracingLightAngleId, state.LightAngle);
                graphContext.cmd.SetComputeFloatParam(state.ComputeShader, CameraFovId, state.CameraFov);
                graphContext.cmd.SetComputeIntParam(state.ComputeShader, DenoiserFilterRadiusId, state.FilterRadius);

                graphContext.cmd.SetComputeTextureParam(state.ComputeShader, state.HorizontalKernel, DepthTextureId, state.DepthTexture);
                graphContext.cmd.SetComputeTextureParam(state.ComputeShader, state.HorizontalKernel, NormalBufferTextureId, state.NormalsTexture);
                graphContext.cmd.SetComputeTextureParam(state.ComputeShader, state.HorizontalKernel, DenoiseInputTextureId, state.NoisyMap);
                graphContext.cmd.SetComputeTextureParam(state.ComputeShader, state.HorizontalKernel, DenoiseOutputTextureRwId, state.Intermediate);
                graphContext.cmd.DispatchCompute(state.ComputeShader, state.HorizontalKernel, dispatchX, dispatchY, 1);

                graphContext.cmd.SetComputeTextureParam(state.ComputeShader, state.VerticalKernel, DepthTextureId, state.DepthTexture);
                graphContext.cmd.SetComputeTextureParam(state.ComputeShader, state.VerticalKernel, NormalBufferTextureId, state.NormalsTexture);
                graphContext.cmd.SetComputeTextureParam(state.ComputeShader, state.VerticalKernel, DenoiseInputTextureId, state.Intermediate);
                graphContext.cmd.SetComputeTextureParam(state.ComputeShader, state.VerticalKernel, DenoiseOutputTextureRwId, state.Output);
                graphContext.cmd.DispatchCompute(state.ComputeShader, state.VerticalKernel, dispatchX, dispatchY, 1);
            }, _profilingSampler);
        }

        internal static TextureDesc CreateDenoiseDesc(RenderTextureDescriptor cameraDescriptor)
        {
            GraphicsFormat format = SystemInfo.IsFormatSupported(GraphicsFormat.R16_SFloat, GraphicsFormatUsage.Linear | GraphicsFormatUsage.Render)
                ? GraphicsFormat.R16_SFloat
                : GraphicsFormat.R16G16B16A16_SFloat;

            return TextureDescriptors.Color2D(cameraDescriptor.width, cameraDescriptor.height,
                format, randomWrite: true);
        }
    }
}
