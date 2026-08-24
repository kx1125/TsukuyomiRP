using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering
{
    [System.Serializable]
    internal sealed class TsukuyomiScreenSpaceGlobalIlluminationDebugPass : RasterPass
    {
        [Write(BuiltinTexture.ActiveColor)]
        public TextureSlot activeColor = TextureSlot.Write("ActiveColor", BuiltinTexture.ActiveColor);

        private Material _material;
        private Shader _shader;
        private bool _missingShaderLogged;

        public override string Name => "Tsukuyomi SSGI Debug Output";

        public bool Configure()
        {
            if (!TsukuyomiRenderPipelineResourcesProvider.TryGet(out TsukuyomiRenderPipelineResources resources))
                return false;

            Shader shader = resources.SsgiDebugOutputShader;
            if (shader == null)
            {
                if (!_missingShaderLogged)
                {
                    Debug.LogError("Tsukuyomi SSGI Debug Output requires a debug shader in TsukuyomiRenderPipelineResources.");
                    _missingShaderLogged = true;
                }

                ReleaseMaterial();
                return false;
            }

            _missingShaderLogged = false;
            if (_material != null && _shader == shader)
                return true;

            ReleaseMaterial();
            _shader = shader;
            _material = CoreUtils.CreateEngineMaterial(shader);
            return _material != null;
        }

        public override bool IsActive(in FrameContext frame)
        {
            return base.IsActive(frame) && _material != null;
        }

        public override void Record(in RasterPassContext context)
        {
            TextureHandle activeColorTexture = context.Resources.ActiveColor;
            if (_material == null || !activeColorTexture.IsValid())
                return;

            context.Builder.SetRenderAttachment(activeColorTexture, 0, AccessFlags.Write);
            context.Builder.UseAllGlobalTextures(true);
            context.Builder.AllowPassCulling(false);

            Material material = _material;
            context.SetRenderFunc((data, graphContext) =>
            {
                Blitter.BlitTexture(
                    graphContext.cmd,
                    new Vector4(1.0f, 1.0f, 0.0f, 0.0f),
                    material,
                    0);
            });
        }

        public void Dispose()
        {
            ReleaseMaterial();
        }

        private void ReleaseMaterial()
        {
            if (_material != null)
                CoreUtils.Destroy(_material);

            _material = null;
            _shader = null;
        }
    }
}
