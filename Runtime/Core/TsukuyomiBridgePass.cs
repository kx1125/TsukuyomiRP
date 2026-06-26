using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Reflection;

namespace Tsukuyomi.Rendering
{
    // Bridges Tsukuyomi passes into URP's native RecordRenderGraph flow.
    public class TsukuyomiBridgePass : ScriptableRenderPass
    {
        private readonly PassRegistry _registry;
        private readonly InjectionPoint _injectionPoint;
        private readonly ResourceHub _resourceHub;

        public TsukuyomiBridgePass(PassRegistry registry, InjectionPoint injectionPoint, ResourceHub resourceHub)
        {
            _registry = registry;
            _injectionPoint = injectionPoint;
            _resourceHub = resourceHub;
        }

        public void ConfigureInputFromTextureSlots()
        {
            ScriptableRenderPassInput inputs = ScriptableRenderPassInput.None;

            foreach (var pass in _registry.GetPasses(_injectionPoint))
            {
                foreach (TextureSlot slot in EnumerateTextureSlots(pass))
                {
                    inputs |= ToRenderPassInput(slot);
                }
            }

            ConfigureInput(inputs);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var passes = _registry.GetPasses(_injectionPoint);
            var frameContext = new FrameContext(frameData, _resourceHub);
            var resourceData = frameData.Get<UniversalResourceData>();
            var registry = frameData.GetOrCreate<TsukuyomiFrameResourceRegistry>();
            var frameResources = new FrameResources(resourceData, registry);

            var cameraData = frameData.Get<UniversalCameraData>();
            var lightData = frameData.Get<UniversalLightData>();

            foreach (var pass in passes)
            {
                if (!pass.IsActive(frameContext)) continue;

                pass.Setup(frameContext);

                if (pass is RasterPass rasterPass)
                {
                    using var builder = renderGraph.AddRasterRenderPass(pass.Name, out TsukuyomiPassData data);
                    var context = new RasterPassContext(renderGraph, builder, frameData, cameraData, lightData, frameResources, data, _resourceHub);
                    rasterPass.Record(context);
                }
                else if (pass is ComputePass computePass)
                {
                    using var builder = renderGraph.AddComputePass(pass.Name, out TsukuyomiPassData data);
                    var context = new ComputePassContext(renderGraph, builder, frameData, cameraData, lightData, frameResources, data, _resourceHub);
                    computePass.Record(context);
                }
                else if (pass is UnsafePass unsafePass)
                {
                    using var builder = renderGraph.AddUnsafePass(pass.Name, out TsukuyomiPassData data);
                    var context = new UnsafePassContext(renderGraph, builder, frameData, cameraData, lightData, frameResources, data, _resourceHub);
                    unsafePass.Record(context);
                }
                else if (pass is PostPass postPass)
                {
                    if (frameResources.ActiveColor.IsValid())
                    {
                        using var builder = renderGraph.AddRasterRenderPass(pass.Name, out TsukuyomiPassData data);
                        var context = new PostPassContext(renderGraph, builder, frameData, cameraData, frameResources, data, _resourceHub);
                        var newColor = postPass.RecordPost(context, frameResources.ActiveColor);

                        if (newColor.IsValid() && newColor != frameResources.ActiveColor)
                        {
                            PassRecorder.SwapActiveColor(frameResources, newColor);
                        }
                    }
                }
            }
        }

        private static IEnumerable<TextureSlot> EnumerateTextureSlots(RenderPassBase pass)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var type = pass.GetType();

            foreach (var field in type.GetFields(flags))
            {
                if (field.FieldType == typeof(TextureSlot))
                    yield return (TextureSlot)field.GetValue(pass);
            }

            foreach (var property in type.GetProperties(flags))
            {
                if (property.PropertyType != typeof(TextureSlot) || property.GetIndexParameters().Length > 0)
                    continue;

                yield return (TextureSlot)property.GetValue(pass);
            }
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
                BuiltinTexture.OpaqueTexture or BuiltinTexture.CameraColorTexture => ScriptableRenderPassInput.Color,
                _ => ScriptableRenderPassInput.None
            };
        }
    }
}


