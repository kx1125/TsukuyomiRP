using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Tsukuyomi.Rendering
{
    internal sealed class TsukuyomiPerObjectShadowResources : ContextItem
    {
        public TextureHandle Shadowmap;

        public override void Reset()
        {
            Shadowmap = TextureHandle.nullHandle;
        }
    }
}
