


using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering
{
    [InjectionPoint(InjectionPoint.BeforePostProcess)]
    [InjectionPoint(InjectionPoint.AfterPostProcess)]
    [System.Serializable]
    public class PostProcessTestPass : PostPass
    {
        [Read(BuiltinTexture.ActiveColor)]
        public TextureSlot source = TextureSlot.Read("Source", BuiltinTexture.ActiveColor);

        [Write("PostProcessTestOutput")]
        public TextureSlot destination = TextureSlot.Write("PostProcessTestOutput", BuiltinTexture.None);

        [SerializeField]
        private Material material;

        private bool _missingMaterialLogged;
        private static readonly int _BlitTextureID = Shader.PropertyToID("_BlitTexture");

        public override string Name => "PostProcessTestPass";
        // protected override TextureSlot SourceSlot => source;
        // protected override TextureSlot DestinationSlot => destination;
        protected override string OutputName => "PostProcessTestOutput";

        public override void Render(in PostPassContext context, TextureHandle source, TextureHandle destination)
        {
            if (material == null)
            {
                if (!_missingMaterialLogged)
                {
                    Debug.LogWarning("[Tsukuyomi] PostProcessTestPass requires a material.");
                    _missingMaterialLogged = true;
                }

                return;
            }

            context.PassData.material = material;

            context.SetRenderFunc((TsukuyomiPassData data, RasterGraphContext ctx) =>
            {
                data.material.SetTexture(_BlitTextureID, data.source);
                Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);
            });
        }
    }
}
