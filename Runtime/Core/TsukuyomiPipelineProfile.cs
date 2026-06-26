using System.Collections.Generic;
using UnityEngine;


namespace Tsukuyomi.Rendering
{
    public enum TsukuyomiShadowDenoiser
    {
        None,
        Spatial
    }

    [CreateAssetMenu(menuName = "TsukuyomiRpP/Pipeline Profile", fileName = "TsukuyomiPipelineProfile")]
    public class TsukuyomiPipelineProfile : ScriptableObject
    {
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
