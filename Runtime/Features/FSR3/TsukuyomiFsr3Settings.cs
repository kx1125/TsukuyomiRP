using System;
using Tsukuyomi.Rendering.FSR3;
using UnityEngine;

namespace Tsukuyomi.Rendering
{
    [Serializable]
    public sealed class TsukuyomiFsr3Settings
    {
        public bool Enabled;
        public Fsr3Upscaler.QualityMode QualityMode = Fsr3Upscaler.QualityMode.Quality;
        public bool PerformSharpenPass = true;
        [Range(0.0f, 1.0f)]
        public float Sharpness = 0.8f;
        [Range(0.0f, 1.0f)]
        public float VelocityFactor = 1.0f;
        public bool EnableAutoExposure = true;
        public bool EnableDebugView;
    }

    [Serializable]
    public sealed class TsukuyomiFsr3Shaders
    {
        public ComputeShader prepareInputsPass;
        public ComputeShader lumaPyramidPass;
        public ComputeShader shadingChangePyramidPass;
        public ComputeShader shadingChangePass;
        public ComputeShader prepareReactivityPass;
        public ComputeShader lumaInstabilityPass;
        public ComputeShader accumulatePass;
        public ComputeShader sharpenPass;
        public ComputeShader autoGenReactivePass;
        public ComputeShader tcrAutoGenPass;
        public ComputeShader debugViewPass;

        public bool IsValid =>
            prepareInputsPass != null &&
            lumaPyramidPass != null &&
            shadingChangePyramidPass != null &&
            shadingChangePass != null &&
            prepareReactivityPass != null &&
            lumaInstabilityPass != null &&
            accumulatePass != null &&
            sharpenPass != null &&
            autoGenReactivePass != null &&
            tcrAutoGenPass != null &&
            debugViewPass != null;

        internal Fsr3UpscalerShaders ToFsr3Shaders()
        {
            return new Fsr3UpscalerShaders
            {
                prepareInputsPass = prepareInputsPass,
                lumaPyramidPass = lumaPyramidPass,
                shadingChangePyramidPass = shadingChangePyramidPass,
                shadingChangePass = shadingChangePass,
                prepareReactivityPass = prepareReactivityPass,
                lumaInstabilityPass = lumaInstabilityPass,
                accumulatePass = accumulatePass,
                sharpenPass = sharpenPass,
                autoGenReactivePass = autoGenReactivePass,
                tcrAutoGenPass = tcrAutoGenPass,
                debugViewPass = debugViewPass
            };
        }
    }
}

