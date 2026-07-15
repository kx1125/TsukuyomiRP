using System;
using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering
{
    public static class TsukuyomiPerObjectShadowDefaults
    {
        public const uint RenderingLayerMask = 1u << 1;
    }

    public enum TsukuyomiPerObjectShadowDepthBits
    {
        Depth16 = 16,
        Depth24 = 24,
        Depth32 = 32
    }

    public enum TsukuyomiPerObjectShadowTileResolution
    {
        _256 = 256,
        _512 = 512,
        _1024 = 1024,
        _1280 = 1280,
        _1536 = 1536,
        _2048 = 2048
    }

    [Serializable]
    public sealed class TsukuyomiDepthBitsParameter : VolumeParameter<TsukuyomiPerObjectShadowDepthBits>
    {
        public TsukuyomiDepthBitsParameter(TsukuyomiPerObjectShadowDepthBits value, bool overrideState = false) : base(value, overrideState)
        {
        }
    }

    [Serializable]
    public sealed class TsukuyomiPerObjectShadowTileResolutionParameter : VolumeParameter<TsukuyomiPerObjectShadowTileResolution>
    {
        public TsukuyomiPerObjectShadowTileResolutionParameter(TsukuyomiPerObjectShadowTileResolution value, bool overrideState = false) : base(value, overrideState)
        {
        }
    }

    public enum TsukuyomiShadowBoundType
    {
        Calculated,
        Customized
    }
}



