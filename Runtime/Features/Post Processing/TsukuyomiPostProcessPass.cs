using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering
{
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Tsukuyomi.Rendering", "Tsukuyomi.Rendering", "PostProcessPlugin")]
    public abstract class PostProcessPassDefinition : RenderPassBase
    {
        public sealed override bool AllowMultiple => false;
        [NonSerialized] private TsukuyomiPostProcessEffect _effect;
        internal TsukuyomiPostProcessEffect Effect => _effect;
        [field: NonSerialized] internal bool Prepared { get; private set; }
        internal abstract TsukuyomiPostProcessEffect CreateEffect();

        public override void PrepareRecording(in PassCameraContext context, ref RenderingData data)
        {
            Prepared = context.AllowsPostProcessing
                && TsukuyomiRenderPipelineResourcesProvider.TryGet(out var resources)
                && _effect.Configure(this, context.Volumes, resources);
        }

        public override void Deactivate(Camera camera) => Prepared = false;
        public override void Dispose() { _effect?.Dispose(); _effect = null; }
        public override void Build(PassPlanBuilder plan)
        {
            _effect = CreateEffect();
            plan.Service("post/" + InjectionPoint, () => new TsukuyomiPostProcessPass()).Add(this);
        }
    }

    internal sealed class TsukuyomiPostProcessPass : GraphFeaturePass, IRenderPassService
    {
        [Read(BuiltinTexture.ActiveColor)]
        public TextureSlot color = TextureSlot.Read("Color", BuiltinTexture.ActiveColor);

        private TsukuyomiPostProcessEffect[] _effects = System.Array.Empty<TsukuyomiPostProcessEffect>();
        private readonly List<PostProcessPassDefinition> _definitions = new();

        private readonly List<TsukuyomiPostProcessEffect> _activeEffects = new();
        // Recording scratch only. Every value is copied to a native node before another camera records.
        private TextureHandle[] _effectOutputs;

        internal void Add(PostProcessPassDefinition definition) => _definitions.Add(definition);
        public override void Build(PassPlanBuilder plan)
        {
            _effects = new TsukuyomiPostProcessEffect[_definitions.Count];
            for (int i = 0; i < _effects.Length; i++) _effects[i] = _definitions[i].Effect;
            _effectOutputs = new TextureHandle[_effects.Length];
            // Resolve the final scene color after other Tsukuyomi passes at this injection point.
            var composite = plan.Add(this, PassPlanBuilder.EventFor(_definitions[0].InjectionPoint), () => Prepared);
            composite.Order = int.MaxValue;
        }
        private Material _uberMaterial;
        private Material _packageFallback;
        private bool _supportsOrderedComposite;

        public override void CollectTextureSlots(System.Collections.Generic.List<TextureSlot> slots)
        {
            slots.Add(color);
        }

        public override string Name => "Tsukuyomi Post Processing";

        internal bool Prepared { get; private set; }
        public override void PrepareRecording(in PassCameraContext context, ref RenderingData data)
            => Prepared = Configure();
        public override void Deactivate(Camera camera) => Prepared = false;
        private bool Configure()
        {
            _activeEffects.Clear();

            if (!TsukuyomiRenderPipelineResourcesProvider.TryGet(out TsukuyomiRenderPipelineResources resources))
                return false;

            for (int i = 0; i < _effects.Length; i++)
            {
                if (!_definitions[i].Prepared)
                    continue;
                if (!ResolveUberMaterial(resources))
                    return false;
                if (_effects[i] is TsukuyomiDiffusionEffect diffusion)
                {
                    if (!diffusion.ResolvePasses(_uberMaterial))
                        continue;
                    // Existing custom Uber shaders can use the package prefilter with their own blur kernels.
                    if (!diffusion.ResolvePrefilter(_uberMaterial) && !diffusion.ResolvePrefilter(GetPackageUberMaterial()))
                        continue;
                }

                _activeEffects.Add(_effects[i]);
            }

            return _activeEffects.Count > 0;
        }

        public override bool IsActive(in FrameContext frame)
        {
            return base.IsActive(frame)
                && _uberMaterial != null
                && _activeEffects.Count > 0;
        }

        public override void Dispose()
        {
            CoreUtils.Destroy(_uberMaterial);
            CoreUtils.Destroy(_packageFallback);
            _uberMaterial = null;
            _packageFallback = null;
        }

        private sealed class RenderData
        {
            public TextureHandle Source;
            public TextureHandle Destination;
            public readonly TsukuyomiPostProcessPlan Plan = new();
            public Material UberMaterial;
            public TsukuyomiPostProcessEffect[] Effects;
        }

        public override void RecordGraph(in FeatureGraphContext context)
        {
            if (_uberMaterial == null || context.CameraData.isPreviewCamera || _activeEffects.Count == 0)
                return;

            TextureHandle source = context.GetTexture(color);
            if (!source.IsValid())
                return;

            TextureHandle destination = PassRecorder.CreateTextureLike(context.RenderGraph, source, "_TsukuyomiPostProcessColor");
            if (!destination.IsValid())
                return;

            for (int i = 0; i < _activeEffects.Count; i++)
                _effectOutputs[i] = _activeEffects[i].RecordGraph(context, source, _uberMaterial);

            using (var node = context.AddUnsafe<RenderData>(Name))
            {
                var passData = node.Data;
                node.Builder.UseTexture(source, AccessFlags.Read);
                node.Builder.UseTexture(destination, AccessFlags.Write);
                node.Builder.AllowGlobalStateModification(true);

                TsukuyomiPostProcessPlan plan = passData.Plan;
                plan.Clear();
                for (int i = 0; i < _activeEffects.Count; i++)
                {
                    node.ReadTexture(_effectOutputs[i]);
                    var buildContext = new TsukuyomiPostProcessBuildContext(context.RenderGraph, node.Builder,
                        context.CameraData, source, destination, _uberMaterial, plan, _effectOutputs[i]);
                    _activeEffects[i].Record(buildContext);
                    _effectOutputs[i] = TextureHandle.nullHandle;
                }

                passData.UberMaterial = ResolveCompositeMaterial(plan);
                passData.Effects = _effects;

                passData.Source = source;
                passData.Destination = destination;

                node.SetRenderFunc(static (state, graphContext) =>
                {
                    for (int i = 0; i < state.Effects.Length; i++)
                        state.Effects[i].ResetUberMaterial(state.UberMaterial);

                    state.Plan.SetupUberMaterial(graphContext, state.UberMaterial);

                    graphContext.cmd.SetRenderTarget(state.Destination, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                    Blitter.BlitTexture(graphContext.cmd, state.Source, new Vector4(1.0f, 1.0f, 0.0f, 0.0f), state.UberMaterial, 0);
                });
            }

            PassRecorder.SwapActiveColor(context.Resources, destination);
        }

        private bool ResolveUberMaterial(TsukuyomiRenderPipelineResources resources)
        {
            if (_uberMaterial != null)
                return true;

            if (resources.PostProcessUberMaterial != null)
            {
                _uberMaterial = new Material(resources.PostProcessUberMaterial) { hideFlags = HideFlags.HideAndDontSave };
            }
            else if (resources.PostProcessUberShader != null)
            {
                _uberMaterial = CoreUtils.CreateEngineMaterial(resources.PostProcessUberShader);
            }
            else
            {
                Debug.LogError("Tsukuyomi Post Processing requires an Uber shader or material in TsukuyomiRenderPipelineResources.");
                return false;
            }

            _supportsOrderedComposite = _uberMaterial != null && _uberMaterial.HasProperty(TsukuyomiPostProcessPlan.OrderId);
            return _uberMaterial != null;
        }

        private Material ResolveCompositeMaterial(TsukuyomiPostProcessPlan plan)
        {
            if (_supportsOrderedComposite || plan.UsesLegacyOrder)
                return _uberMaterial;

            return GetPackageUberMaterial();
        }

        private Material GetPackageUberMaterial()
        {
            if (_packageFallback == null)
            {
                var shader = Shader.Find("Hidden/Tsukuyomi/Post Processing/UberPost");
                if (shader == null)
                    throw new InvalidOperationException("The Tsukuyomi UberPost shader is required for this Profile.");
                _packageFallback = CoreUtils.CreateEngineMaterial(shader);
            }
            return _packageFallback;
        }
    }
}
