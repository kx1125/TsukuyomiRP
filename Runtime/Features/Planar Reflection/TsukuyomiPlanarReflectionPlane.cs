using System.Collections.Generic;
using UnityEngine;

namespace Tsukuyomi.Rendering
{
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Renderer))]
    public sealed class TsukuyomiPlanarReflectionPlane : MonoBehaviour
    {
        private const string LitPbrShaderName = "TsukuyomiRP/Lit/PBR";
        private const string PlanarReflectionKeyword = "_TSUKUYOMI_PLANAR_REFLECTION";

        private static readonly List<TsukuyomiPlanarReflectionPlane> s_ActivePlanes = new();
        private static bool s_WarnedMultiplePlanes;

        public bool enabledInScene = true;

        [Tooltip("Detect the reflection plane normal from the thinnest axis of the Renderer mesh bounds. Plane Transform still controls the plane position when assigned.")]
        public bool autoDetectPlane = true;

        [Tooltip("Manual plane transform used when Auto Detect Plane is disabled. Its local Y axis is the plane normal.")]
        public Transform planeTransform;

        [Tooltip("Moves the detected or manual plane along its normal in world units.")]
        public float planeOffset;

        public int priority;

        [Min(0.0f)]
        public float clipPlaneOffset = 0.07f;

        public bool TryGetPlane(out Vector3 position, out Vector3 normal)
        {
            if (autoDetectPlane && TryGetMeshBounds(out Bounds localBounds))
            {
                Vector3 scaledSize = Vector3.Scale(localBounds.size, Abs(transform.lossyScale));
                position = planeTransform ? planeTransform.position : transform.position;

                if (scaledSize.x <= scaledSize.y && scaledSize.x <= scaledSize.z)
                    normal = transform.right;
                else if (scaledSize.y <= scaledSize.z)
                    normal = transform.up;
                else
                    normal = transform.forward;

                normal.Normalize();
                position += normal * planeOffset;
                return true;
            }

            Transform source = planeTransform ? planeTransform : transform;
            position = source.position;
            normal = source.up.normalized;
            position += normal * planeOffset;
            return normal.sqrMagnitude > 0.0f;
        }

        private bool TryGetMeshBounds(out Bounds bounds)
        {
            if (TryGetComponent(out MeshFilter meshFilter) && meshFilter.sharedMesh)
            {
                bounds = meshFilter.sharedMesh.bounds;
                return true;
            }

            if (TryGetComponent(out SkinnedMeshRenderer skinnedMeshRenderer))
            {
                bounds = skinnedMeshRenderer.localBounds;
                return true;
            }

            bounds = default;
            return false;
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }

        private void OnEnable()
        {
            if (!s_ActivePlanes.Contains(this))
                s_ActivePlanes.Add(this);

            UpdateMaterialKeywords();
        }

        private void OnDisable()
        {
            s_ActivePlanes.Remove(this);
            SetMaterialKeywords(false);
        }

        private void OnValidate()
        {
            clipPlaneOffset = Mathf.Max(0.0f, clipPlaneOffset);
            UpdateMaterialKeywords();
        }

        private void UpdateMaterialKeywords()
        {
            SetMaterialKeywords(enabledInScene && isActiveAndEnabled && gameObject.activeInHierarchy);
        }

        private void SetMaterialKeywords(bool enabled)
        {
            if (!TryGetComponent(out Renderer targetRenderer))
                return;

            Material[] materials = targetRenderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (!material)
                    continue;

                bool usePlanarReflection = enabled
                    && material.shader
                    && material.shader.name == LitPbrShaderName;

                if (usePlanarReflection)
                    material.EnableKeyword(PlanarReflectionKeyword);
                else
                    material.DisableKeyword(PlanarReflectionKeyword);
            }
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
