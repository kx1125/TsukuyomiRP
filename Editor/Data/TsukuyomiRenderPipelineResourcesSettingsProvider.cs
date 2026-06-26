using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tsukuyomi.Rendering.Editor
{
    internal sealed class TsukuyomiRenderPipelineResourcesSettingsProvider : SettingsProvider
    {
        private TsukuyomiRenderPipelineResources _resources;
        private UnityEditor.Editor _resourcesEditor;

        private TsukuyomiRenderPipelineResourcesSettingsProvider()
            : base("Project/Tsukuyomi RP", SettingsScope.Project)
        {
            label = "Tsukuyomi RP";
            keywords = new[] { "Tsukuyomi", "Render Pipeline", "Resources", "Preloaded Assets", "PCSS", "Contact Shadow", "FSR3", "Upscaler" };
        }

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new TsukuyomiRenderPipelineResourcesSettingsProvider();
        }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            _resources = TsukuyomiRenderPipelineResourcesPreloader.GetPreloadedResources();
            if (_resources == null)
                _resources = TsukuyomiRenderPipelineResourcesPreloader.LoadDefaultResources();
        }

        public override void OnGUI(string searchContext)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Default Render Resources", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _resources = (TsukuyomiRenderPipelineResources)EditorGUILayout.ObjectField(
                "Resources",
                _resources,
                typeof(TsukuyomiRenderPipelineResources),
                false);

            if (EditorGUI.EndChangeCheck() && _resources != null)
            {
                TsukuyomiRenderPipelineResourcesPreloader.RegisterResources(_resources);
            }

            TsukuyomiRenderPipelineResources preloadedResources = TsukuyomiRenderPipelineResourcesPreloader.GetPreloadedResources();
            if (preloadedResources == null)
            {
                EditorGUILayout.HelpBox("No Tsukuyomi RP resources are registered in Player Settings > Preloaded Assets.", MessageType.Warning);
            }
            else if (preloadedResources != _resources)
            {
                EditorGUILayout.HelpBox($"Currently preloaded resource is '{preloadedResources.name}'.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox($"Preloaded resource: {_resources.name}", MessageType.None);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use Package Default"))
                {
                    _resources = TsukuyomiRenderPipelineResourcesPreloader.LoadDefaultResources();
                    if (_resources != null)
                        TsukuyomiRenderPipelineResourcesPreloader.RegisterResources(_resources);
                }

                using (new EditorGUI.DisabledScope(_resources == null))
                {
                    if (GUILayout.Button("Select Resource Asset"))
                        Selection.activeObject = _resources;
                }
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(_resources == null))
            {
                UnityEditor.Editor.CreateCachedEditor(_resources, null, ref _resourcesEditor);
                _resourcesEditor?.OnInspectorGUI();
            }
        }

        public override void OnDeactivate()
        {
            if (_resourcesEditor != null)
            {
                Object.DestroyImmediate(_resourcesEditor);
                _resourcesEditor = null;
            }
        }
    }
}


