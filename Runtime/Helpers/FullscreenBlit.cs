using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;

namespace Tsukuyomi.Rendering
{
    public static class FullscreenBlit
    {
        public static TextureHandle CreateDestination(
            in PostPassContext context,
            TextureHandle source,
            string name,
            bool clearBuffer = false)
        {
            return context.CreateTextureLike(source, name, clearBuffer);
        }

        public static TextureHandle BlitAndSwap(
            in PostPassContext context,
            TextureHandle source,
            Material material,
            int passIndex,
            string destinationName,
            string passName)
        {
            var destination = CreateDestination(context, source, destinationName);
            if (!source.IsValid() || !destination.IsValid() || material == null)
                return source;

            PassRecorder.AddBlitAndSwapColorPass(
                context.RenderGraph,
                context.Resources,
                source,
                destination,
                material,
                passIndex,
                passName);

            return destination;
        }
    }
}
