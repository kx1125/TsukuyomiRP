using UnityEngine;
using UnityEngine.Rendering;
using System;
using System.Collections.Generic;

namespace Tsukuyomi.Rendering
{
    public sealed class ResourceHub : IDisposable
    {
        private readonly Dictionary<string, RTHandle> _historyTextures = new();
        private readonly Dictionary<string, GraphicsBuffer> _persistentBuffers = new();

        public RTHandle GetOrCreateHistoryTexture(string key, in RenderTextureDescriptor desc)
        {
            if (_historyTextures.TryGetValue(key, out var rt))
                return rt;

            rt = RTHandles.Alloc(desc.width, desc.height, colorFormat: desc.graphicsFormat, name: key);
            _historyTextures.Add(key, rt);
            return rt;
        }

        public GraphicsBuffer GetOrCreateBuffer(string key, int count, int stride)
        {
            if (_persistentBuffers.TryGetValue(key, out var buffer))
                return buffer;

            buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, stride);
            _persistentBuffers.Add(key, buffer);
            return buffer;
        }

        public void Dispose()
        {
            foreach (var rt in _historyTextures.Values)
                rt.Release();

            foreach (var buffer in _persistentBuffers.Values)
                buffer.Dispose();

            _historyTextures.Clear();
            _persistentBuffers.Clear();
        }
    }
}
