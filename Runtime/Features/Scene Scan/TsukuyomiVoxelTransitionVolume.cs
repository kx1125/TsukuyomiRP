using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering
{
    [Serializable, VolumeComponentMenu("TsukuyomiRP/VoxelTransition"), DisplayInfo(name = "VoxelTransition")]
    public sealed class TsukuyomiVoxelTransitionVolume : TsukuyomiSceneEffectVolume
    {
        private static readonly VoxelTransitionParameters Defaults = VoxelTransitionParameters.Default;
        public MinFloatParameter cellSize = new(Defaults.CellSize, .01f);
        public ClampedFloatParameter samplingStrength = new(Defaults.SamplingStrength, 0f, 1f);
        public ClampedFloatParameter distortionStrength = new(Defaults.DistortionStrength, 0f, 1f);
        public BoolParameter samplingCompleted = new(Defaults.SamplingCompleted);
    }
}
