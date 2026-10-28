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

        public override void CollectTextureSlots(System.Collections.Generic.List<TextureSlot> slots)
        {
            slots.Add(activeColor);
        }

        public override string Name => "Tsukuyomi SSGI Debug Output";

        private readonly System.Func<bool> _requested;
        internal bool Prepared { get; private set; }
        public TsukuyomiScreenSpaceGlobalIlluminationDebugPass(System.Func<bool> requested) => _requested = requested;
        public override void PrepareRecording(in PassCameraContext context, ref RenderingData data)
            => Prepared = _requested() && Configure();
        public override void Deactivate(Camera camera) => Prepared = false;
        private bool Configure()
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
            return base.IsActive(frame) && Prepared && _material != null;
        }

        private sealed class RenderData
        {
            public Material Material;
        }

        public override void Record(in RasterPassContext context)
        {
            var passData = context.GetOrCreateData<RenderData>();
            TextureHandle activeColorTexture = context.Resources.ActiveColor;
            if (_material == null || !activeColorTexture.IsValid())
                return;

            context.ColorAttachment(activeColorTexture);
            context.Builder.UseAllGlobalTextures(true);
            context.Builder.AllowPassCulling(false);

            passData.Material = _material;

            context.SetRenderFunc(passData, static (state, graphContext) =>
            {
                Blitter.BlitTexture(
                    graphContext.cmd,
                    new Vector4(1.0f, 1.0f, 0.0f, 0.0f),
                    state.Material,
                    0);
            });
        }

        public override void Dispose()
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
