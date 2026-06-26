using UnityEngine;

namespace Tsukuyomi.Rendering
{
    public static class TsukuyomiRenderPipelineResourcesProvider
    {
        public const string DefaultResourceAssetPath = "Packages/tsukuyomi.render-pipelines.universal/Runtime/Data/TsukuyomiRenderPipelineResources.asset";

        private static TsukuyomiRenderPipelineResources s_Current;
        private static bool s_MissingResourcesLogged;
        private static bool s_MultipleResourcesLogged;

        public static TsukuyomiRenderPipelineResources Current
        {
            get
            {
                if (s_Current == null)
                    s_Current = FindPreloadedResources();
                return s_Current;
            }
        }

        public static bool TryGet(out TsukuyomiRenderPipelineResources resources)
        {
            resources = Current;
            if (resources != null)
                return true;

            if (!s_MissingResourcesLogged)
            {
                Debug.LogError("Tsukuyomi RP default resources are not preloaded. Add TsukuyomiRenderPipelineResources to Player Settings > Preloaded Assets.");
                s_MissingResourcesLogged = true;
            }

            return false;
        }

        public static void ResetCache()
        {
            s_Current = null;
            s_MissingResourcesLogged = false;
            s_MultipleResourcesLogged = false;
        }

        private static TsukuyomiRenderPipelineResources FindPreloadedResources()
        {
#if UNITY_EDITOR
            TsukuyomiRenderPipelineResources preloadedResources = FindEditorPreloadedResources();
            if (preloadedResources != null)
                return preloadedResources;
#endif

            TsukuyomiRenderPipelineResources[] resources = Resources.FindObjectsOfTypeAll<TsukuyomiRenderPipelineResources>();
            if (resources == null || resources.Length == 0)
                return null;

            TsukuyomiRenderPipelineResources selected = resources[0];
            for (int i = 0; i < resources.Length; i++)
            {
                if (resources[i] != null && resources[i].name == "TsukuyomiRenderPipelineResources")
                {
                    selected = resources[i];
                    break;
                }
            }

            if (resources.Length > 1 && !s_MultipleResourcesLogged)
            {
                Debug.LogWarning($"Found {resources.Length} Tsukuyomi RP resource assets. Using '{selected.name}'.");
                s_MultipleResourcesLogged = true;
            }

            return selected;
        }

#if UNITY_EDITOR
        private static TsukuyomiRenderPipelineResources FindEditorPreloadedResources()
        {
            UnityEngine.Object[] preloadedAssets = UnityEditor.PlayerSettings.GetPreloadedAssets();
            for (int i = 0; i < preloadedAssets.Length; i++)
            {
                if (preloadedAssets[i] is TsukuyomiRenderPipelineResources resources)
                    return resources;
            }

            return null;
        }
#endif
    }
}
