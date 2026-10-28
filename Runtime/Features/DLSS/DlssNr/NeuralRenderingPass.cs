using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityRhi.Dlss.Urp;

namespace Tsukuyomi.Rendering
{
    [Serializable, InjectionPoint(InjectionPoint.AfterPostProcess)]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Tsukuyomi.Rendering", "Tsukuyomi.Rendering", "NeuralRenderingPlugin")]
    public sealed class NeuralRenderingPass : RenderPassBase
    {
        public bool FinalCameraInStackOnly = true;
        public NeuralRenderingPass() => InjectionPoint = InjectionPoint.AfterPostProcess;
        public override string Name => "DLSS Neural Rendering";
        [NonSerialized] private DlssNrBackend _backend;
        [NonSerialized] private bool _active;
        public override void Build(PassPlanBuilder plan)
            => plan.AddNative(new Dispatch(this), RenderPassEvent.AfterRenderingPostProcessing, () => _active);
        public override void Initialize()
        {
            _backend = new DlssNrBackend { finalCameraInStackOnly = FinalCameraInStackOnly };
            _backend.Initialize();
        }
        public override void PrepareRecording(in PassCameraContext context, ref RenderingData data)
            => _active = (!FinalCameraInStackOnly || context.Frame.CameraData.resolveFinalTarget)
                && _backend != null && _backend.Configure(ref data);
        public override void Deactivate(Camera camera) => _active = false;
        public override void ReleaseCamera(Camera camera) => _backend?.ReleaseCamera(camera);
        public override void Dispose()
        {
            var backend = _backend; _backend = null;
            if (backend != null) TsukuyomiUpscaling.RetireResources(backend.Dispose);
        }
        private sealed class Dispatch : ScriptableRenderPass
        {
            private readonly NeuralRenderingPass _owner;
            internal Dispatch(NeuralRenderingPass owner)
            { _owner = owner; ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Motion); requiresIntermediateTexture = true; }
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frame)
                => _owner._backend?.RuntimePass?.RecordRenderGraph(graph, frame);
        }
    }
}
