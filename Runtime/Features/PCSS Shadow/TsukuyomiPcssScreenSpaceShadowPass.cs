using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering
{
    [System.Serializable]
    internal sealed class TsukuyomiPcssScreenSpaceShadowPass : UnsafePass
    {
        private const int PenumbraMaskPass = 0;
        private const int BlurHorizontalPass = 1;
        private const int BlurVerticalPass = 2;
        private const int ScreenSpaceShadowPass = 3;
        private const int ContactShadowCompositePass = 4;

        private static readonly int ScreenSpaceShadowmapTextureId = Shader.PropertyToID("_ScreenSpaceShadowmapTexture");
        private static readonly int ContactShadowMapId = Shader.PropertyToID("_ContactShadowMap");
        private static readonly int BaseScreenSpaceShadowmapTextureId = Shader.PropertyToID("_TsukuyomiBaseScreenSpaceShadowmapTexture");
        private static readonly int EnablePcssId = Shader.PropertyToID("_TsukuyomiEnablePCSS");
        private static readonly int EnableMainLightShadowId = Shader.PropertyToID("_TsukuyomiEnableMainLightShadow");
        private static readonly int PenumbraMaskTexId = Shader.PropertyToID("_TsukuyomiPenumbraMaskTex");
        private static readonly int PenumbraMaskTexelSizeId = Shader.PropertyToID("_TsukuyomiPenumbraMaskTex_TexelSize");
        private static readonly int ColorAttachmentTexelSizeId = Shader.PropertyToID("_TsukuyomiColorAttachmentTexelSize");
        private static readonly int FindBlockerSampleCountId = Shader.PropertyToID("_TsukuyomiPcssFindBlockerSampleCount");
        private static readonly int PcfSampleCountId = Shader.PropertyToID("_TsukuyomiPcssPcfSampleCount");
        private static readonly int AngularDiameterId = Shader.PropertyToID("_TsukuyomiPcssAngularDiameter");
        private static readonly int BlockerSearchAngularDiameterId = Shader.PropertyToID("_TsukuyomiPcssBlockerSearchAngularDiameter");
        private static readonly int MinFilterMaxAngularDiameterId = Shader.PropertyToID("_TsukuyomiPcssMinFilterMaxAngularDiameter");
        private static readonly int MaxPenumbraSizeId = Shader.PropertyToID("_TsukuyomiPcssMaxPenumbraSize");
        private static readonly int MaxSamplingDistanceId = Shader.PropertyToID("_TsukuyomiPcssMaxSamplingDistance");
        private static readonly int MinFilterSizeTexelsId = Shader.PropertyToID("_TsukuyomiPcssMinFilterSizeTexels");
        private static GlobalKeyword MainLightShadowsKeyword;
        private static GlobalKeyword MainLightShadowCascadesKeyword;
        private static GlobalKeyword MainLightShadowScreenKeyword;
        private static GlobalKeyword ContactShadowsKeyword;
        private static bool s_KeywordsInitialized;

        [Read(BuiltinTexture.CameraDepthTexture)]
        public TextureSlot depth = TextureSlot.Read("Depth", BuiltinTexture.CameraDepthTexture);

        [Read(BuiltinTexture.MainShadowMap)]
        public TextureSlot mainShadowMap = TextureSlot.Read("MainShadowMap", BuiltinTexture.MainShadowMap);

        private Material _material;
        private Material _screenSpaceShadowsMaterial;
        private bool _ownsMaterial;
        private bool _ownsScreenSpaceShadowsMaterial;
        private TsukuyomiPipelineProfile _profile;
        private TsukuyomiPcssResolvedSettings _settings;
        private bool _contactShadowsEnabled;
        private bool _contactShadowDenoiseEnabled;
        private bool _perObjectShadowsEnabled;
        private readonly ProfilingSampler _pcssPenumbraSampler = new("PCSS Penumbra");
        private readonly ProfilingSampler _screenSpaceShadowSampler = new("Screen Space Shadows");

        public override string Name => "Screen Space Shadows";

        internal static void InitializeKeywords()
        {
            if (s_KeywordsInitialized)
                return;

            MainLightShadowsKeyword = GlobalKeyword.Create(ShaderKeywordStrings.MainLightShadows);
            MainLightShadowCascadesKeyword = GlobalKeyword.Create(ShaderKeywordStrings.MainLightShadowCascades);
            MainLightShadowScreenKeyword = GlobalKeyword.Create(ShaderKeywordStrings.MainLightShadowScreen);
            ContactShadowsKeyword = GlobalKeyword.Create("_CONTACT_SHADOWS");
            s_KeywordsInitialized = true;
        }

        public bool Configure(
            TsukuyomiPipelineProfile profile,
            TsukuyomiPcssVolume volume,
            bool contactShadowsEnabled = false,
            bool contactShadowDenoiseEnabled = false,
            bool perObjectShadowsEnabled = false)
        {
            _profile = profile;
            _contactShadowsEnabled = contactShadowsEnabled;
            _contactShadowDenoiseEnabled = contactShadowDenoiseEnabled;
            _perObjectShadowsEnabled = perObjectShadowsEnabled;

            if (profile == null)
                return false;

            _settings = TsukuyomiPcssResolvedSettings.From(profile, volume);

            if (!_settings.Enabled && !contactShadowsEnabled && !perObjectShadowsEnabled)
                return false;

            if (_material != null && (_settings.Enabled || perObjectShadowsEnabled || contactShadowsEnabled || _screenSpaceShadowsMaterial != null))
                return true;

            if (!TsukuyomiRenderPipelineResourcesProvider.TryGet(out TsukuyomiRenderPipelineResources resources))
                return false;

            if (_material == null && !LoadPcssMaterial(resources))
                return false;

            if (!_settings.Enabled && (contactShadowsEnabled || perObjectShadowsEnabled) && _screenSpaceShadowsMaterial == null && !LoadScreenSpaceShadowsMaterial(resources))
                return false;

            return true;
        }

        private bool LoadPcssMaterial(TsukuyomiRenderPipelineResources resources)
        {
            if (resources.ScreenSpacePcssShadowsMaterial != null)
            {
                _material = resources.ScreenSpacePcssShadowsMaterial;
                _ownsMaterial = false;
                return true;
            }

            Shader shader = resources.ScreenSpacePcssShadowsShader;
            if (shader == null)
            {
                Debug.LogError("Tsukuyomi PCSS requires a Screen Space PCSS Shadows shader in TsukuyomiRenderPipelineResources.");
                return false;
            }

            _material = CoreUtils.CreateEngineMaterial(shader);
            _ownsMaterial = true;
            return _material != null;
        }

        private bool LoadScreenSpaceShadowsMaterial(TsukuyomiRenderPipelineResources resources)
        {
            if (resources.ScreenSpaceShadowsMaterial != null)
            {
                _screenSpaceShadowsMaterial = resources.ScreenSpaceShadowsMaterial;
                _ownsScreenSpaceShadowsMaterial = false;
                return true;
            }

            Shader shader = resources.ScreenSpaceShadowsShader;
            if (shader == null)
                shader = Shader.Find("Hidden/Universal Render Pipeline/ScreenSpaceShadows");

            if (shader == null)
            {
                Debug.LogError("Tsukuyomi Contact Shadow requires Unity's ScreenSpaceShadows shader in TsukuyomiRenderPipelineResources when PCSS is disabled.");
                return false;
            }

            _screenSpaceShadowsMaterial = CoreUtils.CreateEngineMaterial(shader);
            _ownsScreenSpaceShadowsMaterial = true;
            return _screenSpaceShadowsMaterial != null;
        }

        public override bool IsActive(in FrameContext frame)
        {
            return base.IsActive(frame) && _profile != null && (_settings.Enabled || _contactShadowsEnabled || _perObjectShadowsEnabled) && _material != null;
        }

        public void Dispose()
        {
            if (_ownsMaterial)
                CoreUtils.Destroy(_material);
            if (_ownsScreenSpaceShadowsMaterial)
                CoreUtils.Destroy(_screenSpaceShadowsMaterial);
            _material = null;
            _screenSpaceShadowsMaterial = null;
            _ownsMaterial = false;
            _ownsScreenSpaceShadowsMaterial = false;
        }

        public override void Record(in UnsafePassContext context)
        {
            if (!s_KeywordsInitialized || _material == null || _profile == null || (!_settings.Enabled && !_contactShadowsEnabled && !_perObjectShadowsEnabled))
                return;

            var shadowData = context.FrameData.Get<UniversalShadowData>();
            var resourceData = context.FrameData.Get<UniversalResourceData>();

            if (context.CameraData.isPreviewCamera || context.LightData.mainLightIndex < 0)
                return;

            bool useMainLightShadow = shadowData.supportsMainLightShadows && resourceData.mainShadowsTexture.IsValid();
            if (_settings.Enabled && !useMainLightShadow)
                return;

            if (!resourceData.cameraDepthTexture.IsValid())
                return;

            TsukuyomiPcssSettings settings = TsukuyomiPcssSettings.FromResolved(_settings).WithCameraDescriptor(context.CameraData.cameraTargetDescriptor);
            RenderTextureDescriptor screenDesc = CreateScreenShadowDescriptor(context.CameraData.cameraTargetDescriptor);
            RenderTextureDescriptor penumbraDesc = CreatePenumbraDescriptor(context.CameraData.cameraTargetDescriptor, settings.PenumbraMaskScale);

            TextureHandle penumbraMask = UniversalRenderer.CreateRenderGraphTexture(context.RenderGraph, penumbraDesc, "_TsukuyomiPenumbraMaskTex", false, FilterMode.Bilinear);
            TextureHandle penumbraBlurTemp = UniversalRenderer.CreateRenderGraphTexture(context.RenderGraph, penumbraDesc, "_TsukuyomiPenumbraMaskBlurTempTex", false, FilterMode.Bilinear);
            TextureHandle screenShadow = UniversalRenderer.CreateRenderGraphTexture(context.RenderGraph, screenDesc, "_ScreenSpaceShadowmapTexture", false);
            TextureHandle baseScreenShadow = !_settings.Enabled
                ? UniversalRenderer.CreateRenderGraphTexture(context.RenderGraph, screenDesc, "_TsukuyomiBaseScreenSpaceShadowmapTexture", false)
                : TextureHandle.nullHandle;
            TextureHandle cameraDepthTexture = resourceData.cameraDepthTexture;
            TextureHandle mainShadowsTexture = resourceData.mainShadowsTexture;
            TextureHandle activeColorTexture = resourceData.activeColorTexture;
            TextureHandle contactShadowMap = TextureHandle.nullHandle;
            if (_contactShadowsEnabled)
            {
                TextureDesc contactDesc = _contactShadowDenoiseEnabled
                    ? TsukuyomiContactShadowDenoisePass.CreateDenoiseDesc(context.CameraData.cameraTargetDescriptor)
                    : TsukuyomiContactShadowPass.CreateContactShadowDesc(context.CameraData.cameraTargetDescriptor);
                TextureSlot contactSlot = TextureSlot.Read(
                    _contactShadowDenoiseEnabled ? TsukuyomiContactShadowResources.ContactShadowDenoisedMap : TsukuyomiContactShadowResources.ContactShadowMap,
                    contactDesc);
                contactShadowMap = context.GetTexture(contactSlot);
            }
            Material material = _material;
            Material screenSpaceShadowsMaterial = _screenSpaceShadowsMaterial;
            UniversalCameraData cameraData = context.CameraData;
            bool enablePcss = _settings.Enabled;
            bool useOfficialScreenSpaceShadows = !enablePcss && useMainLightShadow && screenSpaceShadowsMaterial != null && baseScreenShadow.IsValid();
            bool enableContactShadows = _contactShadowsEnabled && contactShadowMap.IsValid();
            bool enablePerObjectShadows = _perObjectShadowsEnabled;
            ProfilingSampler pcssPenumbraSampler = _pcssPenumbraSampler;
            ProfilingSampler screenSpaceShadowSampler = _screenSpaceShadowSampler;

            context.Builder.UseTexture(screenShadow, AccessFlags.WriteAll);
            if (baseScreenShadow.IsValid())
                context.Builder.UseTexture(baseScreenShadow, AccessFlags.ReadWrite);
            context.Builder.UseTexture(penumbraMask, AccessFlags.ReadWrite);
            context.Builder.UseTexture(penumbraBlurTemp, AccessFlags.ReadWrite);
            context.Builder.UseTexture(cameraDepthTexture, AccessFlags.Read);
            if (useMainLightShadow)
                context.Builder.UseTexture(mainShadowsTexture, AccessFlags.Read);
            context.Builder.UseTexture(activeColorTexture, AccessFlags.Read);
            if (contactShadowMap.IsValid())
                context.Builder.UseTexture(contactShadowMap, AccessFlags.Read);
            context.Builder.AllowGlobalStateModification(true);
            context.Builder.SetGlobalTextureAfterPass(penumbraMask, PenumbraMaskTexId);
            context.Builder.SetGlobalTextureAfterPass(screenShadow, ScreenSpaceShadowmapTextureId);

            context.SetRenderFunc((data, graphContext) =>
            {
                if (enablePcss)
                {
                    using (new ProfilingScope(graphContext.cmd, pcssPenumbraSampler))
                    {
                        SetCommonGlobals(graphContext, settings, cameraDepthTexture, cameraData);

                        graphContext.cmd.SetRenderTarget(penumbraMask, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                        graphContext.cmd.SetViewport(settings.PenumbraMaskViewport);
                        Blitter.BlitTexture(graphContext.cmd, penumbraMask, Vector2.one, material, PenumbraMaskPass);

                        graphContext.cmd.SetGlobalTexture(PenumbraMaskTexId, penumbraMask);
                        graphContext.cmd.SetGlobalVector(PenumbraMaskTexelSizeId, settings.PenumbraMaskTexelSize);
                        graphContext.cmd.SetRenderTarget(penumbraBlurTemp, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                        graphContext.cmd.SetViewport(settings.PenumbraMaskViewport);
                        Blitter.BlitTexture(graphContext.cmd, penumbraMask, Vector2.one, material, BlurHorizontalPass);

                        graphContext.cmd.SetGlobalTexture(PenumbraMaskTexId, penumbraBlurTemp);
                        graphContext.cmd.SetGlobalVector(PenumbraMaskTexelSizeId, settings.PenumbraMaskTexelSize);
                        graphContext.cmd.SetRenderTarget(penumbraMask, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                        graphContext.cmd.SetViewport(settings.PenumbraMaskViewport);
                        Blitter.BlitTexture(graphContext.cmd, penumbraBlurTemp, Vector2.one, material, BlurVerticalPass);

                        graphContext.cmd.SetGlobalTexture(PenumbraMaskTexId, penumbraMask);
                    }
                }

                using (new ProfilingScope(graphContext.cmd, screenSpaceShadowSampler))
                {
                    SetCommonGlobals(graphContext, settings, cameraDepthTexture, cameraData);
                    graphContext.cmd.SetKeyword(ContactShadowsKeyword, enableContactShadows);
                    graphContext.cmd.SetGlobalFloat(EnableMainLightShadowId, useMainLightShadow ? 1.0f : 0.0f);
                    graphContext.cmd.SetGlobalFloat("_TsukuyomiEnablePerObjectShadow", enablePerObjectShadows ? 1.0f : 0.0f);

                    if (enablePcss)
                    {
                        graphContext.cmd.SetGlobalFloat(EnablePcssId, 1.0f);
                        graphContext.cmd.SetGlobalTexture(PenumbraMaskTexId, penumbraMask);
                        if (enableContactShadows)
                            graphContext.cmd.SetGlobalTexture(ContactShadowMapId, contactShadowMap);

                        graphContext.cmd.SetRenderTarget(screenShadow, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                        graphContext.cmd.SetViewport(settings.ColorAttachmentViewport);
                        Blitter.BlitTexture(graphContext.cmd, screenShadow, Vector2.one, material, ScreenSpaceShadowPass);
                    }
                    else
                    {
                        if (useOfficialScreenSpaceShadows)
                        {
                            graphContext.cmd.SetRenderTarget(baseScreenShadow, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                            graphContext.cmd.SetViewport(settings.ColorAttachmentViewport);
                            Blitter.BlitTexture(graphContext.cmd, baseScreenShadow, Vector2.one, screenSpaceShadowsMaterial, 0);
                            graphContext.cmd.SetGlobalTexture(BaseScreenSpaceShadowmapTextureId, baseScreenShadow);
                        }

                        if (enableContactShadows)
                            graphContext.cmd.SetGlobalTexture(ContactShadowMapId, contactShadowMap);

                        graphContext.cmd.SetRenderTarget(screenShadow, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                        graphContext.cmd.SetViewport(settings.ColorAttachmentViewport);
                        Blitter.BlitTexture(graphContext.cmd, screenShadow, Vector2.one, material, ContactShadowCompositePass);
                    }

                    graphContext.cmd.SetKeyword(ContactShadowsKeyword, false);
                    graphContext.cmd.SetKeyword(MainLightShadowsKeyword, false);
                    graphContext.cmd.SetKeyword(MainLightShadowCascadesKeyword, false);
                    graphContext.cmd.SetKeyword(MainLightShadowScreenKeyword, true);
                }
            });
        }

        private static RenderTextureDescriptor CreateScreenShadowDescriptor(RenderTextureDescriptor cameraDescriptor)
        {
            cameraDescriptor.depthStencilFormat = GraphicsFormat.None;
            cameraDescriptor.msaaSamples = 1;
            cameraDescriptor.graphicsFormat = SystemInfo.IsFormatSupported(GraphicsFormat.R8_UNorm, GraphicsFormatUsage.Blend)
                ? GraphicsFormat.R8_UNorm
                : GraphicsFormat.B8G8R8A8_UNorm;
            return cameraDescriptor;
        }

        private static RenderTextureDescriptor CreatePenumbraDescriptor(RenderTextureDescriptor cameraDescriptor, int maskScale)
        {
            cameraDescriptor.width = Mathf.Max(1, cameraDescriptor.width / Mathf.Max(1, maskScale));
            cameraDescriptor.height = Mathf.Max(1, cameraDescriptor.height / Mathf.Max(1, maskScale));
            cameraDescriptor.depthStencilFormat = GraphicsFormat.None;
            cameraDescriptor.msaaSamples = 1;
            cameraDescriptor.graphicsFormat = GraphicsFormat.R8_UNorm;
            cameraDescriptor.colorFormat = RenderTextureFormat.R8;
            cameraDescriptor.autoGenerateMips = false;
            cameraDescriptor.useMipMap = false;
            return cameraDescriptor;
        }

        private static void SetCommonGlobals(
            UnsafeGraphContext context,
            TsukuyomiPcssSettings settings,
            TextureHandle cameraDepthTexture,
            UniversalCameraData cameraData)
        {
            SetPcssSettings(context.cmd, settings);
            context.cmd.SetGlobalVector(PenumbraMaskTexelSizeId, settings.PenumbraMaskTexelSize);
            context.cmd.SetGlobalVector(ColorAttachmentTexelSizeId, settings.ColorAttachmentTexelSize);
        }

        private static void SetPcssSettings(UnsafeCommandBuffer cmd, TsukuyomiPcssSettings settings)
        {
            cmd.SetGlobalFloat(FindBlockerSampleCountId, settings.FindBlockerSampleCount);
            cmd.SetGlobalFloat(PcfSampleCountId, settings.PcfSampleCount);
            cmd.SetGlobalFloat(AngularDiameterId, settings.AngularDiameter);
            cmd.SetGlobalFloat(BlockerSearchAngularDiameterId, settings.BlockerSearchAngularDiameter);
            cmd.SetGlobalFloat(MinFilterMaxAngularDiameterId, settings.MinFilterMaxAngularDiameter);
            cmd.SetGlobalFloat(MaxPenumbraSizeId, settings.MaxPenumbraSize);
            cmd.SetGlobalFloat(MaxSamplingDistanceId, settings.MaxSamplingDistance);
            cmd.SetGlobalFloat(MinFilterSizeTexelsId, settings.MinFilterSizeTexels);
        }

        private readonly struct TsukuyomiPcssSettings
        {
            public readonly float FindBlockerSampleCount;
            public readonly float PcfSampleCount;
            public readonly float AngularDiameter;
            public readonly float BlockerSearchAngularDiameter;
            public readonly float MinFilterMaxAngularDiameter;
            public readonly float MaxPenumbraSize;
            public readonly float MaxSamplingDistance;
            public readonly float MinFilterSizeTexels;
            public readonly Vector4 PenumbraMaskTexelSize;
            public readonly Vector4 ColorAttachmentTexelSize;
            public readonly Rect PenumbraMaskViewport;
            public readonly Rect ColorAttachmentViewport;
            public readonly int PenumbraMaskScale;

            private TsukuyomiPcssSettings(TsukuyomiPcssResolvedSettings settings)
            {
                FindBlockerSampleCount = Mathf.Clamp(settings.FindBlockerSampleCount, 1, 64);
                PcfSampleCount = Mathf.Clamp(settings.PcfSampleCount, 1, 64);
                AngularDiameter = Mathf.Max(0.001f, settings.AngularDiameter);
                BlockerSearchAngularDiameter = Mathf.Max(0.001f, settings.BlockerSearchAngularDiameter);
                MinFilterMaxAngularDiameter = Mathf.Max(0.001f, settings.MinFilterMaxAngularDiameter);
                MaxPenumbraSize = Mathf.Max(0.0f, settings.MaxPenumbraSize);
                MaxSamplingDistance = Mathf.Max(0.0f, settings.MaxSamplingDistance);
                MinFilterSizeTexels = Mathf.Max(0.0f, settings.MinFilterSizeTexels);
                PenumbraMaskScale = Mathf.Clamp(settings.PenumbraMaskScale, 1, 32);
                PenumbraMaskTexelSize = Vector4.zero;
                ColorAttachmentTexelSize = Vector4.zero;
                PenumbraMaskViewport = Rect.zero;
                ColorAttachmentViewport = Rect.zero;
            }

            private TsukuyomiPcssSettings(TsukuyomiPcssSettings settings, RenderTextureDescriptor cameraDescriptor)
            {
                FindBlockerSampleCount = settings.FindBlockerSampleCount;
                PcfSampleCount = settings.PcfSampleCount;
                AngularDiameter = settings.AngularDiameter;
                BlockerSearchAngularDiameter = settings.BlockerSearchAngularDiameter;
                MinFilterMaxAngularDiameter = settings.MinFilterMaxAngularDiameter;
                MaxPenumbraSize = settings.MaxPenumbraSize;
                MaxSamplingDistance = settings.MaxSamplingDistance;
                MinFilterSizeTexels = settings.MinFilterSizeTexels;
                PenumbraMaskScale = settings.PenumbraMaskScale;

                int penumbraWidth = Mathf.Max(1, cameraDescriptor.width / PenumbraMaskScale);
                int penumbraHeight = Mathf.Max(1, cameraDescriptor.height / PenumbraMaskScale);
                PenumbraMaskTexelSize = new Vector4(1.0f / penumbraWidth, 1.0f / penumbraHeight, penumbraWidth, penumbraHeight);
                ColorAttachmentTexelSize = new Vector4(1.0f / cameraDescriptor.width, 1.0f / cameraDescriptor.height, cameraDescriptor.width, cameraDescriptor.height);
                PenumbraMaskViewport = new Rect(0.0f, 0.0f, penumbraWidth, penumbraHeight);
                ColorAttachmentViewport = new Rect(0.0f, 0.0f, cameraDescriptor.width, cameraDescriptor.height);
            }

            public static TsukuyomiPcssSettings FromResolved(TsukuyomiPcssResolvedSettings settings)
            {
                return new TsukuyomiPcssSettings(settings);
            }

            public TsukuyomiPcssSettings WithCameraDescriptor(RenderTextureDescriptor cameraDescriptor)
            {
                return new TsukuyomiPcssSettings(this, cameraDescriptor);
            }
        }
    }
}


