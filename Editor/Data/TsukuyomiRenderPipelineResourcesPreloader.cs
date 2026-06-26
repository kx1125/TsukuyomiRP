using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Tsukuyomi.Rendering.Editor
{
    internal static class TsukuyomiRenderPipelineResourcesPreloader
    {
        internal static TsukuyomiRenderPipelineResources LoadDefaultResources()
        {
            return AssetDatabase.LoadAssetAtPath<TsukuyomiRenderPipelineResources>(TsukuyomiRenderPipelineResourcesProvider.DefaultResourceAssetPath);
        }

        internal static bool IsDefaultResourcesPreloaded()
        {
            TsukuyomiRenderPipelineResources resources = LoadDefaultResources();
            if (resources == null)
                return false;

            return IsResourcesPreloaded(resources);
        }

        internal static TsukuyomiRenderPipelineResources GetPreloadedResources()
        {
            Object[] preloadedAssets = PlayerSettings.GetPreloadedAssets();
            for (int i = 0; i < preloadedAssets.Length; i++)
            {
                if (preloadedAssets[i] is TsukuyomiRenderPipelineResources resources)
                    return resources;
            }

            return null;
        }

        internal static bool IsResourcesPreloaded(TsukuyomiRenderPipelineResources resources)
        {
            if (resources == null)
                return false;

            Object[] preloadedAssets = PlayerSettings.GetPreloadedAssets();
            for (int i = 0; i < preloadedAssets.Length; i++)
            {
                if (preloadedAssets[i] == resources)
                    return true;
            }

            return false;
        }

        internal static void RegisterDefaultResources()
        {
            TsukuyomiRenderPipelineResources resources = LoadDefaultResources();
            if (resources == null)
            {
                Debug.LogError($"Could not find Tsukuyomi RP default resources at '{TsukuyomiRenderPipelineResourcesProvider.DefaultResourceAssetPath}'.");
                return;
            }

            RegisterResources(resources);
        }

        internal static void RegisterResources(TsukuyomiRenderPipelineResources resources)
        {
            if (resources == null)
            {
                Debug.LogError("Could not register null Tsukuyomi RP resources.");
                return;
            }

            Object[] preloadedAssets = PlayerSettings.GetPreloadedAssets();
            var updatedAssets = new List<Object>(preloadedAssets.Length + 1);

            for (int i = 0; i < preloadedAssets.Length; i++)
            {
                Object asset = preloadedAssets[i];
                if (asset == null)
                    continue;

                if (asset is TsukuyomiRenderPipelineResources)
                    continue;

                updatedAssets.Add(asset);
            }

            updatedAssets.Add(resources);

            PlayerSettings.SetPreloadedAssets(updatedAssets.ToArray());
            AssetDatabase.SaveAssets();
            TsukuyomiRenderPipelineResourcesProvider.ResetCache();
            Debug.Log($"Tsukuyomi RP default resources are registered in Preloaded Assets: {resources.name}");
        }
    }
}
