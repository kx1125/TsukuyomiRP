using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Tsukuyomi.Rendering
{
    public static class HistoryTextureHelper
    {
        public readonly struct ImportedPair
        {
            public readonly TextureHandle Previous;
            public readonly TextureHandle Current;
            public ImportedPair(TextureHandle previous, TextureHandle current)
            {
                Previous = previous;
                Current = current;
            }
        }

        /// <summary>Declares Read(previous)/Write(current); does not rotate, allocate or take ownership.</summary>
        public static ImportedPair ImportPair(in PassResourceBuilder resources, RTHandle previous, RTHandle current)
            => new(previous != null ? resources.ImportTexture(previous, AccessFlags.Read) : TextureHandle.nullHandle,
                current != null ? resources.ImportTexture(current, AccessFlags.Write) : TextureHandle.nullHandle);

        public static TextureHandle ImportHistoryTexture(
            RenderGraph renderGraph,
            ResourceHub resourceHub,
            string key,
            in RenderTextureDescriptor descriptor)
        {
            if (resourceHub == null)
                return TextureHandle.nullHandle;

            var history = resourceHub.GetOrCreateHistoryTexture(key, descriptor, out bool reallocated);
            return renderGraph.ImportTexture(history, new ImportResourceParams
            {
                clearOnFirstUse = reallocated,
                clearColor = Color.clear
            });
        }
    }
}
