#if ENABLE_UPSCALER_FRAMEWORK
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering
{
    internal sealed class TsukuyomiFsr3FrameData : ContextItem
    {
        public TextureHandle ColorOpaqueOnly;
        public TextureHandle ReactiveMask;
        public TextureHandle TransparencyAndCompositionMask;

        public override void Reset()
        {
            ColorOpaqueOnly = TextureHandle.nullHandle;
            ReactiveMask = TextureHandle.nullHandle;
            TransparencyAndCompositionMask = TextureHandle.nullHandle;
        }
    }

    internal static class TsukuyomiFsr3MaskSupport
    {
        public static bool IsEnabled(UniversalCameraData cameraData, bool requireManual = false)
        {
            TsukuyomiFsr3Settings settings = TsukuyomiUpscaling.ActiveFsr3Settings;
            if (settings == null || !settings.Enabled || settings.ReactiveMaskMode == TsukuyomiFsr3ReactiveMaskMode.Disabled)
                return false;
            if (TsukuyomiUpscaling.Active == null || TsukuyomiUpscaling.Active.ActualBackend != UpscalerBackend.FSR3)
                return false;
            if (requireManual && settings.ReactiveMaskMode != TsukuyomiFsr3ReactiveMaskMode.AutoAndManual)
                return false;
            if (cameraData == null || cameraData.camera == null || cameraData.cameraType != CameraType.Game)
                return false;
            return TsukuyomiUpscaling.IsSupportedCamera(cameraData.camera, out _);
        }
    }

    internal sealed class TsukuyomiFsr3OpaqueOnlyCapturePass : ScriptableRenderPass
    {
        public TsukuyomiFsr3OpaqueOnlyCapturePass()
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            if (!TsukuyomiFsr3MaskSupport.IsEnabled(cameraData))
                return;

            UniversalResourceData resources = frameData.Get<UniversalResourceData>();
            TextureHandle source = resources.cameraColor;
            if (!source.IsValid())
                return;

            TextureDesc desc = source.GetDescriptor(renderGraph);
            desc.name = "_TsukuyomiFsr3ColorOpaqueOnly";
            desc.format = GraphicsFormat.B10G11R11_UFloatPack32;
            desc.msaaSamples = MSAASamples.None;
            desc.depthBufferBits = DepthBits.None;
            desc.clearBuffer = false;
            desc.enableRandomWrite = false;
            TextureHandle destination = renderGraph.CreateTexture(desc);
            renderGraph.AddBlitPass(source, destination, Vector2.one, Vector2.zero, passName: "Tsukuyomi FSR3 Capture Opaque Only");
            frameData.GetOrCreate<TsukuyomiFsr3FrameData>().ColorOpaqueOnly = destination;
        }
    }

    internal sealed class TsukuyomiFsr3ManualMaskPass : ScriptableRenderPass
    {
        private static readonly List<ShaderTagId> ShaderTags = new() { new ShaderTagId("TsukuyomiFsr3Mask") };
        private IReadOnlyList<IRenderMaskViewProvider> _providers;
        private readonly List<RenderMaskView> _views = new();
        private readonly List<RendererListHandle> _lists = new();

        private sealed class PassData
        {
            public RendererListHandle RendererList;
            public readonly List<RendererListHandle> ViewLists = new();
            public readonly List<RenderMaskView> Views = new();
            public Matrix4x4 CameraView, CameraProjection;
        }
        public TsukuyomiFsr3ManualMaskPass() => renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing - 1;
        public void Configure(IReadOnlyList<IRenderMaskViewProvider> providers) => _providers = providers;

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            if (!TsukuyomiFsr3MaskSupport.IsEnabled(cameraData, requireManual: true))
                return;

            UniversalResourceData resources = frameData.Get<UniversalResourceData>();
            UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
            UniversalLightData lightData = frameData.Get<UniversalLightData>();
            TextureHandle cameraColor = resources.cameraColor;
            if (!cameraColor.IsValid())
                return;

            TextureDesc maskDesc = cameraColor.GetDescriptor(renderGraph);
            maskDesc.name = "_TsukuyomiFsr3ReactiveMask";
            maskDesc.format = GraphicsFormat.R8_UNorm;
            maskDesc.msaaSamples = MSAASamples.None;
            maskDesc.depthBufferBits = DepthBits.None;
            maskDesc.clearBuffer = true;
            maskDesc.clearColor = Color.clear;
            maskDesc.enableRandomWrite = false;
            TextureHandle reactive = renderGraph.CreateTexture(maskDesc);
            maskDesc.name = "_TsukuyomiFsr3TransparencyAndCompositionMask";
            TextureHandle composition = renderGraph.CreateTexture(maskDesc);

            _views.Clear(); _lists.Clear();
            int customMask = 0;
            if (_providers != null)
                foreach (var provider in _providers)
                    if (provider.TryGetMaskView(cameraData, out var view)
                        && TryCreateViewRendererList(renderGraph, frameData, renderingData, cameraData, lightData, view, out var list))
                    { _views.Add(view); _lists.Add(list); customMask |= view.LayerMask; }
            DrawingSettings drawingSettings = CreateDrawingSettings(renderingData, cameraData, lightData, cameraData.GetViewMatrix());
            FilteringSettings filteringSettings = new(RenderQueueRange.transparent, ~customMask);
            RendererListHandle rendererList = renderGraph.CreateRendererList(new RendererListParams(renderingData.cullResults, drawingSettings, filteringSettings));

            using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>("Tsukuyomi FSR3 Manual Masks", out PassData passData))
            {
                passData.RendererList = rendererList;
                passData.ViewLists.Clear(); passData.Views.Clear();
                passData.ViewLists.AddRange(_lists); passData.Views.AddRange(_views);
                passData.CameraView = cameraData.GetViewMatrix();
                passData.CameraProjection = cameraData.GetProjectionMatrix();
                builder.SetRenderAttachment(reactive, 0, AccessFlags.Write);
                builder.SetRenderAttachment(composition, 1, AccessFlags.Write);
                if (resources.activeDepthTexture.IsValid())
                    builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                builder.UseRendererList(rendererList);
                foreach (var list in _lists) builder.UseRendererList(list);
                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    context.cmd.DrawRendererList(data.RendererList);
                    for (int i = 0; i < data.ViewLists.Count; i++)
                    {
                        context.cmd.SetViewProjectionMatrices(data.Views[i].View, data.Views[i].Projection);
                        context.cmd.DrawRendererList(data.ViewLists[i]);
                    }
                    context.cmd.SetViewProjectionMatrices(data.CameraView, data.CameraProjection);
                });
            }

            TsukuyomiFsr3FrameData fsrData = frameData.GetOrCreate<TsukuyomiFsr3FrameData>();
            fsrData.ReactiveMask = reactive;
            fsrData.TransparencyAndCompositionMask = composition;
        }

        private static DrawingSettings CreateDrawingSettings(
            UniversalRenderingData renderingData,
            UniversalCameraData cameraData,
            UniversalLightData lightData,
            Matrix4x4 viewMatrix)
        {
            DrawingSettings settings = RenderingUtils.CreateDrawingSettings(
                ShaderTags,
                renderingData,
                cameraData,
                lightData,
                SortingCriteria.CommonTransparent);
            SortingSettings sorting = settings.sortingSettings;
            sorting.criteria = SortingCriteria.CommonTransparent;
            sorting.worldToCameraMatrix = viewMatrix;
            sorting.cameraPosition = cameraData.worldSpaceCameraPos;
            settings.sortingSettings = sorting;
            return settings;
        }

        private static bool TryCreateViewRendererList(RenderGraph graph, ContextContainer frame,
            UniversalRenderingData renderingData, UniversalCameraData cameraData, UniversalLightData lightData,
            RenderMaskView view, out RendererListHandle rendererList)
        {
            rendererList = default;
            if (!cameraData.camera.TryGetCullingParameters(out ScriptableCullingParameters culling)) return false;
            culling.cullingMatrix = view.Projection * view.View;
            culling.origin = cameraData.camera.transform.position;
            culling.cullingMask = unchecked((uint)view.LayerMask);
            if (!view.UseOcclusionCulling) culling.cullingOptions &= ~CullingOptions.OcclusionCull;
            var results = frame.Get<CullContextData>().Cull(ref culling);
            var drawing = CreateDrawingSettings(renderingData, cameraData, lightData, view.View);
            rendererList = graph.CreateRendererList(new RendererListParams(results, drawing, new FilteringSettings(RenderQueueRange.transparent, view.LayerMask)));
            return true;
        }
    }
}
#endif
