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
            TsukuyomiFsr3Settings settings = TsukuyomiRenderPipelineProjectSettings.Current.Fsr3Settings;
            if (settings == null || !settings.Enabled || settings.ReactiveMaskMode == TsukuyomiFsr3ReactiveMaskMode.Disabled)
                return false;
            if (UniversalRenderPipeline.asset == null || UniversalRenderPipeline.asset.upscalerName != TsukuyomiFsr3Upscaler.UpscalerName)
                return false;
            if (requireManual && settings.ReactiveMaskMode != TsukuyomiFsr3ReactiveMaskMode.AutoAndManual)
                return false;
            if (cameraData == null || cameraData.camera == null || cameraData.cameraType != CameraType.Game)
                return false;
            return !cameraData.isPreviewCamera && !cameraData.xr.enabled && cameraData.renderType != CameraRenderType.Overlay;
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
        private FirstPersonViewPass _firstPersonPass;

        private sealed class PassData
        {
            public RendererListHandle RendererList;
            public RendererListHandle FirstPersonRendererList;
            public bool HasFirstPersonRendererList;
            public Matrix4x4 FirstPersonView;
            public Matrix4x4 FirstPersonProjection;
            public Matrix4x4 CameraView;
            public Matrix4x4 CameraProjection;
        }

        public TsukuyomiFsr3ManualMaskPass()
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing - 1;
        }

        public void Configure(TsukuyomiPipelineProfile profile)
        {
            _firstPersonPass = null;
            if (profile?.Passes == null)
                return;

            for (int i = 0; i < profile.Passes.Count; i++)
            {
                if (profile.Passes[i] is FirstPersonViewPass firstPersonPass && firstPersonPass.Enabled)
                {
                    _firstPersonPass = firstPersonPass;
                    break;
                }
            }
        }

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

            int firstPersonMask = _firstPersonPass?.CullingMask.value ?? 0;
            DrawingSettings drawingSettings = CreateDrawingSettings(renderingData, cameraData, lightData, cameraData.GetViewMatrix());
            FilteringSettings filteringSettings = new(RenderQueueRange.transparent, ~firstPersonMask);
            RendererListHandle rendererList = renderGraph.CreateRendererList(
                new RendererListParams(renderingData.cullResults, drawingSettings, filteringSettings));

            bool hasFirstPersonRendererList = TryCreateFirstPersonRendererList(
                renderGraph,
                frameData,
                renderingData,
                cameraData,
                lightData,
                out RendererListHandle firstPersonRendererList,
                out Matrix4x4 firstPersonView,
                out Matrix4x4 firstPersonProjection);

            using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass<PassData>("Tsukuyomi FSR3 Manual Masks", out PassData passData))
            {
                passData.RendererList = rendererList;
                passData.FirstPersonRendererList = firstPersonRendererList;
                passData.HasFirstPersonRendererList = hasFirstPersonRendererList;
                passData.FirstPersonView = firstPersonView;
                passData.FirstPersonProjection = firstPersonProjection;
                passData.CameraView = cameraData.GetViewMatrix();
                passData.CameraProjection = cameraData.GetProjectionMatrix();
                builder.SetRenderAttachment(reactive, 0, AccessFlags.Write);
                builder.SetRenderAttachment(composition, 1, AccessFlags.Write);
                if (resources.activeDepthTexture.IsValid())
                    builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                builder.UseRendererList(rendererList);
                if (hasFirstPersonRendererList)
                    builder.UseRendererList(firstPersonRendererList);
                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    context.cmd.DrawRendererList(data.RendererList);
                    if (data.HasFirstPersonRendererList)
                    {
                        context.cmd.SetViewProjectionMatrices(data.FirstPersonView, data.FirstPersonProjection);
                        context.cmd.DrawRendererList(data.FirstPersonRendererList);
                        context.cmd.SetViewProjectionMatrices(data.CameraView, data.CameraProjection);
                    }
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

        private bool TryCreateFirstPersonRendererList(
            RenderGraph renderGraph,
            ContextContainer frameData,
            UniversalRenderingData renderingData,
            UniversalCameraData cameraData,
            UniversalLightData lightData,
            out RendererListHandle rendererList,
            out Matrix4x4 viewMatrix,
            out Matrix4x4 projectionMatrix)
        {
            rendererList = default;
            viewMatrix = default;
            projectionMatrix = default;
            Camera camera = cameraData.camera;
            if (_firstPersonPass == null || _firstPersonPass.CullingMask.value == 0 || camera == null ||
                camera.cameraType != CameraType.Game || cameraData.renderType != CameraRenderType.Base || camera.stereoEnabled ||
                (!string.IsNullOrEmpty(_firstPersonPass.CameraTag) && !camera.CompareTag(_firstPersonPass.CameraTag)) ||
                !camera.TryGetCullingParameters(out ScriptableCullingParameters cullingParameters))
            {
                return false;
            }

            int width = Mathf.Max(1, cameraData.cameraTargetDescriptor.width);
            int height = Mathf.Max(1, cameraData.cameraTargetDescriptor.height);
            viewMatrix = camera.worldToCameraMatrix;
            projectionMatrix = Matrix4x4.Perspective(
                Mathf.Clamp(_firstPersonPass.FieldOfView, 1.0f, 179.0f),
                (float)width / height,
                Mathf.Max(0.001f, _firstPersonPass.NearClipPlane),
                Mathf.Max(_firstPersonPass.NearClipPlane + 0.001f, _firstPersonPass.FarClipPlane));
            projectionMatrix.m02 += camera.projectionMatrix.m02 - camera.nonJitteredProjectionMatrix.m02;
            projectionMatrix.m12 += camera.projectionMatrix.m12 - camera.nonJitteredProjectionMatrix.m12;

            cullingParameters.cullingMatrix = projectionMatrix * viewMatrix;
            cullingParameters.origin = camera.transform.position;
            cullingParameters.cullingMask = unchecked((uint)_firstPersonPass.CullingMask.value);
            if (!_firstPersonPass.UseOcclusionCulling)
                cullingParameters.cullingOptions &= ~CullingOptions.OcclusionCull;

            CullingResults cullingResults = frameData.Get<CullContextData>().Cull(ref cullingParameters);
            DrawingSettings drawingSettings = CreateDrawingSettings(renderingData, cameraData, lightData, viewMatrix);
            FilteringSettings filteringSettings = new(RenderQueueRange.transparent, _firstPersonPass.CullingMask.value);
            rendererList = renderGraph.CreateRendererList(new RendererListParams(cullingResults, drawingSettings, filteringSettings));
            return true;
        }
    }
}
#endif
