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
        private readonly ProfilingSampler _profilingSampler = new("Diffuse Shadow Denoise");

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

        public override void Record(in ComputePassContext context)
        {
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

            ComputeShader computeShader = _computeShader;
            int horizontalKernel = _horizontalKernel;
            int verticalKernel = _verticalKernel;
            int width = context.CameraData.cameraTargetDescriptor.width;
            int height = context.CameraData.cameraTargetDescriptor.height;
            int filterRadius = Mathf.Clamp(_settings.FilterSize, 1, 32);
            float cameraFov = context.CameraData.camera.fieldOfView * Mathf.Deg2Rad;
            float lightAngle = 2.5f * Mathf.Deg2Rad;
            ProfilingSampler profilingSampler = _profilingSampler;

            context.SetRenderFunc((data, graphContext) =>
            {
                using (new ProfilingScope(graphContext.cmd, profilingSampler))
                {
                    int dispatchX = Mathf.CeilToInt(width / (float)TileSize);
                    int dispatchY = Mathf.CeilToInt(height / (float)TileSize);

                    graphContext.cmd.SetComputeFloatParam(computeShader, RaytracingLightAngleId, lightAngle);
                    graphContext.cmd.SetComputeFloatParam(computeShader, CameraFovId, cameraFov);
                    graphContext.cmd.SetComputeIntParam(computeShader, DenoiserFilterRadiusId, filterRadius);

                    graphContext.cmd.SetComputeTextureParam(computeShader, horizontalKernel, DepthTextureId, depthTexture);
                    graphContext.cmd.SetComputeTextureParam(computeShader, horizontalKernel, NormalBufferTextureId, normalsTexture);
                    graphContext.cmd.SetComputeTextureParam(computeShader, horizontalKernel, DenoiseInputTextureId, noisyMap);
                    graphContext.cmd.SetComputeTextureParam(computeShader, horizontalKernel, DenoiseOutputTextureRwId, intermediate);
                    graphContext.cmd.DispatchCompute(computeShader, horizontalKernel, dispatchX, dispatchY, 1);

                    graphContext.cmd.SetComputeTextureParam(computeShader, verticalKernel, DepthTextureId, depthTexture);
                    graphContext.cmd.SetComputeTextureParam(computeShader, verticalKernel, NormalBufferTextureId, normalsTexture);
                    graphContext.cmd.SetComputeTextureParam(computeShader, verticalKernel, DenoiseInputTextureId, intermediate);
                    graphContext.cmd.SetComputeTextureParam(computeShader, verticalKernel, DenoiseOutputTextureRwId, output);
                    graphContext.cmd.DispatchCompute(computeShader, verticalKernel, dispatchX, dispatchY, 1);
                }
            });
        }

        internal static TextureDesc CreateDenoiseDesc(RenderTextureDescriptor cameraDescriptor)
        {
            GraphicsFormat format = SystemInfo.IsFormatSupported(GraphicsFormat.R16_SFloat, FormatUsage.Linear | FormatUsage.Render)
                ? GraphicsFormat.R16_SFloat
                : GraphicsFormat.R16G16B16A16_SFloat;

            return new TextureDesc(cameraDescriptor.width, cameraDescriptor.height)
            {
                colorFormat = format,
                depthBufferBits = DepthBits.None,
                msaaSamples = MSAASamples.None,
                enableRandomWrite = true,
                clearBuffer = true,
                clearColor = Color.clear
            };
        }
    }
}
