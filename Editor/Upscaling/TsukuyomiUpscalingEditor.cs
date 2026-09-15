using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityRhi;
using UnityRhi.Dlss.Urp;

namespace Tsukuyomi.Rendering.Editor
{
    [InitializeOnLoad]
    internal static class TsukuyomiUpscalingEditor
    {
        private const string Root = "Packages/tsukuyomi.render-pipelines.universal/";
        static TsukuyomiUpscalingEditor() => EditorApplication.delayCall += Migrate;

        [MenuItem("Tools/Tsukuyomi RP/Upscaling/Migrate Legacy Settings")]
        public static void Migrate()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
            foreach (var guid in AssetDatabase.FindAssets("t:TsukuyomiRenderPipelineResources"))
            {
                var resources = AssetDatabase.LoadAssetAtPath<TsukuyomiRenderPipelineResources>(AssetDatabase.GUIDToAssetPath(guid));
                if (!resources) continue;
                var so = new SerializedObject(resources);
                SetShader(so, "dlssPrepareInputsShader", "DlssPrepareInputs.shader");
                SetShader(so, "dlssNrPrepareInputsShader", "DlssNrPrepareInputs.shader");
                so.ApplyModifiedPropertiesWithoutUndo();
            }
#if ENABLE_UPSCALER_FRAMEWORK
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset", new[] { "Assets" }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (!asset || asset.upscalerOptions.OfType<TsukuyomiUpscalerOptions>().Any()) continue;
                if (asset.upscalerName == UnityRhiDlssIUpscaler.UpscalerName || asset.upscalerName == "Tsukuyomi FSR3" ||
                    (asset == UniversalRenderPipeline.asset && TsukuyomiRenderPipelineProjectSettings.Current.Fsr3Settings.Enabled))
                    GetOrCreate(asset, true);
            }
#endif
            AssetDatabase.SaveAssets();
        }
        private static void SetShader(SerializedObject so, string field, string filename)
        {
            var property = so.FindProperty(field);
            if (property != null && !property.objectReferenceValue)
                property.objectReferenceValue = AssetDatabase.LoadAssetAtPath<UnityEngine.Shader>(Root + "Shaders/DLSS/" + filename);
        }

#if ENABLE_UPSCALER_FRAMEWORK
        internal static TsukuyomiUpscalerOptions GetOrCreate(UniversalRenderPipelineAsset asset, bool select)
        {
            var options = asset.upscalerOptions.OfType<TsukuyomiUpscalerOptions>().FirstOrDefault();
            if (!options)
            {
                options = ScriptableObject.CreateInstance<TsukuyomiUpscalerOptions>();
                options.name = "Tsukuyomi Upscaler Settings";
                options.upscalerName = TsukuyomiUpscaling.UpscalerName;
                var legacy = asset.upscalerOptions.OfType<UnityRhiDlssOptions>().FirstOrDefault();
                options.fsr3 = JsonUtility.FromJson<TsukuyomiFsr3Settings>(JsonUtility.ToJson(TsukuyomiRenderPipelineProjectSettings.Current.Fsr3Settings));
                if (asset.upscalerName == UnityRhiDlssIUpscaler.UpscalerName && legacy)
                {
                    options.backend = UpscalerBackend.DLSS;
                    options.quality = legacy.qualityMode switch
                    {
                        UpscalerMode.NATIVE => UpscalerQuality.NativeAA,
                        UpscalerMode.BALANCED => UpscalerQuality.Balanced,
                        UpscalerMode.PERFORMANCE => UpscalerQuality.Performance,
                        UpscalerMode.ULTRA_PERFORMANCE => UpscalerQuality.UltraPerformance,
                        _ => UpscalerQuality.Quality
                    };
                    options.dlssPreset = legacy.preset;
                    options.dlssMotionVectorScale = legacy.motionVectorScale;
                }
                else if (options.fsr3.Enabled || asset.upscalerName == "Tsukuyomi FSR3")
                {
                    options.backend = UpscalerBackend.FSR3;
                    options.quality = options.fsr3.QualityMode switch
                    {
                        FSR3.Fsr3Upscaler.QualityMode.NativeAA => UpscalerQuality.NativeAA,
                        FSR3.Fsr3Upscaler.QualityMode.Balanced => UpscalerQuality.Balanced,
                        FSR3.Fsr3Upscaler.QualityMode.Performance => UpscalerQuality.Performance,
                        FSR3.Fsr3Upscaler.QualityMode.UltraPerformance => UpscalerQuality.UltraPerformance,
                        _ => UpscalerQuality.Quality
                    };
                    options.fsr3UltraQuality = options.fsr3.QualityMode == FSR3.Fsr3Upscaler.QualityMode.UltraQuality;
                }
                options.neuralRendering = AssetDatabase.FindAssets("t:VolumeProfile", new[] { "Assets" })
                    .Select(g => AssetDatabase.LoadAssetAtPath<VolumeProfile>(AssetDatabase.GUIDToAssetPath(g)))
                    .Any(p => p && p.TryGet<DlssNrVolume>(out var nr) && nr.IsActive());
                AssetDatabase.AddObjectToAsset(options, asset);
                asset.upscalerOptions.Add(options);
                EditorUtility.SetDirty(options);
            }
            if (select) asset.upscalerName = TsukuyomiUpscaling.UpscalerName;
            EditorUtility.SetDirty(asset);
            return options;
        }
