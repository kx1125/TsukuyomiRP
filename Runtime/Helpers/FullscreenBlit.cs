using UnityEngine;
using UnityEngine.Rendering;
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
            if (!source.IsValid() || material == null || context.Resources.IsActiveTargetBackBuffer)
                return source;

            // PostPass already owns the open raster builder and its attachments.
            // Reuse that destination instead of nesting another RenderGraph pass.
            TextureHandle destination = context.PassData.destination;
            if (!destination.IsValid())
            {
                destination = CreateDestination(context, source, destinationName);
                context.Builder.UseTexture(source, AccessFlags.Read);
                context.Builder.SetRenderAttachment(destination, 0, AccessFlags.WriteAll);
            }
            else if (context.PassData.source != source)
            {
                throw new System.InvalidOperationException("A PostPass blit must use its declared source. Record additional stages as separate graph passes.");
            }

            context.PassData.source = source;
            context.PassData.destination = destination;
            context.PassData.material = material;
            context.PassData.passIndex = passIndex;
            context.SetRenderFunc(static (data, graphContext) =>
                Blitter.BlitTexture(graphContext.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, data.passIndex));
            PassRecorder.SwapActiveColor(context.Resources, destination);

            return destination;
        }
    }
}
