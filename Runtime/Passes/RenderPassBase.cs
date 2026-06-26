
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

        public virtual int Priority => 0;

        public virtual bool IsActive(in FrameContext frame) => Enabled;
        public virtual void Setup(in FrameContext frame) { }
    }
}
