using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Tsukuyomi.Rendering
{
    internal abstract class ScanGlitchNode : GraphFeaturePass, System.IDisposable
    {
        protected readonly TsukuyomiScanPostProcessPass Owner;
        private readonly TextureSlot _source = TextureSlot.Read("Glitch Source", BuiltinTexture.ActiveColor);
        private readonly TextureSlot _depth = TextureSlot.Read("Glitch Depth", BuiltinTexture.CameraDepthTexture);
        private Material _material;
        private Shader _shader;
        private bool _missingShaderLogged;
        protected abstract int Program { get; }
        protected ScanGlitchNode(TsukuyomiScanPostProcessPass owner) => Owner = owner;

        internal bool HasEffect(Camera camera)
        {
            if (!Owner.TryGetActive(camera, out var p)) return false;
            var effects = Owner.GetEffects(camera);
            if (Program == 0) return (effects & ScanPostEffect.VoxelGlitch) != 0 && p.VoxelStrength > 0f
                && p.VoxelTransitionWeight > 0f && (p.Coverage.Mode == ScanCoverageMode.Full || p.Coverage.Progress < .9999f);
            return p.GlitchStrength > 0f && (((effects & ScanPostEffect.HorizontalGlitch) != 0 && Mathf.Abs(p.HorizontalGlitch) > .00001f)
                || ((effects & ScanPostEffect.ChromaticSplit) != 0 && Mathf.Abs(p.ChromaticSplit) > .00001f));
        }

        internal bool Configure(Camera camera)
        {
            if (!HasEffect(camera) || !TsukuyomiRenderPipelineResourcesProvider.TryGet(out var resources)) return false;
            var shader = resources.ScanGlitchShader;
            if (!shader)
            {
                if (!_missingShaderLogged) Debug.LogError("Scan post processing requires ScanGlitchShader.");
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

        public override bool IsActive(in FrameContext frame) => Enabled && HasEffect(frame.CameraData.camera);
        public override void CollectTextureSlots(List<TextureSlot> slots) { slots.Add(_source); if (Program == 0) slots.Add(_depth); }
        private sealed class Data
        {
            public Material Material;
            public TextureHandle Source;
            public int Program;
            public Vector4 Origin, Curves, Glitch, Grid, Mode;
        }

        public override void RecordGraph(in FeatureGraphContext graph)
        {
            if (!_material || graph.Resources.IsActiveTargetBackBuffer || !Owner.TryGetActive(graph.CameraData.camera, out var p)) return;
            var source = graph.GetTexture(_source); if (!source.IsValid()) return;
            var depth = graph.GetTexture(_depth); if (Program == 0 && !depth.IsValid()) return;
            var outputDesc = TextureDescriptors.ColorLike(graph.RenderGraph.GetTextureDesc(source), Program == 0 ? "ScanVoxelOutput" : "ScanHorizontalOutput");
            TextureHandle output;
            using (var node = graph.AddRaster<Data>(Name))
            {
                node.Data.Source = node.ReadTexture(source); if (Program == 0) node.ReadTexture(depth);
                output = node.ColorAttachment(outputDesc, access: AccessFlags.WriteAll);
                node.Data.Material = _material; node.Data.Program = Program;
                node.Data.Origin = ScanEffectShaderData.Origin(p.Coverage);
                node.Data.Curves = ScanEffectShaderData.Curves(p.Coverage, p.Reveal);
                var effects = Owner.GetEffects(graph.CameraData.camera);
                node.Data.Glitch = new Vector4((effects & ScanPostEffect.HorizontalGlitch) != 0 ? p.HorizontalGlitch : 0f,
                    (effects & ScanPostEffect.ChromaticSplit) != 0 ? p.ChromaticSplit : 0f, p.Random01, Mathf.Max(0f, p.GlitchStrength));
                node.Data.Grid = new Vector4(Mathf.Max(.01f, p.CellSize), Mathf.Max(.01f, p.Coverage.MaxRange),
                    Mathf.Max(0f, p.VoxelStrength), Mathf.Max(.01f, p.Coverage.RingWidth));
                node.Data.Mode = new Vector4((float)p.Coverage.Mode, Mathf.Clamp01(p.VoxelTransitionWeight), 0f, 0f);
                node.SetRenderFunc(static (data, context) =>
                {
                    data.Material.SetVector("_ScanOriginProgress", data.Origin); data.Material.SetVector("_ScanCurveParams", data.Curves);
                    data.Material.SetVector("_ScanGlitchParams", data.Glitch); data.Material.SetVector("_ScanGlitchGrid", data.Grid);
                    data.Material.SetVector("_ScanGlitchMode", data.Mode);
                    Blitter.BlitTexture(context.cmd, data.Source, new Vector4(1, 1, 0, 0), data.Material, data.Program);
                });
            }
            graph.SetActiveColor(output);
        }

        public void Dispose() { if (_material) CoreUtils.Destroy(_material); _material = null; _shader = null; }
    }

    internal sealed class ScanVoxelGlitchNode : ScanGlitchNode
    {
        public ScanVoxelGlitchNode(TsukuyomiScanPostProcessPass owner) : base(owner) { }
        protected override int Program => 0;
        public override string Name => "Tsukuyomi Scene Scan Voxel Glitch";
        public override int Priority => 10;
    }

    internal sealed class ScanChannelGlitchNode : ScanGlitchNode
    {
        public ScanChannelGlitchNode(TsukuyomiScanPostProcessPass owner) : base(owner) { }
        protected override int Program => 1;
        public override string Name => "Tsukuyomi Scene Scan Horizontal Glitch / Chromatic Split";
    }
}
