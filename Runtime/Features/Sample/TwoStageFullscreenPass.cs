using UnityEngine;
using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering
{
    /// <summary>Example for a material with horizontal/vertical blur passes and stable material parameters.</summary>
    [System.Serializable]
    [InjectionPoint(InjectionPoint.BeforePostProcess)]
    public sealed class TwoStageFullscreenPass : GraphFeaturePass
    {
        [Read(BuiltinTexture.ActiveColor)]
        public TextureSlot color = TextureSlot.Read("Color", BuiltinTexture.ActiveColor);
        public Material material;
        public int horizontalPass;
        public int verticalPass = 1;
        private static readonly ProfilingSampler HorizontalSampler = new("Tsukuyomi Blur Horizontal");
        private static readonly ProfilingSampler VerticalSampler = new("Tsukuyomi Blur Vertical");
        public override string Name => "Two Stage Fullscreen";
        public override void CollectTextureSlots(System.Collections.Generic.List<TextureSlot> slots) => slots.Add(color);

        public override void RecordGraph(in FeatureGraphContext graph)
        {
            var source = graph.GetTexture(color);
            // Validate the whole branch before adding either node.
            if (!source.IsValid() || material == null || horizontalPass < 0 || verticalPass < 0
                || horizontalPass >= material.passCount || verticalPass >= material.passCount) return;
            var horizontal = graph.AddFullscreen(source, material, horizontalPass, HorizontalSampler, "Tsukuyomi Blur Horizontal");
            var output = graph.AddFullscreen(horizontal, material, verticalPass, VerticalSampler, "Tsukuyomi Blur Vertical");
            graph.SetActiveColor(output);
        }
    }
}
