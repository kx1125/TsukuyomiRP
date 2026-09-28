using UnityEngine;
using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering
{
    /// <summary>Allocation helper only. URP owns rotation/release; the algorithm owns temporal validity.</summary>
    public abstract class BufferedTextureHistory : CameraHistoryItem
    {
        protected bool EnsureAllocated(int id, ref Hash128 descriptorKey, ref RenderTextureDescriptor descriptor,
            FilterMode filterMode, string name)
        {
            Hash128 newKey = Hash128.Compute(ref descriptor);
            RTHandle current = GetCurrentFrameRT(id);
            if (current != null && descriptorKey == newKey) return false;
            if (current != null) ReleaseHistoryFrameRT(id);
            AllocHistoryFrameRT(id, 2, ref descriptor, filterMode, name);
            descriptorKey = newKey;
            return true;
        }
    }
}
