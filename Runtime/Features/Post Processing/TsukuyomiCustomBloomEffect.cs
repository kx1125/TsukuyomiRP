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

        public override void Record(in TsukuyomiPostProcessBuildContext context)
        {
            RenderTextureDescriptor cameraDescriptor = context.CameraData.cameraTargetDescriptor;
            int width = Mathf.Max(1, cameraDescriptor.width / 4);
            int height = Mathf.Max(1, cameraDescriptor.height / 4);

            TextureHandle prefilter = context.CreateTexture(context.CreateColorDesc(width, height, "_TsukuyomiBloomPrefilter"));
            TextureHandle prefilterBlur = context.CreateTexture(context.CreateColorDesc(width, height, "_TsukuyomiBloomPrefilterBlur"));
            TextureHandle[] mipUp = new TextureHandle[Iterations];
            TextureHandle[] mipDown = new TextureHandle[Iterations];

            int mipWidth = width;
            int mipHeight = height;
            for (int level = 0; level < Iterations; level++)
            {
                mipWidth = Mathf.Max(1, mipWidth / 2);
                mipHeight = Mathf.Max(1, mipHeight / 2);
                mipUp[level] = context.CreateTexture(context.CreateColorDesc(mipWidth, mipHeight, $"_TsukuyomiBloomMipUp{level}"));
                mipDown[level] = context.CreateTexture(context.CreateColorDesc(mipWidth, mipHeight, $"_TsukuyomiBloomMipDown{level}"));
            }

            context.UseTexture(prefilter, AccessFlags.ReadWrite);
            context.UseTexture(prefilterBlur, AccessFlags.ReadWrite);
            for (int i = 0; i < Iterations; i++)
            {
                context.UseTexture(mipUp[i], AccessFlags.ReadWrite);
                context.UseTexture(mipDown[i], AccessFlags.ReadWrite);
            }

            TextureHandle source = context.SourceColor;
            Material material = context.UberMaterial;
            TsukuyomiCustomBloomResolvedSettings settings = _settings;

            context.AddStage(_sampler, graphContext =>
            {
                Vector4 scaleBias = new(1.0f, 1.0f, 0.0f, 0.0f);

                material.SetVector(BloomParamsId, settings.Params);
                material.SetVector(BloomBlurCompositeWeightId, settings.BlurCompositeWeight);
                material.SetColor(BloomColorTintId, settings.Tint);

                graphContext.cmd.SetRenderTarget(prefilter, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                Blitter.BlitTexture(graphContext.cmd, source, scaleBias, material, PrefilterPass);

                graphContext.cmd.SetGlobalVector(BloomBlurScalerId, new Vector4(1.0f, 0.0f, 0.0f, 0.0f));
                graphContext.cmd.SetRenderTarget(prefilterBlur, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                Blitter.BlitTexture(graphContext.cmd, prefilter, scaleBias, material, PreBlurPass);

                graphContext.cmd.SetGlobalVector(BloomBlurScalerId, new Vector4(0.0f, 1.0f, 0.0f, 0.0f));
                graphContext.cmd.SetRenderTarget(prefilter, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                Blitter.BlitTexture(graphContext.cmd, prefilterBlur, scaleBias, material, PreBlurPass);

                TextureHandle last = prefilter;
                for (int level = 0; level < Iterations; level++)
                {
                    graphContext.cmd.SetRenderTarget(mipDown[level], RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                    Blitter.BlitTexture(graphContext.cmd, last, scaleBias, material, DownsamplePass);
                    last = mipDown[level];
                }

                for (int level = 0; level < Iterations; level++)
                {
                    int passIndex = FirstMipBlurPass + level;
                    graphContext.cmd.SetGlobalVector(BloomBlurScalerId, new Vector4(1.0f, 0.0f, 0.0f, 0.0f));
                    graphContext.cmd.SetRenderTarget(mipUp[level], RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                    Blitter.BlitTexture(graphContext.cmd, mipDown[level], scaleBias, material, passIndex);

                    graphContext.cmd.SetGlobalVector(BloomBlurScalerId, new Vector4(0.0f, 1.0f, 0.0f, 0.0f));
                    graphContext.cmd.SetRenderTarget(mipDown[level], RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                    Blitter.BlitTexture(graphContext.cmd, mipUp[level], scaleBias, material, passIndex);
                }

                for (int level = 0; level < Iterations; level++)
                    graphContext.cmd.SetGlobalTexture(BloomMipDownIds[level], mipDown[level]);

                graphContext.cmd.SetRenderTarget(prefilterBlur, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                Blitter.BlitTexture(graphContext.cmd, prefilter, scaleBias, material, CombinePass);
            });

            context.AddUberSetup((graphContext, uberMaterial) =>
            {
                graphContext.cmd.SetGlobalTexture(BloomTextureId, prefilterBlur);
                uberMaterial.SetVector(BloomParamsId, settings.Params);
                uberMaterial.EnableKeyword(BloomKeyword);
            });
        }
    }
}
