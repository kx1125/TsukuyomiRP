using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Tsukuyomi.Rendering
{
    /// <summary>
    /// Creates/imports textures and declares shader access on the current pass in one step.
    /// Sampling/UAV access and raster attachments are separate, explicit operations.
    /// This stack-only view is valid only while the owning pass builder is open.
    /// </summary>
    public readonly ref struct PassResourceBuilder
    {
        private readonly RenderGraph _renderGraph;
        private readonly IBaseRenderGraphBuilder _builder;
        private readonly FrameResources _frameResources;

        public PassResourceBuilder(RenderGraph renderGraph, IBaseRenderGraphBuilder builder, FrameResources frameResources = null)
        {
            _renderGraph = renderGraph;
            _builder = builder;
            _frameResources = frameResources;
        }

        public TextureHandle CreateTexture(in TextureDesc desc, AccessFlags access)
        {
            TextureHandle handle = _renderGraph.CreateTexture(desc);
            _builder.UseTexture(handle, access);
            return handle;
        }

        public TextureHandle ImportTexture(RTHandle texture, AccessFlags access)
        {
            TextureHandle handle = _renderGraph.ImportTexture(texture);
            return UseTexture(handle, access);
        }

        public TextureHandle ImportTexture(RTHandle texture, AccessFlags access, ImportResourceParams importParams)
            => UseTexture(_renderGraph.ImportTexture(texture, importParams), access);

        public TextureHandle ReadTexture(TextureHandle handle) => UseTexture(handle, AccessFlags.Read);

        // Reads never create a missing named resource, even if the supplied slot is writable.
        public TextureHandle ReadTexture(in TextureSlot slot)
            => ReadTexture(slot.IsBuiltin ? _frameResources.GetBuiltin(slot.Builtin)
                : _frameResources.GetTexture(slot.Name, slot.CustomDesc));

        public BufferHandle ReadBuffer(BufferHandle handle) => UseBuffer(handle, AccessFlags.Read);

        public BufferHandle ReadBuffer(in BufferSlot slot)
            => ReadBuffer(_frameResources.GetBuffer(slot.Name, slot.CustomDesc));

        public TextureHandle UseTexture(in TextureSlot slot, AccessFlags access)
            => UseTexture(PassRecorder.ResolveTexture(_renderGraph, slot, _frameResources), access);

        public BufferHandle UseBuffer(in BufferSlot slot, AccessFlags access)
            => UseBuffer(PassRecorder.ResolveBuffer(_renderGraph, slot, _frameResources), access);

        public TextureHandle ColorAttachment(TextureHandle handle, int index = 0, AccessFlags access = AccessFlags.Write)
            => PassRecorder.ColorAttachment((IRasterRenderGraphBuilder)_builder, handle, index, access);

        public TextureHandle ColorAttachment(in TextureDesc desc, int index = 0, AccessFlags access = AccessFlags.Write)
            => ColorAttachment(_renderGraph.CreateTexture(desc), index, access);

        public TextureHandle DepthAttachment(TextureHandle handle, AccessFlags access = AccessFlags.ReadWrite)
            => PassRecorder.DepthAttachment((IRasterRenderGraphBuilder)_builder, handle, access);

        /// <summary>Declares access to an existing handle. Invalid optional handles are ignored.</summary>
        public TextureHandle UseTexture(TextureHandle handle, AccessFlags access)
        {
            if (handle.IsValid())
                _builder.UseTexture(handle, access);
            return handle;
        }

        public BufferHandle UseBuffer(BufferHandle handle, AccessFlags access)
        {
            if (handle.IsValid())
                _builder.UseBuffer(handle, access);
            return handle;
        }
    }
}
