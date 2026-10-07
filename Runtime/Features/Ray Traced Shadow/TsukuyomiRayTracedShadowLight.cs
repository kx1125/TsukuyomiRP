using System.Collections.Generic;
using UnityEngine;

namespace Tsukuyomi.Rendering
{
    public enum RayTracedShadowMode { Off, RayTraced }

    /// <summary>Finite-emitter shadow settings for a native directional, point or spot light.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Light))]
    [AddComponentMenu("Rendering/Tsukuyomi Ray Traced Shadow Light")]
    public sealed class TsukuyomiRayTracedShadowLight : MonoBehaviour
    {
        public RayTracedShadowMode ShadowMode = RayTracedShadowMode.Off;
        [Tooltip("Higher priorities receive ray tracing slots first.")]
        public int ShadowPriority;
        [Min(0), Tooltip("World-space sphere radius for point lights, disk radius for spot lights. Zero produces hard shadows.")]
        public float SourceRadius = 0.05f;
        [Range(0, 180), Tooltip("Directional light angular diameter in degrees. Zero produces hard shadows.")]
        public float AngularDiameter = 0.5f;

        private Light _light;
        public Light Light => _light ? _light : (_light = GetComponent<Light>());
        internal static readonly HashSet<TsukuyomiRayTracedShadowLight> ActiveLights = new();

        internal bool IsUsable
        {
            get
            {
                var light = Light;
                return isActiveAndEnabled && gameObject.activeInHierarchy && ShadowMode == RayTracedShadowMode.RayTraced
                    && light && light.isActiveAndEnabled && light.shadows != LightShadows.None
                    && light.lightmapBakeType != LightmapBakeType.Baked
                    && light.intensity > 0 && float.IsFinite(light.intensity)
                    && light.shadowStrength > 0 && float.IsFinite(SourceRadius) && SourceRadius >= 0
                    && float.IsFinite(AngularDiameter) && AngularDiameter >= 0
                    && (light.type == LightType.Directional || light.type == LightType.Point || light.type == LightType.Spot)
                    && (light.type == LightType.Directional || (float.IsFinite(light.range) && light.range > 0));
            }
        }

        private void OnEnable() => ActiveLights.Add(this);
        private void OnDisable() => ActiveLights.Remove(this);
        private void OnDestroy() => ActiveLights.Remove(this);
    }
}
