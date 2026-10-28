using System.Collections.Generic;
using System;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine;

namespace Tsukuyomi.Rendering
{
    [Serializable, InjectionPoint(InjectionPoint.BeforePostProcess)]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Tsukuyomi.Rendering", "Tsukuyomi.Rendering", "ScanPostProcessPlugin")]
    public sealed class ScanPostProcessPass : RenderPassBase
    {
        public ScanPostProcessPass() => InjectionPoint = InjectionPoint.BeforePostProcess;
        public override string Name => "ScanPostProcess";
        public ScanPostProcessParameters ScanPostProcess = ScanPostProcessParameters.Default;
        public ScanPostEffect ScanPostEffects = ScanPostEffect.All;

        public override void Build(PassPlanBuilder plan)
            => plan.Service(SceneEffectPass.Key, () => new SceneEffectPass()).Register(this);
    }

    [System.Serializable]
    public sealed class TsukuyomiScanPostProcessPass : CameraEffectPass<ScanPostProcessParameters>
    {
        private readonly ScanPostProcessPass _defaults;
        public TsukuyomiScanPostProcessPass() { }
        internal TsukuyomiScanPostProcessPass(ScanPostProcessPass settings) => _defaults = settings;
        public override void PrepareRecording(in PassCameraContext context, ref RenderingData data)
        {
            if (_defaults != null)
            {
                UpdateSettings(context.Camera, _defaults, context.Volumes);
                UpdateEffectSettings(context.Camera, _defaults, context.Volumes);
            }
            if (_standaloneComposite != null) _standaloneComposite.PrepareRecording(context, ref data);
        }
        public override void Build(PassPlanBuilder plan)
        {
            _standaloneComposite = new ScanColorCompositePass(null, this, null);
            base.Build(plan);
        }

        [System.NonSerialized] private readonly Dictionary<Camera, ScanPostEffect> _effects = new();
        [System.NonSerialized] private ScanColorCompositePass _standaloneComposite;
        public override string Name => "Tsukuyomi Scan Post Process";
        protected override ScanPostProcessParameters DefaultParameters => ScanPostProcessParameters.Default;
        protected override bool ResolveSettings(RenderPassBase profile, UnityEngine.Rendering.VolumeStack stack, out ScanPostProcessParameters parameters)
            => SceneEffectSettings.Resolve(profile as ScanPostProcessPass, stack, out parameters);
        protected override void ApplyAnimation(ref ScanPostProcessParameters parameters, in ScanAnimationParameters animation)
        {
            animation.Apply(ref parameters.Coverage);
            parameters.Flash = animation.Flash; parameters.Reveal = animation.Reveal; parameters.Random01 = animation.Random01;
            parameters.HorizontalGlitch = animation.HorizontalGlitch; parameters.ChromaticSplit = animation.ChromaticSplit;
        }


        private static readonly Dictionary<Camera, (ScanPostEffect Mask, ScanPostEffect Values)> CameraEffects = new();
        [System.NonSerialized] private readonly Dictionary<Camera, ScanPostEffect> _panelEffects = new();
        public static void SetCameraEffectEnabled(Camera camera, ScanPostEffect effect, bool enabled)
        {
            if (!camera) throw new System.ArgumentNullException(nameof(camera));
            CameraEffects.TryGetValue(camera, out var state);
            CameraEffects[camera] = (state.Mask | effect, enabled ? state.Values | effect : state.Values & ~effect);
        }
        public new static void ClearCamera(Camera camera)
        { CameraEffectPass<ScanPostProcessParameters>.ClearCamera(camera); if (!ReferenceEquals(camera, null)) CameraEffects.Remove(camera); }
        public new static bool HasCameraOverrides(Camera camera)
            => CameraEffectPass<ScanPostProcessParameters>.HasCameraOverrides(camera) || camera && CameraEffects.ContainsKey(camera);
        internal static void ResetCameraEffects() => CameraEffects.Clear();
        internal void UpdateEffectSettings(Camera camera, RenderPassBase profile, UnityEngine.Rendering.VolumeStack stack)
        {
            SceneEffectCameras.Prune(CameraEffects); SceneEffectCameras.Prune(_panelEffects);
            if (camera) _panelEffects[camera] = SceneEffectSettings.Effects(profile as ScanPostProcessPass, stack);
        }

        public void SetEffectEnabled(Camera camera, ScanPostEffect effect, bool enabled)
        {
            if (!camera) throw new System.ArgumentNullException(nameof(camera));
            var current = GetEffects(camera);
            _effects[camera] = enabled ? current | effect : current & ~effect;
        }

        public bool IsEffectEnabled(Camera camera, ScanPostEffect effect) => camera && (GetEffects(camera) & effect) != 0;
        internal ScanPostEffect GetEffects(Camera camera)
        {
            var effects = camera && _panelEffects.TryGetValue(camera, out var panel) ? panel : ScanPostEffect.All;
            if (camera && _effects.TryGetValue(camera, out var manual)) effects = manual;
            if (camera && CameraEffects.TryGetValue(camera, out var control)) effects = (effects & ~control.Mask) | (control.Values & control.Mask);
            return effects;
        }
        internal bool TryGetActive(Camera camera, out ScanPostProcessParameters p)
            => TryGetActiveParameters(camera, out p) && p.Coverage.Blend > 0f;
        public override bool IsActive(in FrameContext frame) => TryGetActive(frame.CameraData.camera, out _) && GetEffects(frame.CameraData.camera) != ScanPostEffect.None;

        public override void CollectTextureSlots(List<TextureSlot> slots)
        {
            slots.Add(TextureSlot.Read("Scan Source", BuiltinTexture.ActiveColor));
            slots.Add(TextureSlot.Read("Scan Depth", BuiltinTexture.CameraDepthTexture));
            slots.Add(TextureSlot.Read("Scan Normals", BuiltinTexture.CameraNormals));
        }

        public override void RecordGraph(in FeatureGraphContext graph)
        {
            if (_standaloneComposite?.Prepared == true) _standaloneComposite.RecordGraph(graph);
        }

        public override void Clear(Camera camera)
        { base.Clear(camera); if (!ReferenceEquals(camera, null)) { _effects.Remove(camera); _panelEffects.Remove(camera); } }
        public override void Deactivate(Camera camera) => _standaloneComposite?.Deactivate(camera);
        public override void Dispose() { _standaloneComposite?.Dispose(); _standaloneComposite = null; _effects.Clear(); _panelEffects.Clear(); ClearAllCameras(); }
    }
}