#endif
        internal static void DrawSettings()
        {
            EditorGUILayout.LabelField("DLSS / FSR3", EditorStyles.boldLabel);
#if ENABLE_UPSCALER_FRAMEWORK
            var asset = UniversalRenderPipeline.asset;
            if (!asset) { EditorGUILayout.HelpBox("Select a URP asset in Graphics or Quality Settings.", MessageType.Info); return; }
            EditorGUILayout.ObjectField("Active URP Asset", asset, typeof(UniversalRenderPipelineAsset), false);
            var options = asset.upscalerOptions.OfType<TsukuyomiUpscalerOptions>().FirstOrDefault();
            if (!options || asset.upscalerName != TsukuyomiUpscaling.UpscalerName)
            {
                if (GUILayout.Button("Use Tsukuyomi Upscaler on This Asset")) { GetOrCreate(asset, true); AssetDatabase.SaveAssets(); }
                return;
            }
            var so = new SerializedObject(options);
            so.Update();
            DrawOptions(so);
            so.ApplyModifiedProperties();
            EditorGUILayout.HelpBox(TsukuyomiUpscaling.GetStatus().ToString(), MessageType.None);
            EditorGUILayout.HelpBox("Use a Game camera with Post Processing enabled, TAA and MSAA disabled. DLSS 5 NR also requires its Volume override and TsukuyomiFeature on the renderer.", MessageType.Info);
#else
            EditorGUILayout.HelpBox("Enable Unity's Upscaler Framework (ENABLE_UPSCALER_FRAMEWORK) for this build target.", MessageType.Info);
#endif
        }

#if ENABLE_UPSCALER_FRAMEWORK
        internal static void DrawOptions(SerializedObject so)
        {
            foreach (var field in new[] { "backend", "quality", "neuralRendering", "dlssPreset" })
                EditorGUILayout.PropertyField(so.FindProperty(field));
            EditorGUILayout.Space();
            var fsr = so.FindProperty("fsr3");
            fsr.isExpanded = EditorGUILayout.Foldout(fsr.isExpanded, "FSR3 Options (including DLSS fallback)", true);
            if (!fsr.isExpanded) return;
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(so.FindProperty("fsr3UltraQuality"));
            var cursor = fsr.Copy(); var end = cursor.GetEndProperty(); bool children = true;
            while (cursor.NextVisible(children) && !SerializedProperty.EqualContents(cursor, end))
            {
                children = false;
                if (cursor.name != "Enabled" && cursor.name != "QualityMode") EditorGUILayout.PropertyField(cursor, true);
            }
            EditorGUI.indentLevel--;
        }
#endif

    }
#if ENABLE_UPSCALER_FRAMEWORK
    [CustomEditor(typeof(TsukuyomiUpscalerOptions))]
    internal sealed class TsukuyomiUpscalerOptionsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            TsukuyomiUpscalingEditor.DrawOptions(serializedObject);
            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
}
