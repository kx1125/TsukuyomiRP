using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering
{
    public sealed class TsukuyomiShadowRendererList
    {
        private readonly struct RendererEntry
        {
            public readonly Renderer Renderer;
            public readonly int DrawCallIndexStartInclusive;
            public readonly int DrawCallIndexEndExclusive;

            public RendererEntry(Renderer renderer, int drawCallIndexStartInclusive, int drawCallIndexEndExclusive)
            {
                Renderer = renderer;
                DrawCallIndexStartInclusive = drawCallIndexStartInclusive;
                DrawCallIndexEndExclusive = drawCallIndexEndExclusive;
            }
        }

        private readonly struct DrawCallData
        {
            public readonly Material Material;
            public readonly int SubmeshIndex;
            public readonly int ShaderPass;

            public DrawCallData(Material material, int submeshIndex, int shaderPass)
            {
                Material = material;
                SubmeshIndex = submeshIndex;
                ShaderPass = shaderPass;
            }
        }

        private static readonly ShaderTagId LightModeTag = new("LightMode");
        private static readonly ShaderTagId ShadowCasterTag = new("ShadowCaster");

        private readonly List<RendererEntry> _renderers = new();
        private readonly List<DrawCallData> _drawCalls = new();

        public TsukuyomiShadowBoundType BoundType;
        public Renderer RenderObject;
        public Bounds Bounds;
        public float Priority;

        public bool TryGetWorldBounds(out Bounds worldBounds, ICollection<int> outAppendRendererIndices = null)
        {
            worldBounds = default;
            bool found = false;

            for (int i = 0; i < _renderers.Count; i++)
            {
                RendererEntry entry = _renderers[i];
                if (!IsEntryEnabled(entry))
                    continue;

                outAppendRendererIndices?.Add(i);

                if (!found)
                {
                    worldBounds = entry.Renderer.bounds;
                    found = true;
                }
                else if (BoundType == TsukuyomiShadowBoundType.Calculated)
                {
                    worldBounds.Encapsulate(entry.Renderer.bounds);
                }
            }

            if (found && BoundType == TsukuyomiShadowBoundType.Customized && RenderObject)
                worldBounds = ResolveCustomWorldBounds(RenderObject, Bounds);

            return found;
        }

        public void Draw(RasterCommandBuffer cmd, int rendererIndex)
        {
            RendererEntry entry = _renderers[rendererIndex];
            for (int i = entry.DrawCallIndexStartInclusive; i < entry.DrawCallIndexEndExclusive; i++)
            {
                DrawCallData drawCall = _drawCalls[i];
                cmd.DrawRenderer(entry.Renderer, drawCall.Material, drawCall.SubmeshIndex, drawCall.ShaderPass);
            }
        }

        public void Clear()
        {
            _renderers.Clear();
            _drawCalls.Clear();
        }

        public void Add(Renderer renderer)
        {
            if (!renderer)
                return;

            int initialDrawCallCount = _drawCalls.Count;
            List<Material> materials = ListPool<Material>.Get();
            try
            {
                renderer.GetSharedMaterials(materials);
                for (int i = 0; i < materials.Count; i++)
                {
                    Material material = materials[i];
                    if (material != null && TryGetShadowCasterPass(material, out int passIndex))
                        _drawCalls.Add(new DrawCallData(material, i, passIndex));
                }

                if (_drawCalls.Count > initialDrawCallCount)
                    _renderers.Add(new RendererEntry(renderer, initialDrawCallCount, _drawCalls.Count));
            }
            catch
            {
                _drawCalls.RemoveRange(initialDrawCallCount, _drawCalls.Count - initialDrawCallCount);
                throw;
            }
            finally
            {
                ListPool<Material>.Release(materials);
            }
        }

        public ReadOnly AsReadOnly()
        {
            return new ReadOnly(this);
        }

        private static bool IsEntryEnabled(RendererEntry entry)
        {
            if (entry.DrawCallIndexEndExclusive <= entry.DrawCallIndexStartInclusive || !entry.Renderer)
                return false;

#if UNITY_EDITOR
            if (UnityEditor.SceneVisibilityManager.instance.IsHidden(entry.Renderer.gameObject))
                return false;
#endif

            return entry.Renderer.enabled
                && entry.Renderer.gameObject.activeInHierarchy
                && entry.Renderer.shadowCastingMode != ShadowCastingMode.Off;
        }

        private static Bounds ResolveCustomWorldBounds(Renderer renderObject, Bounds localBounds)
        {
            if (renderObject is SkinnedMeshRenderer skinnedMeshRenderer)
            {
                Bounds original = skinnedMeshRenderer.localBounds;
                skinnedMeshRenderer.localBounds = localBounds;
                Bounds world = skinnedMeshRenderer.bounds;
                skinnedMeshRenderer.localBounds = original;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    UnityEditor.EditorUtility.ClearDirty(skinnedMeshRenderer);
#endif
                return world;
            }

            Matrix4x4 matrix = renderObject.localToWorldMatrix;
            Vector3 worldMin = matrix.MultiplyPoint3x4(localBounds.min);
            Vector3 worldMax = matrix.MultiplyPoint3x4(localBounds.max);
            Bounds bounds = default;
            bounds.SetMinMax(Vector3.Min(worldMin, worldMax), Vector3.Max(worldMin, worldMax));
            return bounds;
        }

        private static bool TryGetShadowCasterPass(Material material, out int passIndex)
        {
            Shader shader = material.shader;
            for (int i = 0; i < shader.passCount; i++)
            {
                if (shader.FindPassTagValue(i, LightModeTag) == ShadowCasterTag)
                {
                    passIndex = i;
                    return true;
                }
            }

            passIndex = -1;
            return false;
        }

        public readonly struct ReadOnly
        {
            private readonly TsukuyomiShadowRendererList _list;

            public float Priority => _list != null ? _list.Priority : 0.0f;

            internal ReadOnly(TsukuyomiShadowRendererList list)
            {
                _list = list;
            }

            public bool TryGetWorldBounds(out Bounds worldBounds, ICollection<int> outAppendRendererIndices = null)
            {
                if (_list == null)
                {
                    worldBounds = default;
                    return false;
                }

                return _list.TryGetWorldBounds(out worldBounds, outAppendRendererIndices);
            }

            public void Draw(RasterCommandBuffer cmd, int rendererIndex)
            {
                _list?.Draw(cmd, rendererIndex);
            }
        }
    }
}
