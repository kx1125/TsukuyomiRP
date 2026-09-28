using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering
{
    internal sealed class TsukuyomiPostProcessPass : GraphFeaturePass
    {
        private const int UberCompositePass = 0;

        [Read(BuiltinTexture.ActiveColor)]
        public TextureSlot color = TextureSlot.Read("Color", BuiltinTexture.ActiveColor);

        private readonly TsukuyomiPostProcessEffect[] _effects =
        {
            new TsukuyomiCustomBloomEffect(),
            new TsukuyomiTonemappingEffect()
        };

        private readonly List<TsukuyomiPostProcessEffect> _activeEffects = new();
        // Recording scratch only. Every value is copied to a native node before another camera records.
        private readonly TextureHandle[] _effectOutputs;

        public TsukuyomiPostProcessPass() => _effectOutputs = new TextureHandle[_effects.Length];
        private TsukuyomiPipelineProfile _profile;
        private Material _uberMaterial;
        private bool _ownsUberMaterial;

        public override void CollectTextureSlots(System.Collections.Generic.List<TextureSlot> slots)
        {
            slots.Add(color);
        }

        public override string Name => "Tsukuyomi Post Processing";

        public bool Configure(TsukuyomiPipelineProfile profile, VolumeStack volumeStack)
        {
            _profile = profile;
            _activeEffects.Clear();

            if (profile == null)
                return false;

            if (!profile.EnableTsukuyomiPostProcessing)
                return false;

            if (!TsukuyomiRenderPipelineResourcesProvider.TryGet(out TsukuyomiRenderPipelineResources resources))
                return false;

            for (int i = 0; i < _effects.Length; i++)
            {
                if (_effects[i].Configure(profile, volumeStack, resources))
                    _activeEffects.Add(_effects[i]);
            }

            if (_activeEffects.Count == 0)
                return false;

            return ResolveUberMaterial(resources);
        }

        public override bool IsActive(in FrameContext frame)
        {
            return base.IsActive(frame)
                && _profile != null
                && _profile.EnableTsukuyomiPostProcessing
                && _uberMaterial != null
                && _activeEffects.Count > 0;
        }

        public void Dispose()
        {
            for (int i = 0; i < _effects.Length; i++)
                _effects[i].Dispose();

            if (_ownsUberMaterial)
                CoreUtils.Destroy(_uberMaterial);

            _uberMaterial = null;
            _ownsUberMaterial = false;
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
            if (_uberMaterial == null || _activeEffects.Count == 0 || context.CameraData.isPreviewCamera)
                return;

            TextureHandle source = context.GetTexture(color);
            if (!source.IsValid())
                return;

            TextureHandle destination = PassRecorder.CreateTextureLike(
                context.RenderGraph,
                source,
                "_TsukuyomiPostProcessColor");
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

                passData.UberMaterial = _uberMaterial;
                passData.Effects = _effects;

                passData.Source = source;
                passData.Destination = destination;

                node.SetRenderFunc(static (state, graphContext) =>
                {
                    for (int i = 0; i < state.Effects.Length; i++)
                        state.Effects[i].ResetUberMaterial(state.UberMaterial);

                    state.Plan.SetupUberMaterial(graphContext, state.UberMaterial);

                    graphContext.cmd.SetRenderTarget(state.Destination, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                    Blitter.BlitTexture(graphContext.cmd, state.Source, new Vector4(1.0f, 1.0f, 0.0f, 0.0f), state.UberMaterial, UberCompositePass);
                });
            }

            PassRecorder.SwapActiveColor(context.Resources, destination);
        }

        private bool ResolveUberMaterial(TsukuyomiRenderPipelineResources resources)
        {
            if (_uberMaterial != null)
                return true;

            _ownsUberMaterial = false;
            if (resources.PostProcessUberMaterial != null)
            {
                _uberMaterial = resources.PostProcessUberMaterial;
                return true;
            }

            if (resources.PostProcessUberShader == null)
            {
                Debug.LogError("Tsukuyomi Post Processing requires an Uber shader or material in TsukuyomiRenderPipelineResources.");
                return false;
            }

            _uberMaterial = CoreUtils.CreateEngineMaterial(resources.PostProcessUberShader);
            _ownsUberMaterial = true;
            return _uberMaterial != null;
        }
    }
}
