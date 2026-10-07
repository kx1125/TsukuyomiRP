using System;
using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering
{
    [Serializable, VolumeComponentMenu("TsukuyomiRP/Rect Lights")]
    public sealed class TsukuyomiRectLightVolume : VolumeComponent
    {
        public BoolParameter enable = new(false);
        public ClampedIntParameter maxLights = new(16, 1, 64);
    }
}
