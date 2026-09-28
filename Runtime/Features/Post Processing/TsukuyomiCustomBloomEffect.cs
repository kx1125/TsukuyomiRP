using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering
{
    internal sealed class TsukuyomiCustomBloomEffect : TsukuyomiPostProcessEffect
    {
        private const int Iterations = 3;
        private const int PrefilterPass = 1;
        private const int DownsamplePass = 2;
        private const int PreBlurPass = 3;
        private const int FirstMipBlurPass = 4;
        private const int CombinePass = 7;
        private const string BloomKeyword = "_TSUKUYOMI_CUSTOM_BLOOM";

        private static readonly int BloomParamsId = Shader.PropertyToID("_TsukuyomiBloomParams");
        private static readonly int BloomColorTintId = Shader.PropertyToID("_TsukuyomiBloomColorTint");
        private static readonly int BloomBlurScalerId = Shader.PropertyToID("_TsukuyomiBloomBlurScaler");
        private static readonly int BloomBlurCompositeWeightId = Shader.PropertyToID("_TsukuyomiBloomBlurCompositeWeight");
        private static readonly int BloomTextureId = Shader.PropertyToID("_TsukuyomiBloomTexture");
        private static readonly int[] BloomMipDownIds =
        {
            Shader.PropertyToID("_TsukuyomiBloomMipDown0"),
            Shader.PropertyToID("_TsukuyomiBloomMipDown1"),
            Shader.PropertyToID("_TsukuyomiBloomMipDown2")
        };

        private readonly ProfilingSampler _sampler = new("Custom Bloom");
        private TsukuyomiCustomBloomResolvedSettings _settings;

        public override string Name => "Custom Bloom";

        public override bool Configure(
            TsukuyomiPipelineProfile profile,
            VolumeStack volumeStack,
            TsukuyomiRenderPipelineResources resources)
        {
            TsukuyomiCustomBloomVolume volume = volumeStack?.GetComponent<TsukuyomiCustomBloomVolume>();
            _settings = TsukuyomiCustomBloomResolvedSettings.From(profile, volume);
            return _settings.IsActive;
        }

        public override void ResetUberMaterial(Material material)
        {
            material.DisableKeyword(BloomKeyword);
        }

        // One instance per pending graph pass; mip arrays are allocated once.
        private sealed class RenderData
        {
            public TextureHandle Source, Prefilter, PrefilterBlur;
            public Material Material;
            public TsukuyomiCustomBloomResolvedSettings Settings;
            public readonly TextureHandle[] MipUp = new TextureHandle[Iterations];
            public readonly TextureHandle[] MipDown = new TextureHandle[Iterations];
            public void Execute(UnsafeGraphContext graphContext)
            {
                Vector4 scaleBias = new(1.0f, 1.0f, 0.0f, 0.0f);

                Material.SetVector(BloomParamsId, Settings.Params);
                Material.SetVector(BloomBlurCompositeWeightId, Settings.BlurCompositeWeight);
                Material.SetColor(BloomColorTintId, Settings.Tint);

                graphContext.cmd.SetRenderTarget(Prefilter, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                Blitter.BlitTexture(graphContext.cmd, Source, scaleBias, Material, PrefilterPass);

                graphContext.cmd.SetGlobalVector(BloomBlurScalerId, new Vector4(1.0f, 0.0f, 0.0f, 0.0f));
                graphContext.cmd.SetRenderTarget(PrefilterBlur, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                Blitter.BlitTexture(graphContext.cmd, Prefilter, scaleBias, Material, PreBlurPass);

                graphContext.cmd.SetGlobalVector(BloomBlurScalerId, new Vector4(0.0f, 1.0f, 0.0f, 0.0f));
                graphContext.cmd.SetRenderTarget(Prefilter, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                Blitter.BlitTexture(graphContext.cmd, PrefilterBlur, scaleBias, Material, PreBlurPass);

                TextureHandle last = Prefilter;
                for (int level = 0; level < Iterations; level++)
                {
                    graphContext.cmd.SetRenderTarget(MipDown[level], RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                    Blitter.BlitTexture(graphContext.cmd, last, scaleBias, Material, DownsamplePass);
                    last = MipDown[level];
                }

                for (int level = 0; level < Iterations; level++)
                {
                    int passIndex = FirstMipBlurPass + level;
                    graphContext.cmd.SetGlobalVector(BloomBlurScalerId, new Vector4(1.0f, 0.0f, 0.0f, 0.0f));
                    graphContext.cmd.SetRenderTarget(MipUp[level], RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                    Blitter.BlitTexture(graphContext.cmd, MipDown[level], scaleBias, Material, passIndex);

                    graphContext.cmd.SetGlobalVector(BloomBlurScalerId, new Vector4(0.0f, 1.0f, 0.0f, 0.0f));
                    graphContext.cmd.SetRenderTarget(MipDown[level], RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                    Blitter.BlitTexture(graphContext.cmd, MipUp[level], scaleBias, Material, passIndex);
                }

                for (int level = 0; level < Iterations; level++)
                    graphContext.cmd.SetGlobalTexture(BloomMipDownIds[level], MipDown[level]);

                graphContext.cmd.SetRenderTarget(PrefilterBlur, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                Blitter.BlitTexture(graphContext.cmd, Prefilter, scaleBias, Material, CombinePass);
            }

        }

        private sealed class UberData : TsukuyomiPostProcessData
        {
            public TextureHandle Output;
            public TsukuyomiCustomBloomResolvedSettings Settings;
            public override void SetupUber(UnsafeGraphContext graphContext, Material material)
            {
                graphContext.cmd.SetGlobalTexture(BloomTextureId, Output);
                material.SetVector(BloomParamsId, Settings.Params);
                material.EnableKeyword(BloomKeyword);
            }
        }

        private static readonly string[] MipUpNames =
            { "_TsukuyomiBloomMipUp0", "_TsukuyomiBloomMipUp1", "_TsukuyomiBloomMipUp2" };
        private static readonly string[] MipDownNames =
            { "_TsukuyomiBloomMipDown0", "_TsukuyomiBloomMipDown1", "_TsukuyomiBloomMipDown2" };

        public override void Record(in TsukuyomiPostProcessBuildContext context)
        {
            if (!context.EffectOutput.IsValid()) return;
            var data = context.GetOrCreateData<UberData>();
            data.Output = context.EffectOutput;
            data.Settings = _settings;
            context.AddUberSetup(data);
        }

        private static TextureDesc CreateColorDesc(RenderTextureDescriptor camera, int width, int height, string name)
        {
            var format = camera.graphicsFormat == UnityEngine.Experimental.Rendering.GraphicsFormat.None
                ? UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat : camera.graphicsFormat;
            return TextureDescriptors.Color2D(width, height, format, name, FilterMode.Bilinear);
        }

        public override TextureHandle RecordGraph(in FeatureGraphContext graph, TextureHandle source, Material material)
        {
            if (!source.IsValid() || material == null) return TextureHandle.nullHandle;
            using var node = graph.AddUnsafe<RenderData>(Name, _sampler);
            var data = node.Data;
            node.ReadTexture(source);
            node.Builder.AllowGlobalStateModification(true);
            var resources = node.Resources;
            RenderTextureDescriptor cameraDescriptor = graph.CameraData.cameraTargetDescriptor;
            int width = Mathf.Max(1, cameraDescriptor.width / 4);
            int height = Mathf.Max(1, cameraDescriptor.height / 4);

            TextureHandle prefilter = resources.CreateTexture(CreateColorDesc(graph.CameraData.cameraTargetDescriptor, width, height, "_TsukuyomiBloomPrefilter"), AccessFlags.ReadWrite);
            TextureHandle prefilterBlur = resources.CreateTexture(CreateColorDesc(graph.CameraData.cameraTargetDescriptor, width, height, "_TsukuyomiBloomPrefilterBlur"), AccessFlags.ReadWrite);
            TextureHandle[] mipUp = data.MipUp;
            TextureHandle[] mipDown = data.MipDown;

            int mipWidth = width;
            int mipHeight = height;
            for (int level = 0; level < Iterations; level++)
            {
                mipWidth = Mathf.Max(1, mipWidth / 2);
                mipHeight = Mathf.Max(1, mipHeight / 2);
                mipUp[level] = resources.CreateTexture(CreateColorDesc(graph.CameraData.cameraTargetDescriptor, mipWidth, mipHeight, MipUpNames[level]), AccessFlags.ReadWrite);
                mipDown[level] = resources.CreateTexture(CreateColorDesc(graph.CameraData.cameraTargetDescriptor, mipWidth, mipHeight, MipDownNames[level]), AccessFlags.ReadWrite);
            }

            data.Source = source;
            data.Material = material;
            data.Settings = _settings;
            data.Prefilter = prefilter;
            data.PrefilterBlur = prefilterBlur;
            node.SetRenderFunc(static (state, context) => state.Execute(context));
            return prefilterBlur;
        }
    }
}
