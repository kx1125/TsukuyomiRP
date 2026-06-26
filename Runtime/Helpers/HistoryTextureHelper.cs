using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Tsukuyomi.Rendering
{
    public static class HistoryTextureHelper
    {
        public static TextureHandle ImportHistoryTexture(
            RenderGraph renderGraph,
            ResourceHub resourceHub,
            string key,
            in RenderTextureDescriptor descriptor)
        {
            if (resourceHub == null)
                return TextureHandle.nullHandle;

            var history = resourceHub.GetOrCreateHistoryTexture(key, descriptor);
            return renderGraph.ImportTexture(history);
        }
    }
}
