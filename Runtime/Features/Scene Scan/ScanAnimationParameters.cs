using System;
using UnityEngine;

namespace Tsukuyomi.Rendering
{
    /// <summary>Externally evaluated animation only; appearance, range and coverage mode remain panel-authored.</summary>
    [Serializable]
    public struct ScanAnimationParameters
    {
        public Vector3 OriginWS;
        public float Progress, Blend, WorldProgress, ScreenProgress;
        public bool SamplingCompleted;
        public float Flash, Reveal, Random01, HorizontalGlitch, ChromaticSplit;

        internal void Apply(ref ScanCoverageParameters coverage)
        {
            coverage.OriginWS = OriginWS; coverage.Progress = Progress; coverage.Blend = Blend;
            coverage.WorldProgress = WorldProgress; coverage.ScreenProgress = ScreenProgress;
        }
    }
}
