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

        public override void Record(in TsukuyomiPostProcessBuildContext context)
        {
            TsukuyomiTonemappingResolvedSettings settings = _settings;

            context.AddUberSetup((graphContext, uberMaterial) =>
            {
                uberMaterial.SetVector(ToneMapParams0Id, settings.Params0);
                uberMaterial.SetVector(ToneMapParams1Id, settings.Params1);

                string keyword = GetKeyword(settings.Mode);
                if (!string.IsNullOrEmpty(keyword))
                    uberMaterial.EnableKeyword(keyword);
            });
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
