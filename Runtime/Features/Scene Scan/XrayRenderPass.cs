using System.Collections.Generic;
using System;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine;

namespace Tsukuyomi.Rendering
{
    [Serializable, InjectionPoint(InjectionPoint.BeforePostProcess)]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Tsukuyomi.Rendering", "Tsukuyomi.Rendering", "XrayRenderPlugin")]
    public sealed class XrayRenderPass : RenderPassBase
    {
        public XrayRenderPass() => InjectionPoint = InjectionPoint.BeforePostProcess;
        public override string Name => "XrayRender";
        public XrayRenderParameters XrayRender = XrayRenderParameters.Default;

        public override void Build(PassPlanBuilder plan)
            => plan.Service(SceneEffectPass.Key, () => new SceneEffectPass()).Register(this);
    }

    /// <summary>
    /// Publishes frame-local masks before scan composition. Target mask RG stores tag
    /// (numeric tags) and positive linear eye depth in metres. Its private depth buffer
    /// resolves target-to-target occlusion only, so scene geometry never culls hidden targets.
    /// Excluded mask R is one only at a currently visible excluded surface.
    /// </summary>
    [System.Serializable]
    public sealed class TsukuyomiXrayRenderPass : CameraEffectPass<XrayRenderParameters>
    {
        private readonly XrayRenderPass _defaults;
        private readonly System.Func<bool> _excludedRequired;
        public TsukuyomiXrayRenderPass() { }
        internal TsukuyomiXrayRenderPass(XrayRenderPass settings, System.Func<bool> excludedRequired)
        { _defaults = settings; _excludedRequired = excludedRequired; }
        internal bool Prepared { get; private set; }
        public override void PrepareRecording(in PassCameraContext context, ref RenderingData data)
        {
            Prepared = false;
            if (_defaults != null || _excludedRequired != null) UpdateSettings(context.Camera, _defaults, context.Volumes);
            // Exclusion consumers finish preparing before resource resolution.
            if (_excludedRequired == null) Prepared = !context.Frame.CameraData.isPreviewCamera && Configure(context.Camera, false);
        }
        public override void ResolveResources(in PassCameraContext context, ref RenderingData data)
        {
            if (_excludedRequired != null) Prepared = !context.Frame.CameraData.isPreviewCamera && Configure(context.Camera, _excludedRequired());
        }
        public override void Deactivate(Camera camera)
        {
            Prepared = false;
            if (!ReferenceEquals(camera, null)) _excludedRequests.Remove(camera);
        }

        public const string TargetMaskName = "SceneScanTargetMask";
        public const string ExcludedMaskName = "SceneScanExcludedMask";

        [Read(BuiltinTexture.CameraDepthTexture)]
        public TextureSlot sceneDepth = TextureSlot.Read("Scene Depth", BuiltinTexture.CameraDepthTexture);

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
        private static readonly int AlphaClipId = Shader.PropertyToID("_AlphaClip");
        private static readonly int CullId = Shader.PropertyToID("_Cull");
        private static readonly int TagId = Shader.PropertyToID("_ScanTargetClass");

        private readonly Dictionary<MaterialKey, Material> _materials = new();
        private readonly List<MaterialKey> _deadMaterials = new();
        private readonly List<Material> _sourceMaterials = new();
        private readonly HashSet<Renderer> _recordedRenderers = new();
        private readonly Plane[] _frustum = new Plane[6];
        private Shader _shader;
        private bool _missingShaderLogged;

        public override string Name => "Tsukuyomi Xray Render Masks";

        public override void CollectTextureSlots(List<TextureSlot> slots) => slots.Add(sceneDepth);

        [System.NonSerialized] private readonly Dictionary<Camera, RendererMaskEntry[]> _entries = new();
        [System.NonSerialized] private readonly HashSet<Camera> _excludedRequests = new();
        protected override XrayRenderParameters DefaultParameters => XrayRenderParameters.Default;
        protected override bool ResolveSettings(RenderPassBase profile, UnityEngine.Rendering.VolumeStack stack, out XrayRenderParameters parameters)
        {
            SceneEffectCameras.Prune(CameraRenderers);
            return SceneEffectSettings.Resolve(profile as XrayRenderPass, stack, out parameters);
        }
        protected override void ApplyAnimation(ref XrayRenderParameters parameters, in ScanAnimationParameters animation)
        {
            animation.Apply(ref parameters.Coverage);
        }


