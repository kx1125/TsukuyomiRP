using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering
{
    /// <summary>One camera's DXR scene. Build the acceleration structure in the render graph after Update.</summary>
    internal sealed class TsukuyomiRayTracingScene : IDisposable
    {
        public const string ShaderPassName = "TsukuyomiRTShadow";
        public const string ShaderTagValue = "True";
        public RayTracingAccelerationStructure AccelerationStructure { get; private set; }

        private RayTracingInstanceCullingConfig _culling;
        private readonly List<Material> _materials = new();
        private static readonly HashSet<Shader> s_WarnedShaders = new();
        private readonly Dictionary<Renderer, CasterState> _previousCasters = new();
        private readonly HashSet<Renderer> _currentCasters = new();
        private readonly List<Renderer> _removedCasters = new();
        private readonly List<Bounds> _changedBounds = new();
        private int _materialHash, _previousMask;
        private bool _initialized, _allLightsDirty;
        private Matrix4x4 _lodView, _lodProjection;

        private readonly struct CasterState : IEquatable<CasterState>
        {
            private readonly Matrix4x4 _matrix;
            private readonly Bounds _bounds;
            private readonly uint _flags;
            private readonly RayTracingMode _mode;
            private readonly Mesh _mesh;
            public Bounds Bounds => _bounds;
            public CasterState(Renderer renderer, uint flags)
            {
                _matrix = renderer.localToWorldMatrix;
                _bounds = renderer.bounds;
                _flags = flags;
                _mode = renderer.rayTracingMode;
                _mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh
                    : renderer.TryGetComponent<MeshFilter>(out var filter) ? filter.sharedMesh : null;
            }
            public bool Equals(CasterState other) => _matrix == other._matrix && _bounds == other._bounds
                && _flags == other._flags && _mode == other._mode && _mesh == other._mesh;
        }

        public TsukuyomiRayTracingScene()
        {
            AccelerationStructure = CreateAccelerationStructure();
            _culling.flags = RayTracingInstanceCullingFlags.ComputeMaterialsCRC;
            _culling.materialTest.requiredShaderTags = new[]
            {
                new RayTracingInstanceCullingShaderTagConfig
                {
                    tagId = new ShaderTagId(ShaderPassName), tagValueId = new ShaderTagId(ShaderTagValue)
                }
            };
            _culling.instanceTests = new[]
            {
                new RayTracingInstanceCullingTest
                {
                    allowOpaqueMaterials = true, allowAlphaTestedMaterials = true,
                    allowTransparentMaterials = false, allowVisualEffects = false,
                    layerMask = -1, instanceMask = 0xff,
                    shadowCastingModeMask = (1 << (int)ShadowCastingMode.On)
                        | (1 << (int)ShadowCastingMode.TwoSided) | (1 << (int)ShadowCastingMode.ShadowsOnly)
                }
            };
            _culling.alphaTestedMaterialConfig.renderQueueLowerBound = (int)RenderQueue.AlphaTest;
            _culling.alphaTestedMaterialConfig.renderQueueUpperBound = (int)RenderQueue.GeometryLast;
            _culling.alphaTestedMaterialConfig.optionalShaderKeywords = new[] { "_ALPHATEST_ON" };
            _culling.transparentMaterialConfig.renderQueueLowerBound = (int)RenderQueue.GeometryLast + 1;
            _culling.transparentMaterialConfig.renderQueueUpperBound = int.MaxValue;
            _culling.transparentMaterialConfig.optionalShaderKeywords = new[] { "_SURFACE_TYPE_TRANSPARENT" };

            // AnyHit handles both per-material _Cull and per-renderer TwoSided. Native culling cannot
            // express _Cull=Front or a different _Cull for each submesh of the same Renderer.
            _culling.triangleCullingConfig.forceDoubleSided = true;
            _culling.triangleCullingConfig.frontTriangleCounterClockwise = false;
            _culling.subMeshFlagsConfig.opaqueMaterials = RayTracingSubMeshFlags.Enabled | RayTracingSubMeshFlags.UniqueAnyHitCalls;
            _culling.subMeshFlagsConfig.alphaTestedMaterials = RayTracingSubMeshFlags.Enabled | RayTracingSubMeshFlags.UniqueAnyHitCalls;
            _culling.subMeshFlagsConfig.transparentMaterials = RayTracingSubMeshFlags.Disabled;
        }

        private static RayTracingAccelerationStructure CreateAccelerationStructure() => new(
            new RayTracingAccelerationStructure.Settings(RayTracingAccelerationStructure.ManagementMode.Manual,
                RayTracingAccelerationStructure.RayTracingModeMask.Everything, -1));

        /// <returns>Whether all temporal shadow histories must be rejected. Check HasChangesForLight for local changes.</returns>
        public bool Update(Camera camera, int casterLayerMask)
        {
            if (AccelerationStructure == null) throw new ObjectDisposedException(nameof(TsukuyomiRayTracingScene));
            bool changed = !_initialized || casterLayerMask != _previousMask;
            _changedBounds.Clear();
            bool lodViewChanged = camera && (_lodView != camera.worldToCameraMatrix || _lodProjection != camera.projectionMatrix);
            if (camera) { _lodView = camera.worldToCameraMatrix; _lodProjection = camera.projectionMatrix; }
            _previousMask = casterLayerMask;
            _culling.instanceTests[0].layerMask = casterLayerMask;
            _culling.flags = RayTracingInstanceCullingFlags.ComputeMaterialsCRC;
            // LOD selection avoids intersecting overlapping LOD meshes, without frustum culling
            // the scene: a caster outside the screen can still shadow a visible receiver.
            if (camera)
            {
                // Use the rendering camera's LOD inputs, including for Scene View.
                _culling.lodParameters = new LODParameters
                {
                    cameraPosition = camera.transform.position,
                    fieldOfView = camera.fieldOfView,
                    cameraPixelHeight = camera.pixelHeight,
                    isOrthographic = camera.orthographic,
                    orthoSize = camera.orthographic ? camera.orthographicSize : 0
                };
                _culling.flags |= RayTracingInstanceCullingFlags.EnableLODCulling;
            }
            // Unity 6000.5 can retain CullInstances' population cache after ClearInstances:
            // repeated editor camera renders then produce an empty or incomplete TLAS.
            // Keeping the old population instead leaves disabled casters and old LODs behind.
            // Recreate the camera's TLAS before population; Unity owns geometry BLAS lifetime
            // independently, so this does not force static mesh geometry to be rebuilt.
            if (_initialized)
            {
                var next = CreateAccelerationStructure();
                AccelerationStructure.Dispose();
                AccelerationStructure = next;
            }
            var results = AccelerationStructure.CullInstances(ref _culling);
            // Track actual matrices/bounds below. The native transform flag does not identify
            // affected light regions, and recreating the TLAS alone does not invalidate history.
            int materialHash = 0;
            if (results.materialsCRC != null)
                foreach (var material in results.materialsCRC)
                    materialHash ^= HashCode.Combine(material.entityId, material.crc);
            changed |= materialHash != _materialHash;
            _materialHash = materialHash;

            _currentCasters.Clear();
            uint instanceIndex = 1; // Zero stays reserved; bit zero carries TwoSided to the hit shader.
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
            {
                if (!renderer.enabled || !renderer.gameObject.scene.IsValid()
                    || (casterLayerMask & (1 << renderer.gameObject.layer)) == 0
                    || renderer.shadowCastingMode == ShadowCastingMode.Off || renderer.rayTracingMode == RayTracingMode.Off)
                {
                    // Editor property changes can precede the native renderer update.
                    // Apply the current CPU eligibility too, so a disabled caster never lingers.
                    AccelerationStructure.RemoveInstance(renderer);
                    continue;
                }
                bool supported = HasSupportedMaterial(renderer);
                if (!supported || (renderer is not MeshRenderer && renderer is not SkinnedMeshRenderer))
                {
                    AccelerationStructure.RemoveInstance(renderer);
                    continue;
                }

                uint flags = renderer.shadowCastingMode == ShadowCastingMode.TwoSided ? 1u : 0u;
                // InstanceID is available directly in every material hit group. Keep
                // the per-renderer face mode here so it needs no separate global SRV.
                AccelerationStructure.UpdateInstanceID(renderer, (instanceIndex++ << 1) | flags);
                _currentCasters.Add(renderer);
                var state = new CasterState(renderer, flags);
                bool hadPrevious = _previousCasters.TryGetValue(renderer, out var previous);
                bool casterChanged = !hadPrevious || !state.Equals(previous);
                _previousCasters[renderer] = state;
                // Receiver motion vectors cannot describe moving occluders. Conservatively reject
                // history for lights whose influence intersects mutable or displaced geometry.
                casterChanged |= renderer.rayTracingMode == RayTracingMode.DynamicGeometry
                    || renderer.rayTracingMode == RayTracingMode.DynamicGeometryManualUpdate
                    || renderer is SkinnedMeshRenderer || renderer.HasPropertyBlock();
                // Native LOD selection can switch geometry without changing any renderer matrix.
                // During camera movement, conservatively invalidate lights near an LOD group.
                casterChanged |= lodViewChanged && renderer.GetComponentInParent<LODGroup>();
                if (casterChanged)
                {
                    _changedBounds.Add(state.Bounds);
                    if (hadPrevious) _changedBounds.Add(previous.Bounds);
                }
            }
            _removedCasters.Clear();
            foreach (var renderer in _previousCasters.Keys)
                if (!_currentCasters.Contains(renderer)) _removedCasters.Add(renderer);
            foreach (var renderer in _removedCasters)
            {
                if (renderer) AccelerationStructure.RemoveInstance(renderer);
                _changedBounds.Add(_previousCasters[renderer].Bounds);
                _previousCasters.Remove(renderer);
            }

            _initialized = true;
            _allLightsDirty = changed;
            return changed;
        }

        /// <summary>Conservative caster changes within a local light's influence. Directional lights cover the scene.</summary>
        public bool HasChangesForLight(Vector3 position, float range, bool directional)
        {
            if (_allLightsDirty) return true;
            if (directional) return _changedBounds.Count > 0;
            float radiusSquared = Mathf.Max(0, range) * Mathf.Max(0, range);
            foreach (var bounds in _changedBounds)
                if (bounds.SqrDistance(position) <= radiusSquared) return true;
            return false;
        }

        private bool HasSupportedMaterial(Renderer renderer)
        {
            renderer.GetSharedMaterials(_materials);
            bool supported = false;
            foreach (var material in _materials)
            {
                if (!material || !material.shader || material.renderQueue > (int)RenderQueue.GeometryLast
                    || material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT")) continue;
                // Unity canonicalizes ShaderLab tag values ("True" is returned as "true").
                if (string.Equals(material.GetTag(ShaderPassName, false, ""), ShaderTagValue, StringComparison.OrdinalIgnoreCase))
                    supported = true;
                else if (s_WarnedShaders.Add(material.shader))
                    Debug.LogWarning($"Tsukuyomi RT shadows skipped shader '{material.shader.name}': add the {ShaderPassName} pass and SubShader tag {ShaderPassName}={ShaderTagValue} to enable ray traced shadow casting.", material);
            }
            return supported;
        }

        public void Dispose()
        {
            AccelerationStructure?.Dispose();
            AccelerationStructure = null;
            _previousCasters.Clear();
            _currentCasters.Clear();
            _materials.Clear();
            _changedBounds.Clear();
        }
    }
}
