using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine;

using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering
{
    // Bridges Tsukuyomi passes into URP's native RecordRenderGraph flow.
    public class TsukuyomiBridgePass : ScriptableRenderPass
    {
        private readonly System.Collections.Generic.List<TextureSlot> _textureSlots = new();
        private readonly PassRegistry _registry;
        private readonly InjectionPoint _injectionPoint;
        private readonly ResourceHub _resourceHub;

        public TsukuyomiBridgePass(PassRegistry registry, InjectionPoint injectionPoint, ResourceHub resourceHub)
        {
            _registry = registry;
            _injectionPoint = injectionPoint;
            _resourceHub = resourceHub;
        }

        public bool ConfigureInputFromTextureSlots(FrameContext? frame = null)
        {
            ScriptableRenderPassInput inputs = ScriptableRenderPassInput.None;
            bool hasPasses = false;
            requiresIntermediateTexture = false;

            var passes = _registry.GetPasses(_injectionPoint);
            for (int i = 0; i < passes.Count; i++)
            {
                RenderPassBase pass = passes[i];
                if (frame.HasValue && !pass.IsActive(frame.Value))
                    continue;
                if (frame.HasValue && !frame.Value.URPFrameData.GetOrCreate<ResourceRequirements>().CanRecord(pass))
                    continue;
                hasPasses = true;
                _textureSlots.Clear();
                pass.CollectTextureSlots(_textureSlots);
                foreach (TextureSlot slot in _textureSlots)
                {
                    inputs |= ToRenderPassInput(slot);
                    requiresIntermediateTexture |= slot.RequiresIntermediateColor;
                }
            }

            ConfigureInput(inputs);
            return hasPasses;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var passes = _registry.GetPasses(_injectionPoint);
            var cameraData = frameData.Get<UniversalCameraData>();
            ResourceHub cameraResources = cameraData.camera ? _resourceHub?.ForCamera(cameraData.camera) : _resourceHub;
            var frameContext = new FrameContext(frameData, cameraResources);
            var resourceData = frameData.Get<UniversalResourceData>();
            var registry = frameData.GetOrCreate<TsukuyomiFrameResourceRegistry>();
            var frameResources = registry.GetFrameResources(resourceData);
            TextureHandle originalColor = frameResources.ActiveColor;

            var lightData = frameData.Get<UniversalLightData>();

            for (int i = 0; i < passes.Count; i++)
            {
                RenderPassBase pass = passes[i];
                if (!pass.IsActive(frameContext)) continue;
                if (!frameData.GetOrCreate<ResourceRequirements>().CanRecord(pass)) continue;
                if (frameResources.IsActiveTargetBackBuffer && SamplesCameraColor(pass)) continue;

                pass.Setup(frameContext);

                if (pass is GraphFeaturePass graphPass)
                {
                    var context = new FeatureGraphContext(renderGraph, frameData, cameraData, lightData, frameResources, cameraResources);
                    graphPass.RecordGraph(context);
                }
                else if (pass is RasterPass rasterPass)
                {
                    using var builder = renderGraph.AddRasterRenderPass(pass.Name, out TsukuyomiPassData data);
                    var context = new RasterPassContext(renderGraph, builder, frameData, cameraData, lightData, frameResources, data, cameraResources);
                    rasterPass.Record(context);
                }
                else if (pass is ComputePass computePass)
                {
                    using var builder = renderGraph.AddComputePass(pass.Name, out TsukuyomiPassData data);
                    var context = new ComputePassContext(renderGraph, builder, frameData, cameraData, lightData, frameResources, data, cameraResources);
                    computePass.Record(context);
                }
                else if (pass is UnsafePass unsafePass)
                {
                    using var builder = renderGraph.AddUnsafePass(pass.Name, out TsukuyomiPassData data);
                    var context = new UnsafePassContext(renderGraph, builder, frameData, cameraData, lightData, frameResources, data, cameraResources);
                    unsafePass.Record(context);
                }
                else if (pass is PostPass postPass)
                {
                    if (frameResources.ActiveColor.IsValid())
                    {
                        using var builder = renderGraph.AddRasterRenderPass(pass.Name, out TsukuyomiPassData data);
                        var context = new PostPassContext(renderGraph, builder, frameData, cameraData, frameResources, data, cameraResources);
                        var newColor = postPass.RecordPost(context, frameResources.ActiveColor);

                        if (newColor.IsValid() && newColor != frameResources.ActiveColor)
                        {
                            PassRecorder.SwapActiveColor(frameResources, newColor);
                        }
                    }
                }
            }
            // The next camera imports URP's persistent stack target, not our transient replacement.
            if (!cameraData.resolveFinalTarget && originalColor.IsValid()
                && frameResources.ActiveColor != originalColor)
            {
                renderGraph.AddBlitPass(frameResources.ActiveColor, originalColor, Vector2.one, Vector2.zero,
                    passName: "Tsukuyomi Preserve Camera Stack Color");
                frameResources.SetActiveColor(originalColor);
            }
        }

        private bool SamplesCameraColor(RenderPassBase pass)
        {
            _textureSlots.Clear();
            pass.CollectTextureSlots(_textureSlots);
            foreach (TextureSlot slot in _textureSlots)
                if (slot.RequiresIntermediateColor)
                    return true;
            return false;
        }

        internal void CollectResourceRequirements(ResourceRequirements requirements, in FrameContext frame)
        {
            var passes = _registry.GetPasses(_injectionPoint);
            for (int i = 0; i < passes.Count; i++)
                requirements.Collect(passes[i], renderPassEvent, frame);
        }

        private static ScriptableRenderPassInput ToRenderPassInput(TextureSlot slot)
        {
            if (slot.Access == ResourceAccess.Write)
                return ScriptableRenderPassInput.None;

            return slot.Builtin switch
            {
                BuiltinTexture.CameraDepthTexture or BuiltinTexture.CameraDepthAttachment or BuiltinTexture.ActiveDepth => ScriptableRenderPassInput.Depth,
                BuiltinTexture.CameraNormals => ScriptableRenderPassInput.Normal,
                BuiltinTexture.MotionVectorColor or BuiltinTexture.MotionVectorDepth => ScriptableRenderPassInput.Motion,
                BuiltinTexture.OpaqueTexture => ScriptableRenderPassInput.Color,
                _ => ScriptableRenderPassInput.None
            };
        }
    }
}
