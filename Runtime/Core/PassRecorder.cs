using UnityEngine.Rendering.RenderGraphModule;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule.Util;

namespace Tsukuyomi.Rendering
{
    /// <summary>
    /// Helper class to handle texture resolution and binding for Tsukuyomi passes.
    /// </summary>
    public static class PassRecorder
    {
        /// <summary>
        /// Resolves a TextureSlot into a TextureHandle from FrameResources or by creating it in RenderGraph.
        /// </summary>
        public static TextureHandle ResolveTexture(
            RenderGraph rg,
            in TextureSlot slot,
            FrameResources frameResources)
        {
            if (slot.IsBuiltin)
            {
                return frameResources.GetBuiltin(slot.Builtin);
            }
            else if (slot.CustomDesc.HasValue)
            {
                return frameResources.GetOrCreate(rg, slot.Name, slot.CustomDesc.Value);
            }

            return TextureHandle.nullHandle;
        }

        public static BufferHandle ResolveBuffer(
            RenderGraph rg,
            in BufferSlot slot,
            FrameResources frameResources)
        {
            if (slot.CustomDesc.HasValue)
            {
                return frameResources.GetOrCreate(rg, slot.Name, slot.CustomDesc.Value);
            }

            return BufferHandle.nullHandle;
        }

        public static TextureHandle CreateTextureLike(
            RenderGraph rg,
            TextureHandle source,
            string name,
            bool clearBuffer = false)
        {
            if (!source.IsValid())
                return TextureHandle.nullHandle;

            var desc = rg.GetTextureDesc(source);
            desc.name = name;
            desc.clearBuffer = clearBuffer;
            return rg.CreateTexture(desc);
        }

        /// <summary>
        /// Binds a texture to the RenderGraph builder based on access requirements.
        /// </summary>
        public static void BindTexture(
            IRasterRenderGraphBuilder builder,
            TextureHandle handle,
            in TextureSlot slot,
            ref int attachmentIndex)
        {
            if (!handle.IsValid()) return;

            AccessFlags flags = ToAccessFlags(slot.Access);

            if (slot.IsAttachment)
            {
                builder.SetRenderAttachment(handle, attachmentIndex, flags);
                attachmentIndex++;
            }
            else
            {
                builder.UseTexture(handle, flags);
            }
        }

        public static void BindTexture(
            IUnsafeRenderGraphBuilder builder,
            TextureHandle handle,
            in TextureSlot slot,
            ref int attachmentIndex)
        {
            if (!handle.IsValid()) return;

            AccessFlags flags = ToAccessFlags(slot.Access);

            if (slot.IsAttachment)
            {
                builder.SetRenderAttachment(handle, attachmentIndex, flags);
                attachmentIndex++;
            }
            else
            {
                builder.UseTexture(handle, flags);
            }
        }

        public static void BindTexture(
            IComputeRenderGraphBuilder builder,
            TextureHandle handle,
            in TextureSlot slot)
        {
            if (!handle.IsValid()) return;
            builder.UseTexture(handle, ToAccessFlags(slot.Access));
        }

        public static void BindBuffer(
            IBaseRenderGraphBuilder builder,
            BufferHandle handle,
            in BufferSlot slot)
        {
            if (!handle.IsValid()) return;
            builder.UseBuffer(handle, ToAccessFlags(slot.Access));
        }

        public static void BindRendererList(
            IBaseRenderGraphBuilder builder,
            RendererListHandle handle)
        {
            if (!handle.IsValid()) return;
            builder.UseRendererList(handle);
        }

        public static void SwapActiveColor(FrameResources resources, TextureHandle handle)
        {
            if (handle.IsValid())
                resources.SetActiveColor(handle);
        }

        public static IBaseRenderGraphBuilder AddBlitAndSwapColorPass(
            RenderGraph renderGraph,
            FrameResources resources,
            TextureHandle source,
            TextureHandle destination,
            Material material,
            int passIndex,
            string passName)
        {
            var parameters = new RenderGraphUtils.BlitMaterialParameters(source, destination, material, passIndex);
            var builder = renderGraph.AddBlitPass(parameters, passName: passName, returnBuilder: true);
            SwapActiveColor(resources, destination);
            return builder;
        }

        public static AccessFlags ToAccessFlags(ResourceAccess access)
        {
            return access switch
            {
                ResourceAccess.Read => AccessFlags.Read,
                ResourceAccess.Write => AccessFlags.Write,
                ResourceAccess.ReadWrite => AccessFlags.ReadWrite,
                _ => AccessFlags.None
            };
        }
    }
}
