using UnityEngine;

namespace Tsukuyomi.Rendering
{
    [CreateAssetMenu(menuName = "TsukuyomiRpP/Render Pipeline Resources", fileName = "TsukuyomiRenderPipelineResources")]
    public sealed class TsukuyomiRenderPipelineResources : ScriptableObject
    {
        [Header("Rect Lights")]
        [SerializeField] private TsukuyomiRectLightResources rectLightResources;
        public TsukuyomiRectLightResources RectLightResources => rectLightResources;

        [Header("Ray Traced Shadows")]
        [SerializeField] private UnityEngine.Rendering.RayTracingShader rayTracedShadowShader;
        [SerializeField] private ComputeShader rayTracedShadowDenoiser;
        public UnityEngine.Rendering.RayTracingShader RayTracedShadowShader => rayTracedShadowShader;
        public ComputeShader RayTracedShadowDenoiser => rayTracedShadowDenoiser;

        [Header("OpenPBR")]
        [SerializeField] private TsukuyomiOpenPBRResources openPBRResources;
        public TsukuyomiOpenPBRResources OpenPBRResources => openPBRResources;

        [Header("PCSS Shadow")]
        [SerializeField]
        private Shader screenSpacePcssShadowsShader;

        [SerializeField]
        private Material screenSpacePcssShadowsMaterial;

        [SerializeField]
        private Shader screenSpaceShadowsShader;

        [SerializeField]
        private Material screenSpaceShadowsMaterial;

        [Header("Contact Shadow")]
        [SerializeField]
        private ComputeShader contactShadowsComputeShader;

        [SerializeField]
        private ComputeShader contactShadowDenoiserComputeShader;

        [Header("Depth Pyramid")]
        [SerializeField]
        private ComputeShader depthPyramidComputeShader;

        [Header("Ground Truth Ambient Occlusion")]
        [SerializeField]
        private ComputeShader gtaoTraceComputeShader;

        [SerializeField]
        private ComputeShader gtaoSpatialDenoiseComputeShader;

        [SerializeField]
        private ComputeShader gtaoBlurAndUpsampleComputeShader;

        [Header("Multi Scale Volumetric Occlusion")]
        [SerializeField] private ComputeShader msvoDownsample1, msvoDownsample2, msvoRender, msvoUpsample;
        [SerializeField] private Shader msvoCompositeShader;
        public ComputeShader MsvoDownsample1 => msvoDownsample1;
        public ComputeShader MsvoDownsample2 => msvoDownsample2;
        public ComputeShader MsvoRender => msvoRender;
        public ComputeShader MsvoUpsample => msvoUpsample;
        public Shader MsvoCompositeShader => msvoCompositeShader;
        public bool HasMsvoResources => msvoDownsample1 && msvoDownsample2 && msvoRender && msvoUpsample && msvoCompositeShader;

        [Header("Screen Space Global Illumination")]
        [SerializeField]
        private ComputeShader ssgiTraceComputeShader;

        [SerializeField]
        private ComputeShader ssgiTemporalComputeShader;

        [SerializeField]
        private ComputeShader ssgiDiffuseDenoiserComputeShader;

        [SerializeField]
        private ComputeShader ssgiBilateralUpsampleComputeShader;

        [SerializeField]
        private Shader ssgiDebugOutputShader;

        [Header("Volume Light")]
        [SerializeField]
        private Shader volumetricFogShader;

        [SerializeField]
        private Shader downsampleDepthShader;

        [SerializeField]
        private ComputeShader volumetricFogRaymarchComputeShader;

        [SerializeField]
        private ComputeShader volumetricFogBlurComputeShader;

        [SerializeField]
        private ComputeShader volumetricFogUpsampleComputeShader;

        [Header("SSS Skin")]
        [SerializeField]
        private Shader sssSkinBlurShader;

        [SerializeField]
        private Material sssSkinBlurMaterial;

        [Header("Post Processing")]
        [SerializeField]
        private Shader postProcessUberShader;

        [SerializeField]
        private Material postProcessUberMaterial;

        [Header("Scene Scan")]
        [SerializeField]
        private Shader scanMainShader;
        [SerializeField] private Shader scanTargetMaskShader;
        [SerializeField] private Shader scanGridShader;
        [SerializeField] private Shader scanGlitchShader;
        [SerializeField] private Shader voxelTransitionShader;
        [SerializeField] private Shader xrayCompositeShader;

        [Header("FSR3 Upscaler Resources")]
        [SerializeField]
        private TsukuyomiFsr3Shaders fsr3Shaders = new();

        [Header("Default Textures")]
        [SerializeField]
        private Texture2D defaultWhiteTexture;

