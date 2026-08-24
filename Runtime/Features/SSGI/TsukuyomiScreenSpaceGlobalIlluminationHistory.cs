using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering
{
    internal sealed class TsukuyomiScreenSpaceGlobalIlluminationHistory : CameraHistoryItem
    {
        private int _normalId;
        private int _gi0Id;
        private int _gi1Id;
        private Hash128 _normalDescriptorKey;
        private Hash128 _gi0DescriptorKey;
        private Hash128 _gi1DescriptorKey;
        private bool _allocated;
        private int _lastFrame = -1;
        private int _settingsSignature;
        private Matrix4x4 _currentViewProjection = Matrix4x4.identity;
        private Vector2 _currentJitterUv;

        public Matrix4x4 PreviousViewProjection { get; private set; } = Matrix4x4.identity;
        public Vector2 PreviousJitterUv { get; private set; }

        public override void OnCreate(BufferedRTHandleSystem owner, uint typeId)
        {
            base.OnCreate(owner, typeId);
            _normalId = MakeId(0);
            _gi0Id = MakeId(1);
            _gi1Id = MakeId(2);
        }

        public bool Update(
            ref RenderTextureDescriptor cameraDescriptor,
            int traceWidth,
            int traceHeight,
            int secondWidth,
            int secondHeight,
            int settingsSignature,
            Matrix4x4 currentViewProjection,
            Vector2 currentJitterUv,
            int frameIndex)
        {
            bool consecutive = _allocated
                && _lastFrame == frameIndex - 1
                && _settingsSignature == settingsSignature;

            RenderTextureDescriptor normalDescriptor = CreateDescriptor(
                ref cameraDescriptor,
                cameraDescriptor.width,
                cameraDescriptor.height,
                GraphicsFormat.R16G16B16A16_SFloat);
            RenderTextureDescriptor gi0Descriptor = CreateDescriptor(
                ref cameraDescriptor,
                traceWidth,
                traceHeight,
                GraphicsFormat.R16G16B16A16_SFloat);
            RenderTextureDescriptor gi1Descriptor = CreateDescriptor(
                ref cameraDescriptor,
                secondWidth,
                secondHeight,
                GraphicsFormat.R16G16B16A16_SFloat);

            bool reallocated = EnsureAllocated(_normalId, ref _normalDescriptorKey, ref normalDescriptor, FilterMode.Point, "Tsukuyomi SSGI Normal History")
                | EnsureAllocated(_gi0Id, ref _gi0DescriptorKey, ref gi0Descriptor, FilterMode.Bilinear, "Tsukuyomi SSGI History 0")
                | EnsureAllocated(_gi1Id, ref _gi1DescriptorKey, ref gi1Descriptor, FilterMode.Bilinear, "Tsukuyomi SSGI History 1");

            PreviousViewProjection = _allocated ? _currentViewProjection : currentViewProjection;
            PreviousJitterUv = _allocated ? _currentJitterUv : currentJitterUv;
            _currentViewProjection = currentViewProjection;
            _currentJitterUv = currentJitterUv;
            _lastFrame = frameIndex;
            _settingsSignature = settingsSignature;
            _allocated = true;
            return consecutive && !reallocated;
        }

        public RTHandle GetPreviousNormal() => GetPreviousFrameRT(_normalId);
        public RTHandle GetCurrentNormal() => GetCurrentFrameRT(_normalId);
        public RTHandle GetPreviousGi0() => GetPreviousFrameRT(_gi0Id);
        public RTHandle GetCurrentGi0() => GetCurrentFrameRT(_gi0Id);
        public RTHandle GetPreviousGi1() => GetPreviousFrameRT(_gi1Id);
        public RTHandle GetCurrentGi1() => GetCurrentFrameRT(_gi1Id);

        public override void Reset()
        {
            ReleaseHistoryFrameRT(_normalId);
            ReleaseHistoryFrameRT(_gi0Id);
            ReleaseHistoryFrameRT(_gi1Id);
            _normalDescriptorKey = default;
            _gi0DescriptorKey = default;
            _gi1DescriptorKey = default;
            _allocated = false;
            _lastFrame = -1;
            _settingsSignature = 0;
            _currentViewProjection = Matrix4x4.identity;
            _currentJitterUv = Vector2.zero;
            PreviousViewProjection = Matrix4x4.identity;
            PreviousJitterUv = Vector2.zero;
        }

        private bool EnsureAllocated(
            int id,
            ref Hash128 descriptorKey,
            ref RenderTextureDescriptor descriptor,
            FilterMode filterMode,
            string name)
        {
            Hash128 newKey = Hash128.Compute(ref descriptor);
            RTHandle current = GetCurrentFrameRT(id);
            if (current != null && descriptorKey == newKey)
                return false;

            if (current != null)
                ReleaseHistoryFrameRT(id);

            AllocHistoryFrameRT(id, 2, ref descriptor, filterMode, name);
            descriptorKey = newKey;
            return true;
        }

        private static RenderTextureDescriptor CreateDescriptor(
            ref RenderTextureDescriptor cameraDescriptor,
            int width,
            int height,
            GraphicsFormat format)
        {
            RenderTextureDescriptor descriptor = cameraDescriptor;
            descriptor.width = Mathf.Max(1, width);
            descriptor.height = Mathf.Max(1, height);
            descriptor.msaaSamples = 1;
            descriptor.depthStencilFormat = GraphicsFormat.None;
            descriptor.graphicsFormat = format;
            descriptor.enableRandomWrite = true;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            descriptor.mipCount = 1;
            return descriptor;
        }
    }
}
