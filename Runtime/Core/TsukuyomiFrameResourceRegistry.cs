using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Tsukuyomi.Rendering
{
    public sealed class TsukuyomiFrameResourceRegistry : ContextItem
    {
        private readonly Dictionary<string, TextureEntry> _textures = new();
        private readonly Dictionary<string, BufferEntry> _buffers = new();

        public TextureHandle GetOrCreateTexture(RenderGraph renderGraph, string name, TextureDesc desc)
        {
            if (string.IsNullOrEmpty(name))
            {
                Debug.LogError("Tsukuyomi texture resources require a non-empty name.");
                return TextureHandle.nullHandle;
            }

            if (_textures.TryGetValue(name, out TextureEntry entry))
            {
                if (!IsCompatible(entry.Desc, desc))
                    throw new InvalidOperationException($"Tsukuyomi texture resource '{name}' was requested with an incompatible descriptor.");

                return entry.Handle;
            }

            desc.name = name;
            TextureHandle handle = renderGraph.CreateTexture(desc);
            _textures.Add(name, new TextureEntry(handle, desc));
            return handle;
        }

        public BufferHandle GetOrCreateBuffer(RenderGraph renderGraph, string name, BufferDesc desc)
        {
            if (string.IsNullOrEmpty(name))
            {
                Debug.LogError("Tsukuyomi buffer resources require a non-empty name.");
                return BufferHandle.nullHandle;
            }

            if (_buffers.TryGetValue(name, out BufferEntry entry))
            {
                if (!IsCompatible(entry.Desc, desc))
                    throw new InvalidOperationException($"Tsukuyomi buffer resource '{name}' was requested with an incompatible descriptor.");

                return entry.Handle;
            }

            desc.name = name;
            BufferHandle handle = renderGraph.CreateBuffer(desc);
            _buffers.Add(name, new BufferEntry(handle, desc));
            return handle;
        }

        // Reads must not create resources when a producer was skipped or ordered incorrectly.
        public TextureHandle GetTexture(string name, TextureDesc? expectedDesc = null)
        {
            if (string.IsNullOrEmpty(name) || !_textures.TryGetValue(name, out TextureEntry entry))
                return TextureHandle.nullHandle;
            if (expectedDesc.HasValue && !IsCompatible(entry.Desc, expectedDesc.Value))
                throw new InvalidOperationException($"Tsukuyomi texture resource '{name}' was requested with an incompatible descriptor.");
            return entry.Handle;
        }

        public BufferHandle GetBuffer(string name, BufferDesc? expectedDesc = null)
        {
            if (string.IsNullOrEmpty(name) || !_buffers.TryGetValue(name, out BufferEntry entry))
                return BufferHandle.nullHandle;
            if (expectedDesc.HasValue && !IsCompatible(entry.Desc, expectedDesc.Value))
                throw new InvalidOperationException($"Tsukuyomi buffer resource '{name}' was requested with an incompatible descriptor.");
            return entry.Handle;
        }

        public override void Reset()
        {
            _textures.Clear();
            _buffers.Clear();
        }

        private static bool IsCompatible(TextureDesc a, TextureDesc b)
        {
            return a.sizeMode == b.sizeMode
                && a.scale.Equals(b.scale)
                && a.func == b.func
                && a.width == b.width
                && a.height == b.height
                && a.slices == b.slices
                && a.format == b.format
                && a.dimension == b.dimension
                && a.enableRandomWrite == b.enableRandomWrite
                && a.msaaSamples == b.msaaSamples
                && a.useMipMap == b.useMipMap
                && a.autoGenerateMips == b.autoGenerateMips
                && a.filterMode == b.filterMode
                && a.wrapMode == b.wrapMode
                && a.isShadowMap == b.isShadowMap
                && a.anisoLevel == b.anisoLevel
                && a.mipMapBias.Equals(b.mipMapBias)
                && a.bindTextureMS == b.bindTextureMS
                && a.useDynamicScale == b.useDynamicScale
                && a.useDynamicScaleExplicit == b.useDynamicScaleExplicit
                && a.memoryless == b.memoryless
                && a.vrUsage == b.vrUsage
                && a.enableShadingRate == b.enableShadingRate
                && a.fastMemoryDesc.inFastMemory == b.fastMemoryDesc.inFastMemory
                && a.fastMemoryDesc.flags == b.fastMemoryDesc.flags
                && a.fastMemoryDesc.residencyFraction.Equals(b.fastMemoryDesc.residencyFraction)
                && a.fallBackToBlackTexture == b.fallBackToBlackTexture
                && a.disableFallBackToImportedTexture == b.disableFallBackToImportedTexture
                && a.clearBuffer == b.clearBuffer
                && a.clearColor.Equals(b.clearColor)
                && a.discardBuffer == b.discardBuffer;
        }

        private static bool IsCompatible(BufferDesc a, BufferDesc b)
        {
            return a.count == b.count
                && a.stride == b.stride
                && a.target == b.target
                && a.usageFlags == b.usageFlags;
        }

        private readonly struct TextureEntry
        {
            public readonly TextureHandle Handle;
            public readonly TextureDesc Desc;

            public TextureEntry(TextureHandle handle, TextureDesc desc)
            {
                Handle = handle;
                Desc = desc;
            }
        }

        private readonly struct BufferEntry
        {
            public readonly BufferHandle Handle;
            public readonly BufferDesc Desc;

            public BufferEntry(BufferHandle handle, BufferDesc desc)
            {
                Handle = handle;
                Desc = desc;
            }
        }
    }
}
