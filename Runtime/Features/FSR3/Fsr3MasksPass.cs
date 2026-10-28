using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering
{
    [Serializable, InjectionPoint(InjectionPoint.BeforePostProcess)]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Tsukuyomi.Rendering", "Tsukuyomi.Rendering", "Fsr3MasksPlugin")]
    public sealed class Fsr3MasksPass : RenderPassBase
    {
        public Fsr3MasksPass() => InjectionPoint = InjectionPoint.BeforePostProcess;
        public override string Name => "FSR3 Input Masks";
#if ENABLE_UPSCALER_FRAMEWORK
        [NonSerialized] private TsukuyomiFsr3OpaqueOnlyCapturePass _capture;
        [NonSerialized] private TsukuyomiFsr3ManualMaskPass _masks;
        [NonSerialized] private readonly List<IRenderMaskViewProvider> _providers = new();
        [NonSerialized] private bool _active, _manual;
        public override void Build(PassPlanBuilder plan)
        {
            _capture = new TsukuyomiFsr3OpaqueOnlyCapturePass();
            _masks = new TsukuyomiFsr3ManualMaskPass();
            var capture = plan.AddNative(_capture, RenderPassEvent.BeforeRenderingTransparents, () => _active);
            plan.After(plan.AddNative(_masks, RenderPassEvent.BeforeRenderingPostProcessing - 1, () => _manual), capture);
        }
        public override void PrepareRecording(in PassCameraContext context, ref RenderingData data)
        {
            _active = TsukuyomiFsr3MaskSupport.IsEnabled(context.Frame.CameraData);
            _manual = TsukuyomiFsr3MaskSupport.IsEnabled(context.Frame.CameraData, true);
            context.Collect(_providers); _masks.Configure(_providers);
        }
        public override void Deactivate(Camera camera) => _active = _manual = false;
#else
        public override void Build(PassPlanBuilder plan) { }
#endif
    }
}
