using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Tsukuyomi.Rendering
{
    public static class RendererListHelper
    {
        public static RendererListHandle Create(RenderGraph renderGraph, in RendererListParams parameters)
        {
            return renderGraph.CreateRendererList(parameters);
        }

        public static void Bind(IRasterRenderGraphBuilder builder, RendererListHandle handle)
        {
            PassRecorder.BindRendererList(builder, handle);
        }

        public static void Bind(IUnsafeRenderGraphBuilder builder, RendererListHandle handle)
        {
            PassRecorder.BindRendererList(builder, handle);
        }
    }
}
