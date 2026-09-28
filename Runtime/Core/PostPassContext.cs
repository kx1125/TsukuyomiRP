using System;
using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering
{
    public readonly ref struct PostPassContext
    {
        public readonly RenderGraph RenderGraph;
        public readonly IRasterRenderGraphBuilder Builder;
        public readonly ContextContainer FrameData;
        public readonly UniversalCameraData CameraData;
        public readonly FrameResources Resources;
        public readonly ResourceHub ResourceHub;
        public readonly TsukuyomiPassData PassData;

        public PassResourceBuilder GraphResources => new(RenderGraph, Builder, Resources);

        public PostPassContext(
            RenderGraph renderGraph,
            IRasterRenderGraphBuilder builder,
            ContextContainer frameData,
            UniversalCameraData cameraData,
            FrameResources resources,
            TsukuyomiPassData passData,
            ResourceHub resourceHub = null)
        {
            RenderGraph = renderGraph;
            Builder = builder;
            FrameData = frameData;
            CameraData = cameraData;
            Resources = resources;
            ResourceHub = resourceHub;
            PassData = passData;
            PassData.Reset();
            // Optional passes may return before setting up work. Keep the graph valid and cullable.
            Builder.SetRenderFunc<TsukuyomiPassData>(static (_, _) => { });
        }

        public void SetRenderFunc(BaseRenderFunc<TsukuyomiPassData, RasterGraphContext> renderFunc)
        {
            if (renderFunc == null)
                throw new ArgumentNullException(nameof(renderFunc));
            Builder.SetRenderFunc(renderFunc);
            PassData.HasRenderFunction = true;
        }

        /// <summary>Gets a snapshot owned by this pooled graph pass. Overwrite all fields used by the callback.</summary>
        public T GetOrCreateData<T>() where T : class, new() => PassData.GetOrCreateData<T>();

        /// <summary>Registers a typed, preferably static callback and an optional profiling scope.</summary>
        public void SetRenderFunc<T>(T data, BaseRenderFunc<T, RasterGraphContext> renderFunc,
            ProfilingSampler sampler = null) where T : class, new()
        {
            PassRenderFunction<T>.Bind(PassData, data, renderFunc, sampler);
            SetRenderFunc(PassRenderFunction<T>.Raster);
        }

        /// <summary>Blits between the source and destination already bound by PostPass.</summary>
        public void Blit(Material material, int passIndex = 0)
        {
            if (material == null || passIndex < 0 || passIndex >= material.passCount
                || !PassData.source.IsValid() || !PassData.destination.IsValid())
                return;
            PassData.material = material;
            PassData.passIndex = passIndex;
            SetRenderFunc(static (data, context) =>
                Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, data.passIndex));
        }

        public TextureHandle ReadTexture(TextureHandle handle) => GraphResources.ReadTexture(handle);
        public TextureHandle ReadTexture(in TextureSlot slot) => GraphResources.ReadTexture(slot);
        public BufferHandle ReadBuffer(BufferHandle handle) => GraphResources.ReadBuffer(handle);
        public BufferHandle ReadBuffer(in BufferSlot slot) => GraphResources.ReadBuffer(slot);

        public TextureHandle ColorAttachment(TextureHandle handle, int index = 0, AccessFlags access = AccessFlags.Write)
            => GraphResources.ColorAttachment(handle, index, access);
        public TextureHandle ColorAttachment(in TextureDesc desc, int index = 0, AccessFlags access = AccessFlags.Write)
            => GraphResources.ColorAttachment(desc, index, access);
        public TextureHandle DepthAttachment(TextureHandle handle, AccessFlags access = AccessFlags.ReadWrite)
            => GraphResources.DepthAttachment(handle, access);

        public TextureHandle GetTexture(in TextureSlot slot)
        {
            return PassRecorder.ResolveTexture(RenderGraph, slot, Resources);
        }

        public TextureHandle CreateTextureLike(TextureHandle source, string name, bool clearBuffer = false)
        {
            return PassRecorder.CreateTextureLike(RenderGraph, source, name, clearBuffer);
        }

        public void BindTexture(TextureHandle handle, in TextureSlot slot, ref int attachmentIndex)
        {
            PassRecorder.BindTexture(Builder, handle, slot, ref attachmentIndex);
        }
    }
}
