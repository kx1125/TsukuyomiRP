


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

        public override void CollectTextureSlots(System.Collections.Generic.List<TextureSlot> slots)
        {
            slots.Add(SourceSlot);
            slots.Add(DestinationSlot);
            slots.Add(source);
            slots.Add(destination);
        }

        public override string Name => "PostProcessTestPass";
        // protected override TextureSlot SourceSlot => source;
        // protected override TextureSlot DestinationSlot => destination;
        protected override string OutputName => "PostProcessTestOutput";

        public override bool IsActive(in FrameContext frame)
        {
            return base.IsActive(frame) && material != null;
        }

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

            context.Blit(material);
        }
    }
}
