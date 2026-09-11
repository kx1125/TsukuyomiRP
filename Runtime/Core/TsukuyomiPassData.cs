using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine;

namespace Tsukuyomi.Rendering
{
    /// <summary>
    /// Unified data class for all Tsukuyomi RenderGraph passes.
    /// Pooled by RenderGraph; reset before recording so skipped passes cannot reuse stale data.
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

        public bool HasRenderFunction { get; internal set; }

        internal void Reset()
        {
            source = destination = default;
            buffer = default;
            rendererList = default;
            material = null;
            passIndex = 0;
            parameters = default;
            HasRenderFunction = false;
        }
    }
}
