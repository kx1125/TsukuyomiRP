using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering
{
    [System.Serializable]
    internal sealed class TsukuyomiVolumetricFogPass : UnsafePass
    {
        private const int DownsampleDepthPass = 0;
        private const int VolumetricFogRenderPass = 0;
        private const int VolumetricFogHorizontalBlurPass = 1;
        private const int VolumetricFogVerticalBlurPass = 2;
        private const int VolumetricFogDepthAwareUpsampleCompositionPass = 3;
        private const int VolumetricFogCopyPass = 4;
        private const int MaxAdditionalLights = 256;

        private static readonly float[] Anisotropies = new float[MaxAdditionalLights];
        private static readonly float[] Scatterings = new float[MaxAdditionalLights];
        private static readonly float[] RadiiSq = new float[MaxAdditionalLights];

        private static readonly int DownsampledCameraDepthTextureId = Shader.PropertyToID("_DownsampledCameraDepthTexture");
        private static readonly int VolumetricFogTextureId = Shader.PropertyToID("_VolumetricFogTexture");
        private static readonly int FrameCountId = Shader.PropertyToID("_FrameCount");
        private static readonly int CustomAdditionalLightsCountId = Shader.PropertyToID("_CustomAdditionalLightsCount");
        private static readonly int DistanceId = Shader.PropertyToID("_Distance");
        private static readonly int BaseHeightId = Shader.PropertyToID("_BaseHeight");
        private static readonly int MaximumHeightId = Shader.PropertyToID("_MaximumHeight");
        private static readonly int GroundHeightId = Shader.PropertyToID("_GroundHeight");
        private static readonly int DensityId = Shader.PropertyToID("_Density");
        private static readonly int AbsortionId = Shader.PropertyToID("_Absortion");
        private static readonly int ProbeVolumeContributionWeigthId = Shader.PropertyToID("_ProbeVolumeContributionWeight");
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int MaxStepsId = Shader.PropertyToID("_MaxSteps");
        private static readonly int TransmittanceThresholdId = Shader.PropertyToID("_TransmittanceThreshold");
        private static readonly int AnisotropiesArrayId = Shader.PropertyToID("_Anisotropies");
        private static readonly int ScatteringsArrayId = Shader.PropertyToID("_Scatterings");
        private static readonly int RadiiSqArrayId = Shader.PropertyToID("_RadiiSq");
        private static GlobalKeyword MainLightShadowCascadesKeyword;
        private static GlobalKeyword MainLightShadowScreenKeyword;
        private static bool s_KeywordsInitialized;

        [Read(BuiltinTexture.CameraDepthTexture)]
        public TextureSlot depth = TextureSlot.Read("Depth", BuiltinTexture.CameraDepthTexture);

        [Read(BuiltinTexture.ActiveColor)]
        public TextureSlot color = TextureSlot.Read("Color", BuiltinTexture.ActiveColor);

        private TsukuyomiPipelineProfile _profile;
        private TsukuyomiVolumeLightResolvedSettings _settings;
        private Material _volumetricFogMaterial;
        private Material _downsampleDepthMaterial;
        private bool _ownsVolumetricFogMaterial;
        private bool _ownsDownsampleDepthMaterial;

        private readonly ProfilingSampler _downsampleDepthSampler = new("Downsample Depth");
        private readonly ProfilingSampler _raymarchSampler = new("Raymarch");
        private readonly ProfilingSampler _blurSampler = new("Blur");
        private readonly ProfilingSampler _upsampleSampler = new("Upsample");
        private readonly ProfilingSampler _compositeSampler = new("Composite");

        public override string Name => "Volume Light";

        internal static void InitializeKeywords()
        {
            if (s_KeywordsInitialized)
                return;

            MainLightShadowCascadesKeyword = GlobalKeyword.Create(ShaderKeywordStrings.MainLightShadowCascades);
            MainLightShadowScreenKeyword = GlobalKeyword.Create(ShaderKeywordStrings.MainLightShadowScreen);
            s_KeywordsInitialized = true;
        }

        public bool Configure(TsukuyomiPipelineProfile profile, TsukuyomiVolumeLightVolume volume)
        {
            _profile = profile;

            if (profile == null)
                return false;

            _settings = TsukuyomiVolumeLightResolvedSettings.From(profile, volume);
            if (!_settings.IsActive)
                return false;

            if (_volumetricFogMaterial != null && _downsampleDepthMaterial != null)
                return true;

            if (!TsukuyomiRenderPipelineResourcesProvider.TryGet(out TsukuyomiRenderPipelineResources resources))
                return false;

            _volumetricFogMaterial = ResolveMaterial(
                resources.VolumetricFogMaterial,
                resources.VolumetricFogShader,
                "Tsukuyomi Volume Light requires a VolumetricFog shader or material in TsukuyomiRenderPipelineResources.",
                ref _ownsVolumetricFogMaterial);
            _downsampleDepthMaterial = ResolveMaterial(
                resources.DownsampleDepthMaterial,
                resources.DownsampleDepthShader,
                "Tsukuyomi Volume Light requires a DownsampleDepth shader or material in TsukuyomiRenderPipelineResources.",
                ref _ownsDownsampleDepthMaterial);

            return _volumetricFogMaterial != null && _downsampleDepthMaterial != null;
        }

        public override bool IsActive(in FrameContext frame)
        {
            return base.IsActive(frame) && _profile != null && _settings.IsActive && _volumetricFogMaterial != null && _downsampleDepthMaterial != null;
        }

        public void Dispose()
        {
            if (_ownsVolumetricFogMaterial)
                CoreUtils.Destroy(_volumetricFogMaterial);
            if (_ownsDownsampleDepthMaterial)
                CoreUtils.Destroy(_downsampleDepthMaterial);

            _volumetricFogMaterial = null;
            _downsampleDepthMaterial = null;
            _ownsVolumetricFogMaterial = false;
            _ownsDownsampleDepthMaterial = false;
        }

        public override void Record(in UnsafePassContext context)
        {
            if (_profile == null || !_settings.IsActive || _volumetricFogMaterial == null || _downsampleDepthMaterial == null)
                return;

            if (context.CameraData.isPreviewCamera)
                return;

            TextureHandle cameraDepth = context.GetTexture(depth);
            TextureHandle cameraColor = context.GetTexture(color);
            if (!cameraDepth.IsValid() || !cameraColor.IsValid())
                return;

            RenderTextureDescriptor cameraDescriptor = context.CameraData.cameraTargetDescriptor;
            TextureHandle downsampledDepth = context.RenderGraph.CreateTexture(CreateHalfDesc(cameraDescriptor, GraphicsFormat.R32_SFloat, "_DownsampledCameraDepth"));
            TextureHandle volumetricFog = context.RenderGraph.CreateTexture(CreateHalfDesc(cameraDescriptor, GraphicsFormat.R16G16B16A16_SFloat, "_VolumetricFog"));
            TextureHandle blurTemp = context.RenderGraph.CreateTexture(CreateHalfDesc(cameraDescriptor, GraphicsFormat.R16G16B16A16_SFloat, "_VolumetricFogBlur"));
            TextureHandle upsampleComposition = context.RenderGraph.CreateTexture(CreateFullDesc(cameraDescriptor, "_VolumetricFogUpsampleComposition"));

            Material volumetricFogMaterial = _volumetricFogMaterial;
            Material downsampleDepthMaterial = _downsampleDepthMaterial;
            TsukuyomiVolumeLightResolvedSettings settings = _settings;
            NativeArray<VisibleLight> visibleLights = context.LightData.visibleLights;
            int mainLightIndex = context.LightData.mainLightIndex;
            int additionalLightsCount = context.LightData.additionalLightsCount;
            int width = cameraDescriptor.width;
            int height = cameraDescriptor.height;

            ProfilingSampler downsampleDepthSampler = _downsampleDepthSampler;
            ProfilingSampler raymarchSampler = _raymarchSampler;
            ProfilingSampler blurSampler = _blurSampler;
            ProfilingSampler upsampleSampler = _upsampleSampler;
            ProfilingSampler compositeSampler = _compositeSampler;

            context.Builder.UseTexture(cameraDepth, AccessFlags.Read);
            context.Builder.UseTexture(cameraColor, AccessFlags.ReadWrite);
            context.Builder.UseTexture(downsampledDepth, AccessFlags.ReadWrite);
            context.Builder.UseTexture(volumetricFog, AccessFlags.ReadWrite);
            context.Builder.UseTexture(blurTemp, AccessFlags.ReadWrite);
            context.Builder.UseTexture(upsampleComposition, AccessFlags.ReadWrite);
            context.Builder.AllowGlobalStateModification(true);

            context.SetRenderFunc((data, graphContext) =>
            {
                Rect halfViewport = new(0.0f, 0.0f, Mathf.Max(1, width / 2), Mathf.Max(1, height / 2));
                Rect fullViewport = new(0.0f, 0.0f, width, height);

                using (new ProfilingScope(graphContext.cmd, downsampleDepthSampler))
                {
                    graphContext.cmd.SetRenderTarget(downsampledDepth, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                    graphContext.cmd.SetViewport(halfViewport);
                    Blitter.BlitTexture(graphContext.cmd, downsampledDepth, Vector2.one, downsampleDepthMaterial, DownsampleDepthPass);
                    graphContext.cmd.SetGlobalTexture(DownsampledCameraDepthTextureId, downsampledDepth);
                }

                graphContext.cmd.SetKeyword(MainLightShadowScreenKeyword, false);
                graphContext.cmd.SetKeyword(MainLightShadowCascadesKeyword, true);

                using (new ProfilingScope(graphContext.cmd, raymarchSampler))
                {
                    UpdateVolumetricFogMaterialParameters(volumetricFogMaterial, settings, mainLightIndex, additionalLightsCount, visibleLights);
                    graphContext.cmd.SetRenderTarget(volumetricFog, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                    graphContext.cmd.SetViewport(halfViewport);
                    Blitter.BlitTexture(graphContext.cmd, volumetricFog, Vector2.one, volumetricFogMaterial, VolumetricFogRenderPass);
                }

                using (new ProfilingScope(graphContext.cmd, blurSampler))
                {
                    int blurIterations = Mathf.Clamp(settings.BlurIterations, 1, 4);
                    for (int i = 0; i < blurIterations; i++)
                    {
                        graphContext.cmd.SetRenderTarget(blurTemp, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                        graphContext.cmd.SetViewport(halfViewport);
                        Blitter.BlitTexture(graphContext.cmd, volumetricFog, Vector2.one, volumetricFogMaterial, VolumetricFogHorizontalBlurPass);
                        graphContext.cmd.SetRenderTarget(volumetricFog, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                        graphContext.cmd.SetViewport(halfViewport);
                        Blitter.BlitTexture(graphContext.cmd, blurTemp, Vector2.one, volumetricFogMaterial, VolumetricFogVerticalBlurPass);
                    }
                }

                using (new ProfilingScope(graphContext.cmd, upsampleSampler))
                {
                    graphContext.cmd.SetGlobalTexture(VolumetricFogTextureId, volumetricFog);
                    graphContext.cmd.SetRenderTarget(upsampleComposition, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                    graphContext.cmd.SetViewport(fullViewport);
                    Blitter.BlitTexture(graphContext.cmd, cameraColor, Vector2.one, volumetricFogMaterial, VolumetricFogDepthAwareUpsampleCompositionPass);
                }

                using (new ProfilingScope(graphContext.cmd, compositeSampler))
                {
                    graphContext.cmd.SetRenderTarget(cameraColor, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                    graphContext.cmd.SetViewport(fullViewport);
                    Blitter.BlitTexture(graphContext.cmd, upsampleComposition, Vector2.one, volumetricFogMaterial, VolumetricFogCopyPass);
                }
            });
        }

        private static Material ResolveMaterial(Material material, Shader shader, string errorMessage, ref bool ownsMaterial)
        {
            ownsMaterial = false;
            if (material != null)
                return material;

            if (shader == null)
            {
                Debug.LogError(errorMessage);
                return null;
            }

            ownsMaterial = true;
            return CoreUtils.CreateEngineMaterial(shader);
        }

        private static TextureDesc CreateHalfDesc(RenderTextureDescriptor cameraDescriptor, GraphicsFormat format, string name)
        {
            return new TextureDesc(Mathf.Max(1, cameraDescriptor.width / 2), Mathf.Max(1, cameraDescriptor.height / 2))
            {
                name = name,
                colorFormat = format,
                depthBufferBits = DepthBits.None,
                msaaSamples = MSAASamples.None,
                clearBuffer = false,
                clearColor = Color.clear,
                filterMode = FilterMode.Bilinear
            };
        }

        private static TextureDesc CreateFullDesc(RenderTextureDescriptor cameraDescriptor, string name)
        {
            return new TextureDesc(cameraDescriptor.width, cameraDescriptor.height)
            {
                name = name,
                colorFormat = cameraDescriptor.graphicsFormat,
                depthBufferBits = DepthBits.None,
                msaaSamples = MSAASamples.None,
                clearBuffer = false,
                filterMode = FilterMode.Bilinear
            };
        }

        private static void UpdateVolumetricFogMaterialParameters(
            Material volumetricFogMaterial,
            TsukuyomiVolumeLightResolvedSettings settings,
            int mainLightIndex,
            int additionalLightsCount,
            NativeArray<VisibleLight> visibleLights)
        {
            bool enableMainLightContribution = settings.EnableMainLightContribution && settings.Scattering > 0.0f && mainLightIndex > -1;
            bool enableAdditionalLightsContribution = settings.EnableAdditionalLightsContribution && additionalLightsCount > 0;
            bool enableProbeVolumeContribution = settings.EnableProbeVolumeContribution && settings.ProbeVolumeContributionWeight > 0.0f;

            SetKeyword(volumetricFogMaterial, "_PROBE_VOLUME_CONTRIBUTION_ENABLED", enableProbeVolumeContribution);
            SetKeyword(volumetricFogMaterial, "_MAIN_LIGHT_CONTRIBUTION_DISABLED", !enableMainLightContribution);
            SetKeyword(volumetricFogMaterial, "_ADDITIONAL_LIGHTS_CONTRIBUTION_DISABLED", !enableAdditionalLightsContribution);

            UpdateLightsParameters(volumetricFogMaterial, settings, enableMainLightContribution, enableAdditionalLightsContribution, mainLightIndex, visibleLights);

            volumetricFogMaterial.SetInteger(FrameCountId, Time.renderedFrameCount % 64);
            volumetricFogMaterial.SetInteger(CustomAdditionalLightsCountId, additionalLightsCount);
            volumetricFogMaterial.SetFloat(DistanceId, Mathf.Max(0.0f, settings.Distance));
            volumetricFogMaterial.SetFloat(BaseHeightId, settings.BaseHeight);
            volumetricFogMaterial.SetFloat(MaximumHeightId, Mathf.Max(settings.BaseHeight, settings.MaximumHeight));
            volumetricFogMaterial.SetFloat(GroundHeightId, settings.EnableGround ? settings.GroundHeight : float.MinValue);
            volumetricFogMaterial.SetFloat(DensityId, Mathf.Clamp01(settings.Density));
            volumetricFogMaterial.SetFloat(AbsortionId, 1.0f / Mathf.Max(0.05f, settings.AttenuationDistance));
            volumetricFogMaterial.SetFloat(ProbeVolumeContributionWeigthId, enableProbeVolumeContribution ? settings.ProbeVolumeContributionWeight : 0.0f);
            volumetricFogMaterial.SetColor(TintId, settings.Tint);
            volumetricFogMaterial.SetInteger(MaxStepsId, Mathf.Clamp(settings.MaxSteps, 8, 256));
            volumetricFogMaterial.SetFloat(TransmittanceThresholdId, Mathf.Clamp(settings.TransmittanceThreshold, 0.0f, 0.1f));
        }

        private static void UpdateLightsParameters(
            Material volumetricFogMaterial,
            TsukuyomiVolumeLightResolvedSettings settings,
            bool enableMainLightContribution,
            bool enableAdditionalLightsContribution,
            int mainLightIndex,
            NativeArray<VisibleLight> visibleLights)
        {
            for (int i = 0; i < MaxAdditionalLights; i++)
            {
                Anisotropies[i] = 0.0f;
                Scatterings[i] = 0.0f;
                RadiiSq[i] = 0.0f;
            }

            int mainLightSlot = Mathf.Clamp(visibleLights.Length - 1, 0, MaxAdditionalLights - 1);
            if (enableMainLightContribution && visibleLights.Length > 0)
            {
                Anisotropies[mainLightSlot] = settings.Anisotropy;
                Scatterings[mainLightSlot] = settings.Scattering;
            }

            if (enableAdditionalLightsContribution)
            {
                int additionalLightIndex = 0;
                for (int i = 0; i < visibleLights.Length && additionalLightIndex < MaxAdditionalLights; i++)
                {
                    if (i == mainLightIndex)
                        continue;

                    float anisotropy = 0.0f;
                    float scattering = 0.0f;
                    float radius = 0.0f;

                    if (TsukuyomiVolumetricLightManager.TryGet(visibleLights[i].light, out TsukuyomiVolumetricAdditionalLight volumetricLight)
                        && volumetricLight.isActiveAndEnabled)
                    {
                        anisotropy = volumetricLight.Anisotropy;
                        scattering = volumetricLight.Scattering;
                        radius = volumetricLight.Radius;
                    }

                    Anisotropies[additionalLightIndex] = anisotropy;
                    Scatterings[additionalLightIndex] = scattering;
                    RadiiSq[additionalLightIndex++] = radius * radius;
                }
            }

            if (enableMainLightContribution || enableAdditionalLightsContribution)
            {
                volumetricFogMaterial.SetFloatArray(AnisotropiesArrayId, Anisotropies);
                volumetricFogMaterial.SetFloatArray(ScatteringsArrayId, Scatterings);
                volumetricFogMaterial.SetFloatArray(RadiiSqArrayId, RadiiSq);
            }
        }

        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled)
                material.EnableKeyword(keyword);
            else
                material.DisableKeyword(keyword);
        }
    }
}
