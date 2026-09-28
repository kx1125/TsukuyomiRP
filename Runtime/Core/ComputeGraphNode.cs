using System;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Tsukuyomi.Rendering
{
    /// <summary>One native pooled T snapshot. Must be disposed before opening the next node.</summary>
    public readonly ref struct ComputeGraphNode<T> where T : class, new()
    {
        public readonly IComputeRenderGraphBuilder Builder;
        public readonly T Data;
        public readonly PassResourceBuilder Resources;

        internal ComputeGraphNode(RenderGraph graph, FrameResources frameResources, string name, ProfilingSampler sampler)
        {
            Builder = sampler == null ? graph.AddComputePass(name, out Data) : graph.AddComputePass(name, out Data, sampler);
            Resources = new PassResourceBuilder(graph, Builder, frameResources);
        }

        public TextureHandle ReadTexture(TextureHandle handle) => Resources.ReadTexture(handle);
        public TextureHandle ReadTexture(in TextureSlot slot) => Resources.ReadTexture(slot);
        public BufferHandle ReadBuffer(BufferHandle handle) => Resources.ReadBuffer(handle);
        public BufferHandle ReadBuffer(in BufferSlot slot) => Resources.ReadBuffer(slot);
        public void SetRenderFunc(BaseRenderFunc<T, ComputeGraphContext> renderFunc)
        {
            if (renderFunc == null) throw new ArgumentNullException(nameof(renderFunc));
            Builder.SetRenderFunc(renderFunc);
        }

        public void Dispose() => Builder.Dispose();
    }
}
