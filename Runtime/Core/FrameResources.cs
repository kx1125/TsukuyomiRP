using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;


namespace Tsukuyomi.Rendering
{
    public class FrameResources
    {
        private readonly UniversalResourceData _resourceData;
        private readonly TsukuyomiFrameResourceRegistry _registry;

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
        }

        public FrameResources(UniversalResourceData resourceData, TsukuyomiFrameResourceRegistry registry = null)
        {
            _resourceData = resourceData;
            _registry = registry;

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
            if (_registry != null)
                return _registry.GetOrCreateTexture(rg, name, desc);

            desc.name = name;
            return rg.CreateTexture(desc);
        }

        public BufferHandle GetOrCreate(RenderGraph rg, string name, BufferDesc desc)
        {
            if (_registry != null)
                return _registry.GetOrCreateBuffer(rg, name, desc);

            desc.name = name;
            return rg.CreateBuffer(desc);
        }

        public void SetActiveColor(TextureHandle handle)
        {
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


