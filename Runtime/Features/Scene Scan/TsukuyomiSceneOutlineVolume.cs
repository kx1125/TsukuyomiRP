using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering
{
    [Serializable, VolumeComponentMenu("TsukuyomiRP/Scene Outline"), DisplayInfo(name = "Scene Outline")]
    public sealed class TsukuyomiSceneOutlineVolume : VolumeComponent
    {
        public BoolParameter enable = new(true);
        public ColorParameter lineColor = new(Color.black, hdr: true, showAlpha: false, showEyeDropper: true);
        public ClampedFloatParameter opacity = new(1f, 0f, 1f);
        [Tooltip("Sampling radius in output pixels.")]
        public ClampedFloatParameter width = new(SceneOutlinePass.DefaultWidth, 1f, 8f);
        public ClampedFloatParameter depthThreshold = new(SceneOutlinePass.DefaultDepthThreshold, .001f, 1f);
        public BoolParameter useNormals = new(true);
        public ClampedFloatParameter normalThreshold = new(SceneOutlinePass.DefaultNormalThreshold, .01f, 2f);
        [Tooltip("Accumulate edge coverage when this camera uses Tsukuyomi DLSS or FSR3.")]
        public BoolParameter temporalStability = new(true);

        // URP blends from its stack defaults before the renderer knows the camera's Graph Profile.
        // Track the part of each blended value that still comes from that starting value.
        private struct ProfileBlend<T>
        {
            public T StackDefault;
            public float Remaining;

            public void Track(VolumeParameter<T> stack, VolumeParameter<T> layer, float weight)
            {
                if (!layer.overrideState) return;
                if (!stack.overrideState)
                {
                    StackDefault = stack.value;
                    Remaining = 1f;
                }
                Remaining *= 1f - weight;
            }
        }

        private ProfileBlend<Color> _colorBlend;
        private ProfileBlend<float> _opacityBlend, _widthBlend, _depthBlend, _normalBlend;

        public override void Override(VolumeComponent state, float interpFactor)
        {
            if (interpFactor <= 0f) return;
            var stack = (TsukuyomiSceneOutlineVolume)state;
            stack._colorBlend.Track(stack.lineColor, lineColor, interpFactor);
            stack._opacityBlend.Track(stack.opacity, opacity, interpFactor);
            stack._widthBlend.Track(stack.width, width, interpFactor);
            stack._depthBlend.Track(stack.depthThreshold, depthThreshold, interpFactor);
            stack._normalBlend.Track(stack.normalThreshold, normalThreshold, interpFactor);
            base.Override(state, interpFactor);
        }

        internal void Apply(ref SceneOutlineSettings settings)
        {
            if (enable.overrideState && !enable.value)
            {
                settings = default;
                return;
            }
            if (lineColor.overrideState)
                settings.LineColor = lineColor.value + (settings.LineColor - _colorBlend.StackDefault) * _colorBlend.Remaining;
            settings.Opacity = Resolve(opacity, settings.Opacity, _opacityBlend);
            settings.Width = Resolve(width, settings.Width, _widthBlend);
            settings.DepthThreshold = Resolve(depthThreshold, settings.DepthThreshold, _depthBlend);
            settings.NormalThreshold = Resolve(normalThreshold, settings.NormalThreshold, _normalBlend);
            if (useNormals.overrideState) settings.UseNormals = useNormals.value;
            if (temporalStability.overrideState) settings.TemporalStability = temporalStability.value;
        }

        private static float Resolve(ClampedFloatParameter parameter, float profile, ProfileBlend<float> blend)
            => parameter.overrideState ? parameter.value + (profile - blend.StackDefault) * blend.Remaining : profile;
    }
}
