using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering
{
    // Serialization placeholder for old Studio renderer assets. Unified SSS
    // uses the ordinary URP depth, normals and shadow maps through TsukuyomiFeature.
    public sealed class StudioSceneDepthNormalsFeature : ScriptableRendererFeature
    {
        public Shader sceneDepthNormalsShader;
        public override void Create() { }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) { }
    }
}
