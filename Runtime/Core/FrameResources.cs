using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;


namespace Tsukuyomi.Rendering
{
    public class FrameResources
    {
        private readonly UniversalResourceData _resourceData;
        private readonly TsukuyomiFrameResourceRegistry _registry;

        public bool IsActiveTargetBackBuffer => _resourceData != null && _resourceData.isActiveTargetBackBuffer;

        public TextureHandle ActiveColor;
        public TextureHandle ActiveDepth;
        public TextureHandle CameraColorAttachment;
        public TextureHandle CameraDepthAttachment;
        public TextureHandle CameraColorTexture;
        public TextureHandle CameraDepthTexture;
        public TextureHandle CameraNormals;
        public TextureHandle MotionVectorColor;
        public TextureHandle MotionVectorDepth;
        public TextureHandle OpaqueTexture;
        public TextureHandle MainShadowMap;

        public FrameResources()
        {
            _registry = new TsukuyomiFrameResourceRegistry();
        }

        public FrameResources(UniversalResourceData resourceData, TsukuyomiFrameResourceRegistry registry = null)
        {
            _resourceData = resourceData;
            _registry = registry ?? new TsukuyomiFrameResourceRegistry();

            ActiveColor = resourceData.activeColorTexture;
            ActiveDepth = resourceData.activeDepthTexture;
            CameraColorAttachment = resourceData.activeColorTexture;
            CameraDepthAttachment = resourceData.activeDepthTexture;
            CameraColorTexture = resourceData.cameraColor;
            CameraDepthTexture = resourceData.cameraDepthTexture;
            CameraNormals = resourceData.cameraNormalsTexture;
            MotionVectorColor = resourceData.motionVectorColor;
            MotionVectorDepth = resourceData.motionVectorDepth;
            OpaqueTexture = resourceData.cameraOpaqueTexture;
            MainShadowMap = resourceData.mainShadowsTexture;
        }

        public TextureHandle GetBuiltin(BuiltinTexture builtin)
        {
            return builtin switch
            {
                BuiltinTexture.ActiveColor => ActiveColor,
                BuiltinTexture.ActiveDepth => ActiveDepth,
                BuiltinTexture.CameraColorAttachment => CameraColorAttachment,
                BuiltinTexture.CameraDepthAttachment => CameraDepthAttachment,
                BuiltinTexture.CameraColorTexture => CameraColorTexture,
                BuiltinTexture.CameraDepthTexture => CameraDepthTexture,
                BuiltinTexture.CameraNormals => CameraNormals,
                BuiltinTexture.MotionVectorColor => MotionVectorColor,
                BuiltinTexture.MotionVectorDepth => MotionVectorDepth,
                BuiltinTexture.OpaqueTexture => OpaqueTexture,
                BuiltinTexture.MainShadowMap => MainShadowMap,
                _ => TextureHandle.nullHandle
            };
        }

        public TextureHandle GetOrCreate(RenderGraph rg, string name, TextureDesc desc)
        {
            return _registry.GetOrCreateTexture(rg, name, desc);
        }

        public BufferHandle GetOrCreate(RenderGraph rg, string name, BufferDesc desc)
        {
            return _registry.GetOrCreateBuffer(rg, name, desc);
        }

        public TextureHandle GetTexture(string name, TextureDesc? expectedDesc = null)
        {
            return _registry.GetTexture(name, expectedDesc);
        }

        public BufferHandle GetBuffer(string name, BufferDesc? expectedDesc = null)
        {
            return _registry.GetBuffer(name, expectedDesc);
        }

        public void SetActiveColor(TextureHandle handle)
        {
            if (!handle.IsValid())
                throw new System.ArgumentException("Active color must be a valid texture.", nameof(handle));
            if (IsActiveTargetBackBuffer && handle != ActiveColor)
                throw new System.InvalidOperationException("Cannot replace the backbuffer with cameraColor. Request an intermediate texture and record before the final blit.");
            ActiveColor = handle;
            CameraColorAttachment = handle;
            CameraColorTexture = handle;

            if (_resourceData != null)
            {
                _resourceData.cameraColor = handle;
            }
        }

        public void SetCameraColorAttachment(TextureHandle handle)
        {
            SetActiveColor(handle);
        }
    }
}

