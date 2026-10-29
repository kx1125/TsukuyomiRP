using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace Tsukuyomi.Rendering
{
    internal sealed class TsukuyomiMultiScaleAmbientOcclusionPass : UnsafePass
    {
        private readonly MsvoPass _defaults;
        private readonly TsukuyomiMultiScaleAmbientOcclusionCompositePass _composite;
        private readonly TsukuyomiMultiScaleAmbientOcclusionRestoreKeywordsPass _restore = new();
        internal bool Prepared { get; private set; }
        private bool _compositeReady;
        public TsukuyomiMultiScaleAmbientOcclusionPass(MsvoPass settings)
        { _defaults = settings; _composite = new(this); }
        public override void Build(PassPlanBuilder plan)
        {
            var main = plan.Add(this, RenderPassEvent.BeforeRenderingTransparents - 1, () => Prepared,
                _ => Settings.IsLighting ? RenderPassEvent.AfterRenderingPrePasses + 2 : RenderPassEvent.BeforeRenderingTransparents - 1);
            plan.After(plan.Add(_composite, RenderPassEvent.BeforeRenderingTransparents - 1,
                () => Prepared && _compositeReady && (!Settings.IsLighting || Settings.DebugOcclusion)), main);
            plan.After(plan.Add(_restore, RenderPassEvent.BeforeRenderingPostProcessing - 1,
                () => Prepared && Settings.IsLighting), main);
        }
        public override void Initialize() => InitializeKeywords();
        public override void PrepareRecording(in PassCameraContext context, ref RenderingData data)
        {
            Prepared = Configure(_defaults, context.Volumes?.GetComponent<TsukuyomiMultiScaleAmbientOcclusionVolume>())
                && IsActive(context.Frame);
            _compositeReady = Prepared && _composite.Configure();
        }
        public override void Deactivate(Camera camera)
        {
            Prepared = false;
            Shader.DisableKeyword("_SCREEN_SPACE_OCCLUSION");
            Shader.SetGlobalTexture("_ScreenSpaceOcclusionTexture", Texture2D.whiteTexture);
        }

        internal const string OutputName = "Tsukuyomi MSVO Result";
        internal TsukuyomiMultiScaleAmbientOcclusionResolvedSettings Settings { get; private set; }
        private ComputeShader _downsample1, _downsample2, _render, _upsample;
        private readonly TextureSlot _depth = TextureSlot.Read("Depth", BuiltinTexture.CameraDepthTexture);
        private static readonly int ScreenSpaceOcclusion = Shader.PropertyToID("_ScreenSpaceOcclusionTexture");
        private static readonly int AmbientOcclusionParam = Shader.PropertyToID("_AmbientOcclusionParam");
        private static readonly ProfilingSampler Sampler = new("Multi Scale Volumetric Occlusion");
        public override string Name => "Multi Scale Volumetric Occlusion";

        private static GlobalKeyword s_ScreenSpaceOcclusionKeyword;
        private static bool s_KeywordsInitialized;
        internal static GlobalKeyword ScreenSpaceOcclusionGlobalKeyword => s_ScreenSpaceOcclusionKeyword;
        internal static void InitializeKeywords()
        {
            if (s_KeywordsInitialized) return;
            s_ScreenSpaceOcclusionKeyword = GlobalKeyword.Create("_SCREEN_SPACE_OCCLUSION");
            s_KeywordsInitialized = true;
        }

        public bool Configure(MsvoPass profile, TsukuyomiMultiScaleAmbientOcclusionVolume volume)
        {
            if (profile == null) return false;
            Settings = TsukuyomiMultiScaleAmbientOcclusionResolvedSettings.From(profile, volume);
            return ConfigureResources();
        }

        private bool ConfigureResources()
        {
            if (!Settings.IsActive || !SystemInfo.supportsComputeShaders
                || !TsukuyomiRenderPipelineResourcesProvider.TryGet(out var resources)) return false;
            _downsample1 = resources.MsvoDownsample1;
            _downsample2 = resources.MsvoDownsample2;
            _render = resources.MsvoRender;
            _upsample = resources.MsvoUpsample;
            return _downsample1 && _downsample2 && _render && _upsample;
        }

        public override void CollectTextureSlots(List<TextureSlot> slots) => slots.Add(_depth);
        public override bool IsActive(in FrameContext frame)
            => base.IsActive(frame) && Settings.IsActive && _downsample1 && _downsample2 && _render && _upsample
                && frame.CameraData.cameraType != CameraType.Preview && frame.CameraData.cameraType != CameraType.Reflection
                && frame.CameraData.cameraTargetDescriptor.dimension == TextureDimension.Tex2D;

        private sealed class RenderData
        {
            public readonly TsukuyomiMultiScaleAmbientOcclusionRenderer.Buffers Buffers = new();
            public TextureHandle Depth, Output;
            public ComputeShader Downsample1, Downsample2, Render, Upsample;
            public Vector4 ZBufferParams;
            public float TanHalfFov;
            public TsukuyomiMultiScaleAmbientOcclusionResolvedSettings Settings;
        }

        public override void Record(in UnsafePassContext context)
        {
            var camera = context.CameraData.camera;
            TextureHandle depth = context.GetTexture(_depth);
            if (!depth.IsValid() || !camera) return;
            var state = context.GetOrCreateData<RenderData>();
            var descriptor = context.CameraData.cameraTargetDescriptor;
            state.Settings = Settings;
            state.Depth = context.ReadTexture(depth);
            state.Downsample1 = _downsample1; state.Downsample2 = _downsample2;
            state.Render = _render; state.Upsample = _upsample;
            float ratio = camera.farClipPlane / camera.nearClipPlane;
            state.ZBufferParams = SystemInfo.usesReversedZBuffer ? new Vector4(ratio - 1, 1, 0, 0) : new Vector4(1 - ratio, ratio, 0, 0);
            state.TanHalfFov = 1f / context.CameraData.GetProjectionMatrix()[0, 0];
            TsukuyomiMultiScaleAmbientOcclusionRenderer.Allocate(context, state.Buffers, descriptor.width, descriptor.height, Settings.ScaleCount);
            state.Output = context.Resources.GetOrCreate(context.RenderGraph, OutputName,
                TextureDescriptors.Color2D(descriptor.width, descriptor.height, GraphicsFormat.R8_UNorm,
                    OutputName, FilterMode.Bilinear, randomWrite: true));
            context.Builder.UseTexture(state.Output, AccessFlags.ReadWrite);
            if (Settings.IsLighting)
            {
                TsukuyomiMultiScaleAmbientOcclusionPass.InitializeKeywords();
                context.Builder.SetGlobalTextureAfterPass(state.Output, ScreenSpaceOcclusion);
                context.FrameData.Get<UnityEngine.Rendering.Universal.UniversalResourceData>().ssaoTexture = state.Output;
            }
            context.Builder.AllowGlobalStateModification(true);
            context.Builder.AllowPassCulling(false);
            context.SetRenderFunc(state, static (data, graphContext) =>
            {
                var cmd = CommandBufferHelpers.GetNativeCommandBuffer(graphContext.cmd);
                TsukuyomiMultiScaleAmbientOcclusionRenderer.GenerateAOMap(cmd, data.Buffers, data.Depth, data.Output,
                    data.Downsample1, data.Downsample2, data.Render, data.Upsample, data.ZBufferParams, data.TanHalfFov, data.Settings);
                if (data.Settings.IsLighting)
                {
                    graphContext.cmd.SetKeyword(TsukuyomiMultiScaleAmbientOcclusionPass.ScreenSpaceOcclusionGlobalKeyword, true);
                    cmd.SetGlobalVector(AmbientOcclusionParam, new Vector4(1, 0, 0, data.Settings.DirectLightingStrength));
                }
            }, Sampler);
        }
    }
}
