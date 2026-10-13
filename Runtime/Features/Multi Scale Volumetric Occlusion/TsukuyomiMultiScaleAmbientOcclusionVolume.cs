using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering
{
    public enum TsukuyomiMsvoCompositionMode
    {
        [InspectorName("Color Composite")] ColorComposite = 0,
        [InspectorName("Lighting")] Lighting = 1
    }

    [Serializable]
    public sealed class TsukuyomiMsvoCompositionModeParameter : VolumeParameter<TsukuyomiMsvoCompositionMode>
    {
        public TsukuyomiMsvoCompositionModeParameter(TsukuyomiMsvoCompositionMode value, bool overrideState = false)
            : base(value, overrideState) { }
    }

    [Serializable, VolumeComponentMenu("TsukuyomiRP/Multi Scale Volumetric Occlusion")]
    [DisplayInfo(name = "Multi Scale Volumetric Occlusion")]
    public sealed class TsukuyomiMultiScaleAmbientOcclusionVolume : VolumeComponent
    {
        public BoolParameter enable = new(false);
        [Tooltip("Color Composite multiplies rendered opaque color. Lighting publishes the AO texture before opaque lighting.")]
        public TsukuyomiMsvoCompositionModeParameter compositionMode = new(TsukuyomiMsvoCompositionMode.ColorComposite);
        public BoolParameter debugOcclusion = new(false);
        public ClampedFloatParameter intensity = new(.6f, 0f, 4f);
        public ClampedFloatParameter thicknessModifier = new(10f, 0.01f, 100f);
        [Tooltip("Number of AO scales, from half resolution to one-sixteenth resolution. Lower values reduce broad occlusion and GPU work.")]
        public ClampedIntParameter scaleCount = new(4, 1, 4);
        [Tooltip("Uses all 12 sample groups instead of the original 7. Costs more depth samples.")]
        public BoolParameter exhaustiveSampling = new(false);
        [Tooltip("Depth sphere diameter in deinterleaved pixels. Controls the depth test thickness; the XY sampling pattern is fixed.")]
        public ClampedFloatParameter depthSphereDiameter = new(10f, 1f, 40f);
        [Tooltip("Base-10 logarithmic tolerance used by the depth-aware blur.")]
        public ClampedFloatParameter blurTolerance = new(-4.6f, -8f, 0f);
        [Tooltip("Base-10 logarithmic depth tolerance used during upsampling.")]
        public ClampedFloatParameter upsampleTolerance = new(-12f, -12f, -1f);
        [Tooltip("Base-10 logarithmic weight used to soften depth mismatches during upsampling.")]
        public ClampedFloatParameter noiseFilterTolerance = new(0f, -8f, 8f);
        [Tooltip("Reference width used to normalize the blur tolerance between resolutions.")]
        public ClampedFloatParameter blurReferenceWidth = new(1920f, 320f, 7680f);
        [Tooltip("Color Composite shadow tint, passed directly without gamma conversion. Black gives neutral darkening. Unused in Lighting mode.")]
        public ColorParameter occlusionColor = new(new Color(.0980392173f, 0, 0, 1));
        [Tooltip("Influence on direct lighting in Lighting mode. Zero affects indirect lighting only.")]
        public ClampedFloatParameter directLightingStrength = new(.25f, 0f, 1f);
    }

    // Immutable per-camera snapshot: graph callbacks never read mutable assets or Volume stacks.
    internal readonly struct TsukuyomiMultiScaleAmbientOcclusionResolvedSettings
    {
        public readonly bool Enabled;
        public readonly TsukuyomiMsvoCompositionMode CompositionMode;
        public readonly bool DebugOcclusion;
        public readonly float Intensity;
        public readonly float ThicknessModifier;
        public readonly int ScaleCount;
        public readonly bool ExhaustiveSampling;
        public readonly float DepthSphereDiameter;
        public readonly float BlurTolerance;
        public readonly float UpsampleTolerance;
        public readonly float NoiseFilterTolerance;
        public readonly float BlurReferenceWidth;
        public readonly Color OcclusionColor;
        public readonly float DirectLightingStrength;
        public bool IsLighting => CompositionMode == TsukuyomiMsvoCompositionMode.Lighting;
        public bool IsActive => Enabled && Intensity > 0;

        private TsukuyomiMultiScaleAmbientOcclusionResolvedSettings(bool enabled,
            TsukuyomiMsvoCompositionMode compositionMode,
            bool debugOcclusion,
            float intensity,
            float thicknessModifier,
            int scaleCount,
            bool exhaustiveSampling,
            float depthSphereDiameter,
            float blurTolerance,
            float upsampleTolerance,
            float noiseFilterTolerance,
            float blurReferenceWidth,
            Color occlusionColor,
            float directLightingStrength)
        {
            Enabled = enabled;
            CompositionMode = compositionMode == TsukuyomiMsvoCompositionMode.Lighting ? compositionMode : TsukuyomiMsvoCompositionMode.ColorComposite;
            DebugOcclusion = debugOcclusion;
            Intensity = Mathf.Clamp(intensity, 0, 4);
            ThicknessModifier = Mathf.Clamp(thicknessModifier, .01f, 100);
            ScaleCount = Mathf.Clamp(scaleCount, 1, 4);
            ExhaustiveSampling = exhaustiveSampling;
            DepthSphereDiameter = Mathf.Clamp(depthSphereDiameter, 1, 40);
            BlurTolerance = Mathf.Clamp(blurTolerance, -8, 0);
            UpsampleTolerance = Mathf.Clamp(upsampleTolerance, -12, -1);
            NoiseFilterTolerance = Mathf.Clamp(noiseFilterTolerance, -8, 8);
            BlurReferenceWidth = Mathf.Clamp(blurReferenceWidth, 320, 7680);
            OcclusionColor = new Color(Mathf.Clamp01(occlusionColor.r), Mathf.Clamp01(occlusionColor.g), Mathf.Clamp01(occlusionColor.b), 1);
            DirectLightingStrength = Mathf.Clamp01(directLightingStrength);
        }

        public static TsukuyomiMultiScaleAmbientOcclusionResolvedSettings From(
            TsukuyomiPipelineProfile settings, TsukuyomiMultiScaleAmbientOcclusionVolume volume)
        {
            return new(settings.EnableMSVO && Resolve(volume?.enable, true),
                Resolve(volume?.compositionMode, settings.MsvoCompositionMode),
                Resolve(volume?.debugOcclusion, settings.MsvoDebugOcclusion),
                Resolve(volume?.intensity, settings.MsvoIntensity),
                Resolve(volume?.thicknessModifier, settings.MsvoThicknessModifier),
                Resolve(volume?.scaleCount, settings.MsvoScaleCount),
                Resolve(volume?.exhaustiveSampling, settings.MsvoExhaustiveSampling),
                Resolve(volume?.depthSphereDiameter, settings.MsvoDepthSphereDiameter),
                Resolve(volume?.blurTolerance, settings.MsvoBlurTolerance),
                Resolve(volume?.upsampleTolerance, settings.MsvoUpsampleTolerance),
                Resolve(volume?.noiseFilterTolerance, settings.MsvoNoiseFilterTolerance),
                Resolve(volume?.blurReferenceWidth, settings.MsvoBlurReferenceWidth),
                Resolve(volume?.occlusionColor, settings.MsvoOcclusionColor),
                Resolve(volume?.directLightingStrength, settings.MsvoDirectLightingStrength));
        }

        private static T Resolve<T>(VolumeParameter<T> parameter, T fallback)
            => parameter != null && parameter.overrideState ? parameter.value : fallback;
    }
}
