using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light), typeof(UniversalAdditionalLightData))]
    [ExecuteAlways]
    public sealed class TsukuyomiVolumetricAdditionalLight : MonoBehaviour
    {
        [SerializeField, Range(-1.0f, 1.0f)]
        private float anisotropy = 0.25f;

        [SerializeField, Range(0.0f, 16.0f)]
        private float scattering = 1.0f;

        [SerializeField, Range(0.0f, 1.0f)]
        private float radius = 0.2f;

        public float Anisotropy
        {
            get => anisotropy;
            set => anisotropy = Mathf.Clamp(value, -1.0f, 1.0f);
        }

        public float Scattering
        {
            get => scattering;
            set => scattering = Mathf.Clamp(value, 0.0f, 16.0f);
        }

        public float Radius
        {
            get => radius;
            set => radius = Mathf.Clamp01(value);
        }

        private void OnEnable()
        {
            TsukuyomiVolumetricLightManager.Register(GetComponent<Light>(), this);
        }

        private void OnDisable()
        {
            TsukuyomiVolumetricLightManager.Unregister(GetComponent<Light>(), this);
        }
    }
}
