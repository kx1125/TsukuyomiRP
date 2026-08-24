using Tsukuyomi.Rendering;
using UnityEditor.Rendering;
using UnityEngine;

namespace UnityEditor.Rendering.Tsukuyomi
{
    [CustomEditor(typeof(TsukuyomiScreenSpaceGlobalIlluminationVolume))]
    internal sealed class TsukuyomiScreenSpaceGlobalIlluminationVolumeEditor : VolumeComponentEditor
    {
        private SerializedDataParameter _enable;
        private SerializedDataParameter _debugOutput;
        private SerializedDataParameter _halfResolution;
        private SerializedDataParameter _depthBufferThickness;
        private SerializedDataParameter _maxRaySteps;
        private SerializedDataParameter _rayMiss;
        private SerializedDataParameter _enableProbeVolumes;
        private SerializedDataParameter _denoise;
        private SerializedDataParameter _denoiserRadius;
        private SerializedDataParameter _secondDenoiser;
        private SerializedDataParameter _halfResolutionDenoiser;

        public override void OnEnable()
        {
            var fetcher = new PropertyFetcher<TsukuyomiScreenSpaceGlobalIlluminationVolume>(serializedObject);
            _enable = Unpack(fetcher.Find(x => x.enable));
            _debugOutput = Unpack(fetcher.Find(x => x.debugOutput));
            _halfResolution = Unpack(fetcher.Find(x => x.halfResolution));
            _depthBufferThickness = Unpack(fetcher.Find(x => x.depthBufferThickness));
            _maxRaySteps = Unpack(fetcher.Find(x => x.maxRaySteps));
            _rayMiss = Unpack(fetcher.Find(x => x.rayMiss));
            _enableProbeVolumes = Unpack(fetcher.Find(x => x.enableProbeVolumes));
            _denoise = Unpack(fetcher.Find(x => x.denoise));
            _denoiserRadius = Unpack(fetcher.Find(x => x.denoiserRadius));
            _secondDenoiser = Unpack(fetcher.Find(x => x.secondDenoiser));
            _halfResolutionDenoiser = Unpack(fetcher.Find(x => x.halfResolutionDenoiser));
            base.OnEnable();
        }

        public override void OnInspectorGUI()
        {
            DrawSection("General");
            PropertyField(_enable, EditorGUIUtility.TrTextContent(
                "Enable",
                "Enables Tsukuyomi screen-space global illumination for this camera."));
            PropertyField(_debugOutput, EditorGUIUtility.TrTextContent(
                "Debug Output",
                "Displays the final denoised and upsampled SSGI signal full screen without writing it into color history."));

            DrawSection("Tracing");
            PropertyField(_halfResolution, EditorGUIUtility.TrTextContent(
                "Half Resolution",
                "Traces at half width and height, then performs a depth-aware upsample."));
            PropertyField(_depthBufferThickness, EditorGUIUtility.TrTextContent(
                "Depth Buffer Thickness",
                "Expands the depth surface used during hierarchical ray intersection."));
            PropertyField(_maxRaySteps, EditorGUIUtility.TrTextContent(
                "Max Ray Steps",
                "Limits hierarchical screen-space ray traversal."));
            PropertyField(_rayMiss, EditorGUIUtility.TrTextContent(
                "Ray Miss",
                "Selects the environment fallback used when no valid screen-space hit is found."));
            PropertyField(_enableProbeVolumes, EditorGUIUtility.TrTextContent(
                "Enable Probe Volumes",
                "Uses Adaptive Probe Volume irradiance for ray misses when APV is active."));

            DrawSection("Denoising");
            PropertyField(_denoise, EditorGUIUtility.TrTextContent(
                "Denoise",
                "Enables temporal accumulation, history validation, and spatial bilateral filtering."));
            PropertyField(_denoiserRadius, EditorGUIUtility.TrTextContent(
                "Denoiser Radius",
                "Controls the spatial filter footprint."));
            PropertyField(_secondDenoiser, EditorGUIUtility.TrTextContent(
                "Second Denoiser",
                "Runs an additional temporal and spatial filtering stage."));
            PropertyField(_halfResolutionDenoiser, EditorGUIUtility.TrTextContent(
                "Half Resolution Denoiser",
                "Runs the spatial filter at half resolution when tracing at full resolution."));
        }

        private static void DrawSection(string title)
        {
            EditorGUILayout.Space(4.0f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }
    }
}
