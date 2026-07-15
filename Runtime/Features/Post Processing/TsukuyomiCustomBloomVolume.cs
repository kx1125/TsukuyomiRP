using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering
{
    [Serializable]
    public sealed class ClampedVector4Parameter : VolumeParameter<Vector4>
    {
        public ClampedVector4Parameter(Vector4 value, bool overrideState = false)
            : base(Clamp01(value), overrideState)
        {
        }

        public override Vector4 value
        {
            get => m_Value;
            set => m_Value = Clamp01(value);
        }

        public override void Interp(Vector4 from, Vector4 to, float t)
        {
            m_Value = Clamp01(Vector4.Lerp(from, to, t));
        }

        private static Vector4 Clamp01(Vector4 value)
        {
            value.x = Mathf.Clamp01(value.x);
            value.y = Mathf.Clamp01(value.y);
            value.z = Mathf.Clamp01(value.z);
            value.w = Mathf.Clamp01(value.w);
            return value;
        }
    }

    [Serializable, VolumeComponentMenu("TsukuyomiRP/Postprocessing/Custom Bloom")]
    public sealed class TsukuyomiCustomBloomVolume : VolumeComponent
    {
        public BoolParameter enable = new(false, BoolParameter.DisplayType.EnumPopup);
        public MinFloatParameter threshold = new(0.7f, 0.0f);
        public MinFloatParameter intensity = new(0.75f, 0.0f);
        public ClampedFloatParameter lumRangeScale = new(0.2f, 0.0f, 1.0f);
        public ClampedFloatParameter preFilterScale = new(2.5f, 0.0f, 5.0f);
        public ClampedVector4Parameter blurCompositeWeight = new(new Vector4(0.3f, 0.3f, 0.26f, 0.15f));
        public ColorParameter tint = new(new Color(1.0f, 1.0f, 1.0f, 0.0f), false, true, true);
    }

    internal readonly struct TsukuyomiCustomBloomResolvedSettings
    {
        public readonly bool Enabled;
        public readonly float Threshold;
        public readonly float Intensity;
        public readonly float LumRangeScale;
        public readonly float PreFilterScale;
        public readonly Vector4 BlurCompositeWeight;
        public readonly Color Tint;

        private TsukuyomiCustomBloomResolvedSettings(TsukuyomiPipelineProfile profile, TsukuyomiCustomBloomVolume volume)
        {
            Enabled = profile.EnableCustomBloom && Resolve(volume?.enable, true);
            Threshold = Resolve(volume?.threshold, profile.CustomBloomThreshold);
            Intensity = Resolve(volume?.intensity, profile.CustomBloomIntensity);
            LumRangeScale = Resolve(volume?.lumRangeScale, profile.CustomBloomLumRangeScale);
            PreFilterScale = Resolve(volume?.preFilterScale, profile.CustomBloomPreFilterScale);
            BlurCompositeWeight = Clamp01(Resolve(volume?.blurCompositeWeight, profile.CustomBloomBlurCompositeWeight));
            Tint = Resolve(volume?.tint, profile.CustomBloomTint);
        }

        public bool IsActive => Enabled && Intensity > 0.0f && PreFilterScale > 0.0f;

        public Vector4 Params => new(Threshold, LumRangeScale, PreFilterScale, Intensity);

        public static TsukuyomiCustomBloomResolvedSettings From(TsukuyomiPipelineProfile profile, TsukuyomiCustomBloomVolume volume)
        {
            return new TsukuyomiCustomBloomResolvedSettings(profile, volume);
        }

        private static bool Resolve(BoolParameter parameter, bool fallback)
        {
            return parameter != null && parameter.overrideState ? parameter.value : fallback;
        }

        private static float Resolve(VolumeParameter<float> parameter, float fallback)
        {
            return parameter != null && parameter.overrideState ? parameter.value : fallback;
        }

        private static Vector4 Resolve(VolumeParameter<Vector4> parameter, Vector4 fallback)
        {
            return parameter != null && parameter.overrideState ? parameter.value : fallback;
        }

        private static Color Resolve(VolumeParameter<Color> parameter, Color fallback)
        {
            return parameter != null && parameter.overrideState ? parameter.value : fallback;
        }

        private static Vector4 Clamp01(Vector4 value)
        {
            value.x = Mathf.Clamp01(value.x);
            value.y = Mathf.Clamp01(value.y);
            value.z = Mathf.Clamp01(value.z);
            value.w = Mathf.Clamp01(value.w);
            return value;
        }
    }
}
