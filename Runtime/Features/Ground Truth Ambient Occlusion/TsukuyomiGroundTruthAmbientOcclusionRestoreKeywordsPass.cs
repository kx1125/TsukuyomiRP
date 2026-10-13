using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Tsukuyomi.Rendering
{
    [System.Serializable]
    internal sealed class TsukuyomiGroundTruthAmbientOcclusionRestoreKeywordsPass : RasterPass
    {
        [ReadWrite(BuiltinTexture.ActiveColor)]
        public TextureSlot activeColor = TextureSlot.ReadWrite("ActiveColor", BuiltinTexture.ActiveColor);

        public override void CollectTextureSlots(System.Collections.Generic.List<TextureSlot> slots)
        {
            slots.Add(activeColor);
        }

        public override string Name => "Tsukuyomi Restore GTAO Keywords";

        public override void Record(in RasterPassContext context)
        {
            TextureHandle activeColorTexture = context.Resources.ActiveColor;
            if (!activeColorTexture.IsValid())
                return;

            // This state-only pass must load and preserve the existing camera color.
            context.Builder.SetRenderAttachment(activeColorTexture, 0, AccessFlags.ReadWrite);
            context.Builder.AllowPassCulling(false);
            context.Builder.AllowGlobalStateModification(true);
            context.SetRenderFunc(static (data, graphContext) =>
            {
                graphContext.cmd.SetKeyword(
                    TsukuyomiGroundTruthAmbientOcclusionPass.ScreenSpaceOcclusionGlobalKeyword,
                    false);
            });
        }
    }
}
