using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering
{
    [Serializable, VolumeComponentMenu("Tsukuyomi RP/Volume Light")]
    public sealed class TsukuyomiVolumeLightVolume : VolumeComponent
    {
        public BoolParameter enable = new(false, BoolParameter.DisplayType.EnumPopup);
        public ClampedFloatParameter distance = new(64.0f, 0.0f, 512.0f);
        public FloatParameter baseHeight = new(0.0f);
        public FloatParameter maximumHeight = new(50.0f);
        public BoolParameter enableGround = new(false, BoolParameter.DisplayType.Checkbox);
        public FloatParameter groundHeight = new(0.0f);
        public ClampedFloatParameter density = new(0.2f, 0.0f, 1.0f);
        public MinFloatParameter attenuationDistance = new(128.0f, 0.05f);
        public BoolParameter enableProbeVolumeContribution = new(false, BoolParameter.DisplayType.Checkbox);
        public ClampedFloatParameter probeVolumeContributionWeight = new(1.0f, 0.0f, 1.0f);
        public BoolParameter enableMainLightContribution = new(false, BoolParameter.DisplayType.Checkbox);
        public ClampedFloatParameter anisotropy = new(0.4f, -1.0f, 1.0f);
        public ClampedFloatParameter scattering = new(0.15f, 0.0f, 1.0f);
        public ColorParameter tint = new(Color.white, true, false, true);
        public BoolParameter enableAdditionalLightsContribution = new(false, BoolParameter.DisplayType.Checkbox);
        public ClampedIntParameter maxSteps = new(128, 8, 256);
        public ClampedIntParameter blurIterations = new(2, 1, 4);
        public ClampedFloatParameter transmittanceThreshold = new(0.01f, 0.0f, 0.1f);
    }

    internal readonly struct TsukuyomiVolumeLightResolvedSettings
    {
        public readonly bool Enabled;
        public readonly float Distance;
        public readonly float BaseHeight;
        public readonly float MaximumHeight;
        public readonly bool EnableGround;
        public readonly float GroundHeight;
        public readonly float Density;
        public readonly float AttenuationDistance;
        public readonly bool EnableProbeVolumeContribution;
        public readonly float ProbeVolumeContributionWeight;
        public readonly bool EnableMainLightContribution;
        public readonly float Anisotropy;
        public readonly float Scattering;
        public readonly Color Tint;
        public readonly bool EnableAdditionalLightsContribution;
        public readonly int MaxSteps;
        public readonly int BlurIterations;
        public readonly float TransmittanceThreshold;

        private TsukuyomiVolumeLightResolvedSettings(TsukuyomiPipelineProfile profile, TsukuyomiVolumeLightVolume volume)
        {
            Enabled = profile.EnableVolumeLight && Resolve(volume?.enable, true);
            Distance = Resolve(volume?.distance, profile.VolumeLightDistance);
            BaseHeight = Resolve(volume?.baseHeight, profile.VolumeLightBaseHeight);
            MaximumHeight = Mathf.Max(BaseHeight, Resolve(volume?.maximumHeight, profile.VolumeLightMaximumHeight));
            EnableGround = Resolve(volume?.enableGround, profile.VolumeLightEnableGround);
            GroundHeight = Resolve(volume?.groundHeight, profile.VolumeLightGroundHeight);
            Density = Resolve(volume?.density, profile.VolumeLightDensity);
            AttenuationDistance = Resolve(volume?.attenuationDistance, profile.VolumeLightAttenuationDistance);
            EnableProbeVolumeContribution = Resolve(volume?.enableProbeVolumeContribution, profile.VolumeLightEnableProbeVolumeContribution);
            ProbeVolumeContributionWeight = Resolve(volume?.probeVolumeContributionWeight, profile.VolumeLightProbeVolumeContributionWeight);
            EnableMainLightContribution = Resolve(volume?.enableMainLightContribution, profile.VolumeLightEnableMainLightContribution);
            Anisotropy = Resolve(volume?.anisotropy, profile.VolumeLightAnisotropy);
            Scattering = Resolve(volume?.scattering, profile.VolumeLightScattering);
            Tint = Resolve(volume?.tint, profile.VolumeLightTint);
            EnableAdditionalLightsContribution = Resolve(volume?.enableAdditionalLightsContribution, profile.VolumeLightEnableAdditionalLightsContribution);
            MaxSteps = Resolve(volume?.maxSteps, profile.VolumeLightMaxSteps);
            BlurIterations = Resolve(volume?.blurIterations, profile.VolumeLightBlurIterations);
            TransmittanceThreshold = Resolve(volume?.transmittanceThreshold, profile.VolumeLightTransmittanceThreshold);
        }

        public bool IsActive => Enabled && Distance > 0.0f && GroundHeight < MaximumHeight && Density > 0.0f;

        public static TsukuyomiVolumeLightResolvedSettings From(TsukuyomiPipelineProfile profile, TsukuyomiVolumeLightVolume volume)
        {
            return new TsukuyomiVolumeLightResolvedSettings(profile, volume);
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

        private static Color Resolve(VolumeParameter<Color> parameter, Color fallback)
        {
            return parameter != null && parameter.overrideState ? parameter.value : fallback;
        }
    }
}
