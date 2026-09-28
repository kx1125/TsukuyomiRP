using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering
{
    /// <summary>Recording-only view. Close each node before opening another or publishing its output.</summary>
    public readonly ref struct FeatureGraphContext
    {
        public readonly RenderGraph RenderGraph;
        public readonly ContextContainer FrameData;
        public readonly UniversalCameraData CameraData;
        public readonly UniversalLightData LightData;
        public readonly FrameResources Resources;
        public readonly ResourceHub ResourceHub;

        public FeatureGraphContext(RenderGraph renderGraph, ContextContainer frameData,
            UniversalCameraData cameraData, UniversalLightData lightData, FrameResources resources,
            ResourceHub resourceHub = null)
        {
            RenderGraph = renderGraph;
            FrameData = frameData;
            CameraData = cameraData;
            LightData = lightData;
            Resources = resources;
            ResourceHub = resourceHub;
        }

        public TextureHandle GetTexture(in TextureSlot slot) => PassRecorder.ResolveTexture(RenderGraph, slot, Resources);
        public BufferHandle GetBuffer(in BufferSlot slot) => PassRecorder.ResolveBuffer(RenderGraph, slot, Resources);
        public TextureHandle CreateTexture(in TextureDesc desc) => RenderGraph.CreateTexture(desc);

        public RasterGraphNode<T> AddRaster<T>(string name, ProfilingSampler sampler = null) where T : class, new()
            => new(RenderGraph, Resources, name, sampler);
        public ComputeGraphNode<T> AddCompute<T>(string name, ProfilingSampler sampler = null) where T : class, new()
            => new(RenderGraph, Resources, name, sampler);
        public UnsafeGraphNode<T> AddUnsafe<T>(string name, ProfilingSampler sampler = null) where T : class, new()
            => new(RenderGraph, Resources, name, sampler);

        public void SetActiveColor(TextureHandle output)
        {
            if (!Resources.IsActiveTargetBackBuffer) PassRecorder.SwapActiveColor(Resources, output);
        }

        private sealed class FullscreenData
        {
            public TextureHandle Source;
            public Material Material;
            public int ShaderPass;
            public bool Bilinear;
        }

        /// <summary>Creates a color target with the source layout. Material parameters must remain stable until execution.</summary>
        public TextureHandle AddFullscreen(TextureHandle source, Material material, int shaderPass = 0,
            ProfilingSampler sampler = null, string name = "Tsukuyomi Fullscreen")
        {
            if (!CanSample(source) || material == null || shaderPass < 0 || shaderPass >= material.passCount)
                return source;
            var desc = TextureDescriptors.ColorLike(RenderGraph.GetTextureDesc(source), name);
            using var node = AddRaster<FullscreenData>(name, sampler);
            node.Data.Source = node.ReadTexture(source);
            node.Data.Material = material;
            node.Data.ShaderPass = shaderPass;
            var output = node.ColorAttachment(desc, access: AccessFlags.WriteAll);
            node.SetRenderFunc(static (data, context) =>
                Blitter.BlitTexture(context.cmd, data.Source, new Vector4(1, 1, 0, 0), data.Material, data.ShaderPass));
            return output;
        }

        /// <summary>Copies sampled color to a new single-sample target with the source layout.</summary>
        public TextureHandle Copy(TextureHandle source, bool bilinear = false,
            ProfilingSampler sampler = null, string name = "Tsukuyomi Copy")
        {
            if (!CanSample(source)) return source;
            var desc = TextureDescriptors.ColorLike(RenderGraph.GetTextureDesc(source), name);
            using var node = AddRaster<FullscreenData>(name, sampler);
            node.Data.Source = node.ReadTexture(source);
            node.Data.Bilinear = bilinear;
            var output = node.ColorAttachment(desc, access: AccessFlags.WriteAll);
            node.SetRenderFunc(static (data, context) =>
                Blitter.BlitTexture(context.cmd, data.Source, new Vector4(1, 1, 0, 0), 0, data.Bilinear));
            return output;
        }

        private bool CanSample(TextureHandle source)
        {
            if (!source.IsValid() || (Resources.IsActiveTargetBackBuffer && source == Resources.ActiveColor)) return false;
            var desc = RenderGraph.GetTextureDesc(source);
            // The standard Blitter samples 2D color (including XR arrays), not raw MSAA, depth or cubemaps.
            return desc.colorFormat != UnityEngine.Experimental.Rendering.GraphicsFormat.None && !desc.bindTextureMS
                && (desc.dimension == TextureDimension.Tex2D || desc.dimension == TextureDimension.Tex2DArray);
        }
    }
}
