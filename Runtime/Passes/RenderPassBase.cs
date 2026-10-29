
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering
{
    [System.Serializable]
    public abstract class RenderPassBase : IRenderPassRuntime
    {
        [SerializeField, HideInInspector] private string _nodeId = System.Guid.NewGuid().ToString("N");
        public string NodeId => _nodeId;
        public void RenewNodeId() => _nodeId = System.Guid.NewGuid().ToString("N");
        public void EnsureNodeId() { if (string.IsNullOrEmpty(_nodeId)) RenewNodeId(); }
        public bool Enabled = true;
        public abstract string Name { get; }
        public virtual bool AllowMultiple => false;
        public virtual string MenuPath => Name;

        // A Profile stores the template; PassSession executes its private serialized clone.
        public virtual IRenderPassRuntime CreateRuntime() => this;
        public virtual void Build(PassPlanBuilder plan) => plan.Add(this, PassPlanBuilder.EventFor(InjectionPoint));
        public virtual void Initialize() { }
        /// <summary>Resolve this camera's settings before activity checks, resource requests and recording.</summary>
        public virtual void PrepareRecording(in PassCameraContext context, ref RenderingData renderingData) { }
        void IRenderPassRuntime.Prepare(in PassCameraContext context, ref RenderingData renderingData)
            => PrepareRecording(context, ref renderingData);
        public virtual void ResolveResources(in PassCameraContext context, ref RenderingData renderingData) { }
        public virtual void Deactivate(Camera camera) { }
        public virtual void ReleaseCamera(Camera camera) { }
        public virtual void Dispose() { }

        /// <summary>Request an intermediate color attachment even when no color texture is sampled.</summary>
        public virtual bool RequiresIntermediateColorTarget => false;
        
        [SerializeField, HideInInspector]
        private InjectionPoint _injectionPoint;
        public InjectionPoint InjectionPoint 
        { 
            get => _injectionPoint; 
            set => _injectionPoint = value; 
        }

        // Override for allocation-free discovery. Read current values on every call.
        // Reflection remains a compatibility fallback for third-party passes.
        public virtual void CollectTextureSlots(System.Collections.Generic.List<TextureSlot> slots)
            => TextureSlotMetadata.Collect(this, slots);


        /// <summary>Declare derived resources after resolving this camera's settings; do not read graph handles here.</summary>
        public virtual void CollectResourceRequirements(in FrameContext frame, in ResourceRequirementCollector requirements) { }

        public virtual bool IsActive(in FrameContext frame) => Enabled;
        public virtual void Setup(in FrameContext frame) { }
        public virtual void ValidateSettings() { }
    }
}
