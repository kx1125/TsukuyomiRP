using UnityEngine;
using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering
{
    /// <summary>Prevents previews, disabled features and subsequent cameras using stale SSS data.</summary>
    public static class TsukuyomiSpecularPBRSSSState
    {
        internal static readonly int ReadyId = Shader.PropertyToID("_TsukuyomiSpecularPBRSSSReady");
        internal static readonly int DepthId = Shader.PropertyToID("_TsukuyomiSpecularPBRSSSDepth");
        internal static readonly int LightingScaleId = Shader.PropertyToID("_TsukuyomiSpecularPBRSSSLightingScale");

        // Reset per-camera SSS state in edit mode as well as in the Player.
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Initialize()
        {
            RenderPipelineManager.beginCameraRendering -= ResetCamera;
            RenderPipelineManager.beginCameraRendering += ResetCamera;
            Shader.SetGlobalFloat(ReadyId, 0);
        }

        private static void ResetCamera(ScriptableRenderContext context, Camera camera) => Shader.SetGlobalFloat(ReadyId, 0);
    }
}
