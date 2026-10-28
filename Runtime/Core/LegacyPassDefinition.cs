using System;
using UnityEngine;

namespace Tsukuyomi.Rendering
{
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Tsukuyomi.Rendering", "Tsukuyomi.Rendering", "LegacyPassPlugin")]
    public sealed class LegacyPassDefinition : RenderPassDefinition
    {
        [SerializeReference] public RenderPassBase Pass;
        public override string Name => Pass?.Name ?? "Missing legacy pass";
        public override bool AllowMultiple => true;
        public override void ValidateSettings() => Pass?.ValidateSettings();
        public override IRenderPassRuntime CreateRuntime()
        {
            if (Pass == null) throw new InvalidOperationException("Restore the missing legacy pass type before applying.");
            Pass.Enabled = true; Pass.InjectionPoint = InjectionPoint;
            return Pass.CreateRuntime();
        }
    }
}
