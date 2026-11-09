using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Tsukuyomi.Rendering
{
    [Serializable]
    [InjectionPoint(InjectionPoint.BeforePostProcess)]
    public sealed class DiffusionPass : PostProcessPassDefinition
    {
        internal const float MinHighlightLimit = 0.01f;
        internal const string HighlightLimitTooltip = "Maximum linear RGB peak used by the blur. Highlights roll off above 75% of this value.";

        [Range(0f, 1f)]
        public float Intensity = 0.15f;

        [Tooltip("Kernel offset at 1080p. Below 1, the minimum blur fades smoothly to zero.")]
        [Range(0f, 4f)]
        public float Radius = 1f;

        [Tooltip(HighlightLimitTooltip)]
        [Min(MinHighlightLimit)]
        public float HighlightLimit = 1f;

        public override string Name => "Diffusion";

        public DiffusionPass()
        {
            InjectionPoint = InjectionPoint.BeforePostProcess;
        }

        internal override TsukuyomiPostProcessEffect CreateEffect()
        {
            return new TsukuyomiDiffusionEffect();
        }
    }

    internal sealed class TsukuyomiDiffusionEffect : TsukuyomiPostProcessEffect
    {
        private const string Keyword = "_TSUKUYOMI_DIFFUSION";

        private static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
        private static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
        private static readonly int RadiusId = Shader.PropertyToID("_TsukuyomiDiffusionRadius");
        private static readonly int HighlightLimitId = Shader.PropertyToID("_TsukuyomiDiffusionHighlightLimit");
        private static readonly int IntensityId = Shader.PropertyToID("_TsukuyomiDiffusionIntensity");
        private static readonly int TextureId = Shader.PropertyToID("_TsukuyomiDiffusionTexture");

        private float _intensity;
        private float _radius;
        private float _highlightLimit;
        private Material _prefilterMaterial;
        private int _prefilterPass;
        private int _downsamplePass;
        private int _upsamplePass;
        public override string Name => "Diffusion";

        internal bool ResolvePasses(Material material)
        {
            _downsamplePass = material.FindPass("Diffusion Downsample");
            _upsamplePass = material.FindPass("Diffusion Upsample");
            return _downsamplePass >= 0 && _upsamplePass >= 0;
        }

        internal bool ResolvePrefilter(Material material)
        {
            _prefilterMaterial = material;
            _prefilterPass = material.FindPass("Diffusion Prefilter");
            return _prefilterPass >= 0;
        }

        public override bool Configure(PostProcessPassDefinition profile, VolumeStack volumeStack,
            TsukuyomiRenderPipelineResources resources)
        {
            var defaults = (DiffusionPass)profile;
            var volume = volumeStack?.GetComponent<TsukuyomiDiffusionVolume>();
            bool enabled = volume?.enable.overrideState != true || volume.enable.value;
            _radius = Mathf.Clamp(volume?.radius.overrideState == true ? volume.radius.value : defaults.Radius, 0f, 4f);
            _intensity = Mathf.Clamp01(volume?.intensity.overrideState == true ? volume.intensity.value : defaults.Intensity);
            _highlightLimit = Mathf.Max(DiffusionPass.MinHighlightLimit,
                volume?.highlightLimit.overrideState == true ? volume.highlightLimit.value : defaults.HighlightLimit);
            // The two reductions have a minimum footprint. Fade that footprint continuously at small radii.
            _intensity *= Mathf.Min(1f, _radius);
            return defaults.Enabled && enabled && _intensity > 0f;
        }

        private sealed class BlurData
        {
            public TextureHandle Source, Half, Quarter, Output;
            public Material PrefilterMaterial, Material;
            public int Prefilter, Downsample, Upsample;
            public float Radius, HighlightLimit;
        }

        public override TextureHandle RecordGraph(in FeatureGraphContext graph, TextureHandle source, Material material)
        {
            var desc = TextureDescriptors.ColorLike(graph.RenderGraph.GetTextureDesc(source), "_TsukuyomiDiffusionHalf");
            Vector2Int size = ViewportSize(desc);
            desc.sizeMode = TextureSizeMode.Explicit;
            desc.width = Mathf.Max(1, (size.x + 1) / 2);
            desc.height = Mathf.Max(1, (size.y + 1) / 2);
            desc.filterMode = FilterMode.Bilinear;
            desc.wrapMode = TextureWrapMode.Clamp;
            var quarterDesc = desc;
            quarterDesc.name = "_TsukuyomiDiffusionQuarter";
            quarterDesc.width = (desc.width + 1) / 2;
            quarterDesc.height = (desc.height + 1) / 2;
            using var node = graph.AddUnsafe<BlurData>(Name);
            var data = node.Data;
            data.Source = node.ReadTexture(source);
            data.Half = node.Builder.CreateTransientTexture(desc);
            data.Quarter = node.Builder.CreateTransientTexture(quarterDesc);
            desc.name = "_TsukuyomiDiffusionBlur";
            data.Output = node.Resources.CreateTexture(desc, AccessFlags.WriteAll);
            data.PrefilterMaterial = _prefilterMaterial;
            data.Material = material;
            data.Prefilter = _prefilterPass;
            data.Downsample = _downsamplePass;
            data.Upsample = _upsamplePass;
            data.Radius = Mathf.Max(1f, _radius) * size.y / 1080f;
            data.HighlightLimit = _highlightLimit;
            node.SetRenderFunc(static (state, context) =>
            {
                DrawBlur(context, state.Source, state.Half, state.PrefilterMaterial, state.Prefilter, state.Radius, state.HighlightLimit);
                DrawBlur(context, state.Half, state.Quarter, state.Material, state.Downsample, state.Radius);
                DrawBlur(context, state.Quarter, state.Output, state.Material, state.Upsample, state.Radius);
            });
            return data.Output;
        }

        private static Vector2Int ViewportSize(in TextureDesc desc)
        {
            var viewport = RTHandles.rtHandleProperties.currentViewportSize;
            return desc.sizeMode switch
            {
                TextureSizeMode.Scale => Vector2Int.RoundToInt(Vector2.Scale(viewport, desc.scale)),
                TextureSizeMode.Functor => desc.func(viewport),
                _ => new Vector2Int(desc.width, desc.height)
            };
        }

        private static void DrawBlur(UnsafeGraphContext context, TextureHandle source,
            TextureHandle destination, Material material, int shaderPass, float radius, float highlightLimit = 1f)
        {
            RTHandle input = source;
            var scale = input.useScaling ? input.rtHandleProperties.rtHandleScale : Vector4.one;
            var properties = context.renderGraphPool.GetTempMaterialPropertyBlock();
            properties.SetTexture(BlitTextureId, input);
            properties.SetVector(BlitScaleBiasId, new Vector4(scale.x, scale.y, 0f, 0f));
            properties.SetFloat(RadiusId, radius);
            properties.SetFloat(HighlightLimitId, highlightLimit);
            var target = new RenderTargetIdentifier(destination, 0, CubemapFace.Unknown, -1);
            context.cmd.SetRenderTarget(target, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            context.cmd.DrawProcedural(Matrix4x4.identity, material, shaderPass, MeshTopology.Triangles, 3, 1, properties);
        }

        private sealed class UberData : TsukuyomiPostProcessData
        {
            public TextureHandle Output;
            public float Intensity;
            public override PostBlendKind Kind => PostBlendKind.Diffusion;

            public override void SetupUber(UnsafeGraphContext context, Material material)
            {
                context.cmd.SetGlobalTexture(TextureId, Output);
                material.SetFloat(IntensityId, Intensity);
                material.EnableKeyword(Keyword);
            }
        }

        public override void Record(in TsukuyomiPostProcessBuildContext context)
        {
            var data = context.GetOrCreateData<UberData>();
            data.Output = context.EffectOutput;
            data.Intensity = _intensity;
            context.AddUberSetup(data);
        }

        public override void ResetUberMaterial(Material material)
        {
            material.DisableKeyword(Keyword);
        }
    }
}
