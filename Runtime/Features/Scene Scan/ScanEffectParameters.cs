using System;
using UnityEngine;

namespace Tsukuyomi.Rendering
{
    public enum ScanCoverageMode { Full, Scan }

    [Serializable]
    public struct ScanCoverageParameters
    {
        public ScanCoverageMode Mode;
        public Vector3 OriginWS;
        public float Progress, Blend, WorldProgress, ScreenProgress, MaxRange, RingWidth, NoiseStrength;
        public static ScanCoverageParameters Default => new()
        {
            Mode = ScanCoverageMode.Full, Progress = 1f, Blend = 1f, WorldProgress = 1f,
            ScreenProgress = 1f, MaxRange = 100f, RingWidth = 5f, NoiseStrength = 2f
        };
    }

    [Serializable]
    public struct VoxelTransitionParameters
    {
        public ScanCoverageParameters Coverage;
        public float CellSize, SamplingStrength, DistortionStrength;
        public bool SamplingCompleted;
        public static VoxelTransitionParameters Default => new()
        {
            Coverage = ScanCoverageParameters.Default, CellSize = 5f,
            SamplingStrength = 1f, DistortionStrength = 1f, SamplingCompleted = true
        };
    }

    [Flags]
    public enum ScanPostEffect
    {
        None = 0, Scan = 1, VoxelGlitch = 2, HorizontalGlitch = 4, ChromaticSplit = 8,
        All = Scan | VoxelGlitch | HorizontalGlitch | ChromaticSplit
    }

    [Serializable]
    public struct ScanPostProcessParameters
    {
        public ScanCoverageParameters Coverage;
        public float CellSize, Desaturation, TintStrength, OutlineStrength, FresnelStrength;
        public float SurfaceGridStrength, FlashStrength, PulseSpeed, BasicIntensity, GridSubdivisions;
        public float Flash, Reveal, Random01, VoxelStrength, VoxelTransitionWeight;
        public float HorizontalGlitch, ChromaticSplit, GlitchStrength;
        [ColorUsage(true, true)] public Color AccentColor, TintColor;
        public int DebugView;
        public static ScanPostProcessParameters Default => new()
        {
            Coverage = ScanCoverageParameters.Default, CellSize = 5f,
            Desaturation = 1f, TintStrength = 1f, OutlineStrength = 1f, FresnelStrength = 1f,
            SurfaceGridStrength = 1f, FlashStrength = .05f, PulseSpeed = 2f,
            BasicIntensity = .8f, GridSubdivisions = 4f, Reveal = 1f,
            VoxelStrength = 1f, VoxelTransitionWeight = 1f, GlitchStrength = 1f,
            AccentColor = new Color(0f, .5f, 1f, 1f), TintColor = new Color(0f, .05f, .1f, 1f)
        };
    }

    [Serializable]
    public struct XrayRenderParameters
    {
        public ScanCoverageParameters Coverage;
        public float CellSize, Intensity;
        [ColorUsage(true, true)] public Color PrimaryColor, SecondaryColor;
        public static XrayRenderParameters Default => new()
        {
            Coverage = ScanCoverageParameters.Default, CellSize = 5f, Intensity = 1f,
            PrimaryColor = new Color(0f, .5f, 1f, 1f), SecondaryColor = new Color(1f, .10f, .005f, 1f)
        };
    }

    public enum RendererMaskRole { Xray, Excluded }

    /// <summary>Numeric render tags and mask roles carry no gameplay classification.</summary>
    [Serializable]
    public struct RendererMaskEntry
    {
        public Renderer Renderer;
        public RendererMaskRole Role;
        public int Tag;
        public RendererMaskEntry(Renderer renderer, int tag = 1, RendererMaskRole role = RendererMaskRole.Xray)
        {
            Renderer = renderer; Tag = tag; Role = role;
        }
    }
}
