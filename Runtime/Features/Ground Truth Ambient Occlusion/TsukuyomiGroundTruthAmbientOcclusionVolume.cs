using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering
{
    [Serializable, VolumeComponentMenu("TsukuyomiRP/Ground Truth Ambient Occlusion")]
    public sealed class TsukuyomiGroundTruthAmbientOcclusionVolume : VolumeComponent
    {
        public BoolParameter enable = new(false);
        public BoolParameter downSample = new(true);
        public ClampedFloatParameter intensity = new(1.0f, 0.0f, 4.0f);
        public ClampedFloatParameter directLightingStrength = new(0.25f, 0.0f, 1.0f);
        public ClampedFloatParameter radius = new(2.0f, 0.25f, 5.0f);
        public ClampedFloatParameter thickness = new(0.5f, 0.001f, 1.0f);
        public ClampedFloatParameter spatialBilateralAggressiveness = new(0.15f, 0.0f, 1.0f);
        public ClampedFloatParameter blurSharpness = new(0.1f, 0.0f, 1.0f);
        public ClampedIntParameter stepCount = new(6, 2, 32);
        public ClampedIntParameter maximumRadiusInPixels = new(40, 16, 256);
        public ClampedIntParameter directionCount = new(2, 1, 6);
    }

    internal readonly struct TsukuyomiGroundTruthAmbientOcclusionResolvedSettings
    {
        public readonly bool Enabled;
        public readonly bool DownSample;
        public readonly float Intensity;
        public readonly float DirectLightingStrength;
        public readonly float Radius;
        public readonly float Thickness;
        public readonly float SpatialBilateralAggressiveness;
        public readonly float BlurSharpness;
        public readonly int StepCount;
        public readonly int MaximumRadiusInPixels;
        public readonly int DirectionCount;

        public bool IsActive => Enabled && Intensity > 0.0f && Radius > 0.0f;

        private TsukuyomiGroundTruthAmbientOcclusionResolvedSettings(
            TsukuyomiPipelineProfile profile,
            TsukuyomiGroundTruthAmbientOcclusionVolume volume)
        {
            Enabled = profile.EnableGTAO && Resolve(volume?.enable, true);
            DownSample = Resolve(volume?.downSample, profile.GtaoDownSample);
            Intensity = Resolve(volume?.intensity, profile.GtaoIntensity);
            DirectLightingStrength = Resolve(volume?.directLightingStrength, profile.GtaoDirectLightingStrength);
            Radius = Resolve(volume?.radius, profile.GtaoRadius);
            Thickness = Resolve(volume?.thickness, profile.GtaoThickness);
            SpatialBilateralAggressiveness = Resolve(volume?.spatialBilateralAggressiveness, profile.GtaoSpatialBilateralAggressiveness);
            BlurSharpness = Resolve(volume?.blurSharpness, profile.GtaoBlurSharpness);
            StepCount = Resolve(volume?.stepCount, profile.GtaoStepCount);
            MaximumRadiusInPixels = Resolve(volume?.maximumRadiusInPixels, profile.GtaoMaximumRadiusInPixels);
            DirectionCount = Resolve(volume?.directionCount, profile.GtaoDirectionCount);
        }

        public static TsukuyomiGroundTruthAmbientOcclusionResolvedSettings From(
            TsukuyomiPipelineProfile profile,
            TsukuyomiGroundTruthAmbientOcclusionVolume volume)
        {
            return new TsukuyomiGroundTruthAmbientOcclusionResolvedSettings(profile, volume);
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
    }
}
