using System;
using UnityEngine;

namespace Tsukuyomi.Rendering
{
    [Serializable, InjectionPoint(InjectionPoint.AfterOpaque)]
    public sealed class MsvoPass : RenderPassDefinition
    {
        public MsvoPass() => InjectionPoint = InjectionPoint.AfterOpaque;
        public override string Name => "Multi Scale Volumetric Occlusion";

        [Tooltip("Color Composite multiplies rendered opaque color. Lighting publishes the AO texture before opaque lighting.")]
        public TsukuyomiMsvoCompositionMode MsvoCompositionMode = TsukuyomiMsvoCompositionMode.ColorComposite;

        public bool MsvoDebugOcclusion = false;

        [Range(0f, 4f)]
        public float MsvoIntensity = .6f;

        [Range(0.01f, 100f)]
        public float MsvoThicknessModifier = 10f;

        [Tooltip("Number of AO scales, from half resolution to one-sixteenth resolution. Lower values reduce broad occlusion and GPU work.")]
        [Range(1f, 4f)]
        public int MsvoScaleCount = 4;

        [Tooltip("Uses all 12 sample groups instead of the original 7. Costs more depth samples.")]
        public bool MsvoExhaustiveSampling = false;

        [Tooltip("Depth sphere diameter in deinterleaved pixels. Controls the depth test thickness; the XY sampling pattern is fixed.")]
        [Range(1f, 40f)]
        public float MsvoDepthSphereDiameter = 10f;

        [Tooltip("Base-10 logarithmic tolerance used by the depth-aware blur.")]
        [Range(-8f, 0f)]
        public float MsvoBlurTolerance = -4.6f;

        [Tooltip("Base-10 logarithmic depth tolerance used during upsampling.")]
        [Range(-12f, -1f)]
        public float MsvoUpsampleTolerance = -12f;

        [Tooltip("Base-10 logarithmic weight used to soften depth mismatches during upsampling.")]
        [Range(-8f, 8f)]
        public float MsvoNoiseFilterTolerance = 0f;

        [Tooltip("Reference width used to normalize the blur tolerance between resolutions.")]
        [Range(320f, 7680f)]
        public float MsvoBlurReferenceWidth = 1920f;

        [Tooltip("Color Composite shadow tint, passed directly without gamma conversion. Black gives neutral darkening. Unused in Lighting mode.")]
        [ColorUsage(false, false)]
        public Color MsvoOcclusionColor = new Color(.0980392173f, 0, 0, 1);

        [Tooltip("Influence on direct lighting in Lighting mode. Zero affects indirect lighting only.")]
        [Range(0f, 1f)]
        public float MsvoDirectLightingStrength = .25f;


        public override IRenderPassRuntime CreateRuntime() => new TsukuyomiMultiScaleAmbientOcclusionPass(this);
    }
}
