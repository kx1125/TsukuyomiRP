using System;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering
{
    public readonly ref struct RasterPassContext
    {
        public readonly RenderGraph RenderGraph;
        public readonly IRasterRenderGraphBuilder Builder;
        public readonly ContextContainer FrameData;
        public readonly UniversalCameraData CameraData;
        public readonly UniversalLightData LightData;
        public readonly FrameResources Resources;
        public readonly ResourceHub ResourceHub;
        public readonly TsukuyomiPassData PassData;

        public RasterPassContext(
            RenderGraph renderGraph,
            IRasterRenderGraphBuilder builder,
            ContextContainer frameData,
            UniversalCameraData cameraData,
            UniversalLightData lightData,
            FrameResources resources,
            TsukuyomiPassData passData,
            ResourceHub resourceHub = null)
        {
            RenderGraph = renderGraph;
            Builder = builder;
            FrameData = frameData;
            CameraData = cameraData;
            LightData = lightData;
            Resources = resources;
            ResourceHub = resourceHub;
            PassData = passData;
            PassData.Reset();
            // Optional passes may return before setting up work. Keep the graph valid and cullable.
            Builder.SetRenderFunc<TsukuyomiPassData>(static (_, _) => { });
        }

        public void SetRenderFunc(BaseRenderFunc<TsukuyomiPassData, RasterGraphContext> renderFunc)
        {
            if (renderFunc == null)
                throw new ArgumentNullException(nameof(renderFunc));
            Builder.SetRenderFunc(renderFunc);
            PassData.HasRenderFunction = true;
        }

        public TextureHandle GetTexture(in TextureSlot slot)
        {
            return PassRecorder.ResolveTexture(RenderGraph, slot, Resources);
        }

        public BufferHandle GetBuffer(in BufferSlot slot)
        {
            return PassRecorder.ResolveBuffer(RenderGraph, slot, Resources);
        }

        public void BindTexture(TextureHandle handle, in TextureSlot slot, ref int attachmentIndex)
        {
            PassRecorder.BindTexture(Builder, handle, slot, ref attachmentIndex);
        }

        public void BindBuffer(BufferHandle handle, in BufferSlot slot)
        {
            PassRecorder.BindBuffer(Builder, handle, slot);
        }

        public void BindRendererList(RendererListHandle handle)
        {
            PassRecorder.BindRendererList(Builder, handle);
        }
    }
}
