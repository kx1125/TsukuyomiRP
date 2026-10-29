using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Tsukuyomi.Rendering
{
    internal sealed class TsukuyomiMultiScaleAmbientOcclusionCompositePass : UnsafePass
    {
        private readonly TsukuyomiMultiScaleAmbientOcclusionPass _source;
        private readonly TextureSlot _color = TextureSlot.ReadWrite("Color", BuiltinTexture.ActiveColor);
        private Material _ownedMaterial, _material;
        private static readonly int Occlusion = Shader.PropertyToID("_MsvoOcclusionTexture");
        private static readonly int OcclusionColor = Shader.PropertyToID("_MsvoOcclusionColor");
        public override bool RequiresIntermediateColorTarget => true;
        public override string Name => "MSVO Color Composite / Debug";
        public TsukuyomiMultiScaleAmbientOcclusionCompositePass(TsukuyomiMultiScaleAmbientOcclusionPass source) => _source = source;
        public override void CollectTextureSlots(List<TextureSlot> slots) => slots.Add(_color);

        public bool Configure()
        {
            if (!TsukuyomiRenderPipelineResourcesProvider.TryGet(out var resources) || !resources.MsvoCompositeShader) return false;
            if (!_ownedMaterial || _ownedMaterial.shader != resources.MsvoCompositeShader)
            {
                CoreUtils.Destroy(_ownedMaterial);
                _ownedMaterial = CoreUtils.CreateEngineMaterial(resources.MsvoCompositeShader);
            }
            _material = _ownedMaterial;
            return true;
        }

        public override bool IsActive(in FrameContext frame)
            => base.IsActive(frame) && _source.IsActive(frame) && _material
                && (!_source.Settings.IsLighting || _source.Settings.DebugOcclusion);

        private sealed class RenderData
        {
            public readonly MaterialPropertyBlock Properties = new();
            public TextureHandle Occlusion, Color;
            public Material Material;
            public Color Tint;
            public bool Debug;
            public int Width, Height;
        }

        public override void Record(in UnsafePassContext context)
        {
            var output = context.Resources.GetTexture(TsukuyomiMultiScaleAmbientOcclusionPass.OutputName);
            var color = context.Resources.ActiveColor;
            if (!output.IsValid() || !color.IsValid()) return;
            var state = context.GetOrCreateData<RenderData>();
            state.Occlusion = context.ReadTexture(output);
            state.Color = color;
            state.Material = _material;
            state.Tint = _source.Settings.OcclusionColor;
            state.Debug = _source.Settings.DebugOcclusion;
            state.Width = context.CameraData.cameraTargetDescriptor.width;
            state.Height = context.CameraData.cameraTargetDescriptor.height;
            context.Builder.UseTexture(color, AccessFlags.ReadWrite);
            context.Builder.AllowPassCulling(false);
            context.SetRenderFunc(state, static (data, graphContext) =>
            {
                var cmd = CommandBufferHelpers.GetNativeCommandBuffer(graphContext.cmd);
                RTHandle ao = data.Occlusion;
                data.Properties.Clear();
                data.Properties.SetTexture(Occlusion, ao.rt);
                data.Properties.SetVector(OcclusionColor, new Vector4(data.Tint.r, data.Tint.g, data.Tint.b, 1));
                graphContext.cmd.SetRenderTarget(data.Color);
                cmd.SetViewport(new Rect(0, 0, data.Width, data.Height));
                cmd.DrawProcedural(Matrix4x4.identity, data.Material, data.Debug ? 1 : 0, MeshTopology.Triangles, 3, 1, data.Properties);
            });
        }

        public override void Dispose() { CoreUtils.Destroy(_ownedMaterial); _ownedMaterial = null; _material = null; }
    }
}
