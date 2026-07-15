using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering
{
    internal sealed class TsukuyomiPostProcessPass : UnsafePass
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
        private TsukuyomiPipelineProfile _profile;
        private Material _uberMaterial;
        private bool _ownsUberMaterial;

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

        public override void Record(in UnsafePassContext context)
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

            context.Builder.UseTexture(source, AccessFlags.Read);
            context.Builder.UseTexture(destination, AccessFlags.Write);
            context.Builder.AllowGlobalStateModification(true);

            var plan = new TsukuyomiPostProcessPlan();
            var buildContext = new TsukuyomiPostProcessBuildContext(
                context.RenderGraph,
                context.Builder,
                context.CameraData,
                source,
                destination,
                _uberMaterial,
                plan);

            for (int i = 0; i < _activeEffects.Count; i++)
                _activeEffects[i].Record(buildContext);

            Material uberMaterial = _uberMaterial;
            TsukuyomiPostProcessEffect[] effects = _effects;

            context.SetRenderFunc((data, graphContext) =>
            {
                for (int i = 0; i < effects.Length; i++)
                    effects[i].ResetUberMaterial(uberMaterial);

                plan.ExecuteStages(graphContext);
                plan.SetupUberMaterial(graphContext, uberMaterial);

                graphContext.cmd.SetRenderTarget(destination, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                Blitter.BlitTexture(graphContext.cmd, source, new Vector4(1.0f, 1.0f, 0.0f, 0.0f), uberMaterial, UberCompositePass);
            });

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
