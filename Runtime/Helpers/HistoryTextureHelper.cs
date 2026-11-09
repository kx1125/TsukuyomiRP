using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Tsukuyomi.Rendering
{
    public static class HistoryTextureHelper
    {
        internal static Vector2 CalculateJitterUv(
            Matrix4x4 jitteredGpuProjection,
            Matrix4x4 nonJitteredGpuProjection)
        {
            // Motion vectors are generated from URP's non-jittered matrices. Recover the
            // current projection offset in the same normalized screen-UV convention used
            // by ComputeWorldSpacePosition before sampling any jittered history texture.
            Vector4 referenceViewPosition = new(0.0f, 0.0f, -1.0f, 1.0f);
            Vector4 jitteredClip = jitteredGpuProjection * referenceViewPosition;
            Vector4 nonJitteredClip = nonJitteredGpuProjection * referenceViewPosition;
            if (Mathf.Abs(jitteredClip.w) < 0.000001f || Mathf.Abs(nonJitteredClip.w) < 0.000001f)
                return Vector2.zero;

            Vector2 jitterNdc = new(
                jitteredClip.x / jitteredClip.w - nonJitteredClip.x / nonJitteredClip.w,
                jitteredClip.y / jitteredClip.w - nonJitteredClip.y / nonJitteredClip.w);
            float yScale = SystemInfo.graphicsUVStartsAtTop ? -0.5f : 0.5f;
            return new Vector2(jitterNdc.x * 0.5f, jitterNdc.y * yScale);
        }

        public readonly struct ImportedPair
        {
            public readonly TextureHandle Previous;
            public readonly TextureHandle Current;
            public ImportedPair(TextureHandle previous, TextureHandle current)
            {
                Previous = previous;
                Current = current;
            }
        }

        /// <summary>Declares Read(previous)/Write(current); does not rotate, allocate or take ownership.</summary>
        public static ImportedPair ImportPair(in PassResourceBuilder resources, RTHandle previous, RTHandle current)
            => new(previous != null ? resources.ImportTexture(previous, AccessFlags.Read) : TextureHandle.nullHandle,
                current != null ? resources.ImportTexture(current, AccessFlags.Write) : TextureHandle.nullHandle);

        public static TextureHandle ImportHistoryTexture(
            RenderGraph renderGraph,
            ResourceHub resourceHub,
            string key,
            in RenderTextureDescriptor descriptor)
        {
            if (resourceHub == null)
                return TextureHandle.nullHandle;

            var history = resourceHub.GetOrCreateHistoryTexture(key, descriptor, out bool reallocated);
            return renderGraph.ImportTexture(history, new ImportResourceParams
            {
                clearOnFirstUse = reallocated,
                clearColor = Color.clear
            });
        }
    }
}
