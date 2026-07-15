using UnityEngine;

namespace Tsukuyomi.Rendering
{
    public interface ITsukuyomiShadowCaster
    {
        int Id { get; set; }
        float Priority { get; set; }
        TsukuyomiShadowRendererList.ReadOnly RendererList { get; }
        Transform Transform { get; }
        bool CanCastShadow();
    }
}
