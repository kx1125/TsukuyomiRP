using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering
{
    [System.Serializable]
    internal sealed class TsukuyomiSssSkinPass : UnsafePass
    {
        private const string SkinDepthTextureName = "_SSSSkinDepthTexture";
        private const string SkinNormalsTextureName = "_SSSSkinNormalsTexture";
        private const string SkinMaskTextureName = "_SSSSkinMaskTexture";
        private const string SkinLightingTextureName = "_SSSSkinLightingTexture";
        private const string SkinLightingBlurredTextureName = "_SSSSkinLightingBlurredTexture";
        private const string SkinLightingDepthTextureName = "_SSSSkinLightingDepthTexture";
        private const string SkinLightingDepthMaskTextureName = "_SSSSkinLightingDepthMaskTexture";
        private const string SkinLightingBlurPingTextureName = "_SSSSkinLightingBlurPing";
        private const string SkinLightingBlurPongTextureName = "_SSSSkinLightingBlurPong";

        private static readonly int SkinDepthTextureId = Shader.PropertyToID(SkinDepthTextureName);
        private static readonly int SkinNormalsTextureId = Shader.PropertyToID(SkinNormalsTextureName);
        private static readonly int SkinMaskTextureId = Shader.PropertyToID(SkinMaskTextureName);
        private static readonly int SkinLightingTextureId = Shader.PropertyToID(SkinLightingTextureName);
        private static readonly int SkinLightingBlurredTextureId = Shader.PropertyToID(SkinLightingBlurredTextureName);
        private static readonly int LightingTexBlurredId = Shader.PropertyToID("_LightingTexBlurred");
        private static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
        private static readonly int BlitTextureTexelSizeId = Shader.PropertyToID("_BlitTexture_TexelSize");
        private static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
        private static readonly int TexelOffsetScaleId = Shader.PropertyToID("_TexelOffsetScale");
        private static readonly int BlurColorId = Shader.PropertyToID("_SSSSkinBlurColor");
        private static readonly int DepthTestId = Shader.PropertyToID("_SSSSkinDepthTest");
        private static readonly int NormalTestId = Shader.PropertyToID("_SSSSkinNormalTest");
        private static readonly int MaxDistanceId = Shader.PropertyToID("_SSSSkinMaxDistance");
        private static readonly int SampleCountId = Shader.PropertyToID("_SSSSkinSampleCount");
        private static readonly int RandomizedRotationId = Shader.PropertyToID("_SSSSkinRandomizedRotation");
        private static readonly int UseSharedDepthNormalsId = Shader.PropertyToID("_SSSSkinUseSharedDepthNormals");
        private static readonly int DitherScaleId = Shader.PropertyToID("DitherScale");
        private static readonly int DitherIntensityId = Shader.PropertyToID("DitherIntensity");
        private static readonly int NoiseTextureId = Shader.PropertyToID("NoiseTexture");

        private readonly List<ShaderTagId> _depthNormalsTags = new();
        private readonly List<ShaderTagId> _skinMaskTags = new();
        private readonly List<ShaderTagId> _lightingTags = new();

        private readonly ProfilingSampler _normalsSampler = new("SSSSkin DepthNormals");
        private readonly ProfilingSampler _maskSampler = new("SSSSkin Mask");
        private readonly ProfilingSampler _lightingDepthSampler = new("SSSSkin Lighting Depth");
        private readonly ProfilingSampler _lightingSampler = new("SSSSkin Lighting");
        private readonly ProfilingSampler _blurSampler = new("SSSSkin Lighting Blur");

        private FilteringSettings _filteringSettings = new(RenderQueueRange.opaque);
        private TsukuyomiPipelineProfile _profile;
        private TsukuyomiSssSkinResolvedSettings _settings;
        private Material _blurMaterial;
        private Texture2D _defaultNoiseTexture;
        private bool _useSharedDepthNormals;
        private bool _ownsBlurMaterial;
        private bool _warnedMissingLayer;

        public override string Name => "SSS Skin";

        public bool Configure(TsukuyomiPipelineProfile profile, TsukuyomiSssSkinVolume volume, bool useSharedDepthNormals)
        {
            _profile = profile;

            if (profile == null)
                return false;

            _settings = TsukuyomiSssSkinResolvedSettings.From(profile, volume);
            if (!_settings.Enabled)
                return false;

            LayerMask skinLayerMask = _settings.SkinLayerMask;
            if (skinLayerMask.value == 0)
            {
                int skinLayer = LayerMask.NameToLayer("Skin");
                if (skinLayer >= 0)
                    skinLayerMask = 1 << skinLayer;
            }

            if (skinLayerMask.value == 0)
            {
                if (!_warnedMissingLayer)
                {
                    Debug.LogWarning("Tsukuyomi SSS Skin skipped: create a Skin layer or assign SSS Skin Layer Mask in the Tsukuyomi profile.");
                    _warnedMissingLayer = true;
                }

                return false;
            }

            _filteringSettings = new FilteringSettings(RenderQueueRange.opaque, skinLayerMask);
            _useSharedDepthNormals = useSharedDepthNormals;

            EnsureShaderTags();

            if (_defaultNoiseTexture == null)
                _defaultNoiseTexture = CreateDefaultNoiseTexture();

            return ResolveResources();
        }

        public override bool IsActive(in FrameContext frame)
        {
            return base.IsActive(frame) && _profile != null && _settings.Enabled && _blurMaterial != null;
        }

        public void Dispose()
        {
            if (_ownsBlurMaterial)
                CoreUtils.Destroy(_blurMaterial);

            CoreUtils.Destroy(_defaultNoiseTexture);
            _blurMaterial = null;
            _defaultNoiseTexture = null;
            _ownsBlurMaterial = false;
        }

        public override void Record(in UnsafePassContext context)
        {
            if (_profile == null || !_settings.Enabled || _blurMaterial == null || context.CameraData.isPreviewCamera)
                return;

            UniversalResourceData resourceData = context.FrameData.Get<UniversalResourceData>();
            UniversalRenderingData renderingData = context.FrameData.Get<UniversalRenderingData>();

            RenderTextureDescriptor baseDescriptor = context.CameraData.cameraTargetDescriptor;
            baseDescriptor.msaaSamples = 1;

            int qualityDivisor = GetQualityDivisor(_settings.Quality);
            RenderTextureDescriptor lightingDescriptor = CreateLightingDescriptor(baseDescriptor, qualityDivisor);
            PassSettings passSettings = CreatePassSettings(lightingDescriptor, context.CameraData.camera);
            bool executeBlur = passSettings.Iterations > 0 && passSettings.Radius > 0.0f;

            TextureSlot skinMaskSlot = TextureSlot.ReadWrite(SkinMaskTextureName, CreateTextureDesc(CreateMaskDescriptor(baseDescriptor), FilterMode.Point, true, Color.clear));
            TextureSlot skinLightingSlot = TextureSlot.ReadWrite(SkinLightingTextureName, CreateTextureDesc(lightingDescriptor, FilterMode.Bilinear, true, Color.clear));
            TextureSlot skinLightingBlurredSlot = TextureSlot.Write(SkinLightingBlurredTextureName, CreateTextureDesc(lightingDescriptor, FilterMode.Bilinear, true, Color.clear));
            TextureSlot blurPingSlot = TextureSlot.ReadWrite(SkinLightingBlurPingTextureName, CreateTextureDesc(lightingDescriptor, FilterMode.Bilinear, true, Color.clear));
            TextureSlot blurPongSlot = TextureSlot.ReadWrite(SkinLightingBlurPongTextureName, CreateTextureDesc(lightingDescriptor, FilterMode.Bilinear, true, Color.clear));

            TextureHandle skinMask = context.GetTexture(skinMaskSlot);
            TextureHandle skinLighting = context.GetTexture(skinLightingSlot);
            TextureHandle skinLightingBlurred = executeBlur ? context.GetTexture(skinLightingBlurredSlot) : TextureHandle.nullHandle;
            TextureHandle blurPing = executeBlur ? context.GetTexture(blurPingSlot) : TextureHandle.nullHandle;
            TextureHandle blurPong = executeBlur ? context.GetTexture(blurPongSlot) : TextureHandle.nullHandle;

            bool useSharedDepthNormals = _useSharedDepthNormals
                && resourceData.cameraDepthTexture.IsValid()
                && resourceData.cameraNormalsTexture.IsValid();

            TextureHandle depthTexture;
            TextureHandle normalsTexture;
            TextureHandle depthAttachment;
            TextureHandle skinDepth = TextureHandle.nullHandle;
            TextureHandle skinNormals = TextureHandle.nullHandle;

            if (useSharedDepthNormals)
            {
                depthTexture = resourceData.cameraDepthTexture;
                normalsTexture = resourceData.cameraNormalsTexture;
                depthAttachment = resourceData.activeDepthTexture.IsValid() ? resourceData.activeDepthTexture : resourceData.cameraDepthTexture;
            }
            else
            {
                TextureSlot skinDepthSlot = TextureSlot.ReadWrite(SkinDepthTextureName, CreateTextureDesc(CreateDepthDescriptor(baseDescriptor), FilterMode.Point, true, Color.clear));
                TextureSlot skinNormalsSlot = TextureSlot.ReadWrite(SkinNormalsTextureName, CreateTextureDesc(CreateNormalDescriptor(baseDescriptor), FilterMode.Point, true, Color.clear));
                skinDepth = context.GetTexture(skinDepthSlot);
                skinNormals = context.GetTexture(skinNormalsSlot);
                depthTexture = skinDepth;
                normalsTexture = skinNormals;
                depthAttachment = skinDepth;
            }

            bool lightingUsesSeparateDepth = qualityDivisor > 1;
            TextureHandle lightingDepthAttachment = depthAttachment;
            TextureHandle lightingDepthMask = TextureHandle.nullHandle;
            if (lightingUsesSeparateDepth)
            {
                TextureSlot lightingDepthSlot = TextureSlot.ReadWrite(SkinLightingDepthTextureName, CreateTextureDesc(CreateDepthDescriptor(lightingDescriptor), FilterMode.Point, true, Color.clear));
                TextureSlot lightingDepthMaskSlot = TextureSlot.ReadWrite(SkinLightingDepthMaskTextureName, CreateTextureDesc(CreateMaskDescriptor(lightingDescriptor), FilterMode.Point, true, Color.clear));
                lightingDepthAttachment = context.GetTexture(lightingDepthSlot);
                lightingDepthMask = context.GetTexture(lightingDepthMaskSlot);
            }

            RendererListHandle depthNormalsRendererList = useSharedDepthNormals
                ? default
                : CreateRendererList(context.RenderGraph, renderingData, context.CameraData, context.LightData, _depthNormalsTags, false);
            RendererListHandle maskRendererList = CreateRendererList(context.RenderGraph, renderingData, context.CameraData, context.LightData, _skinMaskTags, false);
            RendererListHandle lightingDepthRendererList = lightingUsesSeparateDepth
                ? CreateRendererList(context.RenderGraph, renderingData, context.CameraData, context.LightData, _skinMaskTags, false)
                : default;
            RendererListHandle lightingRendererList = CreateRendererList(context.RenderGraph, renderingData, context.CameraData, context.LightData, _lightingTags, true);

            context.Builder.UseTexture(skinMask, AccessFlags.ReadWrite);
            context.Builder.UseTexture(skinLighting, AccessFlags.ReadWrite);
            if (executeBlur)
            {
                context.Builder.UseTexture(skinLightingBlurred, AccessFlags.Write);
                context.Builder.UseTexture(blurPing, AccessFlags.ReadWrite);
                context.Builder.UseTexture(blurPong, AccessFlags.ReadWrite);
            }

            if (useSharedDepthNormals)
            {
                if (SameHandle(depthTexture, depthAttachment))
                    context.Builder.UseTexture(depthTexture, AccessFlags.ReadWrite);
                else
                {
                    context.Builder.UseTexture(depthTexture, AccessFlags.Read);
                    if (depthAttachment.IsValid())
                        context.Builder.UseTexture(depthAttachment, AccessFlags.ReadWrite);
                }

                context.Builder.UseTexture(normalsTexture, AccessFlags.Read);
            }
            else
            {
                context.Builder.UseTexture(skinDepth, AccessFlags.ReadWrite);
                context.Builder.UseTexture(skinNormals, AccessFlags.ReadWrite);
            }

            if (lightingUsesSeparateDepth)
            {
                context.Builder.UseTexture(lightingDepthAttachment, AccessFlags.ReadWrite);
                context.Builder.UseTexture(lightingDepthMask, AccessFlags.ReadWrite);
            }

            if (resourceData.mainShadowsTexture.IsValid())
                context.Builder.UseTexture(resourceData.mainShadowsTexture, AccessFlags.Read);
            if (resourceData.additionalShadowsTexture.IsValid())
                context.Builder.UseTexture(resourceData.additionalShadowsTexture, AccessFlags.Read);

            if (depthNormalsRendererList.IsValid())
                context.Builder.UseRendererList(depthNormalsRendererList);
            context.Builder.UseRendererList(maskRendererList);
            if (lightingDepthRendererList.IsValid())
                context.Builder.UseRendererList(lightingDepthRendererList);
            context.Builder.UseRendererList(lightingRendererList);
            context.Builder.AllowGlobalStateModification(true);
            context.Builder.SetGlobalTextureAfterPass(skinMask, SkinMaskTextureId);
            context.Builder.SetGlobalTextureAfterPass(skinLighting, SkinLightingTextureId);
            context.Builder.SetGlobalTextureAfterPass(executeBlur ? skinLightingBlurred : skinLighting, SkinLightingBlurredTextureId);
            context.Builder.SetGlobalTextureAfterPass(executeBlur ? skinLightingBlurred : skinLighting, LightingTexBlurredId);
            if (skinDepth.IsValid())
                context.Builder.SetGlobalTextureAfterPass(skinDepth, SkinDepthTextureId);
            if (skinNormals.IsValid())
                context.Builder.SetGlobalTextureAfterPass(skinNormals, SkinNormalsTextureId);

            Material blurMaterial = _blurMaterial;
            Texture noiseTexture = _settings.NoiseTexture != null ? _settings.NoiseTexture : _defaultNoiseTexture;
            ProfilingSampler normalsSampler = _normalsSampler;
            ProfilingSampler maskSampler = _maskSampler;
            ProfilingSampler lightingDepthSampler = _lightingDepthSampler;
            ProfilingSampler lightingSampler = _lightingSampler;
            ProfilingSampler blurSampler = _blurSampler;
            Rect lightingViewport = new(0.0f, 0.0f, lightingDescriptor.width, lightingDescriptor.height);

            context.SetRenderFunc((data, graphContext) =>
            {
                CommandBuffer command = CommandBufferHelpers.GetNativeCommandBuffer(graphContext.cmd);

                if (!useSharedDepthNormals)
                {
                    using (new ProfilingScope(graphContext.cmd, normalsSampler))
                    {
                        graphContext.cmd.SetRenderTarget(skinNormals, skinDepth);
                        graphContext.cmd.ClearRenderTarget(true, true, Color.clear);
                        graphContext.cmd.DrawRendererList(depthNormalsRendererList);
                    }
                }

                using (new ProfilingScope(graphContext.cmd, maskSampler))
                {
                    graphContext.cmd.SetRenderTarget(skinMask, depthAttachment);
                    graphContext.cmd.ClearRenderTarget(false, true, Color.clear);
                    graphContext.cmd.DrawRendererList(maskRendererList);
                }

                if (lightingUsesSeparateDepth)
                {
                    using (new ProfilingScope(graphContext.cmd, lightingDepthSampler))
                    {
                        graphContext.cmd.SetRenderTarget(lightingDepthMask, lightingDepthAttachment);
                        command.SetViewport(lightingViewport);
                        graphContext.cmd.ClearRenderTarget(true, true, Color.clear);
                        graphContext.cmd.DrawRendererList(lightingDepthRendererList);
                    }
                }

                using (new ProfilingScope(graphContext.cmd, lightingSampler))
                {
                    graphContext.cmd.SetRenderTarget(skinLighting, lightingDepthAttachment);
                    command.SetViewport(lightingViewport);
                    graphContext.cmd.ClearRenderTarget(false, true, Color.clear);
                    graphContext.cmd.DrawRendererList(lightingRendererList);
                    graphContext.cmd.SetGlobalTexture(SkinLightingTextureId, skinLighting);
                }

                if (passSettings.Iterations <= 0 || passSettings.Radius <= 0.0f)
                {
                    graphContext.cmd.SetGlobalTexture(SkinLightingBlurredTextureId, skinLighting);
                    graphContext.cmd.SetGlobalTexture(LightingTexBlurredId, skinLighting);
                    return;
                }

                using (new ProfilingScope(graphContext.cmd, blurSampler))
                {
                    ExecuteBlur(
                        graphContext,
                        passSettings,
                        blurMaterial,
                        noiseTexture,
                        depthTexture,
                        normalsTexture,
                        skinMask,
                        skinLighting,
                        skinLightingBlurred,
                        blurPing,
                        blurPong,
                        useSharedDepthNormals);
                }
            });
        }

        private void EnsureShaderTags()
        {
            if (_depthNormalsTags.Count == 0)
                _depthNormalsTags.Add(new ShaderTagId("DepthNormals"));
            if (_skinMaskTags.Count == 0)
                _skinMaskTags.Add(new ShaderTagId("SSSSkinMask"));
            if (_lightingTags.Count == 0)
                _lightingTags.Add(new ShaderTagId("SSSSkinLighting"));
        }

        private bool ResolveResources()
        {
            if (_blurMaterial != null)
                return true;

            if (!TsukuyomiRenderPipelineResourcesProvider.TryGet(out TsukuyomiRenderPipelineResources resources))
                return false;

            if (resources.SssSkinBlurMaterial != null)
            {
                _blurMaterial = resources.SssSkinBlurMaterial;
                _ownsBlurMaterial = false;
                return true;
            }

            if (resources.SssSkinBlurShader == null)
            {
                Debug.LogError("Tsukuyomi SSS Skin requires a SSS Skin blur shader or material in TsukuyomiRenderPipelineResources.");
                return false;
            }

            _blurMaterial = CoreUtils.CreateEngineMaterial(resources.SssSkinBlurShader);
            _ownsBlurMaterial = true;
            return _blurMaterial != null;
        }

        private PassSettings CreatePassSettings(RenderTextureDescriptor descriptor, Camera camera)
        {
            int iterations = Mathf.Clamp(_settings.ScatteringIterations, 0, 10);
            float radius = Mathf.Max(0.0f, _settings.ScatteringRadius);
            float fovCompensation = camera != null && !camera.orthographic
                ? 60.0f / Mathf.Max(1.0f, camera.fieldOfView)
                : 1.0f;

            return new PassSettings
            {
                Descriptor = descriptor,
                Iterations = iterations,
                Radius = radius,
                SampleCount = Mathf.Clamp(_settings.ShaderIterations + 1, 2, 33),
                DepthTest = Mathf.Max(0.00001f, _settings.DepthTest / 20.0f),
                NormalTest = Mathf.Max(0.001f, _settings.NormalTest),
                MaxDistance = Mathf.Max(0.0f, _settings.MaxDistance),
                Color = _settings.SssColor,
                RandomizedRotation = _settings.RandomizedRotation,
                DitherScale = _settings.DitherScale,
                DitherIntensity = _settings.DitherIntensity,
                VerticalOffsetScale = new Vector4(0.0f, descriptor.height * radius * 0.002f * fovCompensation, 0.0f, 0.0f),
                HorizontalOffsetScale = new Vector4(descriptor.width * radius * 0.002f * fovCompensation, 0.0f, 0.0f, 0.0f)
            };
        }

        private static void ExecuteBlur(
            UnsafeGraphContext context,
            PassSettings settings,
            Material material,
            Texture noiseTexture,
            TextureHandle depthTexture,
            TextureHandle normalsTexture,
            TextureHandle maskTexture,
            TextureHandle source,
            TextureHandle destination,
            TextureHandle blurPing,
            TextureHandle blurPong,
            bool useSharedDepthNormals)
        {
            UnsafeCommandBuffer unsafeCommand = context.cmd;
            CommandBuffer command = CommandBufferHelpers.GetNativeCommandBuffer(unsafeCommand);

            int width = Mathf.Max(1, settings.Descriptor.width);
            int height = Mathf.Max(1, settings.Descriptor.height);
            Rect viewport = new(0.0f, 0.0f, width, height);

            unsafeCommand.SetGlobalTexture(SkinDepthTextureId, depthTexture);
            unsafeCommand.SetGlobalTexture(SkinNormalsTextureId, normalsTexture);
            unsafeCommand.SetGlobalTexture(SkinMaskTextureId, maskTexture);
            if (noiseTexture != null)
                command.SetGlobalTexture(NoiseTextureId, noiseTexture);
            command.SetGlobalVector(BlitScaleBiasId, new Vector4(1, 1, 0, 0));
            command.SetGlobalVector(BlitTextureTexelSizeId, new Vector4(1.0f / width, 1.0f / height, width, height));
            command.SetGlobalVector(BlurColorId, settings.Color);
            command.SetGlobalFloat(DepthTestId, settings.DepthTest);
            command.SetGlobalFloat(NormalTestId, settings.NormalTest);
            command.SetGlobalFloat(MaxDistanceId, settings.MaxDistance);
            command.SetGlobalInt(SampleCountId, settings.SampleCount);
            command.SetGlobalFloat(RandomizedRotationId, settings.RandomizedRotation ? 1.0f : 0.0f);
            command.SetGlobalFloat(UseSharedDepthNormalsId, useSharedDepthNormals ? 1.0f : 0.0f);
            command.SetGlobalFloat(DitherScaleId, settings.DitherScale);
            command.SetGlobalFloat(DitherIntensityId, settings.DitherIntensity);

            for (int i = 0; i < settings.Iterations; i++)
            {
                unsafeCommand.SetGlobalTexture(BlitTextureId, i == 0 ? source : blurPong);
                command.SetGlobalVector(TexelOffsetScaleId, settings.VerticalOffsetScale);
                unsafeCommand.SetRenderTarget(blurPing);
                command.SetViewport(viewport);
                CoreUtils.DrawFullScreen(command, material, null, 0);

                unsafeCommand.SetGlobalTexture(BlitTextureId, blurPing);
                command.SetGlobalVector(TexelOffsetScaleId, settings.HorizontalOffsetScale);

                unsafeCommand.SetRenderTarget(i == settings.Iterations - 1 ? destination : blurPong);
                command.SetViewport(viewport);
                CoreUtils.DrawFullScreen(command, material, null, 0);
            }
        }

        private RendererListHandle CreateRendererList(
            RenderGraph renderGraph,
            UniversalRenderingData renderingData,
            UniversalCameraData cameraData,
            UniversalLightData lightData,
            List<ShaderTagId> shaderTags,
            bool includeLightingPerObjectData)
        {
            DrawingSettings drawingSettings = RenderingUtils.CreateDrawingSettings(shaderTags, renderingData, cameraData, lightData, cameraData.defaultOpaqueSortFlags);
            if (!includeLightingPerObjectData)
                drawingSettings.perObjectData = PerObjectData.None;

            RendererListParams rendererListParams = new(renderingData.cullResults, drawingSettings, _filteringSettings);
            return renderGraph.CreateRendererList(rendererListParams);
        }

        private static RenderTextureDescriptor CreateDepthDescriptor(RenderTextureDescriptor baseDescriptor)
        {
            RenderTextureDescriptor descriptor = baseDescriptor;
            descriptor.graphicsFormat = GraphicsFormat.None;
            descriptor.colorFormat = RenderTextureFormat.Depth;
            descriptor.depthStencilFormat = SystemInfo.IsFormatSupported(GraphicsFormat.D32_SFloat, GraphicsFormatUsage.Render)
                ? GraphicsFormat.D32_SFloat
                : GraphicsFormat.D24_UNorm_S8_UInt;
            return descriptor;
        }

        private static RenderTextureDescriptor CreateNormalDescriptor(RenderTextureDescriptor baseDescriptor)
        {
            RenderTextureDescriptor descriptor = baseDescriptor;
            descriptor.depthStencilFormat = GraphicsFormat.None;
            descriptor.graphicsFormat = GetNormalFormat();
            return descriptor;
        }

        private static RenderTextureDescriptor CreateMaskDescriptor(RenderTextureDescriptor baseDescriptor)
        {
            RenderTextureDescriptor descriptor = baseDescriptor;
            descriptor.depthStencilFormat = GraphicsFormat.None;
            descriptor.graphicsFormat = GraphicsFormat.R8_UNorm;
            return descriptor;
        }

        private static RenderTextureDescriptor CreateLightingDescriptor(RenderTextureDescriptor baseDescriptor, int qualityDivisor)
        {
            RenderTextureDescriptor descriptor = baseDescriptor;
            descriptor.width = Mathf.Max(1, descriptor.width / Mathf.Max(1, qualityDivisor));
            descriptor.height = Mathf.Max(1, descriptor.height / Mathf.Max(1, qualityDivisor));
            descriptor.depthStencilFormat = GraphicsFormat.None;
            descriptor.graphicsFormat = GetLightingFormat();
            return descriptor;
        }

        private static TextureDesc CreateTextureDesc(RenderTextureDescriptor descriptor, FilterMode filterMode, bool clearBuffer, Color clearColor)
        {
            TextureDesc desc = new(descriptor)
            {
                filterMode = filterMode,
                msaaSamples = MSAASamples.None,
                clearBuffer = clearBuffer,
                clearColor = clearColor
            };
            return desc;
        }

        private static int GetQualityDivisor(TsukuyomiSssSkinQuality quality)
        {
            return quality switch
            {
                TsukuyomiSssSkinQuality.Medium => 2,
                TsukuyomiSssSkinQuality.Low => 4,
                _ => 1
            };
        }

        private static bool SameHandle(TextureHandle a, TextureHandle b)
        {
            return a.IsValid() && b.IsValid() && a.Equals(b);
        }

        private static GraphicsFormat GetNormalFormat()
        {
            if (SystemInfo.IsFormatSupported(GraphicsFormat.R8G8B8A8_SNorm, GraphicsFormatUsage.Render))
                return GraphicsFormat.R8G8B8A8_SNorm;
            if (SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_SFloat, GraphicsFormatUsage.Render))
                return GraphicsFormat.R16G16B16A16_SFloat;
            return GraphicsFormat.R32G32B32A32_SFloat;
        }

        private static GraphicsFormat GetLightingFormat()
        {
            if (SystemInfo.IsFormatSupported(GraphicsFormat.B10G11R11_UFloatPack32, GraphicsFormatUsage.Render))
                return GraphicsFormat.B10G11R11_UFloatPack32;
            if (SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_SFloat, GraphicsFormatUsage.Render))
                return GraphicsFormat.R16G16B16A16_SFloat;
            return GraphicsFormat.R8G8B8A8_UNorm;
        }

        private static Texture2D CreateDefaultNoiseTexture()
        {
            const int size = 64;
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "SSSSkin Default Noise",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    uint hash = (uint)(x * 1973 + y * 9277 + 89173);
                    hash ^= hash << 13;
                    hash ^= hash >> 17;
                    hash ^= hash << 5;
                    pixels[y * size + x] = new Color32((byte)(hash & 0xff), (byte)((hash >> 8) & 0xff), 0, 255);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private struct PassSettings
        {
            public RenderTextureDescriptor Descriptor;
            public int Iterations;
            public float Radius;
            public int SampleCount;
            public float DepthTest;
            public float NormalTest;
            public float MaxDistance;
            public Color Color;
            public bool RandomizedRotation;
            public float DitherScale;
            public float DitherIntensity;
            public Vector4 VerticalOffsetScale;
            public Vector4 HorizontalOffsetScale;
        }
    }
}