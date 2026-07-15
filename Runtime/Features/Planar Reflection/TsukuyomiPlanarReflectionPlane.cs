using System.Collections.Generic;
using UnityEngine;

namespace Tsukuyomi.Rendering
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class TsukuyomiPlanarReflectionPlane : MonoBehaviour
    {
        private static readonly List<TsukuyomiPlanarReflectionPlane> s_ActivePlanes = new();
        private static bool s_WarnedMultiplePlanes;

        public bool enabledInScene = true;
        public Transform planeTransform;
        public int priority;

        [Min(0.0f)]
        public float clipPlaneOffset = 0.07f;

        public Transform ReflectionTransform => planeTransform ? planeTransform : transform;
        public Vector3 PlanePosition => ReflectionTransform.position;
        public Vector3 PlaneNormal => ReflectionTransform.up.normalized;

        private void OnEnable()
        {
            if (!s_ActivePlanes.Contains(this))
                s_ActivePlanes.Add(this);
        }

        private void OnDisable()
        {
            s_ActivePlanes.Remove(this);
        }

        private void OnValidate()
        {
            clipPlaneOffset = Mathf.Max(0.0f, clipPlaneOffset);
        }

        public static bool TryGetActivePlane(out TsukuyomiPlanarReflectionPlane plane)
        {
            plane = null;
            int activeCount = 0;

            for (int i = s_ActivePlanes.Count - 1; i >= 0; i--)
            {
                TsukuyomiPlanarReflectionPlane candidate = s_ActivePlanes[i];
                if (!candidate)
                {
                    s_ActivePlanes.RemoveAt(i);
                    continue;
                }

                if (!candidate.enabledInScene || !candidate.isActiveAndEnabled || !candidate.gameObject.activeInHierarchy)
                    continue;

                activeCount++;
                if (plane == null || candidate.priority > plane.priority)
                    plane = candidate;
            }

            if (activeCount > 1 && !s_WarnedMultiplePlanes)
            {
                Debug.LogWarning("Tsukuyomi planar reflection V1 supports one active plane. The highest priority plane will be used.");
                s_WarnedMultiplePlanes = true;
            }

            return plane != null;
        }
    }
}
