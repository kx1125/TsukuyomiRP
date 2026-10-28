using System.Collections.Generic;
using UnityEngine;

namespace Tsukuyomi.Rendering
{
    [CreateAssetMenu(menuName = "TsukuyomiRpP/Pipeline Profile", fileName = "TsukuyomiPipelineProfile")]
    public sealed class TsukuyomiPipelineProfile : ScriptableObject
    {
        // The custom Inspector draws each node. Hiding the list also hides its parameters from NextVisible.
        [SerializeReference] public List<RenderPassBase> Passes = new();
        [SerializeField, HideInInspector] public string GraphLayoutData;

        public const int CurrentSchemaVersion = 1;
        [SerializeField, HideInInspector] private int _schemaVersion = CurrentSchemaVersion;
        [SerializeField, HideInInspector] private int _revision;
        public int SchemaVersion => _schemaVersion;
        public int Revision => _revision;
        public void NotifyChanged() { unchecked { _revision++; } }
        public void SetNodeEnabled(string nodeId, bool enabled)
        {
            foreach (var pass in Passes)
                if (pass != null && pass.NodeId == nodeId && pass.Enabled != enabled)
                { pass.Enabled = enabled; NotifyChanged(); return; }
        }
        private void OnValidate() => NotifyChanged();
        internal int SwitchSignature()
        {
            unchecked
            {
                int hash = Passes?.Count ?? 0;
                if (Passes != null) foreach (var pass in Passes)
                    hash = hash * 397 ^ (pass == null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(pass)
                        ^ (pass.Enabled ? 1 : 0) ^ (int)pass.InjectionPoint);
                return hash;
            }
        }
    }
}