        private static readonly Dictionary<Camera, RendererMaskEntry[]> CameraRenderers = new();
        public static void SetCameraRenderers(Camera camera, IReadOnlyList<RendererMaskEntry> entries)
        {
            if (!camera) throw new System.ArgumentNullException(nameof(camera));
            SceneEffectCameras.Prune(CameraRenderers);
            CopyRenderers(CameraRenderers, camera, entries);
        }
        public new static void ClearCamera(Camera camera)
        { CameraEffectPass<XrayRenderParameters>.ClearCamera(camera); if (!ReferenceEquals(camera, null)) CameraRenderers.Remove(camera); }
        public new static bool HasCameraOverrides(Camera camera)
            => CameraEffectPass<XrayRenderParameters>.HasCameraOverrides(camera) || camera && CameraRenderers.ContainsKey(camera);
        internal static void ResetCameraRenderers() => CameraRenderers.Clear();
        private bool TryGetRenderers(Camera camera, out RendererMaskEntry[] entries)
        { return CameraRenderers.TryGetValue(camera, out entries) || _entries.TryGetValue(camera, out entries); }

        public void SetRenderers(Camera camera, IReadOnlyList<RendererMaskEntry> entries) => CopyRenderers(_entries, camera, entries);
        private static void CopyRenderers(Dictionary<Camera, RendererMaskEntry[]> registrations, Camera camera, IReadOnlyList<RendererMaskEntry> entries)
        {
            if (!camera) throw new System.ArgumentNullException(nameof(camera));
            int count = entries?.Count ?? 0;
            if (registrations.TryGetValue(camera, out var old) && old.Length == count)
            {
                bool same = true;
                for (int i = 0; i < count; i++)
                    if (!ReferenceEquals(old[i].Renderer, entries[i].Renderer) || old[i].Role != entries[i].Role || old[i].Tag != entries[i].Tag) { same = false; break; }
                if (same) return;
            }
            var copy = new RendererMaskEntry[count];
            for (int i = 0; i < count; i++) copy[i] = entries[i];
            registrations[camera] = copy;
        }

        internal bool HasHighlight(Camera camera)
        {
            if (!HighlightAllowed) return false;
            if (!TryGetActiveParameters(camera, out var p) || p.Intensity <= 0f || p.Coverage.Blend <= 0f
                || !TryGetRenderers(camera, out var entries)) return false;
            for (int i = 0; i < entries.Length; i++) if (entries[i].Role == RendererMaskRole.Xray && entries[i].Renderer && entries[i].Tag > 0) return true;
            return false;
        }

        internal bool HighlightAllowed { get; set; } = true;

        private bool Configure(Camera camera, bool excludedRequired)
        {
            if (excludedRequired && TryGetRenderers(camera, out _)) _excludedRequests.Add(camera);
            else _excludedRequests.Remove(camera);
            if (!HasHighlight(camera) && !_excludedRequests.Contains(camera)) return false;
            if (!TsukuyomiRenderPipelineResourcesProvider.TryGet(out var resources)) return false;
            Shader shader = resources.ScanTargetMaskShader;
            if (!shader)
            {
                if (!_missingShaderLogged) Debug.LogError("XrayRender requires ScanTargetMaskShader.");
                _missingShaderLogged = true; return false;
            }
            _missingShaderLogged = false;
            if (_shader != shader) { ReleaseMaterials(); _shader = shader; }
            RemoveDestroyedSourceMaterials();
            return true;
        }

        public override bool IsActive(in FrameContext frame) => Prepared && _shader != null
            && (HasHighlight(frame.CameraData.camera) || _excludedRequests.Contains(frame.CameraData.camera));

        public override void Clear(Camera camera)
        {
            base.Clear(camera);
            if (!ReferenceEquals(camera, null)) { _entries.Remove(camera); _excludedRequests.Remove(camera); }
        }

        private readonly struct MaterialKey : System.IEquatable<MaterialKey>
        {
            public readonly Material Source;
            public readonly int Tag;
            private readonly int _sourceId;

            public MaterialKey(Material source, int tag)
            {
                Source = source;
                Tag = tag;
                _sourceId = ReferenceEquals(source, null) ? 0
                    : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(source);
            }

            public bool Equals(MaterialKey other) => ReferenceEquals(Source, other.Source) && Tag == other.Tag;
            public override bool Equals(object obj) => obj is MaterialKey other && Equals(other);
            public override int GetHashCode() => _sourceId * 397 ^ (int)Tag;
        }

