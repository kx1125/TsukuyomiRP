using System.Collections.Generic;
using UnityEngine;

namespace Tsukuyomi.Rendering
{
    public static class TsukuyomiVolumetricLightManager
    {
        private static readonly Dictionary<Light, TsukuyomiVolumetricAdditionalLight> Lights = new();

        public static bool TryGet(Light light, out TsukuyomiVolumetricAdditionalLight volumetricLight)
        {
            volumetricLight = null;
            return light != null && Lights.TryGetValue(light, out volumetricLight);
        }

        public static void Register(Light light, TsukuyomiVolumetricAdditionalLight volumetricLight)
        {
            if (light == null || volumetricLight == null)
                return;

            Lights[light] = volumetricLight;
        }

        public static void Unregister(Light light, TsukuyomiVolumetricAdditionalLight volumetricLight)
        {
            if (light == null || volumetricLight == null)
                return;

            if (Lights.TryGetValue(light, out TsukuyomiVolumetricAdditionalLight current) && current == volumetricLight)
                Lights.Remove(light);
        }
    }
}
