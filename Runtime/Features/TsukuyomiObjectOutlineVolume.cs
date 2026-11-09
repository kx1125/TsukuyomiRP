using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace Tsukuyomi.Rendering
{
    [Serializable]
    [VolumeComponentMenu("TsukuyomiRP/Object Outline")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [DisplayInfo(name = "Object Outline")]
    public sealed class TsukuyomiObjectOutlineVolume : VolumeComponent
    {
        public BoolParameter enable = new(true);

        [FormerlySerializedAs("referenceDistance")]
        [Tooltip("Camera distance in world units where the outline starts fading from its material width.")]
        public MinFloatParameter distanceFadeStart = new(3.0f, 0.0f);

        [Tooltip("Camera distance in world units where the outline disappears. At or below Start, this acts as a hard cutoff.")]
        public MinFloatParameter distanceFadeEnd = new(50.0f, 0.0f);

        internal bool HasFadeOverride => distanceFadeStart.overrideState || distanceFadeEnd.overrideState;
    }
}