        private readonly struct DrawCall
        {
            public readonly Renderer Renderer;
            public readonly Material Material;
            public readonly int Submesh;

            public DrawCall(Renderer renderer, Material material, int submesh)
            {
                Renderer = renderer;
                Material = material;
                Submesh = submesh;
            }
        }

        private sealed class MaskData
        {
            public readonly List<DrawCall> Draws = new();
            public int ShaderPass;
        }

        public override void RecordGraph(in FeatureGraphContext graph)
        {
            TextureHandle cameraDepth = graph.GetTexture(sceneDepth);
            Camera camera = graph.CameraData.camera;
            if (_shader == null || !camera || !cameraDepth.IsValid())
                return;

            // Preserve the camera's texture-array/dynamic-resolution layout, remove MSAA.
            RenderTextureDescriptor descriptor = graph.CameraData.cameraTargetDescriptor;
            descriptor.msaaSamples = 1;
            descriptor.bindMS = false;
            descriptor.depthStencilFormat = GraphicsFormat.None;
            descriptor.graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            descriptor.enableRandomWrite = false;
            descriptor.memoryless = RenderTextureMemoryless.None;

            TextureDesc targetDesc = new(descriptor)
            {
                name = TargetMaskName,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                clearBuffer = true,
                clearColor = Color.clear
            };
            TextureHandle targetMask = graph.Resources.GetOrCreate(graph.RenderGraph, TargetMaskName, targetDesc);

            descriptor.graphicsFormat = GraphicsFormat.None;
            descriptor.depthStencilFormat = GetDepthFormat();
            TextureDesc depthDesc = new(descriptor)
            {
                name = "SceneScanTargetDepth",
                clearBuffer = true
            };
            TextureHandle targetDepth = graph.CreateTexture(depthDesc);

            GeometryUtility.CalculateFrustumPlanes(camera.cullingMatrix, _frustum);
            using (var node = graph.AddRaster<MaskData>("Tsukuyomi Scene Scan Target Mask"))
            {
                // Mesh draws only cover part of the target. WriteAll/Discard would let
                // the native graph skip the clear and leave the background undefined.
                node.ColorAttachment(targetMask, access: AccessFlags.Write);
                node.DepthAttachment(targetDepth, AccessFlags.ReadWrite);
                node.Data.ShaderPass = 0;
                PopulateDraws(node.Data.Draws, camera, false);
                node.SetRenderFunc(static (data, context) => Draw(data, context));
            }

            descriptor.depthStencilFormat = GraphicsFormat.None;
            descriptor.graphicsFormat = GraphicsFormat.R8_UNorm;
            TextureDesc excludedDesc = new(descriptor)
            {
                name = ExcludedMaskName,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                clearBuffer = true,
                clearColor = Color.clear
            };
            TextureHandle excludedMask = graph.Resources.GetOrCreate(graph.RenderGraph, ExcludedMaskName, excludedDesc);
            using (var node = graph.AddRaster<MaskData>("Tsukuyomi Scene Scan Visible Excluded Mask"))
            {
                node.ReadTexture(cameraDepth);
                node.ColorAttachment(excludedMask, access: AccessFlags.Write);
                node.Data.ShaderPass = 1;
                PopulateDraws(node.Data.Draws, camera, true);
                node.SetRenderFunc(static (data, context) => Draw(data, context));
            }
        }

        private void PopulateDraws(List<DrawCall> draws, Camera camera, bool excluded)
        {
            draws.Clear(); _recordedRenderers.Clear();
            if (!TryGetRenderers(camera, out var entries)) return;
            if (!excluded && !HasHighlight(camera)) return;
            if (excluded && !_excludedRequests.Contains(camera)) return;
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if ((entry.Role == RendererMaskRole.Excluded) != excluded || (!excluded && entry.Tag <= 0)) continue;
                Renderer renderer = entry.Renderer;
                if (!renderer || !renderer.enabled || renderer.forceRenderingOff || !renderer.gameObject.activeInHierarchy
                    || (camera.cullingMask & (1 << renderer.gameObject.layer)) == 0
                    || !GeometryUtility.TestPlanesAABB(_frustum, renderer.bounds) || !_recordedRenderers.Add(renderer)) continue;
                Mesh mesh = null;
                if (renderer is SkinnedMeshRenderer skinned)
                    mesh = skinned.sharedMesh;
                else if (renderer.TryGetComponent(out MeshFilter filter))
                    mesh = filter.sharedMesh;
                if (!mesh || mesh.subMeshCount == 0)
                    continue;

                renderer.GetSharedMaterials(_sourceMaterials);
                int count = Mathf.Min(mesh.subMeshCount, _sourceMaterials.Count);
                for (int submesh = 0; submesh < count; submesh++)
                {
                    Material source = _sourceMaterials[submesh];
                    if (source == null)
                        continue;
                    Material material = GetMaskMaterial(source, excluded ? 0 : entry.Tag);
                    if (material != null)
                        draws.Add(new DrawCall(renderer, material, submesh));
                }
            }
        }

