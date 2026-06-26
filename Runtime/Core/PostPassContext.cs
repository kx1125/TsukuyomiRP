using System;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering
{
    public readonly ref struct PostPassContext
    {
        public readonly RenderGraph RenderGraph;
        public readonly IRasterRenderGraphBuilder Builder;
        public readonly ContextContainer FrameData;
        public readonly UniversalCameraData CameraData;
        public readonly FrameResources Resources;
        public readonly ResourceHub ResourceHub;
        public readonly TsukuyomiPassData PassData;

        public PostPassContext(
            RenderGraph renderGraph,
            IRasterRenderGraphBuilder builder,
            ContextContainer frameData,
            UniversalCameraData cameraData,
            FrameResources resources,
            TsukuyomiPassData passData,
            ResourceHub resourceHub = null)
        {
            RenderGraph = renderGraph;
            Builder = builder;
            FrameData = frameData;
            CameraData = cameraData;
            Resources = resources;
            ResourceHub = resourceHub;
            PassData = passData;
        }

        public void SetRenderFunc(Action<TsukuyomiPassData, RasterGraphContext> renderFunc)
        {
            Builder.SetRenderFunc<TsukuyomiPassData>(new BaseRenderFunc<TsukuyomiPassData, RasterGraphContext>(renderFunc));
        }

        public TextureHandle GetTexture(in TextureSlot slot)
        {
            return PassRecorder.ResolveTexture(RenderGraph, slot, Resources);
        }

        public TextureHandle CreateTextureLike(TextureHandle source, string name, bool clearBuffer = false)
        {
            return PassRecorder.CreateTextureLike(RenderGraph, source, name, clearBuffer);
        }

        public void BindTexture(TextureHandle handle, in TextureSlot slot, ref int attachmentIndex)
        {
            PassRecorder.BindTexture(Builder, handle, slot, ref attachmentIndex);
        }
    }
}
