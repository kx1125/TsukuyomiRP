using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Tsukuyomi.Rendering
{
    [System.Serializable]
    internal sealed class TsukuyomiContactShadowPass : ComputePass
    {
        private const int TileSize = 8;
        private const string KernelName = "ContactShadowMap";

        private static readonly int CameraDepthTextureId = Shader.PropertyToID("_CameraDepthTexture");
        private static readonly int ContactShadowTextureUavId = Shader.PropertyToID("_ContactShadowTextureUAV");
        private static readonly int ContactShadowParams1Id = Shader.PropertyToID("_ContactShadowParamsParameters");
        private static readonly int ContactShadowParams2Id = Shader.PropertyToID("_ContactShadowParamsParameters2");
        private static readonly int ContactShadowParams3Id = Shader.PropertyToID("_ContactShadowParamsParameters3");

        [Read(BuiltinTexture.CameraDepthTexture)]
        public TextureSlot depth = TextureSlot.Read("Depth", BuiltinTexture.CameraDepthTexture);

        private TsukuyomiPipelineProfile _profile;
        private TsukuyomiContactShadowResolvedSettings _settings;
        private ComputeShader _computeShader;
        private int _kernel = -1;
        private static readonly ProfilingSampler _profilingSampler = new("Contact Shadow");

        public override void CollectTextureSlots(System.Collections.Generic.List<TextureSlot> slots)
        {
            slots.Add(depth);
        }

        public override string Name => "Contact Shadow";

        public bool Configure(TsukuyomiPipelineProfile profile, TsukuyomiContactShadowVolume volume)
        {
            _profile = profile;

            if (profile == null)
                return false;

            _settings = TsukuyomiContactShadowResolvedSettings.From(profile, volume);

            if (!_settings.Enabled)
                return false;

            if (!TsukuyomiRenderPipelineResourcesProvider.TryGet(out TsukuyomiRenderPipelineResources resources))
                return false;

            _computeShader = resources.ContactShadowsComputeShader;
            if (_computeShader == null)
            {
                Debug.LogError("Tsukuyomi Contact Shadow requires a ContactShadow compute shader in TsukuyomiRenderPipelineResources.");
                return false;
            }

            _kernel = _computeShader.FindKernel(KernelName);
            return _kernel >= 0;
        }

        public override bool IsActive(in FrameContext frame)
        {
            return base.IsActive(frame) && _profile != null && _settings.Enabled && _computeShader != null && _kernel >= 0;
        }

        private sealed class RenderData
        {
            public TextureHandle DepthTexture;
            public TextureHandle ContactShadowMap;
            public ComputeShader ComputeShader;
            public int Kernel;
            public int Width;
            public int Height;
            public TsukuyomiContactShadowSettings Settings;
        }

        public override void Record(in ComputePassContext context)
        {
            var passData = context.GetOrCreateData<RenderData>();
            if (_profile == null || !_settings.Enabled || _computeShader == null)
                return;

            TextureHandle depthTexture = context.ReadTexture(depth);
            if (!depthTexture.IsValid())
                return;

            if (context.CameraData.isPreviewCamera || context.LightData.mainLightIndex < 0)
                return;

            TextureDesc desc = CreateContactShadowDesc(context.CameraData.cameraTargetDescriptor);
            TextureSlot outputSlot = TextureSlot.Write(TsukuyomiContactShadowResources.ContactShadowMap, desc);
            TextureHandle contactShadowMap = context.GraphResources.UseTexture(outputSlot, AccessFlags.Write);
            if (!contactShadowMap.IsValid())
                return;


            passData.ComputeShader = _computeShader;
            passData.Kernel = _kernel;
            passData.Width = context.CameraData.cameraTargetDescriptor.width;
            passData.Height = context.CameraData.cameraTargetDescriptor.height;
            passData.Settings = TsukuyomiContactShadowSettings.FromResolved(_settings);

            passData.DepthTexture = depthTexture;
            passData.ContactShadowMap = contactShadowMap;

            context.SetRenderFunc(passData, static (state, graphContext) =>
            {
                graphContext.cmd.SetComputeVectorParam(state.ComputeShader, ContactShadowParams1Id, state.Settings.Params1);
                graphContext.cmd.SetComputeVectorParam(state.ComputeShader, ContactShadowParams2Id, state.Settings.Params2);
                graphContext.cmd.SetComputeVectorParam(state.ComputeShader, ContactShadowParams3Id, state.Settings.Params3);
                graphContext.cmd.SetComputeTextureParam(state.ComputeShader, state.Kernel, CameraDepthTextureId, state.DepthTexture);
                graphContext.cmd.SetComputeTextureParam(state.ComputeShader, state.Kernel, ContactShadowTextureUavId, state.ContactShadowMap);
                graphContext.cmd.DispatchCompute(state.ComputeShader, state.Kernel, Mathf.CeilToInt(state.Width / (float)TileSize), Mathf.CeilToInt(state.Height / (float)TileSize), 1);
            }, _profilingSampler);
        }

        internal static TextureDesc CreateContactShadowDesc(RenderTextureDescriptor cameraDescriptor)
        {
            GraphicsFormat format = SystemInfo.IsFormatSupported(GraphicsFormat.R8_UNorm, GraphicsFormatUsage.Linear | GraphicsFormatUsage.Render)
                ? GraphicsFormat.R8_UNorm
                : GraphicsFormat.B8G8R8A8_UNorm;

            return TextureDescriptors.Color2D(cameraDescriptor.width, cameraDescriptor.height,
                format, randomWrite: true);
        }

        internal readonly struct TsukuyomiContactShadowSettings
        {
            public readonly Vector4 Params1;
            public readonly Vector4 Params2;
            public readonly Vector4 Params3;

            private TsukuyomiContactShadowSettings(TsukuyomiContactShadowResolvedSettings settings)
            {
                float maxDistance = Mathf.Max(0.0f, settings.MaxDistance);
                float fadeDistance = Mathf.Clamp(settings.FadeDistance, 0.0f, maxDistance);
                float fadeOneOverRange = 1.0f / Mathf.Max(1e-6f, fadeDistance);
                float minDistance = Mathf.Min(Mathf.Max(0.0f, settings.MinDistance), maxDistance);
                float fadeInDistance = Mathf.Clamp(settings.FadeInDistance, 1e-6f, Mathf.Max(1e-6f, maxDistance));

                Params1 = new Vector4(
                    Mathf.Clamp01(settings.Length),
                    Mathf.Clamp01(settings.DistanceScaleFactor),
                    maxDistance,
                    fadeOneOverRange);

                Params2 = new Vector4(
                    0.0f,
                    minDistance,
                    fadeInDistance,
                    Mathf.Clamp01(settings.RayBias) * 0.01f);

                Params3 = new Vector4(
                    Mathf.Clamp(settings.SampleCount, 4, 64),
                    Mathf.Clamp(settings.ThicknessScale, 0.02f, 10.0f) * 10.0f,
                    Time.renderedFrameCount % 8,
                    0.0f);
            }

            public static TsukuyomiContactShadowSettings FromResolved(TsukuyomiContactShadowResolvedSettings settings)
            {
                return new TsukuyomiContactShadowSettings(settings);
            }
        }
    }
}
