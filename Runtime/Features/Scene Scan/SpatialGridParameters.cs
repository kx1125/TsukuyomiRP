using System;
using UnityEngine;

namespace Tsukuyomi.Rendering
{
    public enum SpatialGridCoverage { Full, Scan }

    /// <summary>Backend-independent world-space grid appearance and externally driven coverage.</summary>
    [Serializable]
    public struct SpatialGridParameters
    {
        public Vector3 OriginWS;
        public float Spacing, Radius, LineWidth, PointSize, Intensity;
        [ColorUsage(true, true)] public Color Color;
        public SpatialGridCoverage Coverage;
        public float Blend, TransitionProgress, WorldProgress, ScreenProgress;
        public float MaxRange, RingWidth, NoiseStrength, PulseSpeed;

        public static SpatialGridParameters Default => new()
        {
            Spacing = 5f, Radius = 40f, LineWidth = .025f, PointSize = .12f, Intensity = 1f,
            Color = new Color(0f, .5f, 1f, 1f), Coverage = SpatialGridCoverage.Full,
            Blend = 1f, TransitionProgress = 1f, WorldProgress = 1f, ScreenProgress = 1f,
            MaxRange = 100f, RingWidth = 5f, NoiseStrength = 2f, PulseSpeed = 2f
        };
    }
}
