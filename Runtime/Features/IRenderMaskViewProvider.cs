using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering
{
    /// <summary>Optional view capability consumed by reactive-mask passes, without knowing the producing pass type.</summary>
    public interface IRenderMaskViewProvider
    {
        bool TryGetMaskView(UniversalCameraData camera, out RenderMaskView view);
    }
    public struct RenderMaskView
    {
        public Matrix4x4 View, Projection;
        public int LayerMask;
        public bool UseOcclusionCulling;
    }
}
