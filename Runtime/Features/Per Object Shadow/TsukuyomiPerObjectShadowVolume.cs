using System;
using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering
{
    [Serializable, VolumeComponentMenu("TsukuyomiRP/Per Object Shadows")]
    public sealed class TsukuyomiPerObjectShadowVolume : VolumeComponent
    {
        public BoolParameter enable = new(false);
        public TsukuyomiDepthBitsParameter depthBits = new(TsukuyomiPerObjectShadowDepthBits.Depth16);
        public TsukuyomiPerObjectShadowTileResolutionParameter tileResolution = new(TsukuyomiPerObjectShadowTileResolution._1024);
        public ClampedFloatParameter shadowLengthOffset = new(500.0f, 0.0f, 1000.0f);
    }

    internal readonly struct TsukuyomiPerObjectShadowResolvedSettings
    {
        public readonly bool Enabled;
        public readonly TsukuyomiPerObjectShadowDepthBits DepthBits;
        public readonly TsukuyomiPerObjectShadowTileResolution TileResolution;
        public readonly float ShadowLengthOffset;

        private TsukuyomiPerObjectShadowResolvedSettings(TsukuyomiPipelineProfile profile, TsukuyomiPerObjectShadowVolume volume)
        {
            Enabled = profile.EnablePerObjectShadow && Resolve(volume?.enable, true);
            DepthBits = Resolve(volume?.depthBits, profile.PerObjectShadowDepthBits);
            TileResolution = Resolve(volume?.tileResolution, profile.PerObjectShadowTileResolution);
            ShadowLengthOffset = Resolve(volume?.shadowLengthOffset, profile.PerObjectShadowLengthOffset);
        }

        public static TsukuyomiPerObjectShadowResolvedSettings From(TsukuyomiPipelineProfile profile, TsukuyomiPerObjectShadowVolume volume)
        {
            return new TsukuyomiPerObjectShadowResolvedSettings(profile, volume);
        }

        private static bool Resolve(BoolParameter parameter, bool fallback)
        {
            return parameter != null && parameter.overrideState ? parameter.value : fallback;
        }

        private static T Resolve<T>(VolumeParameter<T> parameter, T fallback)
        {
            return parameter != null && parameter.overrideState ? parameter.value : fallback;
        }
    }
}