        [SerializeField]
        private Texture2D defaultBlackTexture;

        [SerializeField]
        private Texture2D defaultNormalTexture;

        public Shader ScreenSpacePcssShadowsShader => screenSpacePcssShadowsShader;
        public Material ScreenSpacePcssShadowsMaterial => screenSpacePcssShadowsMaterial;
        public Shader ScreenSpaceShadowsShader => screenSpaceShadowsShader;
        public Material ScreenSpaceShadowsMaterial => screenSpaceShadowsMaterial;
        public ComputeShader ContactShadowsComputeShader => contactShadowsComputeShader;
        public ComputeShader ContactShadowDenoiserComputeShader => contactShadowDenoiserComputeShader;
        public ComputeShader DepthPyramidComputeShader => depthPyramidComputeShader;
        public ComputeShader GtaoTraceComputeShader => gtaoTraceComputeShader;
        public ComputeShader GtaoSpatialDenoiseComputeShader => gtaoSpatialDenoiseComputeShader;
        public ComputeShader GtaoBlurAndUpsampleComputeShader => gtaoBlurAndUpsampleComputeShader;
        public ComputeShader SsgiTraceComputeShader => ssgiTraceComputeShader;
        public ComputeShader SsgiTemporalComputeShader => ssgiTemporalComputeShader;
        public ComputeShader SsgiDiffuseDenoiserComputeShader => ssgiDiffuseDenoiserComputeShader;
        public ComputeShader SsgiBilateralUpsampleComputeShader => ssgiBilateralUpsampleComputeShader;
        public Shader SsgiDebugOutputShader => ssgiDebugOutputShader;
        public Shader VolumetricFogShader => volumetricFogShader;
        public Shader DownsampleDepthShader => downsampleDepthShader;
        public ComputeShader VolumetricFogRaymarchComputeShader => volumetricFogRaymarchComputeShader;
        public ComputeShader VolumetricFogBlurComputeShader => volumetricFogBlurComputeShader;
        public ComputeShader VolumetricFogUpsampleComputeShader => volumetricFogUpsampleComputeShader;
        public Shader SssSkinBlurShader => sssSkinBlurShader;
        public Material SssSkinBlurMaterial => sssSkinBlurMaterial;
        public Shader PostProcessUberShader => postProcessUberShader;
        public Material PostProcessUberMaterial => postProcessUberMaterial;
        public Shader ScanMainShader => scanMainShader;
        public Shader ScanTargetMaskShader => scanTargetMaskShader;
        public Shader ScanGridShader => scanGridShader;
        public Shader ScanGlitchShader => scanGlitchShader;
        public Shader VoxelTransitionShader => voxelTransitionShader;
        public Shader XrayCompositeShader => xrayCompositeShader;
        public Texture2D DefaultWhiteTexture => defaultWhiteTexture;
        public Texture2D DefaultBlackTexture => defaultBlackTexture;
        public Texture2D DefaultNormalTexture => defaultNormalTexture;
        public TsukuyomiFsr3Shaders Fsr3Shaders => fsr3Shaders;
        [SerializeField] private Shader dlssPrepareInputsShader;
        [SerializeField] private Shader dlssNrPrepareInputsShader;
        public Shader DlssPrepareInputsShader => dlssPrepareInputsShader;
        public Shader DlssNrPrepareInputsShader => dlssNrPrepareInputsShader;

        public bool HasPcssResources => screenSpacePcssShadowsShader != null || screenSpacePcssShadowsMaterial != null;
        public bool HasContactShadowResources => contactShadowsComputeShader != null;
        public bool HasContactShadowDenoiserResources => contactShadowDenoiserComputeShader != null;
        public bool HasDepthPyramidResources => depthPyramidComputeShader != null;
        public bool HasGtaoResources => gtaoTraceComputeShader != null
            && gtaoSpatialDenoiseComputeShader != null
            && gtaoBlurAndUpsampleComputeShader != null;
        public bool HasSsgiResources => ssgiTraceComputeShader != null
            && ssgiTemporalComputeShader != null
            && ssgiDiffuseDenoiserComputeShader != null
            && ssgiBilateralUpsampleComputeShader != null;
        public bool HasVolumeLightResources => volumetricFogShader != null && downsampleDepthShader != null;
        public bool HasSssSkinResources => sssSkinBlurShader != null || sssSkinBlurMaterial != null;
        public bool HasPostProcessResources => postProcessUberShader != null || postProcessUberMaterial != null;
        public bool HasFsr3Resources => fsr3Shaders != null && fsr3Shaders.IsValid;
    }
}
