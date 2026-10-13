using Tsukuyomi.Rendering;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace Tsukuyomi.Rendering.Editor
{
    [CustomEditor(typeof(TsukuyomiMultiScaleAmbientOcclusionVolume))]
    internal sealed class TsukuyomiMultiScaleAmbientOcclusionVolumeEditor : VolumeComponentEditor
    {
        private SerializedDataParameter _enable;
        private SerializedDataParameter _compositionMode;
        private SerializedDataParameter _debugOcclusion;
        private SerializedDataParameter _intensity;
        private SerializedDataParameter _thicknessModifier;
        private SerializedDataParameter _scaleCount;
        private SerializedDataParameter _exhaustiveSampling;
        private SerializedDataParameter _depthSphereDiameter;
        private SerializedDataParameter _blurTolerance;
        private SerializedDataParameter _upsampleTolerance;
        private SerializedDataParameter _noiseFilterTolerance;
        private SerializedDataParameter _blurReferenceWidth;
        private SerializedDataParameter _occlusionColor;
        private SerializedDataParameter _directLightingStrength;
        public override void OnEnable()
        {
            var fetcher = new PropertyFetcher<TsukuyomiMultiScaleAmbientOcclusionVolume>(serializedObject);
            _enable = Unpack(fetcher.Find(x => x.enable));
            _compositionMode = Unpack(fetcher.Find(x => x.compositionMode));
            _debugOcclusion = Unpack(fetcher.Find(x => x.debugOcclusion));
            _intensity = Unpack(fetcher.Find(x => x.intensity));
            _thicknessModifier = Unpack(fetcher.Find(x => x.thicknessModifier));
            _scaleCount = Unpack(fetcher.Find(x => x.scaleCount));
            _exhaustiveSampling = Unpack(fetcher.Find(x => x.exhaustiveSampling));
            _depthSphereDiameter = Unpack(fetcher.Find(x => x.depthSphereDiameter));
            _blurTolerance = Unpack(fetcher.Find(x => x.blurTolerance));
            _upsampleTolerance = Unpack(fetcher.Find(x => x.upsampleTolerance));
            _noiseFilterTolerance = Unpack(fetcher.Find(x => x.noiseFilterTolerance));
            _blurReferenceWidth = Unpack(fetcher.Find(x => x.blurReferenceWidth));
            _occlusionColor = Unpack(fetcher.Find(x => x.occlusionColor));
            _directLightingStrength = Unpack(fetcher.Find(x => x.directLightingStrength));
            base.OnEnable();
        }
        public override void OnInspectorGUI()
        {
            PropertyField(_enable);
            PropertyField(_compositionMode);
            PropertyField(_debugOcclusion);
            PropertyField(_intensity);
            PropertyField(_thicknessModifier);
            PropertyField(_scaleCount);
            PropertyField(_exhaustiveSampling);
            PropertyField(_depthSphereDiameter);
            PropertyField(_blurTolerance);
            PropertyField(_upsampleTolerance);
            PropertyField(_noiseFilterTolerance);
            PropertyField(_blurReferenceWidth);
            PropertyField(_occlusionColor);
            PropertyField(_directLightingStrength);
            EditorGUILayout.HelpBox("Enable MSVO in the renderer's Pipeline Profile first. Unchecked overrides inherit that profile. Occlusion Color applies to Color Composite; Direct Lighting Strength applies to Lighting.", MessageType.Info);
        }
    }
}
