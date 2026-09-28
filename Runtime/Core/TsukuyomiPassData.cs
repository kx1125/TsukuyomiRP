using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine;

namespace Tsukuyomi.Rendering
{
    /// <summary>
    /// Unified data class for all Tsukuyomi RenderGraph passes.
    /// Pooled by RenderGraph; reset before recording so skipped passes cannot reuse stale data.
    /// </summary>
    public class TsukuyomiPassData
    {
        public TextureHandle source;
        public TextureHandle destination;
        public BufferHandle buffer;
        public RendererListHandle rendererList;
        
        public Material material;
        public int passIndex;
        public Vector4 parameters;

        private System.Collections.Generic.Dictionary<System.Type, object> _recordData;
        private System.Type _primaryDataType;
        private object _primaryData;

        internal object RenderData;
        internal System.Delegate RenderFunction;
        internal UnityEngine.Rendering.ProfilingSampler RenderSampler;

        // Data belongs to this pooled graph pass, so pending passes/cameras never share it.
        // Callers must overwrite every field they read, including optional branch state.
        public T GetOrCreateData<T>() where T : class, new()
        {
            // Most passes use one snapshot type. Keep that path free of dictionary lookups.
            if (_primaryDataType == typeof(T))
                return (T)_primaryData;
            if (_primaryData == null)
            {
                var data = new T();
                _primaryData = data;
                _primaryDataType = typeof(T);
                return data;
            }

            _recordData ??= new System.Collections.Generic.Dictionary<System.Type, object>();
            if (!_recordData.TryGetValue(typeof(T), out object value))
            {
                value = new T();
                _recordData.Add(typeof(T), value);
            }
            return (T)value;
        }

        public bool HasRenderFunction { get; internal set; }

        internal void Reset()
        {
            source = destination = default;
            buffer = default;
            rendererList = default;
            material = null;
            passIndex = 0;
            parameters = default;
            HasRenderFunction = false;
            RenderData = null;
            RenderFunction = null;
            RenderSampler = null;
        }
    }
}
