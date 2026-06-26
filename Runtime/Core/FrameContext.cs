using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering
{
    public class FrameContext
    {
        public ContextContainer URPFrameData { get; private set; }
        public UniversalCameraData CameraData { get; private set; }
        public UniversalLightData LightData { get; private set; }
        public UniversalResourceData ResourceData { get; private set; }
        public ResourceHub Resources { get; private set; }
        
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
