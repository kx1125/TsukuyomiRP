using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Tsukuyomi.Rendering
{
    [System.Serializable]
    internal sealed class TsukuyomiGroundTruthAmbientOcclusionRestoreKeywordsPass : RasterPass
    {
        [Write(BuiltinTexture.ActiveColor)]
        public TextureSlot activeColor = TextureSlot.Write("ActiveColor", BuiltinTexture.ActiveColor);

        public override string Name => "Tsukuyomi Restore GTAO Keywords";

        public override void Record(in RasterPassContext context)
        {
            TextureHandle activeColorTexture = context.Resources.ActiveColor;
            if (!activeColorTexture.IsValid())
                return;

            context.Builder.SetRenderAttachment(activeColorTexture, 0, AccessFlags.Write);
            context.Builder.AllowGlobalStateModification(true);
            context.SetRenderFunc((data, graphContext) =>
            {
                graphContext.cmd.SetKeyword(
                    TsukuyomiGroundTruthAmbientOcclusionPass.ScreenSpaceOcclusionGlobalKeyword,
                    false);
            });
        }
    }
}
