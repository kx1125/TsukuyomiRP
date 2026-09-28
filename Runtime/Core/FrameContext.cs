using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering
{
    public readonly struct FrameContext
    {
        public ContextContainer URPFrameData { get; }
        public UniversalCameraData CameraData { get; }
        public UniversalLightData LightData { get; }
        public UniversalResourceData ResourceData { get; }
        public ResourceHub Resources { get; }
        
        public FrameContext(ContextContainer frameData, ResourceHub resources = null)
        {
            URPFrameData = frameData;
            CameraData = frameData.Get<UniversalCameraData>();
            LightData = frameData.Get<UniversalLightData>();
            ResourceData = frameData.Get<UniversalResourceData>();
            Resources = resources;
        }
    }
}
