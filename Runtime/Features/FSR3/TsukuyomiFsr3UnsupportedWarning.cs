#if !ENABLE_UPSCALER_FRAMEWORK
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tsukuyomi.Rendering
{
#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    internal static class TsukuyomiFsr3UnsupportedWarning
    {
        private static bool s_logged;

#if UNITY_EDITOR
        static TsukuyomiFsr3UnsupportedWarning()
        {
            EditorApplication.delayCall += LogIfFsr3IsEnabled;
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void RuntimeInitialize()
        {
            RenderPipelineManager.beginContextRendering -= OnBeginContextRendering;
            RenderPipelineManager.beginContextRendering += OnBeginContextRendering;
            LogIfFsr3IsEnabled();
        }

        private static void OnBeginContextRendering(ScriptableRenderContext context, System.Collections.Generic.List<Camera> cameras)
        {
            LogIfFsr3IsEnabled();
        }

        private static void LogIfFsr3IsEnabled()
        {
            if (s_logged)
                return;

            TsukuyomiFsr3Settings settings = TsukuyomiRenderPipelineProjectSettings.Current.Fsr3Settings;
            if (settings == null || !settings.Enabled)
            {
                return;
            }

            Debug.LogWarning(
                "Tsukuyomi FSR3 is enabled, but ENABLE_UPSCALER_FRAMEWORK is not defined, so FSR3 will not run. " +
                "Enable it in Project Settings > Player > Other Settings > Scripting Define Symbols for the active build target, then let Unity recompile.");
            s_logged = true;
        }
    }
}
#endif
