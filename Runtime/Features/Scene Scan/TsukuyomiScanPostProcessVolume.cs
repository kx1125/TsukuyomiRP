using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering
{
    [Serializable, VolumeComponentMenu("TsukuyomiRP/ScanPostProcess"), DisplayInfo(name = "ScanPostProcess")]
    public sealed class TsukuyomiScanPostProcessVolume : TsukuyomiSceneEffectVolume
    {
        private static readonly ScanPostProcessParameters Defaults = ScanPostProcessParameters.Default;
        public MinFloatParameter cellSize = new(Defaults.CellSize, .01f);
        public ClampedFloatParameter desaturation = new(Defaults.Desaturation, 0f, 1f);
        public ClampedFloatParameter tintStrength = new(Defaults.TintStrength, 0f, 1f);
        public MinFloatParameter outlineStrength = new(Defaults.OutlineStrength, 0f);
        public MinFloatParameter fresnelStrength = new(Defaults.FresnelStrength, 0f);
        public MinFloatParameter surfaceGridStrength = new(Defaults.SurfaceGridStrength, 0f);
        public MinFloatParameter flashStrength = new(Defaults.FlashStrength, 0f);
        public MinFloatParameter pulseSpeed = new(Defaults.PulseSpeed, 0f);
        public ClampedFloatParameter basicIntensity = new(Defaults.BasicIntensity, 0f, 1f);
        public MinFloatParameter gridSubdivisions = new(Defaults.GridSubdivisions, 1f);
        public FloatParameter flash = new(Defaults.Flash);
        public ClampedFloatParameter reveal = new(Defaults.Reveal, 0f, 1f);
        public ClampedFloatParameter random01 = new(Defaults.Random01, 0f, 1f);
        public ClampedFloatParameter voxelStrength = new(Defaults.VoxelStrength, 0f, 2f);
        public ClampedFloatParameter voxelTransitionWeight = new(Defaults.VoxelTransitionWeight, 0f, 1f);
        public FloatParameter horizontalGlitch = new(Defaults.HorizontalGlitch);
        public MinFloatParameter chromaticSplit = new(Defaults.ChromaticSplit, 0f);
        public ClampedFloatParameter glitchStrength = new(Defaults.GlitchStrength, 0f, 2f);
        public ColorParameter accentColor = new(Defaults.AccentColor, true, true, true);
        public ColorParameter tintColor = new(Defaults.TintColor, true, true, true);
        public ClampedIntParameter debugView = new(Defaults.DebugView, 0, 6);
        public BoolParameter enableScan = new(true);
        public BoolParameter enableVoxelGlitch = new(true);
        public BoolParameter enableHorizontalGlitch = new(true);
        public BoolParameter enableChromaticSplit = new(true);
    }
}
