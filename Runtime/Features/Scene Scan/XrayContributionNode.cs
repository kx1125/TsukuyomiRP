using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Tsukuyomi.Rendering
{
    internal sealed class XrayContributionNode : GraphFeaturePass, System.IDisposable
    {
        private readonly TsukuyomiXrayRenderPass _owner;
        private readonly TextureSlot _depth = TextureSlot.Read("Xray Depth", BuiltinTexture.CameraDepthTexture);
        private Material _material;
        private Shader _shader;
        private bool _missingShaderLogged;
        public XrayContributionNode(TsukuyomiXrayRenderPass owner) => _owner = owner;
        public override string Name => "Tsukuyomi Xray Color Contribution";
        public override bool RequiresIntermediateColorTarget => true;
        public override bool IsActive(in FrameContext frame) => Enabled && Prepared && _owner.HasHighlight(frame.CameraData.camera);
        public override void CollectTextureSlots(List<TextureSlot> slots) => slots.Add(_depth);

        internal bool Prepared { get; private set; }
        public override void PrepareRecording(in PassCameraContext context, ref RenderingData data)
            => Prepared = !context.Frame.CameraData.isPreviewCamera && Configure(context.Camera);
        public override void Deactivate(Camera camera) => Prepared = false;
        private bool Configure(Camera camera)
        {
            if (!_owner.HasHighlight(camera) || !TsukuyomiRenderPipelineResourcesProvider.TryGet(out var resources)) return false;
            var shader = resources.XrayCompositeShader;
            if (!shader)
            {
                if (!_missingShaderLogged) Debug.LogError("XrayRender requires XrayCompositeShader.");
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
            public TextureHandle Target, Source;
            public Vector4 Origin, Range, Curves, Primary, Secondary;
            public float Mode, Intensity;
        }

        public override void RecordGraph(in FeatureGraphContext graph)
        {
            if (!_material || !_owner.TryGetActiveParameters(graph.CameraData.camera, out var p)) return;
            var depth = graph.GetTexture(_depth);
            var target = graph.Resources.GetTexture(TsukuyomiXrayRenderPass.TargetMaskName);
            var source = graph.Resources.ActiveColor;
            if (!depth.IsValid() || !target.IsValid() || !source.IsValid()) return;
            var desc = ScanEffectShaderData.ColorDesc(graph, source, "XrayColorContribution", GraphicsFormat.R32G32B32A32_SFloat);
            TextureHandle contribution;
            using (var node = graph.AddRaster<Data>(Name))
            {
                node.ReadTexture(depth); node.Data.Target = node.ReadTexture(target);
                node.Data.Source = node.ReadTexture(graph.RenderGraph.defaultResources.blackTexture);
                contribution = node.ColorAttachment(desc, access: AccessFlags.WriteAll);
                node.Data.Material = _material; node.Data.Origin = ScanEffectShaderData.Origin(p.Coverage);
                node.Data.Range = ScanEffectShaderData.Range(p.Coverage, p.CellSize);
                node.Data.Curves = ScanEffectShaderData.Curves(p.Coverage);
                node.Data.Primary = p.PrimaryColor; node.Data.Secondary = p.SecondaryColor;
                node.Data.Mode = (float)p.Coverage.Mode; node.Data.Intensity = Mathf.Max(0f, p.Intensity);
                node.SetRenderFunc(static (data, context) =>
                {
                    var m = data.Material;
                    m.SetTexture("_ScanTargetMask", data.Target); m.SetVector("_ScanOriginProgress", data.Origin);
                    m.SetVector("_ScanGridRange", data.Range); m.SetVector("_ScanCurveParams", data.Curves);
                    m.SetVector("_XrayPrimaryColor", data.Primary); m.SetVector("_XraySecondaryColor", data.Secondary);
                    m.SetFloat("_ScanCoverageMode", data.Mode); m.SetFloat("_XrayIntensity", data.Intensity);
                    Blitter.BlitTexture(context.cmd, data.Source, new Vector4(1, 1, 0, 0), m, 0);
                });
            }
            var frame = graph.FrameData.GetOrCreate<ScanEffectFrameResources>();
            frame.TargetContribution = contribution; frame.XrayCoverage = p.Coverage;
        }

        public override void Dispose() { if (_material) CoreUtils.Destroy(_material); _material = null; _shader = null; }
    }
}
