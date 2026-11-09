using System;
using UnityEngine;

namespace Tsukuyomi.Rendering
{
    [Serializable, InjectionPoint(InjectionPoint.BeforePostProcess)]
    public sealed class SceneOutlinePass : RenderPassBase
    {
        internal const float DefaultWidth = 1.5f, DefaultDepthThreshold = .03f, DefaultNormalThreshold = .35f;
        public SceneOutlinePass() => InjectionPoint = InjectionPoint.BeforePostProcess;
        public override string Name => "Scene Outline";
        public override string MenuPath => "Scene Scan/Scene Outline";

        [ColorUsage(false, true)] public Color LineColor = Color.black;
        [Range(0f, 1f)] public float Opacity = 1f;
        [Tooltip("Sampling radius in output pixels."), Range(1f, 8f)]
        public float Width = DefaultWidth;
        [Tooltip("Minimum relative change in linear eye depth."), Range(.001f, 1f)]
        public float DepthThreshold = DefaultDepthThreshold;
        public bool UseNormals = true;
        [Tooltip("Minimum difference between unit surface normals."), Range(.01f, 2f)]
        public float NormalThreshold = DefaultNormalThreshold;
        [Tooltip("Accumulate geometric line coverage when this camera uses Tsukuyomi DLSS or FSR3.")]
        public bool TemporalStability = true;

        internal SceneOutlineSettings Resolve(in PassCameraContext context)
        {
            if (!Enabled || !context.Frame.CameraData.postProcessEnabled) return default;

            var settings = new SceneOutlineSettings
            {
                LineColor = LineColor, Opacity = Opacity, Width = Width, DepthThreshold = DepthThreshold,
                UseNormals = UseNormals, NormalThreshold = NormalThreshold, TemporalStability = TemporalStability
            };
            var volume = context.Volumes?.GetComponent<TsukuyomiSceneOutlineVolume>();
            if (volume != null && volume.active) volume.Apply(ref settings);
            return settings;
        }

        public override void Build(PassPlanBuilder plan)
            => plan.Service(SceneEffectPass.Key, () => new SceneEffectPass()).Register(this);
    }

    internal struct SceneOutlineSettings
    {
        public Color LineColor;
        public float Opacity, Width, DepthThreshold, NormalThreshold;
        public bool UseNormals, TemporalStability;
        public bool Visible => Opacity > 0f;
    }
}
