using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering
{
    [Serializable]
    public sealed class TsukuyomiShadowDenoiserParameter : VolumeParameter<TsukuyomiShadowDenoiser>
    {
        public TsukuyomiShadowDenoiserParameter(TsukuyomiShadowDenoiser value, bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }

    [Serializable, VolumeComponentMenu("TsukuyomiRP/Contact Shadows")]
    public sealed class TsukuyomiContactShadowVolume : VolumeComponent
    {
        public BoolParameter enable = new(false);
        public ClampedFloatParameter length = new(0.15f, 0.0f, 1.0f);
        public ClampedFloatParameter distanceScaleFactor = new(0.5f, 0.0f, 1.0f);
        public NoInterpMinFloatParameter maxDistance = new(50.0f, 0.0f);
        public NoInterpMinFloatParameter minDistance = new(0.0f, 0.0f);
        public NoInterpMinFloatParameter fadeDistance = new(5.0f, 0.0f);
        public NoInterpMinFloatParameter fadeInDistance = new(0.0f, 0.0f);
        public ClampedFloatParameter rayBias = new(0.2f, 0.0f, 1.0f);
        public ClampedFloatParameter thicknessScale = new(0.15f, 0.02f, 10.0f);
        public TsukuyomiShadowDenoiserParameter denoiser = new(TsukuyomiShadowDenoiser.None);
        public NoInterpClampedIntParameter sampleCount = new(10, 4, 64);
        public NoInterpClampedIntParameter filterSize = new(16, 1, 32);
    }

    internal readonly struct TsukuyomiContactShadowResolvedSettings
    {
        public readonly bool Enabled;
        public readonly float Length;
        public readonly float DistanceScaleFactor;
        public readonly float MaxDistance;
        public readonly float MinDistance;
        public readonly float FadeDistance;
        public readonly float FadeInDistance;
        public readonly float RayBias;
        public readonly float ThicknessScale;
        public readonly TsukuyomiShadowDenoiser Denoiser;
        public readonly int SampleCount;
        public readonly int FilterSize;

        private TsukuyomiContactShadowResolvedSettings(TsukuyomiPipelineProfile profile, TsukuyomiContactShadowVolume volume)
        {
            Enabled = profile.EnableContactShadow && Resolve(volume?.enable, true);
            Length = Resolve(volume?.length, profile.ContactShadowLength);
            DistanceScaleFactor = Resolve(volume?.distanceScaleFactor, profile.ContactShadowDistanceScaleFactor);
            MaxDistance = Resolve(volume?.maxDistance, profile.ContactShadowMaxDistance);
            MinDistance = Resolve(volume?.minDistance, profile.ContactShadowMinDistance);
            FadeDistance = Resolve(volume?.fadeDistance, profile.ContactShadowFadeDistance);
            FadeInDistance = Resolve(volume?.fadeInDistance, profile.ContactShadowFadeInDistance);
            RayBias = Resolve(volume?.rayBias, profile.ContactShadowRayBias);
            ThicknessScale = Resolve(volume?.thicknessScale, profile.ContactShadowThicknessScale);
            Denoiser = Resolve(volume?.denoiser, profile.ContactShadowDenoiser);
            SampleCount = Resolve(volume?.sampleCount, profile.ContactShadowSampleCount);
            FilterSize = Resolve(volume?.filterSize, profile.ContactShadowFilterSize);
        }

        public static TsukuyomiContactShadowResolvedSettings From(TsukuyomiPipelineProfile profile, TsukuyomiContactShadowVolume volume)
        {
            return new TsukuyomiContactShadowResolvedSettings(profile, volume);
        }

        private static bool Resolve(BoolParameter parameter, bool fallback)
        {
            return parameter != null && parameter.overrideState ? parameter.value : fallback;
        }

        private static int Resolve(VolumeParameter<int> parameter, int fallback)
        {
            return parameter != null && parameter.overrideState ? parameter.value : fallback;
        }

        private static float Resolve(VolumeParameter<float> parameter, float fallback)
        {
            return parameter != null && parameter.overrideState ? parameter.value : fallback;
        }

        private static TsukuyomiShadowDenoiser Resolve(TsukuyomiShadowDenoiserParameter parameter, TsukuyomiShadowDenoiser fallback)
        {
            return parameter != null && parameter.overrideState ? parameter.value : fallback;
        }
    }
}
