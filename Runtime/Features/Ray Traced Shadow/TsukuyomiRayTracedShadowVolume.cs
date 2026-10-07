using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering
{
    [Serializable, VolumeComponentMenu("TsukuyomiRP/Ray Traced Shadows")]
    public sealed class TsukuyomiRayTracedShadowVolume : VolumeComponent
    {
        public BoolParameter enable = new(false);
        public ClampedIntParameter samples = new(8, 1, 64);
        public ClampedIntParameter maxLights = new(4, 1, 16);
        public LayerMaskParameter casterMask = new(-1);
        public MinFloatParameter directionalDistance = new(100, 0.01f);
    }

    internal readonly struct RayTracedShadowSettings
    {
        public readonly bool Enabled;
        public readonly int Samples, MaxLights, CasterMask;
        public readonly float DirectionalDistance;

        private RayTracedShadowSettings(bool enabled, int samples, int maxLights, int casterMask, float directionalDistance)
        {
            Enabled = enabled;
            Samples = Mathf.Clamp(samples, 1, 64);
            MaxLights = Mathf.Clamp(maxLights, 1, 16);
            CasterMask = casterMask;
            DirectionalDistance = float.IsFinite(directionalDistance) ? Mathf.Max(0.01f, directionalDistance) : 100;
        }

        public static RayTracedShadowSettings Resolve(TsukuyomiPipelineProfile profile)
        {
            if (!profile) return new RayTracedShadowSettings(false, 8, 4, -1, 100);
            var volume = VolumeManager.instance.stack?.GetComponent<TsukuyomiRayTracedShadowVolume>();
            bool active = volume != null && volume.active;
            return new RayTracedShadowSettings(
                active && volume.enable.overrideState ? volume.enable.value : profile.EnableRayTracedShadows,
                active && volume.samples.overrideState ? volume.samples.value : profile.RayTracedShadowSamples,
                active && volume.maxLights.overrideState ? volume.maxLights.value : profile.MaxRayTracedShadowLights,
                active && volume.casterMask.overrideState ? volume.casterMask.value : profile.RayTracedShadowCasterMask,
                active && volume.directionalDistance.overrideState ? volume.directionalDistance.value : profile.RayTracedShadowDirectionalDistance);
        }
    }
}
