using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering
{
    [System.Serializable]
    public abstract class RenderObjectPass : RasterPass
    {
        private static readonly int WorldSpaceCameraPosId = Shader.PropertyToID("_WorldSpaceCameraPos");

        private readonly List<ShaderTagId> _shaderTagIds = new();
        [System.NonSerialized]
        private RenderObjectContext _renderObjectContext;

        [Tooltip("Layers rendered by this pass.")]
        public LayerMask CullingMask = 1 << 6;

        public bool RenderOpaque = true;
        public bool RenderTransparent = true;
        public bool UseOcclusionCulling = true;

        protected virtual string DepthTextureName => $"_{GetType().Name}Depth";

        public override bool IsActive(in FrameContext frame)
        {
            return base.IsActive(frame)
                && CullingMask.value != 0
                && (RenderOpaque || RenderTransparent)
                && frame.CameraData != null
                && frame.CameraData.camera
                && IsCameraSupported(frame);
        }

        public sealed override void Record(in RasterPassContext context)
        {
            TextureHandle activeColor = context.Resources.ActiveColor;
            Camera camera = context.CameraData.camera;
            if (!activeColor.IsValid()
                || !camera
                || !camera.TryGetCullingParameters(out ScriptableCullingParameters cullingParameters))
            {
                return;
            }

            ValidateSettings();
            EnsureShaderTags();

            RenderTextureDescriptor cameraDescriptor = context.CameraData.cameraTargetDescriptor;
            int width = Mathf.Max(1, cameraDescriptor.width);
            int height = Mathf.Max(1, cameraDescriptor.height);
            _renderObjectContext ??= new RenderObjectContext();
            _renderObjectContext.Reset(context.CameraData, width, height);
            if (!Render(_renderObjectContext) || !_renderObjectContext.HasView)
                return;

            Matrix4x4 renderView = _renderObjectContext.ViewMatrix;
            Matrix4x4 renderProjection = _renderObjectContext.ProjectionMatrix;
            Vector3 renderCameraPosition = _renderObjectContext.CameraPosition;
            Rect renderViewport = _renderObjectContext.Viewport;

            cullingParameters.cullingMatrix = renderProjection * renderView;
            cullingParameters.origin = renderCameraPosition;
            cullingParameters.cullingMask = unchecked((uint)CullingMask.value);
            if (!UseOcclusionCulling)
                cullingParameters.cullingOptions &= ~CullingOptions.OcclusionCull;

            CullContextData cullContext = context.FrameData.Get<CullContextData>();
            CullingResults cullingResults = cullContext.Cull(ref cullingParameters);
            UniversalRenderingData renderingData = context.FrameData.Get<UniversalRenderingData>();

            RendererListHandle opaqueRendererList = default;
            RendererListHandle transparentRendererList = default;
            if (RenderOpaque)
            {
                opaqueRendererList = CreateRendererList(
                    context,
                    renderingData,
                    cullingResults,
                    renderView,
                    renderCameraPosition,
                    RenderQueueRange.opaque,
                    SortingCriteria.CommonOpaque);
            }

            if (RenderTransparent)
            {
                transparentRendererList = CreateRendererList(
                    context,
                    renderingData,
                    cullingResults,
                    renderView,
                    renderCameraPosition,
                    RenderQueueRange.transparent,
                    SortingCriteria.CommonTransparent);
            }

            TextureHandle depthTexture = context.RenderGraph.CreateTexture(CreateDepthDesc(cameraDescriptor));
            context.Builder.UseAllGlobalTextures(true);
            context.Builder.SetRenderAttachment(activeColor, 0, AccessFlags.ReadWrite);
            context.Builder.SetRenderAttachmentDepth(depthTexture, AccessFlags.Write);
            if (RenderOpaque)
                context.Builder.UseRendererList(opaqueRendererList);
            if (RenderTransparent)
                context.Builder.UseRendererList(transparentRendererList);

            UniversalResourceData resourceData = context.FrameData.Get<UniversalResourceData>();
            if (resourceData.mainShadowsTexture.IsValid())
                context.Builder.UseTexture(resourceData.mainShadowsTexture, AccessFlags.Read);
            if (resourceData.additionalShadowsTexture.IsValid())
                context.Builder.UseTexture(resourceData.additionalShadowsTexture, AccessFlags.Read);

            context.Builder.AllowGlobalStateModification(true);
            context.Builder.AllowPassCulling(false);

            Matrix4x4 cameraView = context.CameraData.GetViewMatrix();
            Matrix4x4 cameraProjection = context.CameraData.GetProjectionMatrix();
            Vector3 cameraPosition = context.CameraData.worldSpaceCameraPos;
            Rect cameraViewport = new(0.0f, 0.0f, width, height);

            context.SetRenderFunc((data, graphContext) =>
            {
                graphContext.cmd.SetViewport(renderViewport);
                graphContext.cmd.ClearRenderTarget(true, false, Color.clear);
                graphContext.cmd.SetViewProjectionMatrices(renderView, renderProjection);
                graphContext.cmd.SetGlobalVector(WorldSpaceCameraPosId, renderCameraPosition);
                if (RenderOpaque)
                    graphContext.cmd.DrawRendererList(opaqueRendererList);
                if (RenderTransparent)
                    graphContext.cmd.DrawRendererList(transparentRendererList);
                graphContext.cmd.SetGlobalVector(WorldSpaceCameraPosId, cameraPosition);
                graphContext.cmd.SetViewProjectionMatrices(cameraView, cameraProjection);
                graphContext.cmd.SetViewport(cameraViewport);
            });
        }

        protected abstract bool Render(RenderObjectContext ctx);

        protected virtual bool IsCameraSupported(in FrameContext frame) => true;

        protected virtual void PopulateShaderTagIds(List<ShaderTagId> shaderTagIds)
        {
            shaderTagIds.Add(new ShaderTagId("UniversalForward"));
            shaderTagIds.Add(new ShaderTagId("UniversalForwardOnly"));
            shaderTagIds.Add(new ShaderTagId("SRPDefaultUnlit"));
        }

        private RendererListHandle CreateRendererList(
            in RasterPassContext context,
            UniversalRenderingData renderingData,
            CullingResults cullingResults,
            Matrix4x4 viewMatrix,
            Vector3 cameraPosition,
            RenderQueueRange renderQueueRange,
            SortingCriteria sortingCriteria)
        {
            DrawingSettings drawingSettings = RenderingUtils.CreateDrawingSettings(
                _shaderTagIds,
                renderingData,
                context.CameraData,
                context.LightData,
                sortingCriteria);

            SortingSettings sortingSettings = drawingSettings.sortingSettings;
            sortingSettings.criteria = sortingCriteria;
            sortingSettings.worldToCameraMatrix = viewMatrix;
            sortingSettings.cameraPosition = cameraPosition;
            drawingSettings.sortingSettings = sortingSettings;

            FilteringSettings filteringSettings = new(renderQueueRange, CullingMask.value);
            return context.RenderGraph.CreateRendererList(
                new RendererListParams(cullingResults, drawingSettings, filteringSettings));
        }

        private void EnsureShaderTags()
        {
            if (_shaderTagIds.Count != 0)
                return;

            PopulateShaderTagIds(_shaderTagIds);
        }

        private TextureDesc CreateDepthDesc(RenderTextureDescriptor cameraDescriptor)
        {
            RenderTextureDescriptor descriptor = cameraDescriptor;
            descriptor.graphicsFormat = GraphicsFormat.None;
            descriptor.depthStencilFormat = GetDepthFormat();
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;

            return new TextureDesc(descriptor)
            {
                name = DepthTextureName,
                filterMode = FilterMode.Point,
                clearBuffer = false,
                clearColor = Color.clear
            };
        }

        private static GraphicsFormat GetDepthFormat()
        {
            if (SystemInfo.IsFormatSupported(GraphicsFormat.D24_UNorm_S8_UInt, GraphicsFormatUsage.Render))
                return GraphicsFormat.D24_UNorm_S8_UInt;
            if (SystemInfo.IsFormatSupported(GraphicsFormat.D32_SFloat, GraphicsFormatUsage.Render))
                return GraphicsFormat.D32_SFloat;
            return GraphicsFormat.D16_UNorm;
        }
    }
}
