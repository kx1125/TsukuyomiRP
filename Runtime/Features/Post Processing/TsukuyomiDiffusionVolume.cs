using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering
{
    [Serializable]
    [VolumeComponentMenu("TsukuyomiRP/Postprocessing/Diffusion")]
    [DisplayInfo(name = "Diffusion Volume")]
    public sealed class TsukuyomiDiffusionVolume : VolumeComponent
    {
        public BoolParameter enable = new(true, BoolParameter.DisplayType.EnumPopup);
        public ClampedFloatParameter intensity = new(0.15f, 0f, 1f);

        [Tooltip("Kernel offset at 1080p. Below 1, the minimum blur fades smoothly to zero.")]
        public ClampedFloatParameter radius = new(1f, 0f, 4f);

        [Tooltip(DiffusionPass.HighlightLimitTooltip)]
        public MinFloatParameter highlightLimit = new(1f, DiffusionPass.MinHighlightLimit);
    }
}
