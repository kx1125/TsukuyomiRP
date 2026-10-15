using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Tsukuyomi.Rendering
{
    // Shared color composition contains no timers, controller discovery or gameplay classification.
    internal sealed class ScanColorCompositePass : GraphFeaturePass, System.IDisposable
    {
        private readonly TsukuyomiVoxelTransitionPass _opening;
        private readonly TsukuyomiScanPostProcessPass _post;
        private readonly TsukuyomiXrayRenderPass _xray;
        private readonly TextureSlot _source = TextureSlot.Read("Effect Source", BuiltinTexture.ActiveColor);
        private readonly TextureSlot _depth = TextureSlot.Read("Effect Depth", BuiltinTexture.CameraDepthTexture);
        private readonly TextureSlot _normals = TextureSlot.Read("Effect Normals", BuiltinTexture.CameraNormals);
        private Material _material;
        private Shader _shader;
        private bool _requiresNormals, _missingShaderLogged;

        public ScanColorCompositePass(TsukuyomiVoxelTransitionPass opening, TsukuyomiScanPostProcessPass post, TsukuyomiXrayRenderPass xray)
        { _opening = opening; _post = post; _xray = xray; }
        public override string Name => "Tsukuyomi Scene Effect Color Composite";

        internal bool HasScan(Camera camera) => _post != null && _post.TryGetActive(camera, out _)
            && _post.IsEffectEnabled(camera, ScanPostEffect.Scan);
        private bool HasOpening(Camera camera) => _opening != null && _opening.TryGetActiveParameters(camera, out var p) && p.Coverage.Blend > 0f;
        private bool HasXray(Camera camera) => _xray != null && _xray.HasHighlight(camera);
        public override bool IsActive(in FrameContext frame) => Enabled && (HasScan(frame.CameraData.camera) || HasOpening(frame.CameraData.camera) || HasXray(frame.CameraData.camera));

        public override void CollectTextureSlots(List<TextureSlot> slots)
        {
            slots.Add(_source); slots.Add(_depth);
            if (_requiresNormals) slots.Add(_normals);
        }

        internal bool Configure(Camera camera)
        {
            _requiresNormals = HasScan(camera);
            if (!_requiresNormals && !HasOpening(camera) && !HasXray(camera)) return false;
            if (!TsukuyomiRenderPipelineResourcesProvider.TryGet(out var resources)) return false;
            var shader = resources.ScanMainShader;
            if (!shader)
            {
                if (!_missingShaderLogged) Debug.LogError("Scene effect composition requires ScanMainShader.");
                _missingShaderLogged = true; return false;
            }
            _missingShaderLogged = false;
            if (_shader != shader || !_material)
            {
                if (_material) CoreUtils.Destroy(_material);
                _shader = shader; _material = CoreUtils.CreateEngineMaterial(shader);
            }
            return _material != null;
        }

        private sealed class Data
        {
            public Material Material;
            public TextureHandle Original, Sampled, Distorted, ScreenMask, TargetMask, Excluded, Contribution;
            public Vector4 Origin, Range, Curves, Accent, Tint, Style, GridFlash, GridSettings, Modules;
            public float CoverageMode;
        }

        public override void RecordGraph(in FeatureGraphContext graph)
        {
            if (!_material || graph.Resources.IsActiveTargetBackBuffer) return;
            Camera camera = graph.CameraData.camera;
            var source = graph.GetTexture(_source); var depth = graph.GetTexture(_depth);
            if (!source.IsValid() || !depth.IsValid()) return;
            bool scan = HasScan(camera);
            TextureHandle normals = graph.GetTexture(_normals);
            if (scan && !normals.IsValid()) return;
            var frame = graph.FrameData.GetOrCreate<ScanEffectFrameResources>();
            bool opening = frame.OpeningActive;
            var original = opening ? frame.OriginalColor : source;
            var parameters = ScanPostProcessParameters.Default;
            if (scan) _post.TryGetActive(camera, out parameters);
            var coverage = scan ? parameters.Coverage : opening ? frame.OpeningCoverage : frame.XrayCoverage;
            var outputDesc = TextureDescriptors.ColorLike(graph.RenderGraph.GetTextureDesc(source), "SceneEffectComposite");
            TextureHandle output;
            using (var node = graph.AddRaster<Data>(Name))
            {
                node.ReadTexture(depth); if (scan) node.ReadTexture(normals);
                node.Data.Original = node.ReadTexture(original);
                node.Data.Sampled = node.ReadTexture(ScanEffectShaderData.Optional(graph, frame.SampledColor));
                node.Data.Distorted = node.ReadTexture(ScanEffectShaderData.Optional(graph, frame.DistortedColor));
                node.Data.ScreenMask = node.ReadTexture(ScanEffectShaderData.Optional(graph, frame.ScreenMask));
                node.Data.TargetMask = node.ReadTexture(ScanEffectShaderData.Optional(graph, graph.Resources.GetTexture(TsukuyomiXrayRenderPass.TargetMaskName)));
                node.Data.Excluded = node.ReadTexture(ScanEffectShaderData.Optional(graph, graph.Resources.GetTexture(TsukuyomiXrayRenderPass.ExcludedMaskName)));
                // Original diagnostic views returned before the target contribution and final blend.
                var contribution = scan && parameters.DebugView >= 2 ? TextureHandle.nullHandle : frame.TargetContribution;
                node.Data.Contribution = node.ReadTexture(ScanEffectShaderData.Optional(graph, contribution));
                output = node.ColorAttachment(outputDesc, access: AccessFlags.WriteAll);
                node.Data.Material = _material;
                node.Data.Origin = ScanEffectShaderData.Origin(coverage);
                node.Data.Range = ScanEffectShaderData.Range(coverage, parameters.CellSize);
                node.Data.Curves = ScanEffectShaderData.Curves(coverage, scan ? parameters.Flash : 0f);
                node.Data.Accent = parameters.AccentColor; node.Data.Tint = parameters.TintColor;
                node.Data.Style = new Vector4(Mathf.Clamp01(parameters.Desaturation), Mathf.Clamp01(parameters.TintStrength),
                    Mathf.Max(0f, parameters.OutlineStrength), Mathf.Max(0f, parameters.FresnelStrength));
                node.Data.GridFlash = new Vector4(Mathf.Max(0f, parameters.SurfaceGridStrength), scan ? Mathf.Max(0f, parameters.FlashStrength) : 0f,
                    scan ? Mathf.Clamp(parameters.DebugView, 0, 6) : 0f, Mathf.Max(0f, parameters.PulseSpeed));
                node.Data.GridSettings = new Vector4(Mathf.Max(1f, parameters.GridSubdivisions), Mathf.Clamp01(parameters.BasicIntensity), parameters.Reveal, parameters.Random01);
                node.Data.Modules = new Vector4(scan ? 1f : 0f, opening ? 1f : 0f, 0f, 0f);
                node.Data.CoverageMode = (float)coverage.Mode;
                node.SetRenderFunc(static (data, context) =>
                {
                    var m = data.Material;
                    m.SetTexture("_TransitionSampled", data.Sampled); m.SetTexture("_TransitionDistorted", data.Distorted);
                    m.SetTexture("_TransitionScreenMask", data.ScreenMask); m.SetTexture("_ScanTargetMask", data.TargetMask);
                    m.SetTexture("_ScanExcludedMask", data.Excluded); m.SetTexture("_XrayContribution", data.Contribution);
                    m.SetVector("_ScanOriginProgress", data.Origin); m.SetVector("_ScanGridRange", data.Range);
                    m.SetVector("_ScanCurveParams", data.Curves); m.SetVector("_ScanDebugColor", data.Accent);
                    m.SetVector("_ScanTintColor", data.Tint); m.SetVector("_ScanStyleParams", data.Style);
                    m.SetVector("_ScanGridFlash", data.GridFlash); m.SetVector("_ScanGridSettings", data.GridSettings);
                    m.SetVector("_ScanModuleFlags", data.Modules); m.SetFloat("_ScanCoverageMode", data.CoverageMode);
                    Blitter.BlitTexture(context.cmd, data.Original, new Vector4(1, 1, 0, 0), m, 0);
                });
            }
            graph.SetActiveColor(output);
        }

        public void Dispose() { if (_material) CoreUtils.Destroy(_material); _material = null; _shader = null; }
    }
}
