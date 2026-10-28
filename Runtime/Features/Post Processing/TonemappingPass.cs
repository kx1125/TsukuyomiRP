using System;
using UnityEngine.Rendering;
using UnityEngine;

namespace Tsukuyomi.Rendering
{
    public enum TsukuyomiTonemappingMode
    {
        None,
        Neutral,
        ACES,
        [InspectorName("ACES Simple")]
        ACESSimple,
        [InspectorName("Gran Turismo")]
        GranTurismo
    }

    [Serializable, InjectionPoint(InjectionPoint.BeforePostProcess)]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Tsukuyomi.Rendering", "Tsukuyomi.Rendering", "TonemappingPlugin")]
    public sealed class TonemappingPass : PostProcessPassDefinition
    {
        public TonemappingPass() => InjectionPoint = InjectionPoint.BeforePostProcess;
        public override string Name => "Tonemapping";

        public TsukuyomiTonemappingMode TonemappingMode = TsukuyomiTonemappingMode.None;

        [Range(1.0f, 20.0f)]
        public float TonemappingMaxBrightness = 1.0f;

        [Range(0.0f, 5.0f)]
        public float TonemappingContrast = 1.11f;

        [Range(0.0f, 1.0f)]
        public float TonemappingLinearSectionStart = 0.2f;

        [Range(0.0f, 1.0f)]
        public float TonemappingLinearSectionLength = 0.4f;

        [Range(1.0f, 3.0f)]
        public float TonemappingBlackPow = 1.29f;

        [Range(0.0f, 1.0f)]
        public float TonemappingBlackMin = 0.0f;

        internal override TsukuyomiPostProcessEffect CreateEffect() => new TsukuyomiTonemappingEffect();
    }

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
            PostProcessPassDefinition profile,
            VolumeStack volumeStack,
            TsukuyomiRenderPipelineResources resources)
        {
            TsukuyomiTonemappingVolume volume = volumeStack?.GetComponent<TsukuyomiTonemappingVolume>();
            _settings = TsukuyomiTonemappingResolvedSettings.From((TonemappingPass)profile, volume);
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
