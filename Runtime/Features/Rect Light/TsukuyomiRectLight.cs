using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering
{
    public enum RectLightDiffuseQuality { Low, Medium, High }

    /// <summary>Rectangle in local XY, emitting toward local +Z. Sizes are world metres.</summary>
    [ExecuteAlways, DisallowMultipleComponent, AddComponentMenu("Rendering/Tsukuyomi Rect Light")]
    public sealed class TsukuyomiRectLight : MonoBehaviour
    {
        public RectLightDiffuseQuality DiffuseQuality = RectLightDiffuseQuality.Medium;
        [Min(0.0001f)] public float Width = 1;
        [Min(0.0001f)] public float Height = 1;
        [ColorUsage(false, true)] public Color Color = Color.white;
        [Min(0), Tooltip("Relative artistic intensity. Low diffuse uses this value before division by area.")]
        public float Intensity = 1;
        [Min(0)] public float Range = 10;
        public RenderingLayerMask RenderingLayer = RenderingLayerMask.defaultRenderingLayerMask;
        public RayTracedShadowMode ShadowMode = RayTracedShadowMode.Off;
        [Range(0, 1)] public float ShadowStrength = 1;
        [Tooltip("Higher priorities receive ray tracing slots first.")]
        public int ShadowPriority;

        internal static readonly HashSet<TsukuyomiRectLight> ActiveLights = new();
        private void OnEnable() => ActiveLights.Add(this);
        private void OnDisable() => ActiveLights.Remove(this);
        private void OnDestroy() => ActiveLights.Remove(this);

        internal bool IsUsable => isActiveAndEnabled && gameObject.activeInHierarchy
            && float.IsFinite(Width) && float.IsFinite(Height) && float.IsFinite(Range)
            && float.IsFinite(Intensity) && Width > 0 && Height > 0 && Range > 0 && Intensity > 0
            && float.IsFinite(Color.r) && float.IsFinite(Color.g) && float.IsFinite(Color.b);

        public RectLightGpuData ToGpuData()
        {
            var t = transform;
            float w = Mathf.Max(Width, .0001f), h = Mathf.Max(Height, .0001f);
            var color = Color.linear * Mathf.Max(0, Intensity);
            return new RectLightGpuData
            {
                PositionRange = new Vector4(t.position.x, t.position.y, t.position.z, Mathf.Max(Range, .0001f)),
                RightWidth = new Vector4(t.right.x, t.right.y, t.right.z, w * .5f),
                UpHeight = new Vector4(t.up.x, t.up.y, t.up.z, h * .5f),
                ForwardInvArea = new Vector4(t.forward.x, t.forward.y, t.forward.z, 1 / (w * h)),
                ColorIntensity = new Vector4(Mathf.Max(0, color.r), Mathf.Max(0, color.g), Mathf.Max(0, color.b), 0),
                Quality = (uint)Mathf.Clamp((int)DiffuseQuality, 0, 2), LayerMask = RenderingLayer,
                ShadowIndex = uint.MaxValue
            };
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(Mathf.Max(0, Width), Mathf.Max(0, Height), 0));
            Gizmos.DrawLine(Vector3.zero, Vector3.forward * Mathf.Min(1, Mathf.Max(0, Range)));
            Gizmos.matrix = Matrix4x4.identity;
        }
    }

    // Six 16-byte blocks. Matches TsukuyomiRectLightData in RectLight.hlsl.
    [StructLayout(LayoutKind.Sequential)]
    public struct RectLightGpuData
    {
        public Vector4 PositionRange, RightWidth, UpHeight, ForwardInvArea, ColorIntensity;
        public uint Quality, LayerMask, ShadowIndex, Padding1;
        public const int Stride = 96;
    }
}
