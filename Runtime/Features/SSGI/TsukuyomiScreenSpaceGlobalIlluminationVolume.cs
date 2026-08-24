using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering
{
    [Flags]
    public enum TsukuyomiSsgiRayMissFallback
    {
        None = 0,
        Sky = 1,
        ReflectionProbes = 2,
        ReflectionProbesAndSky = ReflectionProbes | Sky
    }

    [Serializable]
    public sealed class TsukuyomiSsgiRayMissFallbackParameter : VolumeParameter<TsukuyomiSsgiRayMissFallback>
    {
        public TsukuyomiSsgiRayMissFallbackParameter(
            TsukuyomiSsgiRayMissFallback value,
            bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }

    [Serializable, VolumeComponentMenu("TsukuyomiRP/Screen Space Global Illumination")]
    [DisplayInfo(name = "Screen Space Global Illumination Volume")]
    public sealed class TsukuyomiScreenSpaceGlobalIlluminationVolume : VolumeComponent
    {
        public BoolParameter enable = new(false);
        public BoolParameter debugOutput = new(false);
        public BoolParameter halfResolution = new(true);
        public ClampedFloatParameter depthBufferThickness = new(0.1f, 0.0f, 0.5f);
        public ClampedIntParameter maxRaySteps = new(64, 1, 256);
        public TsukuyomiSsgiRayMissFallbackParameter rayMiss = new(TsukuyomiSsgiRayMissFallback.ReflectionProbesAndSky);
        public BoolParameter enableProbeVolumes = new(true);
        public BoolParameter denoise = new(true);
        public ClampedFloatParameter denoiserRadius = new(0.6f, 0.001f, 10.0f);
        public BoolParameter secondDenoiser = new(false);
        public BoolParameter halfResolutionDenoiser = new(true);
    }

    internal readonly struct TsukuyomiScreenSpaceGlobalIlluminationResolvedSettings
    {
        public readonly bool Enabled;
        public readonly bool DebugOutput;
        public readonly bool HalfResolution;
        public readonly float DepthBufferThickness;
        public readonly int MaxRaySteps;
        public readonly TsukuyomiSsgiRayMissFallback RayMiss;
        public readonly bool EnableProbeVolumes;
        public readonly bool Denoise;
        public readonly float DenoiserRadius;
        public readonly bool SecondDenoiser;
        public readonly bool HalfResolutionDenoiser;

        public bool IsActive => Enabled && MaxRaySteps > 0;

        public int HistorySignature
        {
            get
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + (HalfResolution ? 1 : 0);
                    hash = hash * 31 + DepthBufferThickness.GetHashCode();
                    hash = hash * 31 + MaxRaySteps;
                    hash = hash * 31 + (int)RayMiss;
                    hash = hash * 31 + (EnableProbeVolumes ? 1 : 0);
                    hash = hash * 31 + (Denoise ? 1 : 0);
                    hash = hash * 31 + DenoiserRadius.GetHashCode();
                    hash = hash * 31 + (SecondDenoiser ? 1 : 0);
                    hash = hash * 31 + (HalfResolutionDenoiser ? 1 : 0);
                    return hash;
                }
            }
        }

        private TsukuyomiScreenSpaceGlobalIlluminationResolvedSettings(
            TsukuyomiPipelineProfile profile,
            TsukuyomiScreenSpaceGlobalIlluminationVolume volume)
        {
            Enabled = profile.EnableScreenSpaceGlobalIllumination && Resolve(volume?.enable, true);
            DebugOutput = Resolve(volume?.debugOutput, false);
            HalfResolution = Resolve(volume?.halfResolution, profile.SsgiHalfResolution);
            DepthBufferThickness = Resolve(volume?.depthBufferThickness, profile.SsgiDepthBufferThickness);
            MaxRaySteps = Resolve(volume?.maxRaySteps, profile.SsgiMaxRaySteps);
            RayMiss = Resolve(volume?.rayMiss, profile.SsgiRayMissFallback);
            EnableProbeVolumes = Resolve(volume?.enableProbeVolumes, profile.SsgiEnableProbeVolumes);
            Denoise = Resolve(volume?.denoise, profile.SsgiDenoise);
            DenoiserRadius = Resolve(volume?.denoiserRadius, profile.SsgiDenoiserRadius);
            SecondDenoiser = Resolve(volume?.secondDenoiser, profile.SsgiSecondDenoiser);
            HalfResolutionDenoiser = Resolve(volume?.halfResolutionDenoiser, profile.SsgiHalfResolutionDenoiser);
        }

        public static TsukuyomiScreenSpaceGlobalIlluminationResolvedSettings From(
            TsukuyomiPipelineProfile profile,
            TsukuyomiScreenSpaceGlobalIlluminationVolume volume)
        {
            return new TsukuyomiScreenSpaceGlobalIlluminationResolvedSettings(profile, volume);
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

        private static TsukuyomiSsgiRayMissFallback Resolve(
            VolumeParameter<TsukuyomiSsgiRayMissFallback> parameter,
            TsukuyomiSsgiRayMissFallback fallback)
        {
            return parameter != null && parameter.overrideState ? parameter.value : fallback;
        }
    }
}
