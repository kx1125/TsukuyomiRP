
using UnityEngine;

namespace Tsukuyomi.Rendering
{
    [System.Serializable]
    public abstract class RenderPassBase
    {
        public bool Enabled = true;
        public abstract string Name { get; }
        
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

        public virtual int Priority => 0;

        /// <summary>Declare derived resources after resolving this camera's settings; do not read graph handles here.</summary>
        public virtual void CollectResourceRequirements(in FrameContext frame, in ResourceRequirementCollector requirements) { }

        public virtual bool IsActive(in FrameContext frame) => Enabled;
        public virtual void Setup(in FrameContext frame) { }
        public virtual void ValidateSettings() { }
    }
}
