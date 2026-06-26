using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine;

namespace Tsukuyomi.Rendering
{
    /// <summary>
    /// Unified data class for all Tsukuyomi RenderGraph passes.
    /// Used to pass resource handles and parameters from Setup to Execute phase without GC allocations.
    /// </summary>
    public class TsukuyomiPassData
    {
        public TextureHandle source;
        public TextureHandle destination;
        public BufferHandle buffer;
        public RendererListHandle rendererList;
        
        public Material material;
        public int passIndex;
        public Vector4 parameters;
    }
}
