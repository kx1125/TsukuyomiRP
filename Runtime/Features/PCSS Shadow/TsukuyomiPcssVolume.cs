using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering
{
    [Serializable, VolumeComponentMenu("TsukuyomiRP/PCSS Screen Space Shadows")]
    [DisplayInfo(name = "Pcss Volume")]
    public sealed class TsukuyomiPcssVolume : VolumeComponent
    {
        public BoolParameter enable = new(false);
        public NoInterpClampedIntParameter findBlockerSampleCount = new(16, 1, 64);
        public NoInterpClampedIntParameter pcfSampleCount = new(32, 1, 64);
        public ClampedFloatParameter angularDiameter = new(0.5357f, 0.001f, 10.0f);
        public ClampedFloatParameter blockerSearchAngularDiameter = new(0.5357f, 0.001f, 10.0f);
        public ClampedFloatParameter minFilterMaxAngularDiameter = new(0.5357f, 0.001f, 10.0f);
        public NoInterpMinFloatParameter maxPenumbraSize = new(0.56f, 0.0f);
        public NoInterpMinFloatParameter maxSamplingDistance = new(0.1f, 0.0f);
        public NoInterpMinFloatParameter minFilterSizeTexels = new(1.0f, 0.0f);
        public NoInterpClampedIntParameter penumbraMaskScale = new(4, 1, 32);
    }

    internal readonly struct TsukuyomiPcssResolvedSettings
    {
        public readonly bool Enabled;
        public readonly int FindBlockerSampleCount;
        public readonly int PcfSampleCount;
        public readonly float AngularDiameter;
        public readonly float BlockerSearchAngularDiameter;
        public readonly float MinFilterMaxAngularDiameter;
        public readonly float MaxPenumbraSize;
        public readonly float MaxSamplingDistance;
        public readonly float MinFilterSizeTexels;
        public readonly int PenumbraMaskScale;

        private TsukuyomiPcssResolvedSettings(TsukuyomiPipelineProfile profile, TsukuyomiPcssVolume volume)
        {
            Enabled = profile.EnablePCSS && Resolve(volume?.enable, true);
            FindBlockerSampleCount = Resolve(volume?.findBlockerSampleCount, profile.PcssFindBlockerSampleCount);
            PcfSampleCount = Resolve(volume?.pcfSampleCount, profile.PcssPcfSampleCount);
            AngularDiameter = Resolve(volume?.angularDiameter, profile.PcssAngularDiameter);
            BlockerSearchAngularDiameter = Resolve(volume?.blockerSearchAngularDiameter, profile.PcssBlockerSearchAngularDiameter);
            MinFilterMaxAngularDiameter = Resolve(volume?.minFilterMaxAngularDiameter, profile.PcssMinFilterMaxAngularDiameter);
            MaxPenumbraSize = Resolve(volume?.maxPenumbraSize, profile.PcssMaxPenumbraSize);
            MaxSamplingDistance = Resolve(volume?.maxSamplingDistance, profile.PcssMaxSamplingDistance);
            MinFilterSizeTexels = Resolve(volume?.minFilterSizeTexels, profile.PcssMinFilterSizeTexels);
            PenumbraMaskScale = Resolve(volume?.penumbraMaskScale, profile.PcssPenumbraMaskScale);
        }

        public static TsukuyomiPcssResolvedSettings From(TsukuyomiPipelineProfile profile, TsukuyomiPcssVolume volume)
        {
            return new TsukuyomiPcssResolvedSettings(profile, volume);
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
