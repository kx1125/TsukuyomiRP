using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering
{
    [System.Serializable]
    internal sealed class TsukuyomiVolumetricFogPass : GraphFeaturePass
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

        private static readonly ProfilingSampler _downsampleDepthSampler = new("Downsample Depth");
        private static readonly ProfilingSampler _raymarchSampler = new("Raymarch");
        private static readonly ProfilingSampler _blurSampler = new("Blur");
        private static readonly ProfilingSampler _upsampleSampler = new("Upsample");
        private static readonly ProfilingSampler _compositeSampler = new("Composite");

        public override void CollectTextureSlots(System.Collections.Generic.List<TextureSlot> slots)
        {
            slots.Add(depth);
            slots.Add(color);
        }

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

            if (!TsukuyomiRenderPipelineResourcesProvider.TryGet(out TsukuyomiRenderPipelineResources resources))
                return false;

            _volumetricFogMaterial = ResolveMaterial(
                _volumetricFogMaterial,
                resources.VolumetricFogShader,
                "Tsukuyomi Volume Light requires a VolumetricFog shader in TsukuyomiRenderPipelineResources.");
            _downsampleDepthMaterial = ResolveMaterial(
                _downsampleDepthMaterial,
                resources.DownsampleDepthShader,
                "Tsukuyomi Volume Light requires a DownsampleDepth shader in TsukuyomiRenderPipelineResources.");

            return _volumetricFogMaterial != null && _downsampleDepthMaterial != null;
        }

        public override bool IsActive(in FrameContext frame)
        {
            return base.IsActive(frame) && _profile != null && _settings.IsActive && _volumetricFogMaterial != null && _downsampleDepthMaterial != null;
        }

        public void Dispose()
        {
            CoreUtils.Destroy(_volumetricFogMaterial);
            CoreUtils.Destroy(_downsampleDepthMaterial);

            _volumetricFogMaterial = null;
            _downsampleDepthMaterial = null;
        }

        private sealed class RenderData
        {
            public TextureHandle CameraColor;
            public TextureHandle DownsampledDepth;
            public TextureHandle VolumetricFog;
            public TextureHandle BlurTemp;
            public TextureHandle UpsampleComposition;
            public Material VolumetricFogMaterial;
            public Material DownsampleDepthMaterial;
            public TsukuyomiVolumeLightResolvedSettings Settings;
            public NativeArray<VisibleLight> VisibleLights;
            public int MainLightIndex;
            public int AdditionalLightsCount;
            public int Width;
            public int Height;
        }

        public override void RecordGraph(in FeatureGraphContext context)
        {
            if (_profile == null || !_settings.IsActive || _volumetricFogMaterial == null || _downsampleDepthMaterial == null)
                return;

            if (context.CameraData.isPreviewCamera)
                return;

            TextureHandle cameraDepth = context.GetTexture(depth);
            TextureHandle cameraColor = context.GetTexture(color);
            if (!cameraDepth.IsValid() || !cameraColor.IsValid())
                return;

            using var node = context.AddUnsafe<RenderData>(Name);
            var passData = node.Data;
            var graphResources = node.Resources;
            RenderTextureDescriptor cameraDescriptor = context.CameraData.cameraTargetDescriptor;
            TextureHandle downsampledDepth = graphResources.CreateTexture(CreateHalfDesc(cameraDescriptor, GraphicsFormat.R32_SFloat, "_DownsampledCameraDepth"), AccessFlags.ReadWrite);
            TextureHandle volumetricFog = graphResources.CreateTexture(CreateHalfDesc(cameraDescriptor, GraphicsFormat.R16G16B16A16_SFloat, "_VolumetricFog"), AccessFlags.ReadWrite);
            TextureHandle blurTemp = graphResources.CreateTexture(CreateHalfDesc(cameraDescriptor, GraphicsFormat.R16G16B16A16_SFloat, "_VolumetricFogBlur"), AccessFlags.ReadWrite);
            TextureHandle upsampleComposition = graphResources.CreateTexture(CreateFullDesc(cameraDescriptor, "_VolumetricFogUpsampleComposition"), AccessFlags.ReadWrite);

            passData.VolumetricFogMaterial = _volumetricFogMaterial;
            passData.DownsampleDepthMaterial = _downsampleDepthMaterial;
            passData.Settings = _settings;
            passData.VisibleLights = context.LightData.visibleLights;
            passData.MainLightIndex = context.LightData.mainLightIndex;
            passData.AdditionalLightsCount = context.LightData.additionalLightsCount;
            passData.Width = cameraDescriptor.width;
            passData.Height = cameraDescriptor.height;

            node.Builder.UseTexture(cameraDepth, AccessFlags.Read);
            node.Builder.UseTexture(cameraColor, AccessFlags.ReadWrite);
            node.Builder.AllowGlobalStateModification(true);

            passData.CameraColor = cameraColor;
            passData.DownsampledDepth = downsampledDepth;
            passData.VolumetricFog = volumetricFog;
            passData.BlurTemp = blurTemp;
            passData.UpsampleComposition = upsampleComposition;

            node.SetRenderFunc(static (state, graphContext) =>
            {
                Rect halfViewport = new(0.0f, 0.0f, Mathf.Max(1, state.Width / 2), Mathf.Max(1, state.Height / 2));
                Rect fullViewport = new(0.0f, 0.0f, state.Width, state.Height);

                using (new ProfilingScope(graphContext.cmd, _downsampleDepthSampler))
                {
                    graphContext.cmd.SetRenderTarget(state.DownsampledDepth, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                    graphContext.cmd.SetViewport(halfViewport);
                    Blitter.BlitTexture(graphContext.cmd, state.DownsampledDepth, Vector2.one, state.DownsampleDepthMaterial, DownsampleDepthPass);
                    graphContext.cmd.SetGlobalTexture(DownsampledCameraDepthTextureId, state.DownsampledDepth);
                }

                graphContext.cmd.SetKeyword(MainLightShadowScreenKeyword, false);
                graphContext.cmd.SetKeyword(MainLightShadowCascadesKeyword, true);

                using (new ProfilingScope(graphContext.cmd, _raymarchSampler))
                {
                    UpdateVolumetricFogMaterialParameters(state.VolumetricFogMaterial, state.Settings, state.MainLightIndex, state.AdditionalLightsCount, state.VisibleLights);
                    graphContext.cmd.SetRenderTarget(state.VolumetricFog, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                    graphContext.cmd.SetViewport(halfViewport);
                    Blitter.BlitTexture(graphContext.cmd, state.VolumetricFog, Vector2.one, state.VolumetricFogMaterial, VolumetricFogRenderPass);
                }

                using (new ProfilingScope(graphContext.cmd, _blurSampler))
                {
                    int blurIterations = Mathf.Clamp(state.Settings.BlurIterations, 1, 4);
                    for (int i = 0; i < blurIterations; i++)
                    {
                        graphContext.cmd.SetRenderTarget(state.BlurTemp, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                        graphContext.cmd.SetViewport(halfViewport);
                        Blitter.BlitTexture(graphContext.cmd, state.VolumetricFog, Vector2.one, state.VolumetricFogMaterial, VolumetricFogHorizontalBlurPass);
                        graphContext.cmd.SetRenderTarget(state.VolumetricFog, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                        graphContext.cmd.SetViewport(halfViewport);
                        Blitter.BlitTexture(graphContext.cmd, state.BlurTemp, Vector2.one, state.VolumetricFogMaterial, VolumetricFogVerticalBlurPass);
                    }
                }

                using (new ProfilingScope(graphContext.cmd, _upsampleSampler))
                {
                    graphContext.cmd.SetGlobalTexture(VolumetricFogTextureId, state.VolumetricFog);
                    graphContext.cmd.SetRenderTarget(state.UpsampleComposition, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                    graphContext.cmd.SetViewport(fullViewport);
                    Blitter.BlitTexture(graphContext.cmd, state.CameraColor, Vector2.one, state.VolumetricFogMaterial, VolumetricFogDepthAwareUpsampleCompositionPass);
                }

                using (new ProfilingScope(graphContext.cmd, _compositeSampler))
                {
                    graphContext.cmd.SetRenderTarget(state.CameraColor, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                    graphContext.cmd.SetViewport(fullViewport);
                    Blitter.BlitTexture(graphContext.cmd, state.UpsampleComposition, Vector2.one, state.VolumetricFogMaterial, VolumetricFogCopyPass);
                }
            });
        }

        private static Material ResolveMaterial(Material material, Shader shader, string errorMessage)
        {
            if (material != null && material.shader == shader)
                return material;

            // Each pass owns its materials. Recreate only when the preloaded shader changes.
            CoreUtils.Destroy(material);

            if (shader == null)
            {
                Debug.LogError(errorMessage);
                return null;
            }

            return CoreUtils.CreateEngineMaterial(shader);
        }

        private static TextureDesc CreateHalfDesc(RenderTextureDescriptor cameraDescriptor, GraphicsFormat format, string name)
        {
            return TextureDescriptors.Color2D(Mathf.Max(1, cameraDescriptor.width / 2),
                Mathf.Max(1, cameraDescriptor.height / 2), format, name, FilterMode.Bilinear);
        }

        private static TextureDesc CreateFullDesc(RenderTextureDescriptor cameraDescriptor, string name)
        {
            return TextureDescriptors.Color2D(cameraDescriptor.width, cameraDescriptor.height,
                cameraDescriptor.graphicsFormat, name, FilterMode.Bilinear);
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
