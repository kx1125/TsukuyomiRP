using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;


namespace Tsukuyomi.Rendering
{
    public enum TsukuyomiShadowDenoiser
    {
        None,
        Spatial
    }

    public enum TsukuyomiSssSkinQuality
    {
        High,
        Medium,
        Low
    }

    public enum TsukuyomiTonemappingMode
    {
        None,
        Neutral,
        ACES,
        [InspectorName("ACES Simple")]
        ACESSimple,
        [InspectorName("Gran Turismo")]
        GranTurismo
    }

    [CreateAssetMenu(menuName = "TsukuyomiRpP/Pipeline Profile", fileName = "TsukuyomiPipelineProfile")]
    public class TsukuyomiPipelineProfile : ScriptableObject
    {
        [Header("Planar Reflection")]
        public bool EnablePlanarReflection;

        [Min(0.01f)]
        public float PlanarReflectionRenderTextureScale = 0.5f;

        public LayerMask PlanarReflectionLayerMask = -1;

        public bool EnablePCSS;

        [Min(1)]
        public int PcssFindBlockerSampleCount = 16;

        [Min(1)]
        public int PcssPcfSampleCount = 32;

        [Min(0.001f)]
        public float PcssAngularDiameter = 0.5357f;

        [Min(0.001f)]
        public float PcssBlockerSearchAngularDiameter = 0.5357f;

        [Min(0.001f)]
        public float PcssMinFilterMaxAngularDiameter = 0.5357f;

        [Min(0.0f)]
        public float PcssMaxPenumbraSize = 0.56f;

        [Min(0.0f)]
        public float PcssMaxSamplingDistance = 0.1f;

        [Min(0.0f)]
        public float PcssMinFilterSizeTexels = 1.0f;

        [Range(1, 32)]
        public int PcssPenumbraMaskScale = 4;

        [Header("Per Object Shadow")]
        public bool EnablePerObjectShadow;

        public RenderingLayerMask PerObjectShadowRenderingLayer = TsukuyomiPerObjectShadowDefaults.RenderingLayerMask;

        public TsukuyomiPerObjectShadowDepthBits PerObjectShadowDepthBits = TsukuyomiPerObjectShadowDepthBits.Depth16;

        public TsukuyomiPerObjectShadowTileResolution PerObjectShadowTileResolution = TsukuyomiPerObjectShadowTileResolution._1024;

        [Range(0.0f, 1000.0f)]
        public float PerObjectShadowLengthOffset = 500.0f;

        public bool EnableContactShadow;

        [System.Obsolete("Tsukuyomi contact shadow compute shaders are loaded from TsukuyomiRenderPipelineResources.")]
        [HideInInspector]
        public ComputeShader ContactShadowComputeShader;

        [System.Obsolete("Tsukuyomi contact shadow compute shaders are loaded from TsukuyomiRenderPipelineResources.")]
        [HideInInspector]
        public ComputeShader ContactShadowDenoiserComputeShader;

        [Range(0.0f, 1.0f)]
        public float ContactShadowLength = 0.15f;

        [Range(0.0f, 1.0f)]
        public float ContactShadowDistanceScaleFactor = 0.5f;

        [Min(0.0f)]
        public float ContactShadowMaxDistance = 50.0f;

        [Min(0.0f)]
        public float ContactShadowMinDistance = 0.0f;

        [Min(0.0f)]
        public float ContactShadowFadeDistance = 5.0f;

        [Min(0.0f)]
        public float ContactShadowFadeInDistance = 0.0f;

        [Range(0.0f, 1.0f)]
        public float ContactShadowRayBias = 0.2f;

        [Range(0.02f, 10.0f)]
        public float ContactShadowThicknessScale = 0.15f;

        public TsukuyomiShadowDenoiser ContactShadowDenoiser = TsukuyomiShadowDenoiser.None;

        [Range(4, 64)]
        public int ContactShadowSampleCount = 10;

        [Range(1, 32)]
        public int ContactShadowFilterSize = 16;

        public bool EnableGTAO;

        public bool GtaoDownSample = true;

        [Range(0.0f, 4.0f)]
        public float GtaoIntensity = 1.0f;

        [Range(0.0f, 1.0f)]
        public float GtaoDirectLightingStrength = 0.25f;

        [Range(0.25f, 5.0f)]
        public float GtaoRadius = 2.0f;

        [Range(0.001f, 1.0f)]
        public float GtaoThickness = 0.5f;

        [Range(0.0f, 1.0f)]
        public float GtaoSpatialBilateralAggressiveness = 0.15f;

        [Range(0.0f, 1.0f)]
        public float GtaoBlurSharpness = 0.1f;

