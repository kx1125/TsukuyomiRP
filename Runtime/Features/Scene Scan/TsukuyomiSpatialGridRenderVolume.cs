using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering
{
    [Serializable, VolumeComponentMenu("TsukuyomiRP/SpatialGridRender"), DisplayInfo(name = "SpatialGridRender")]
    public sealed class TsukuyomiSpatialGridRenderVolume : TsukuyomiSceneEffectVolume
    {
        private static readonly SpatialGridParameters Defaults = SpatialGridParameters.Default;
        public MinFloatParameter spacing = new(Defaults.Spacing, .01f);
        public MinFloatParameter radius = new(Defaults.Radius, .01f);
        public MinFloatParameter lineWidth = new(Defaults.LineWidth, .001f);
        public MinFloatParameter pointSize = new(Defaults.PointSize, .01f);
        public MinFloatParameter intensity = new(Defaults.Intensity, 0f);
        public ColorParameter color = new(Defaults.Color, true, true, true);
        public MinFloatParameter pulseSpeed = new(Defaults.PulseSpeed, 0f);
    }
}
