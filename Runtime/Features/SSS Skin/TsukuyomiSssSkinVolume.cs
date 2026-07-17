using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering
{
    [Serializable]
    public sealed class TsukuyomiSssSkinQualityParameter : VolumeParameter<TsukuyomiSssSkinQuality>
    {
        public TsukuyomiSssSkinQualityParameter(TsukuyomiSssSkinQuality value, bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }

    [Serializable]
    [VolumeComponentMenu("TsukuyomiRP/SSS Skin")]
    [DisplayInfo(name = "Sss Skin Volume")]
    public sealed class TsukuyomiSssSkinVolume : VolumeComponent
    {
        public BoolParameter enable = new(false, BoolParameter.DisplayType.EnumPopup);
        public TsukuyomiSssSkinQualityParameter quality = new(TsukuyomiSssSkinQuality.High);
        public ClampedFloatParameter scatteringRadius = new(1.0f, 0.0f, 10.0f);
        public ClampedIntParameter scatteringIterations = new(3, 0, 10);
        public ClampedIntParameter shaderIterations = new(12, 1, 32);
        public ClampedFloatParameter depthTest = new(0.3f, 0.0001f, 5.0f);
        public ClampedFloatParameter normalTest = new(0.3f, 0.001f, 2.0f);
        public ClampedFloatParameter maxDistance = new(10.0f, 0.0f, 200.0f);
        public ColorParameter sssColor = new(Color.yellow, false, false, true);
        public BoolParameter randomizedRotation = new(false, BoolParameter.DisplayType.Checkbox);
        public ClampedFloatParameter ditherScale = new(1.0f, 0.0f, 5.0f);
        public ClampedFloatParameter ditherIntensity = new(0.0f, 0.0f, 0.5f);
        public TextureParameter noiseTexture = new(null);
    }

    internal readonly struct TsukuyomiSssSkinResolvedSettings
    {
        public readonly bool Enabled;
        public readonly LayerMask SkinLayerMask;
        public readonly TsukuyomiSssSkinQuality Quality;
        public readonly float ScatteringRadius;
        public readonly int ScatteringIterations;
        public readonly int ShaderIterations;
        public readonly float DepthTest;
        public readonly float NormalTest;
        public readonly float MaxDistance;
        public readonly Color SssColor;
        public readonly bool RandomizedRotation;
        public readonly float DitherScale;
        public readonly float DitherIntensity;
        public readonly Texture NoiseTexture;

        private TsukuyomiSssSkinResolvedSettings(TsukuyomiPipelineProfile profile, TsukuyomiSssSkinVolume volume)
        {
            Enabled = profile.EnableSssSkin && Resolve(volume?.enable, true);
            SkinLayerMask = profile.SssSkinLayerMask;
            Quality = Resolve(volume?.quality, profile.SssSkinQuality);
            ScatteringRadius = Resolve(volume?.scatteringRadius, profile.SssSkinScatteringRadius);
            ScatteringIterations = Resolve(volume?.scatteringIterations, profile.SssSkinScatteringIterations);
            ShaderIterations = Resolve(volume?.shaderIterations, profile.SssSkinShaderIterations);
            DepthTest = Resolve(volume?.depthTest, profile.SssSkinDepthTest);
            NormalTest = Resolve(volume?.normalTest, profile.SssSkinNormalTest);
            MaxDistance = Resolve(volume?.maxDistance, profile.SssSkinMaxDistance);
            SssColor = Resolve(volume?.sssColor, profile.SssSkinColor);
            RandomizedRotation = Resolve(volume?.randomizedRotation, profile.SssSkinRandomizedRotation);
            DitherScale = Resolve(volume?.ditherScale, profile.SssSkinDitherScale);
            DitherIntensity = Resolve(volume?.ditherIntensity, profile.SssSkinDitherIntensity);
            NoiseTexture = Resolve(volume?.noiseTexture, profile.SssSkinNoiseTexture);
        }

        public bool IsActive => Enabled && SkinLayerMask.value != 0;

        public static TsukuyomiSssSkinResolvedSettings From(TsukuyomiPipelineProfile profile, TsukuyomiSssSkinVolume volume)
        {
            return new TsukuyomiSssSkinResolvedSettings(profile, volume);
        }

        private static bool Resolve(BoolParameter parameter, bool fallback)
        {
            return parameter != null && parameter.overrideState ? parameter.value : fallback;
        }

        private static int Resolve(VolumeParameter<int> parameter, int fallback)
        {
            return parameter != null && parameter.overrideState ? parameter.value : fallback;
        }

        private static float Resolve(VolumeParameter<float> parameter, float fallback)
        {
            return parameter != null && parameter.overrideState ? parameter.value : fallback;
        }

        private static Color Resolve(VolumeParameter<Color> parameter, Color fallback)
        {
            return parameter != null && parameter.overrideState ? parameter.value : fallback;
        }

        private static Texture Resolve(VolumeParameter<Texture> parameter, Texture fallback)
        {
            return parameter != null && parameter.overrideState ? parameter.value : fallback;
        }

        private static TsukuyomiSssSkinQuality Resolve(TsukuyomiSssSkinQualityParameter parameter, TsukuyomiSssSkinQuality fallback)
        {
            return parameter != null && parameter.overrideState ? parameter.value : fallback;
        }
    }
}