        [Range(2, 32)]
        public int GtaoStepCount = 6;

        [Range(16, 256)]
        public int GtaoMaximumRadiusInPixels = 40;

        [Range(1, 6)]
        public int GtaoDirectionCount = 2;

        public bool EnableVolumeLight;

        [Range(0.0f, 512.0f)]
        public float VolumeLightDistance = 64.0f;

        public float VolumeLightBaseHeight = 0.0f;

        public float VolumeLightMaximumHeight = 50.0f;

        public bool VolumeLightEnableGround;

        public float VolumeLightGroundHeight = 0.0f;

        [Range(0.0f, 1.0f)]
        public float VolumeLightDensity = 0.2f;

        [Min(0.05f)]
        public float VolumeLightAttenuationDistance = 128.0f;

        public bool VolumeLightEnableProbeVolumeContribution;

        [Range(0.0f, 1.0f)]
        public float VolumeLightProbeVolumeContributionWeight = 1.0f;

        public bool VolumeLightEnableMainLightContribution;

        [Range(-1.0f, 1.0f)]
        public float VolumeLightAnisotropy = 0.4f;

        [Range(0.0f, 1.0f)]
        public float VolumeLightScattering = 0.15f;

        public Color VolumeLightTint = Color.white;

        public bool VolumeLightEnableAdditionalLightsContribution;

        [Range(8, 256)]
        public int VolumeLightMaxSteps = 128;

        [Range(1, 4)]
        public int VolumeLightBlurIterations = 2;

        [Range(0.0f, 0.1f)]
        public float VolumeLightTransmittanceThreshold = 0.01f;

        [Header("Post Processing")]
        public bool EnableTsukuyomiPostProcessing = true;

        public bool EnableCustomBloom;

        [Min(0.0f)]
        public float CustomBloomThreshold = 0.7f;

        [Min(0.0f)]
        public float CustomBloomIntensity = 0.75f;

        [Range(0.0f, 1.0f)]
        public float CustomBloomLumRangeScale = 0.2f;

        [Range(0.0f, 5.0f)]
        public float CustomBloomPreFilterScale = 2.5f;

        public Vector4 CustomBloomBlurCompositeWeight = new(0.3f, 0.3f, 0.26f, 0.15f);

        public Color CustomBloomTint = new(1.0f, 1.0f, 1.0f, 0.0f);

        public bool EnableTonemapping;

        public TsukuyomiTonemappingMode TonemappingMode = TsukuyomiTonemappingMode.None;

        [Range(1.0f, 20.0f)]
        public float TonemappingMaxBrightness = 1.0f;

        [Range(0.0f, 5.0f)]
        public float TonemappingContrast = 1.11f;

        [Range(0.0f, 1.0f)]
        public float TonemappingLinearSectionStart = 0.2f;

        [Range(0.0f, 1.0f)]
        public float TonemappingLinearSectionLength = 0.4f;

        [Range(1.0f, 3.0f)]
        public float TonemappingBlackPow = 1.29f;

        [Range(0.0f, 1.0f)]
        public float TonemappingBlackMin = 0.0f;


        [Header("SSS Skin")]
        public bool EnableSssSkin;

        public LayerMask SssSkinLayerMask;

        public TsukuyomiSssSkinQuality SssSkinQuality = TsukuyomiSssSkinQuality.High;

        [Range(0.0f, 10.0f)]
        public float SssSkinScatteringRadius = 1.0f;

        [Range(0, 10)]
        public int SssSkinScatteringIterations = 3;

        [Range(1, 32)]
        public int SssSkinShaderIterations = 12;

        [Range(0.0001f, 5.0f)]
        public float SssSkinDepthTest = 0.3f;

        [Range(0.001f, 2.0f)]
        public float SssSkinNormalTest = 0.3f;

        [Range(0.0f, 200.0f)]
        public float SssSkinMaxDistance = 10.0f;

        public Color SssSkinColor = Color.yellow;

        public bool SssSkinRandomizedRotation;

        [Range(0.0f, 5.0f)]
        public float SssSkinDitherScale = 1.0f;

        [Range(0.0f, 0.5f)]
        public float SssSkinDitherIntensity = 0.0f;

        public Texture SssSkinNoiseTexture;
        /// <summary>
        /// Polymorphic list of passes configured via the Graph Editor.
        /// Serialized using Unity's SerializeReference.
        /// </summary>
        [SerializeReference]
        [HideInInspector]
        public List<RenderPassBase> Passes = new();

        /// <summary>
        /// Editor-only JSON data for the GraphView layout.
        /// Runtime rendering code should never touch this.
        /// </summary>
        [SerializeField, HideInInspector]
        public string GraphLayoutData;
    }
}







