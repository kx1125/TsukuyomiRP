using UnityEngine;
using UnityEngine.Rendering;
using System;
using System.Collections.Generic;

namespace Tsukuyomi.Rendering
{
    public sealed class ResourceHub : IDisposable
    {
        private readonly Dictionary<string, HistoryEntry> _historyTextures = new();
        private readonly Dictionary<string, GraphicsBuffer> _persistentBuffers = new();
        private readonly Dictionary<Camera, ResourceHub> _cameras = new();
        private readonly List<Camera> _destroyedCameras = new();

        /// <summary>Bridge contexts receive this camera-specific hub.</summary>
        public ResourceHub ForCamera(Camera camera)
        {
            if (!camera)
                throw new ArgumentNullException(nameof(camera));
            if (!_cameras.TryGetValue(camera, out ResourceHub resources))
            {
                resources = new ResourceHub();
                _cameras.Add(camera, resources);
            }
            return resources;
        }

        public void ReleaseCamera(Camera camera)
        {
            if (ReferenceEquals(camera, null) || !_cameras.TryGetValue(camera, out ResourceHub resources))
                return;
            resources.Dispose();
            _cameras.Remove(camera);
        }

        public void ReleaseDestroyedCameras()
        {
            _destroyedCameras.Clear();
            foreach (Camera camera in _cameras.Keys)
                if (!camera)
                    _destroyedCameras.Add(camera);
            foreach (Camera camera in _destroyedCameras)
                ReleaseCamera(camera);
            _destroyedCameras.Clear();
        }

        public RTHandle GetOrCreateHistoryTexture(string key, in RenderTextureDescriptor desc)
        {
            return GetOrCreateHistoryTexture(key, desc, out _);
        }

        /// <param name="reallocated">True when the caller must initialize/reset its history.</param>
        public RTHandle GetOrCreateHistoryTexture(string key, in RenderTextureDescriptor desc, out bool reallocated)
        {
            ValidateKey(key);
            if (_historyTextures.TryGetValue(key, out HistoryEntry entry)
                && entry.Descriptor.Equals(desc)
                && entry.Handle.rt != null && entry.Handle.rt.IsCreated())
            {
                reallocated = false;
                return entry.Handle;
            }

            // Preserve the full descriptor, including array slices, UAV, mip and depth settings.
            var texture = new RenderTexture(desc) { name = key, hideFlags = HideFlags.HideAndDontSave };
            if (!texture.Create())
            {
                CoreUtils.Destroy(texture);
                throw new InvalidOperationException($"Could not create Tsukuyomi history texture '{key}'.");
            }
            RTHandle handle = RTHandles.Alloc(texture, transferOwnership: true);
            entry?.Handle.Release();
            _historyTextures[key] = new HistoryEntry(handle, desc);
            reallocated = true;
            return handle;
        }

        public GraphicsBuffer GetOrCreateBuffer(string key, int count, int stride)
        {
            ValidateKey(key);
            if (_persistentBuffers.TryGetValue(key, out GraphicsBuffer buffer)
                && buffer.IsValid() && buffer.count == count && buffer.stride == stride)
                return buffer;

            var replacement = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, stride);
            buffer?.Dispose();
            _persistentBuffers[key] = replacement;
            return replacement;
        }

        /// <summary>Call on a camera cut or temporal discontinuity before recording the graph.</summary>
        public void ResetHistory()
        {
            foreach (HistoryEntry entry in _historyTextures.Values)
                entry.Handle.Release();
            _historyTextures.Clear();
        }

        public void Dispose()
        {
            ResetHistory();

            foreach (var buffer in _persistentBuffers.Values)
                buffer.Dispose();

            _persistentBuffers.Clear();
            foreach (ResourceHub cameraResources in _cameras.Values)
                cameraResources.Dispose();
            _cameras.Clear();
            _destroyedCameras.Clear();
        }

        private static void ValidateKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Persistent resources require a non-empty key.", nameof(key));
        }

        private sealed class HistoryEntry
        {
            public readonly RTHandle Handle;
            public readonly RenderTextureDescriptor Descriptor;

            public HistoryEntry(RTHandle handle, RenderTextureDescriptor descriptor)
            {
                Handle = handle;
                Descriptor = descriptor;
            }
        }
    }
}
