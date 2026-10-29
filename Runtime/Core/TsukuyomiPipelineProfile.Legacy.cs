using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Tsukuyomi.Rendering
{
    public sealed partial class TsukuyomiPipelineProfile : ISerializationCallbackReceiver
    {
        [NonSerialized] public bool WasLegacyMigrated;
        public void OnBeforeSerialize() { }
        public void OnAfterDeserialize()
        {
            if (_schemaVersion != 0) return;
            Passes ??= new List<RenderPassBase>();
            AddLegacy<RectLightPass>(EnableRectLights);
            AddLegacy<RayTracedShadowPass>(EnableRayTracedShadows);
            AddLegacy<PlanarReflectionPass>(EnablePlanarReflection);
            AddLegacy<PcssPass>(EnablePCSS);
            AddLegacy<PerObjectShadowPass>(EnablePerObjectShadow);
            AddLegacy<ContactShadowPass>(EnableContactShadow);
            AddLegacy<MsvoPass>(EnableMSVO);
            AddLegacy<GtaoPass>(EnableGTAO);
            AddLegacy<SsgiPass>(EnableScreenSpaceGlobalIllumination);
            AddLegacy<VolumeLightPass>(EnableVolumeLight);
            AddLegacy<SssSkinPass>(EnableSssSkin);
            AddLegacy<BloomPass>(EnableTsukuyomiPostProcessing && EnableCustomBloom);
            AddLegacy<TonemappingPass>(EnableTsukuyomiPostProcessing && EnableTonemapping);
            AddLegacy<NeuralRenderingPass>(true);
            AddLegacy<Fsr3MasksPass>(true);
            foreach (var pass in Passes) pass?.EnsureNodeId();
            _schemaVersion = CurrentSchemaVersion;
            WasLegacyMigrated = true;
            NotifyChanged();
        }
        private void AddLegacy<T>(bool enabled) where T : RenderPassBase, new()
        {
            if (Passes.Exists(pass => pass is T)) return;
            var node = new T { Enabled = enabled };
            foreach (var target in typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                var source = typeof(TsukuyomiPipelineProfile).GetField(target.Name);
                if (source != null && source.FieldType == target.FieldType) target.SetValue(node, source.GetValue(this));
            }
            Passes.Add(node);
        }

        // Keep legacy serialized names for loading existing Profiles.
        [Header("Rect Lights")]
        [HideInInspector] public bool EnableRectLights;
        [Range(1, 64)] [HideInInspector] public int MaxRectLights = 16;

        [Header("Ray Traced Shadows")]
        [HideInInspector] public bool EnableRayTracedShadows;
        [Range(1, 64)] [HideInInspector] public int RayTracedShadowSamples = 8;
        [Range(1, 16)] [HideInInspector] public int MaxRayTracedShadowLights = 4;
        [HideInInspector] public LayerMask RayTracedShadowCasterMask = -1;
        [Min(0.01f)] [HideInInspector] public float RayTracedShadowDirectionalDistance = 100;

        [Header("Planar Reflection")]
        [HideInInspector] public bool EnablePlanarReflection;

        [Min(0.01f)]
        [HideInInspector] public float PlanarReflectionRenderTextureScale = 0.5f;

        [HideInInspector] public LayerMask PlanarReflectionLayerMask = -1;

        [HideInInspector] public bool EnablePCSS;

        [Min(1)]
        [HideInInspector] public int PcssFindBlockerSampleCount = 16;

        [Min(1)]
        [HideInInspector] public int PcssPcfSampleCount = 32;

        [Min(0.001f)]
        [HideInInspector] public float PcssAngularDiameter = 0.5357f;

        [Min(0.001f)]
        [HideInInspector] public float PcssBlockerSearchAngularDiameter = 0.5357f;

        [Min(0.001f)]
        [HideInInspector] public float PcssMinFilterMaxAngularDiameter = 0.5357f;

        [Min(0.0f)]
        [HideInInspector] public float PcssMaxPenumbraSize = 0.56f;

        [Min(0.0f)]
        [HideInInspector] public float PcssMaxSamplingDistance = 0.1f;

        [Min(0.0f)]
        [HideInInspector] public float PcssMinFilterSizeTexels = 1.0f;

        [Range(1, 32)]
        [HideInInspector] public int PcssPenumbraMaskScale = 4;

        [Header("Per Object Shadow")]
        [HideInInspector] public bool EnablePerObjectShadow;

        [HideInInspector] public RenderingLayerMask PerObjectShadowRenderingLayer = TsukuyomiPerObjectShadowDefaults.RenderingLayerMask;

        [HideInInspector] public TsukuyomiPerObjectShadowDepthBits PerObjectShadowDepthBits = TsukuyomiPerObjectShadowDepthBits.Depth16;

        [HideInInspector] public TsukuyomiPerObjectShadowTileResolution PerObjectShadowTileResolution = TsukuyomiPerObjectShadowTileResolution._1024;

        [Range(0.0f, 1000.0f)]
        [HideInInspector] public float PerObjectShadowLengthOffset = 500.0f;

        [HideInInspector] public bool EnableContactShadow;

        [System.Obsolete("Tsukuyomi contact shadow compute shaders are loaded from TsukuyomiRenderPipelineResources.")]
        [HideInInspector] public ComputeShader ContactShadowComputeShader;

        [System.Obsolete("Tsukuyomi contact shadow compute shaders are loaded from TsukuyomiRenderPipelineResources.")]
        [HideInInspector] public ComputeShader ContactShadowDenoiserComputeShader;

        [Range(0.0f, 1.0f)]
        [HideInInspector] public float ContactShadowLength = 0.15f;

        [Range(0.0f, 1.0f)]
        [HideInInspector] public float ContactShadowDistanceScaleFactor = 0.5f;

        [Min(0.0f)]
        [HideInInspector] public float ContactShadowMaxDistance = 50.0f;

        [Min(0.0f)]
        [HideInInspector] public float ContactShadowMinDistance = 0.0f;

        [Min(0.0f)]
        [HideInInspector] public float ContactShadowFadeDistance = 5.0f;

        [Min(0.0f)]
        [HideInInspector] public float ContactShadowFadeInDistance = 0.0f;

        [Range(0.0f, 1.0f)]
        [HideInInspector] public float ContactShadowRayBias = 0.2f;

        [Range(0.02f, 10.0f)]
        [HideInInspector] public float ContactShadowThicknessScale = 0.15f;

        [HideInInspector] public TsukuyomiShadowDenoiser ContactShadowDenoiser = TsukuyomiShadowDenoiser.None;

        [Range(4, 64)]
        [HideInInspector] public int ContactShadowSampleCount = 10;

        [Range(1, 32)]
        [HideInInspector] public int ContactShadowFilterSize = 16;

        [HideInInspector] public bool EnableGTAO;

        [HideInInspector] public bool GtaoDownSample = true;

        [Range(0.0f, 4.0f)]
        [HideInInspector] public float GtaoIntensity = 1.0f;

        [Range(0.0f, 1.0f)]
        [HideInInspector] public float GtaoDirectLightingStrength = 0.25f;

        [Range(0.25f, 5.0f)]
        [HideInInspector] public float GtaoRadius = 2.0f;

        [Range(0.001f, 1.0f)]
        [HideInInspector] public float GtaoThickness = 0.5f;

        [Range(0.0f, 1.0f)]
        [HideInInspector] public float GtaoSpatialBilateralAggressiveness = 0.15f;

        [Range(0.0f, 1.0f)]
        [HideInInspector] public float GtaoBlurSharpness = 0.1f;

        [Range(2, 32)]
        [HideInInspector] public int GtaoStepCount = 6;

        [Range(16, 256)]
        [HideInInspector] public int GtaoMaximumRadiusInPixels = 40;

        [Range(1, 6)]
        [HideInInspector] public int GtaoDirectionCount = 2;

        [Header("Multi Scale Volumetric Occlusion")]
        [Tooltip("MSVO takes precedence over GTAO on cameras where MSVO is active.")]
        [HideInInspector] public bool EnableMSVO;

        [Tooltip("Color Composite multiplies rendered opaque color. Lighting publishes the AO texture before opaque lighting.")]
        [HideInInspector] public TsukuyomiMsvoCompositionMode MsvoCompositionMode = TsukuyomiMsvoCompositionMode.ColorComposite;

        [HideInInspector] public bool MsvoDebugOcclusion = false;

        [Range(0f, 4f)]
        [HideInInspector] public float MsvoIntensity = .6f;

        [Range(0.01f, 100f)]
        [HideInInspector] public float MsvoThicknessModifier = 10f;

        [Tooltip("Number of AO scales, from half resolution to one-sixteenth resolution. Lower values reduce broad occlusion and GPU work.")]
        [Range(1f, 4f)]
        [HideInInspector] public int MsvoScaleCount = 4;

        [Tooltip("Uses all 12 sample groups instead of the original 7. Costs more depth samples.")]
        [HideInInspector] public bool MsvoExhaustiveSampling = false;

        [Tooltip("Depth sphere diameter in deinterleaved pixels. Controls the depth test thickness; the XY sampling pattern is fixed.")]
        [Range(1f, 40f)]
        [HideInInspector] public float MsvoDepthSphereDiameter = 10f;

        [Tooltip("Base-10 logarithmic tolerance used by the depth-aware blur.")]
        [Range(-8f, 0f)]
        [HideInInspector] public float MsvoBlurTolerance = -4.6f;

        [Tooltip("Base-10 logarithmic depth tolerance used during upsampling.")]
        [Range(-12f, -1f)]
        [HideInInspector] public float MsvoUpsampleTolerance = -12f;

        [Tooltip("Base-10 logarithmic weight used to soften depth mismatches during upsampling.")]
        [Range(-8f, 8f)]
        [HideInInspector] public float MsvoNoiseFilterTolerance = 0f;

        [Tooltip("Reference width used to normalize the blur tolerance between resolutions.")]
        [Range(320f, 7680f)]
        [HideInInspector] public float MsvoBlurReferenceWidth = 1920f;

        [Tooltip("Color Composite shadow tint, passed directly without gamma conversion. Black gives neutral darkening. Unused in Lighting mode.")]
        [ColorUsage(false, false)]
        [HideInInspector] public Color MsvoOcclusionColor = new Color(.0980392173f, 0, 0, 1);

        [Tooltip("Influence on direct lighting in Lighting mode. Zero affects indirect lighting only.")]
        [Range(0f, 1f)]
        [HideInInspector] public float MsvoDirectLightingStrength = .25f;

        [Header("Screen Space Global Illumination")]
        [HideInInspector] public bool EnableScreenSpaceGlobalIllumination;

        [HideInInspector] public bool SsgiHalfResolution = true;

        [Range(0.0f, 0.5f)]
        [HideInInspector] public float SsgiDepthBufferThickness = 0.1f;

        [Range(1, 256)]
        [HideInInspector] public int SsgiMaxRaySteps = 64;

        [HideInInspector] public TsukuyomiSsgiRayMissFallback SsgiRayMissFallback = TsukuyomiSsgiRayMissFallback.ReflectionProbesAndSky;

        [HideInInspector] public bool SsgiEnableProbeVolumes = true;

        [HideInInspector] public bool SsgiDenoise = true;

        [Range(0.001f, 10.0f)]
        [HideInInspector] public float SsgiDenoiserRadius = 0.6f;

        [HideInInspector] public bool SsgiSecondDenoiser;

        [HideInInspector] public bool SsgiHalfResolutionDenoiser = true;

        [HideInInspector] public bool EnableVolumeLight;

        [Range(0.0f, 512.0f)]
        [HideInInspector] public float VolumeLightDistance = 64.0f;

        [HideInInspector] public float VolumeLightBaseHeight = 0.0f;

        [HideInInspector] public float VolumeLightMaximumHeight = 50.0f;

        [HideInInspector] public bool VolumeLightEnableGround;

        [HideInInspector] public float VolumeLightGroundHeight = 0.0f;

        [Range(0.0f, 1.0f)]
        [HideInInspector] public float VolumeLightDensity = 0.2f;

        [Min(0.05f)]
        [HideInInspector] public float VolumeLightAttenuationDistance = 128.0f;

        [HideInInspector] public bool VolumeLightEnableProbeVolumeContribution;

        [Range(0.0f, 1.0f)]
        [HideInInspector] public float VolumeLightProbeVolumeContributionWeight = 1.0f;

        [HideInInspector] public bool VolumeLightEnableMainLightContribution;

        [Range(-1.0f, 1.0f)]
        [HideInInspector] public float VolumeLightAnisotropy = 0.4f;

        [Range(0.0f, 1.0f)]
        [HideInInspector] public float VolumeLightScattering = 0.15f;

        [HideInInspector] public Color VolumeLightTint = Color.white;

        [HideInInspector] public bool VolumeLightEnableAdditionalLightsContribution;

        [Range(8, 256)]
        [HideInInspector] public int VolumeLightMaxSteps = 128;

        [Range(1, 4)]
        [HideInInspector] public int VolumeLightBlurIterations = 2;

        [Range(0.0f, 0.1f)]
        [HideInInspector] public float VolumeLightTransmittanceThreshold = 0.01f;

        [Header("Post Processing")]
        [HideInInspector] public bool EnableTsukuyomiPostProcessing = true;

        [HideInInspector] public bool EnableCustomBloom;

        [Min(0.0f)]
        [HideInInspector] public float CustomBloomThreshold = 0.7f;

        [Min(0.0f)]
        [HideInInspector] public float CustomBloomIntensity = 0.75f;

        [Range(0.0f, 1.0f)]
        [HideInInspector] public float CustomBloomLumRangeScale = 0.2f;

        [Range(0.0f, 5.0f)]
        [HideInInspector] public float CustomBloomPreFilterScale = 2.5f;

        [HideInInspector] public Vector4 CustomBloomBlurCompositeWeight = new(0.3f, 0.3f, 0.26f, 0.15f);

        [HideInInspector] public Color CustomBloomTint = new(1.0f, 1.0f, 1.0f, 0.0f);

        [HideInInspector] public bool EnableTonemapping;

        [HideInInspector] public TsukuyomiTonemappingMode TonemappingMode = TsukuyomiTonemappingMode.None;

        [Range(1.0f, 20.0f)]
        [HideInInspector] public float TonemappingMaxBrightness = 1.0f;

        [Range(0.0f, 5.0f)]
        [HideInInspector] public float TonemappingContrast = 1.11f;

        [Range(0.0f, 1.0f)]
        [HideInInspector] public float TonemappingLinearSectionStart = 0.2f;

        [Range(0.0f, 1.0f)]
        [HideInInspector] public float TonemappingLinearSectionLength = 0.4f;

        [Range(1.0f, 3.0f)]
        [HideInInspector] public float TonemappingBlackPow = 1.29f;

        [Range(0.0f, 1.0f)]
        [HideInInspector] public float TonemappingBlackMin = 0.0f;


        [Header("SSS Skin")]
        [HideInInspector] public bool EnableSssSkin;

        [Tooltip("Use RGBA16Float lighting buffers instead of R11G11B10Float when supported.")]
        [HideInInspector] public bool SssSkinHighPrecision;

        [HideInInspector] public LayerMask SssSkinLayerMask;

        [HideInInspector] public TsukuyomiSssSkinQuality SssSkinQuality = TsukuyomiSssSkinQuality.High;

        [Range(0.0f, 10.0f)]
        [HideInInspector] public float SssSkinScatteringRadius = 1.0f;

        [Range(0, 10)]
        [HideInInspector] public int SssSkinScatteringIterations = 3;

        [Range(1, 32)]
        [HideInInspector] public int SssSkinShaderIterations = 12;

        [Range(0.0001f, 5.0f)]
        [HideInInspector] public float SssSkinDepthTest = 0.3f;

        [Range(0.001f, 2.0f)]
        [HideInInspector] public float SssSkinNormalTest = 0.3f;

        [Range(0.0f, 10000.0f)]
        [HideInInspector] public float SssSkinMaxDistance = 10.0f;

        [HideInInspector] public Color SssSkinColor = Color.yellow;

        [HideInInspector] public bool SssSkinRandomizedRotation;

        [Range(0.0f, 32.0f)]
        [HideInInspector] public float SssSkinDitherScale = 1.0f;

        [Range(0.0f, 1.0f)]
        [HideInInspector] public float SssSkinDitherIntensity = 0.0f;

        [HideInInspector] public Texture SssSkinNoiseTexture;
    }
}
