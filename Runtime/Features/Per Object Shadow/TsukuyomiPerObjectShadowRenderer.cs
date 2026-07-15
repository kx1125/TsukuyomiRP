using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace Tsukuyomi.Rendering
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class TsukuyomiPerObjectShadowRenderer : MonoBehaviour
    {
        [Serializable]
        public sealed class ShadowCasterClusterData
        {
            public Renderer renderObject;
            public Renderer[] renderers;
            public TsukuyomiShadowBoundType boundType = TsukuyomiShadowBoundType.Calculated;
            public Bounds bounds = new(Vector3.zero, Vector3.one);
        }

        [Serializable]
        private sealed class ShadowCasterCluster : ITsukuyomiShadowCaster
        {
            private readonly TsukuyomiPerObjectShadowRenderer _owner;
            private readonly TsukuyomiShadowRendererList _rendererList = new();

            public readonly List<Renderer> Renderers = new();

            public ShadowCasterCluster(TsukuyomiPerObjectShadowRenderer owner, Renderer renderObject)
            {
                _owner = owner;
                _rendererList.RenderObject = renderObject;
                _rendererList.BoundType = TsukuyomiShadowBoundType.Calculated;
            }

            public int Id { get; set; } = -1;
            public float Priority { get; set; }
            public TsukuyomiShadowRendererList.ReadOnly RendererList => _rendererList.AsReadOnly();

            public void SetBounds(TsukuyomiShadowBoundType boundType, Bounds bounds)
            {
                _rendererList.BoundType = boundType;
                _rendererList.Bounds = bounds;
            }

            public Transform Transform => _owner.transform;

            public bool CanCastShadow()
            {
                return _owner && _owner.isActiveAndEnabled && _owner.isCastingShadow;
            }

            public void Rebuild()
            {
                _rendererList.Clear();
                foreach (Renderer renderer in Renderers)
                {
                    if (renderer)
                        _rendererList.Add(renderer);
                }
            }

            public void Reset()
            {
                Renderers.Clear();
                _rendererList.Clear();
            }
        }

        public bool isCastingShadow = true;
        public ShadowCasterClusterData[] clusterData;
        public RenderingLayerMask renderingLayerMask = TsukuyomiPerObjectShadowDefaults.RenderingLayerMask;

        private static readonly HashSet<TsukuyomiPerObjectShadowRenderer> s_ActiveRenderers = new();

        private ShadowCasterCluster[] _casterClusters;
        private readonly Dictionary<Renderer, uint> _originalRenderingLayerMasks = new();

        private void Awake()
        {
            AllocateClusters();
            SetupRenderingLayers(renderingLayerMask);
        }

        private void OnEnable()
        {
            SafeCheckEditor();
            SetupRenderingLayers(renderingLayerMask);
            s_ActiveRenderers.Add(this);
            RegisterClusters();
        }

        private void OnDisable()
        {
            UnregisterClusters();
            s_ActiveRenderers.Remove(this);
            RestoreRenderingLayers();
        }

        [Conditional("UNITY_EDITOR")]
        private void OnValidate()
        {
            UnregisterClusters();
            RestoreRenderingLayers();
            AllocateClusters();
            SetupRenderingLayers(renderingLayerMask);
            if (enabled)
                RegisterClusters();
        }

        [Conditional("UNITY_EDITOR")]
        private void OnDrawGizmosSelected()
        {
            SafeCheckEditor();
            if (_casterClusters == null)
                return;

            foreach (ShadowCasterCluster cluster in _casterClusters)
            {
                if (!cluster.RendererList.TryGetWorldBounds(out Bounds bounds))
                    continue;

                Color color = Gizmos.color;
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(bounds.center, bounds.size);
                Gizmos.color = color;
            }
        }

        [Conditional("UNITY_EDITOR")]
        private void SafeCheckEditor()
        {
            if (Application.isPlaying)
                return;

            if (_casterClusters == null)
                AllocateClusters();
        }

        private void AllocateClusters()
        {
            if (clusterData == null || clusterData.Length == 0)
            {
                _casterClusters = Array.Empty<ShadowCasterCluster>();
                return;
            }

            _casterClusters = clusterData
                .Where(item => item != null && item.renderObject != null)
                .Select(item =>
                {
                    ShadowCasterCluster cluster = new(this, item.renderObject);
                    cluster.SetBounds(item.boundType, item.bounds);
                    if (item.renderers == null || !item.renderers.Contains(item.renderObject))
                        cluster.Renderers.Add(item.renderObject);
                    if (item.renderers != null)
                        cluster.Renderers.AddRange(item.renderers.Where(renderer => renderer != null));
                    cluster.Rebuild();
                    return cluster;
                })
                .ToArray();
        }

        internal static void ApplyRenderingLayerMaskToAll(uint mask)
        {
            foreach (TsukuyomiPerObjectShadowRenderer renderer in s_ActiveRenderers)
            {
                if (renderer)
                    renderer.SetupRenderingLayers(mask);
            }
        }

        internal static void RestoreRenderingLayersForAll()
        {
            foreach (TsukuyomiPerObjectShadowRenderer renderer in s_ActiveRenderers)
            {
                if (renderer)
                    renderer.RestoreRenderingLayers();
            }
        }
        private void RegisterClusters()
        {
            if (_casterClusters == null)
                return;

            foreach (ShadowCasterCluster cluster in _casterClusters)
                TsukuyomiShadowCasterManager.Register(cluster);
        }

        private void UnregisterClusters()
        {
            if (_casterClusters == null)
                return;

            foreach (ShadowCasterCluster cluster in _casterClusters)
                TsukuyomiShadowCasterManager.Unregister(cluster);
        }

        private void RestoreRenderingLayers()
        {
            foreach (KeyValuePair<Renderer, uint> item in _originalRenderingLayerMasks)
            {
                if (item.Key)
                    item.Key.renderingLayerMask = item.Value;
            }

            _originalRenderingLayerMasks.Clear();
        }
        private void SetupRenderingLayers(uint mask)
        {
            if (_casterClusters == null)
                return;

            foreach (ShadowCasterCluster cluster in _casterClusters)
            {
                foreach (Renderer renderer in cluster.Renderers)
                {
                    if (!renderer)
                        continue;

                    if (!_originalRenderingLayerMasks.ContainsKey(renderer))
                        _originalRenderingLayerMasks.Add(renderer, renderer.renderingLayerMask);

                    renderer.renderingLayerMask = mask;
                }
            }
        }
    }
}
