using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering
{
    [Serializable, VolumeComponentMenu("TsukuyomiRP/XrayRender"), DisplayInfo(name = "XrayRender")]
    public sealed class TsukuyomiXrayRenderVolume : TsukuyomiSceneEffectVolume
    {
        private static readonly XrayRenderParameters Defaults = XrayRenderParameters.Default;
        public MinFloatParameter cellSize = new(Defaults.CellSize, .01f);
        public MinFloatParameter intensity = new(Defaults.Intensity, 0f);
        public ColorParameter primaryColor = new(Defaults.PrimaryColor, true, true, true);
        public ColorParameter secondaryColor = new(Defaults.SecondaryColor, true, true, true);
    }
}
