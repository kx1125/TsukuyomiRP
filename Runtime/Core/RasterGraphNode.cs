using System;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Tsukuyomi.Rendering
{
    /// <summary>One native pooled T snapshot. Must be disposed before opening the next node.</summary>
    public readonly ref struct RasterGraphNode<T> where T : class, new()
    {
        public readonly IRasterRenderGraphBuilder Builder;
        public readonly T Data;
        public readonly PassResourceBuilder Resources;

        internal RasterGraphNode(RenderGraph graph, FrameResources frameResources, string name, ProfilingSampler sampler)
        {
            Builder = sampler == null ? graph.AddRasterRenderPass(name, out Data) : graph.AddRasterRenderPass(name, out Data, sampler);
            Resources = new PassResourceBuilder(graph, Builder, frameResources);
        }

        public TextureHandle ReadTexture(TextureHandle handle) => Resources.ReadTexture(handle);
        public TextureHandle ReadTexture(in TextureSlot slot) => Resources.ReadTexture(slot);
        public BufferHandle ReadBuffer(BufferHandle handle) => Resources.ReadBuffer(handle);
        public BufferHandle ReadBuffer(in BufferSlot slot) => Resources.ReadBuffer(slot);
        public void SetRenderFunc(BaseRenderFunc<T, RasterGraphContext> renderFunc)
        {
            if (renderFunc == null) throw new ArgumentNullException(nameof(renderFunc));
            Builder.SetRenderFunc(renderFunc);
        }
        public TextureHandle ColorAttachment(TextureHandle handle, int index = 0, AccessFlags access = AccessFlags.Write)
            => Resources.ColorAttachment(handle, index, access);
        public TextureHandle ColorAttachment(in TextureDesc desc, int index = 0, AccessFlags access = AccessFlags.Write)
            => Resources.ColorAttachment(desc, index, access);
        public TextureHandle DepthAttachment(TextureHandle handle, AccessFlags access = AccessFlags.ReadWrite)
            => Resources.DepthAttachment(handle, access);

        public void Dispose() => Builder.Dispose();
    }
}
