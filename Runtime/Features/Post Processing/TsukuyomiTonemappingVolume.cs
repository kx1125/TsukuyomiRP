using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering
{
    [Serializable]
    public sealed class TsukuyomiTonemappingModeParameter : VolumeParameter<TsukuyomiTonemappingMode>
    {
        public TsukuyomiTonemappingModeParameter(TsukuyomiTonemappingMode value, bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }

    [Serializable, VolumeComponentMenu("TsukuyomiRP/Postprocessing/Tonemapping")]
    [DisplayInfo(name = "Tonemapping Volume")]
    public sealed class TsukuyomiTonemappingVolume : VolumeComponent
    {
        public BoolParameter enable = new(false, BoolParameter.DisplayType.EnumPopup);
        public TsukuyomiTonemappingModeParameter mode = new(TsukuyomiTonemappingMode.None);
        public ClampedFloatParameter maxBrightness = new(1.0f, 1.0f, 20.0f);
        public ClampedFloatParameter contrast = new(1.11f, 0.0f, 5.0f);
        public ClampedFloatParameter linearSectionStart = new(0.2f, 0.0f, 1.0f);
        public ClampedFloatParameter linearSectionLength = new(0.4f, 0.0f, 1.0f);
        public ClampedFloatParameter blackPow = new(1.29f, 1.0f, 3.0f);
        public ClampedFloatParameter blackMin = new(0.0f, 0.0f, 1.0f);
    }

    internal readonly struct TsukuyomiTonemappingResolvedSettings
    {
        public readonly bool Enabled;
        public readonly TsukuyomiTonemappingMode Mode;
        public readonly float MaxBrightness;
        public readonly float Contrast;
        public readonly float LinearSectionStart;
        public readonly float LinearSectionLength;
        public readonly float BlackPow;
        public readonly float BlackMin;

        private TsukuyomiTonemappingResolvedSettings(TsukuyomiPipelineProfile profile, TsukuyomiTonemappingVolume volume)
        {
            Enabled = profile.EnableTonemapping && Resolve(volume?.enable, true);
            Mode = Resolve(volume?.mode, profile.TonemappingMode);
            MaxBrightness = Resolve(volume?.maxBrightness, profile.TonemappingMaxBrightness);
            Contrast = Resolve(volume?.contrast, profile.TonemappingContrast);
            LinearSectionStart = Resolve(volume?.linearSectionStart, profile.TonemappingLinearSectionStart);
            LinearSectionLength = Resolve(volume?.linearSectionLength, profile.TonemappingLinearSectionLength);
            BlackPow = Resolve(volume?.blackPow, profile.TonemappingBlackPow);
            BlackMin = Resolve(volume?.blackMin, profile.TonemappingBlackMin);
        }

        public bool IsActive => Enabled && Mode != TsukuyomiTonemappingMode.None;

        public Vector4 Params0 => new(MaxBrightness, Contrast, LinearSectionStart, LinearSectionLength);

        public Vector4 Params1 => new(BlackPow, BlackMin, 0.0f, 0.0f);

        public static TsukuyomiTonemappingResolvedSettings From(TsukuyomiPipelineProfile profile, TsukuyomiTonemappingVolume volume)
        {
            return new TsukuyomiTonemappingResolvedSettings(profile, volume);
        }

        private static T Resolve<T>(VolumeParameter<T> parameter, T fallback)
        {
            return parameter != null && parameter.overrideState ? parameter.value : fallback;
        }
    }
}
