using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering
{
    [Serializable]
    public sealed class ScanCoverageModeParameter : VolumeParameter<ScanCoverageMode>
    {
        public ScanCoverageModeParameter(ScanCoverageMode value, bool overrideState = false) : base(value, overrideState) { }
    }

    public abstract class TsukuyomiSceneEffectVolume : VolumeComponent
    {
        public BoolParameter enable = new(false);
        public ScanCoverageModeParameter coverageMode = new(ScanCoverageMode.Full);
        public Vector3Parameter originWS = new(Vector3.zero);
        public ClampedFloatParameter progress = new(1f, 0f, 1f);
        public ClampedFloatParameter blend = new(1f, 0f, 1f);
        public ClampedFloatParameter worldProgress = new(1f, 0f, 1f);
        public ClampedFloatParameter screenProgress = new(1f, 0f, 1f);
        public MinFloatParameter maxRange = new(100f, .01f);
        public MinFloatParameter ringWidth = new(5f, .01f);
        public MinFloatParameter noiseStrength = new(2f, 0f);
    }
}
