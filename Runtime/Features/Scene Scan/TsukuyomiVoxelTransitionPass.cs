using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Tsukuyomi.Rendering
{
    [System.Serializable]
    public sealed class TsukuyomiVoxelTransitionPass : CameraEffectPass<VoxelTransitionParameters>, System.IDisposable
    {
        private readonly TextureSlot _source = TextureSlot.Read("Voxel Transition Source", BuiltinTexture.ActiveColor);
        private readonly TextureSlot _depth = TextureSlot.Read("Voxel Transition Depth", BuiltinTexture.CameraDepthTexture);
        private Material _material;
        private Shader _shader;
        private bool _missingShaderLogged;
        public override string Name => "Tsukuyomi Voxel Transition";
        public override int Priority => 500;
        protected override VoxelTransitionParameters DefaultParameters => VoxelTransitionParameters.Default;
        protected override bool ResolveSettings(TsukuyomiPipelineProfile profile, UnityEngine.Rendering.VolumeStack stack, out VoxelTransitionParameters parameters)
            => SceneEffectSettings.Resolve(profile, stack, out parameters);
        protected override void ApplyAnimation(ref VoxelTransitionParameters parameters, in ScanAnimationParameters animation)
        {
            animation.Apply(ref parameters.Coverage);
            parameters.SamplingCompleted = animation.SamplingCompleted;
        }

        public override void CollectTextureSlots(List<TextureSlot> slots) { slots.Add(_source); slots.Add(_depth); }
        public override bool IsActive(in FrameContext frame) => TryGetActiveParameters(frame.CameraData.camera, out var p) && p.Coverage.Blend > 0f;

        internal bool Configure(Camera camera)
        {
            if (!TryGetActiveParameters(camera, out var p) || p.Coverage.Blend <= 0f) return false;
            if (!TsukuyomiRenderPipelineResourcesProvider.TryGet(out var resources)) return false;
            var shader = resources.VoxelTransitionShader;
            if (!shader)
            {
                if (!_missingShaderLogged) Debug.LogError("VoxelTransition requires VoxelTransitionShader in TsukuyomiRenderPipelineResources.");
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
            public TextureHandle Source;
            public Vector4 Origin, Range, Curves, Transition;
        }

        public override void RecordGraph(in FeatureGraphContext graph)
        {
            if (!_material || !TryGetActiveParameters(graph.CameraData.camera, out var p)) return;
            var source = graph.GetTexture(_source); var depth = graph.GetTexture(_depth);
            if (!source.IsValid() || !depth.IsValid() || graph.Resources.IsActiveTargetBackBuffer) return;
            var sampledDesc = ScanEffectShaderData.ColorDesc(graph, source, "VoxelTransitionSampled", GraphicsFormat.R32G32B32A32_SFloat);
            var distortedDesc = ScanEffectShaderData.ColorDesc(graph, source, "VoxelTransitionDistorted", GraphicsFormat.R32G32B32A32_SFloat);
            var maskDesc = ScanEffectShaderData.ColorDesc(graph, source, "VoxelTransitionScreenMask", GraphicsFormat.R32_SFloat);
            TextureHandle sampled, distorted, mask;
            using (var node = graph.AddRaster<Data>(Name))
            {
                node.Data.Source = node.ReadTexture(source); node.ReadTexture(depth);
                sampled = node.ColorAttachment(sampledDesc, 0, AccessFlags.WriteAll);
                distorted = node.ColorAttachment(distortedDesc, 1, AccessFlags.WriteAll);
                mask = node.ColorAttachment(maskDesc, 2, AccessFlags.WriteAll);
                node.Data.Material = _material;
                node.Data.Origin = ScanEffectShaderData.Origin(p.Coverage);
                node.Data.Range = ScanEffectShaderData.Range(p.Coverage, p.CellSize);
                node.Data.Curves = ScanEffectShaderData.Curves(p.Coverage);
                node.Data.Transition = new Vector4(Mathf.Clamp01(p.SamplingStrength), p.SamplingCompleted ? 1f : 0f,
                    Mathf.Clamp01(p.DistortionStrength), 0f);
                node.SetRenderFunc(static (data, context) =>
                {
                    data.Material.SetVector("_ScanOriginProgress", data.Origin);
                    data.Material.SetVector("_ScanGridRange", data.Range);
                    data.Material.SetVector("_ScanCurveParams", data.Curves);
                    data.Material.SetVector("_ScanTransitionParams", data.Transition);
                    Blitter.BlitTexture(context.cmd, data.Source, new Vector4(1, 1, 0, 0), data.Material, 0);
                });
            }
            var frame = graph.FrameData.GetOrCreate<ScanEffectFrameResources>();
            frame.OriginalColor = source; frame.SampledColor = sampled; frame.DistortedColor = distorted;
            frame.ScreenMask = mask; frame.OpeningActive = true; frame.OpeningCoverage = p.Coverage;
        }

        public void Dispose()
        {
            if (_material) CoreUtils.Destroy(_material);
            _material = null; _shader = null; ClearAllCameras();
        }
    }
}
