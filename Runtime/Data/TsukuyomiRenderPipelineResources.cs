using UnityEngine;

namespace Tsukuyomi.Rendering
{
    [CreateAssetMenu(menuName = "TsukuyomiRpP/Render Pipeline Resources", fileName = "TsukuyomiRenderPipelineResources")]
    public sealed class TsukuyomiRenderPipelineResources : ScriptableObject
    {
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

        [Header("Volume Light")]
        [SerializeField]
        private Shader volumetricFogShader;

        [SerializeField]
        private Material volumetricFogMaterial;

        [SerializeField]
        private Shader downsampleDepthShader;

        [SerializeField]
        private Material downsampleDepthMaterial;

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
        public Shader VolumetricFogShader => volumetricFogShader;
        public Material VolumetricFogMaterial => volumetricFogMaterial;
        public Shader DownsampleDepthShader => downsampleDepthShader;
        public Material DownsampleDepthMaterial => downsampleDepthMaterial;
        public ComputeShader VolumetricFogRaymarchComputeShader => volumetricFogRaymarchComputeShader;
        public ComputeShader VolumetricFogBlurComputeShader => volumetricFogBlurComputeShader;
        public ComputeShader VolumetricFogUpsampleComputeShader => volumetricFogUpsampleComputeShader;
        public Shader SssSkinBlurShader => sssSkinBlurShader;
        public Material SssSkinBlurMaterial => sssSkinBlurMaterial;
        public Texture2D DefaultWhiteTexture => defaultWhiteTexture;
        public Texture2D DefaultBlackTexture => defaultBlackTexture;
        public Texture2D DefaultNormalTexture => defaultNormalTexture;
        public TsukuyomiFsr3Shaders Fsr3Shaders => fsr3Shaders;

        public bool HasPcssResources => screenSpacePcssShadowsShader != null || screenSpacePcssShadowsMaterial != null;
        public bool HasContactShadowResources => contactShadowsComputeShader != null;
        public bool HasContactShadowDenoiserResources => contactShadowDenoiserComputeShader != null;
        public bool HasDepthPyramidResources => depthPyramidComputeShader != null;
        public bool HasGtaoResources => gtaoTraceComputeShader != null
            && gtaoSpatialDenoiseComputeShader != null
            && gtaoBlurAndUpsampleComputeShader != null;
        public bool HasVolumeLightResources => (volumetricFogShader != null || volumetricFogMaterial != null)
            && (downsampleDepthShader != null || downsampleDepthMaterial != null);
        public bool HasSssSkinResources => sssSkinBlurShader != null || sssSkinBlurMaterial != null;
        public bool HasFsr3Resources => fsr3Shaders != null && fsr3Shaders.IsValid;
    }
}





