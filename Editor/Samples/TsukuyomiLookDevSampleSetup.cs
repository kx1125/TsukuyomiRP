using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Tsukuyomi.Rendering.Editor
{
    internal static class TsukuyomiLookDevSampleSetup
    {
        private const string MenuPath = "Tools/Tsukuyomi RP/Samples/Setup LookDev Sample";
        private const string ImportedSamplesRoot = "Assets/Samples/TsukuyomiRP/";
        private const string SceneSuffix = "/LookDev/LookDev.unity";

        [MenuItem(MenuPath, priority = 2000)]
        private static void SetupFromMenu()
        {
            string scenePath = FindImportedScenePath();
            if (string.IsNullOrEmpty(scenePath))
            {
                EditorUtility.DisplayDialog(
                    "TsukuyomiRP LookDev Sample",
                    "Import the LookDev sample from Package Manager before running this setup.",
                    "OK");
                return;
            }

            ApplyAndOpen(scenePath);
        }

        internal static bool IsImportedLookDevScene(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return false;

            string normalizedPath = assetPath.Replace('\\', '/');
            return normalizedPath.StartsWith(ImportedSamplesRoot, StringComparison.OrdinalIgnoreCase) &&
                   normalizedPath.EndsWith(SceneSuffix, StringComparison.OrdinalIgnoreCase);
        }

        internal static void PromptForSetup(string scenePath)
        {
            int choice = EditorUtility.DisplayDialogComplex(
                "Configure TsukuyomiRP LookDev Sample?",
                "The LookDev sample requires its included URP Asset to enable Adaptive Probe Volumes and TsukuyomiRP rendering features.\n\n" +
                "Apply assigns SampleScene_Asset to Graphics Settings and the active Quality level. If Unity changed the imported Scene GUID, the sample's APV Baking Set is repaired before the scene opens.",
                "Apply and Open",
                "Not Now",
                "Select URP Asset");

            if (choice == 0)
            {
                ApplyAndOpen(scenePath);
            }
            else if (choice == 2)
            {
                SelectPipelineAsset(scenePath);
            }
        }

        private static string FindImportedScenePath()
        {
            string[] guids = AssetDatabase.FindAssets("LookDev t:Scene", new[] { "Assets/Samples" });
            string selectedPath = null;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!IsImportedLookDevScene(path))
                    continue;

                if (selectedPath == null || string.CompareOrdinal(path, selectedPath) > 0)
                    selectedPath = path;
            }

            return selectedPath;
        }

        private static void ApplyAndOpen(string scenePath)
        {
            string sampleRoot = Path.GetDirectoryName(scenePath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(sampleRoot))
                return;

            string pipelineAssetPath = sampleRoot + "/Settings/SampleScene_Asset.asset";
            string bakingSetPath = sampleRoot + "/LookDev/LookDev Baking Set.asset";
            UniversalRenderPipelineAsset pipelineAsset =
                AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelineAssetPath);
            ProbeVolumeBakingSet bakingSet = AssetDatabase.LoadAssetAtPath<ProbeVolumeBakingSet>(bakingSetPath);

            if (pipelineAsset == null || bakingSet == null)
            {
                Debug.LogError(
                    $"TsukuyomiRP LookDev setup could not find its required assets. " +
                    $"Expected '{pipelineAssetPath}' and '{bakingSetPath}'.");
                return;
            }

            if (pipelineAsset.lightProbeSystem != LightProbeSystem.ProbeVolumes)
            {
                Debug.LogError($"'{pipelineAssetPath}' does not have Adaptive Probe Volumes enabled.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            GraphicsSettings.defaultRenderPipeline = pipelineAsset;
            QualitySettings.renderPipeline = pipelineAsset;

            string sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);
            int bakingSetGuidReplacements = RepairBakingSetSceneGuid(bakingSet, sceneGuid);
            AssetDatabase.SaveAssets();

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            int perSceneDataRepairs = RepairPerSceneData(scene, bakingSet, sceneGuid);
            if (perSceneDataRepairs > 0)
            {
                EditorSceneManager.SaveScene(scene);
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            Selection.activeObject = pipelineAsset;
            EditorGUIUtility.PingObject(pipelineAsset);
            SceneView.RepaintAll();

            Debug.Log(
                $"TsukuyomiRP LookDev setup complete. Applied '{pipelineAsset.name}' to Graphics Settings " +
                $"and quality level '{QualitySettings.names[QualitySettings.GetQualityLevel()]}'. " +
                $"APV GUID repairs: baking set {bakingSetGuidReplacements}, scene data {perSceneDataRepairs}.");
        }

        private static int RepairBakingSetSceneGuid(ProbeVolumeBakingSet bakingSet, string sceneGuid)
        {
            if (string.IsNullOrEmpty(sceneGuid) || ContainsSceneGuid(bakingSet, sceneGuid))
                return 0;

            if (bakingSet.sceneGUIDs.Count != 1)
            {
                Debug.LogError(
                    $"Cannot safely repair APV Baking Set '{bakingSet.name}': expected one scene GUID, " +
                    $"but found {bakingSet.sceneGUIDs.Count}.");
                return 0;
            }

            string previousGuid = bakingSet.sceneGUIDs[0];
            SerializedObject serializedBakingSet = new SerializedObject(bakingSet);
            SerializedProperty property = serializedBakingSet.GetIterator();
            int replacements = 0;

            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.String || property.stringValue != previousGuid)
                    continue;

                property.stringValue = sceneGuid;
                replacements++;
            }

            if (replacements > 0)
            {
                serializedBakingSet.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(bakingSet);
            }

            return replacements;
        }

        private static bool ContainsSceneGuid(ProbeVolumeBakingSet bakingSet, string sceneGuid)
        {
            foreach (string existingSceneGuid in bakingSet.sceneGUIDs)
            {
                if (existingSceneGuid == sceneGuid)
                    return true;
            }

            return false;
        }

        private static int RepairPerSceneData(Scene scene, ProbeVolumeBakingSet bakingSet, string sceneGuid)
        {
            int repairs = 0;
            ProbeVolumePerSceneData[] allPerSceneData = Resources.FindObjectsOfTypeAll<ProbeVolumePerSceneData>();

            foreach (ProbeVolumePerSceneData perSceneData in allPerSceneData)
            {
                if (perSceneData == null || perSceneData.gameObject.scene != scene)
                    continue;

                SerializedObject serializedData = new SerializedObject(perSceneData);
                SerializedProperty bakingSetProperty = serializedData.FindProperty("serializedBakingSet");
                SerializedProperty sceneGuidProperty = serializedData.FindProperty("sceneGUID");
                bool changed = false;

                if (bakingSetProperty != null && bakingSetProperty.objectReferenceValue != bakingSet)
                {
                    bakingSetProperty.objectReferenceValue = bakingSet;
                    changed = true;
                }

                if (sceneGuidProperty != null && sceneGuidProperty.stringValue != sceneGuid)
                {
                    sceneGuidProperty.stringValue = sceneGuid;
                    changed = true;
                }

                if (!changed)
                    continue;

                serializedData.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(perSceneData);
                repairs++;
            }

            return repairs;
        }

        private static void SelectPipelineAsset(string scenePath)
        {
            string sampleRoot = Path.GetDirectoryName(scenePath)?.Replace('\\', '/');
            UniversalRenderPipelineAsset pipelineAsset = string.IsNullOrEmpty(sampleRoot)
                ? null
                : AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(sampleRoot + "/Settings/SampleScene_Asset.asset");

            if (pipelineAsset == null)
                return;

            Selection.activeObject = pipelineAsset;
            EditorGUIUtility.PingObject(pipelineAsset);
        }
    }

    internal sealed class TsukuyomiLookDevSamplePostprocessor : AssetPostprocessor
    {
        private const string PromptedSessionKey = "Tsukuyomi.Rendering.LookDevSampleSetupPrompted";
        private static bool s_PromptQueued;

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (Application.isBatchMode || s_PromptQueued || SessionState.GetBool(PromptedSessionKey, false))
                return;

            foreach (string importedAsset in importedAssets)
            {
                if (!TsukuyomiLookDevSampleSetup.IsImportedLookDevScene(importedAsset))
                    continue;

                s_PromptQueued = true;
                SessionState.SetBool(PromptedSessionKey, true);
                string scenePath = importedAsset;
                EditorApplication.delayCall += () =>
                {
                    s_PromptQueued = false;
                    TsukuyomiLookDevSampleSetup.PromptForSetup(scenePath);
                };
                break;
            }
        }
    }
}
