using UnityEngine;
using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering
{
    internal sealed class TsukuyomiTonemappingEffect : TsukuyomiPostProcessEffect
    {
        private const string NeutralKeyword = "_TSUKUYOMI_TONEMAP_NEUTRAL";
        private const string AcesKeyword = "_TSUKUYOMI_TONEMAP_ACES";
        private const string AcesSimpleKeyword = "_TSUKUYOMI_TONEMAP_ACES_SIMPLE";
        private const string GtKeyword = "_TSUKUYOMI_TONEMAP_GT";

        private static readonly int ToneMapParams0Id = Shader.PropertyToID("_TsukuyomiToneMapParams0");
        private static readonly int ToneMapParams1Id = Shader.PropertyToID("_TsukuyomiToneMapParams1");

        private TsukuyomiTonemappingResolvedSettings _settings;

        public override string Name => "Tonemapping";

        public override bool Configure(
            TsukuyomiPipelineProfile profile,
            VolumeStack volumeStack,
            TsukuyomiRenderPipelineResources resources)
        {
            TsukuyomiTonemappingVolume volume = volumeStack?.GetComponent<TsukuyomiTonemappingVolume>();
            _settings = TsukuyomiTonemappingResolvedSettings.From(profile, volume);
            return _settings.IsActive;
        }

        public override void ResetUberMaterial(Material material)
        {
            DisableKeywords(material);
        }

        private sealed class RenderData : TsukuyomiPostProcessData
        {
            public TsukuyomiTonemappingResolvedSettings Settings;

            public override void SetupUber(UnityEngine.Rendering.RenderGraphModule.UnsafeGraphContext graphContext, Material material)
            {
                material.SetVector(ToneMapParams0Id, Settings.Params0);
                material.SetVector(ToneMapParams1Id, Settings.Params1);
                string keyword = GetKeyword(Settings.Mode);
                if (!string.IsNullOrEmpty(keyword))
                    material.EnableKeyword(keyword);
            }
        }

        public override void Record(in TsukuyomiPostProcessBuildContext context)
        {
            var data = context.GetOrCreateData<RenderData>();
            data.Settings = _settings;
            context.AddUberSetup(data);
        }

        private static void DisableKeywords(Material material)
        {
            material.DisableKeyword(NeutralKeyword);
            material.DisableKeyword(AcesKeyword);
            material.DisableKeyword(AcesSimpleKeyword);
            material.DisableKeyword(GtKeyword);
        }

        private static string GetKeyword(TsukuyomiTonemappingMode mode)
        {
            return mode switch
            {
                TsukuyomiTonemappingMode.Neutral => NeutralKeyword,
                TsukuyomiTonemappingMode.ACES => AcesKeyword,
                TsukuyomiTonemappingMode.ACESSimple => AcesSimpleKeyword,
                TsukuyomiTonemappingMode.GranTurismo => GtKeyword,
                _ => null
            };
        }
    }
}
