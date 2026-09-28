using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Tsukuyomi.Rendering
{
    public static class TextureDescriptors
    {
        /// <summary>Preserves source size mode, array/XR layout and dynamic scaling; removes depth, MSAA, mips and UAV.</summary>
        public static TextureDesc ColorLike(in TextureDesc source, string name, bool randomWrite = false)
        {
            var desc = source;
            desc.name = name;
            desc.depthBufferBits = DepthBits.None;
            desc.msaaSamples = MSAASamples.None;
            desc.bindTextureMS = false;
            desc.enableRandomWrite = randomWrite;
            desc.useMipMap = false;
            desc.autoGenerateMips = false;
            desc.clearBuffer = false;
            desc.discardBuffer = false;
            desc.memoryless = RenderTextureMemoryless.None;
            return desc;
        }

        /// <summary>Explicit dimensions (caller chooses floor/ceil), retaining camera array/XR and dynamic-scale policy.</summary>
        public static RenderTextureDescriptor HistoryColor(in RenderTextureDescriptor camera, int width, int height,
            GraphicsFormat format, bool randomWrite = true)
        {
            var desc = camera;
            desc.width = Mathf.Max(1, width);
            desc.height = Mathf.Max(1, height);
            desc.msaaSamples = 1;
            desc.depthStencilFormat = GraphicsFormat.None;
            desc.graphicsFormat = format;
            desc.enableRandomWrite = randomWrite;
            desc.useMipMap = false;
            desc.autoGenerateMips = false;
            desc.mipCount = 1;
            return desc;
        }

        /// <summary>
        /// An explicit-size 2D color texture with no depth or MSAA. The caller chooses size,
        /// format, filtering, UAV access and clearing; camera/XR layout is not inferred.
        /// For arrays, mips or camera descriptor inheritance, construct a TextureDesc directly.
        /// </summary>
        public static TextureDesc Color2D(int width, int height, GraphicsFormat format,
            string name = null, FilterMode filterMode = FilterMode.Point,
            bool randomWrite = false, bool clearBuffer = false, Color clearColor = default)
        {
            return new TextureDesc(width, height)
            {
                name = name,
                colorFormat = format,
                depthBufferBits = DepthBits.None,
                msaaSamples = MSAASamples.None,
                filterMode = filterMode,
                enableRandomWrite = randomWrite,
                clearBuffer = clearBuffer,
                clearColor = clearColor
            };
        }
    }
}