        private Material GetMaskMaterial(Material source, int tag)
        {
            MaterialKey key = new(source, tag);
            if (!_materials.TryGetValue(key, out Material material) || material == null)
            {
                material = CoreUtils.CreateEngineMaterial(_shader);
                if (material == null)
                    return null;
                material.name = $"Scene Scan Mask ({source.name}, {tag})";
                _materials[key] = material;
            }

            // Standard URP/Tsukuyomi and legacy Unity cutout conventions. Custom vertex
            // deformation, alpha UVs or alpha properties require a dedicated mask shader.
            string textureProperty = source.HasProperty(BaseMapId) ? "_BaseMap" : "_MainTex";
            Texture texture = source.HasProperty(textureProperty) ? source.GetTexture(textureProperty) : null;
            material.SetTexture(BaseMapId, texture != null ? texture : Texture2D.whiteTexture);
            material.SetTextureScale("_BaseMap", source.HasProperty(textureProperty) ? source.GetTextureScale(textureProperty) : Vector2.one);
            material.SetTextureOffset("_BaseMap", source.HasProperty(textureProperty) ? source.GetTextureOffset(textureProperty) : Vector2.zero);
            Color color = source.HasProperty(BaseColorId) ? source.GetColor(BaseColorId)
                : source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
            material.SetColor(BaseColorId, color);
            material.SetFloat(CutoffId, source.HasProperty(CutoffId) ? source.GetFloat(CutoffId) : 0.5f);
            bool alphaClip = source.IsKeywordEnabled("_ALPHATEST_ON")
                || (source.HasProperty(AlphaClipId) && source.GetFloat(AlphaClipId) > 0.5f)
                || source.GetTag("RenderType", false) == "TransparentCutout";
            material.SetFloat(AlphaClipId, alphaClip ? 1f : 0f);
            material.SetFloat(CullId, source.HasProperty(CullId) ? source.GetFloat(CullId) : (float)CullMode.Back);
            material.SetFloat(TagId, (float)tag);
            return material;
        }

        private static void Draw(MaskData data, RasterGraphContext context)
        {
            for (int i = 0; i < data.Draws.Count; i++)
            {
                DrawCall draw = data.Draws[i];
                if (draw.Renderer && draw.Material)
                    context.cmd.DrawRenderer(draw.Renderer, draw.Material, draw.Submesh, data.ShaderPass);
            }
        }

        private static GraphicsFormat GetDepthFormat()
        {
            if (SystemInfo.IsFormatSupported(GraphicsFormat.D32_SFloat, GraphicsFormatUsage.Render))
                return GraphicsFormat.D32_SFloat;
            if (SystemInfo.IsFormatSupported(GraphicsFormat.D24_UNorm_S8_UInt, GraphicsFormatUsage.Render))
                return GraphicsFormat.D24_UNorm_S8_UInt;
            return GraphicsFormat.D16_UNorm;
        }

        private void RemoveDestroyedSourceMaterials()
        {
            _deadMaterials.Clear();
            foreach (KeyValuePair<MaterialKey, Material> pair in _materials)
            {
                if (pair.Key.Source == null)
                {
                    CoreUtils.Destroy(pair.Value);
                    _deadMaterials.Add(pair.Key);
                }
            }
            for (int i = 0; i < _deadMaterials.Count; i++)
                _materials.Remove(_deadMaterials[i]);
        }

        private void ReleaseMaterials()
        {
            foreach (Material material in _materials.Values)
                CoreUtils.Destroy(material);
            _materials.Clear();
            _deadMaterials.Clear();
        }

        public override void Dispose()
        {
            ReleaseMaterials();
            _sourceMaterials.Clear();
            _recordedRenderers.Clear();
            _shader = null;
            _entries.Clear(); _excludedRequests.Clear(); ClearAllCameras();
        }
    }
}
