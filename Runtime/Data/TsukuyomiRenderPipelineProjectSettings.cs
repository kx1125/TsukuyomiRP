using UnityEngine;

namespace Tsukuyomi.Rendering
{
    public sealed class TsukuyomiRenderPipelineProjectSettings : ScriptableObject
    {
        public const string ProjectSettingsAssetPath = "ProjectSettings/TsukuyomiRenderPipelineSettings.asset";

        private static TsukuyomiRenderPipelineProjectSettings s_Current;

        [SerializeField]
        private TsukuyomiFsr3Settings fsr3Settings = new();

        public static TsukuyomiRenderPipelineProjectSettings Current
        {
            get
            {
                if (s_Current == null)
                    s_Current = LoadOrCreate();

                return s_Current;
            }
        }

        public TsukuyomiFsr3Settings Fsr3Settings => fsr3Settings ??= new TsukuyomiFsr3Settings();

#if UNITY_EDITOR
        public static void Save()
        {
            TsukuyomiRenderPipelineProjectSettings settings = Current;
            UnityEditor.EditorUtility.SetDirty(settings);
            UnityEditorInternal.InternalEditorUtility.SaveToSerializedFileAndForget(
                new Object[] { settings },
                ProjectSettingsAssetPath,
                true);
        }
#endif

        private static TsukuyomiRenderPipelineProjectSettings LoadOrCreate()
        {
#if UNITY_EDITOR
            Object[] objects = UnityEditorInternal.InternalEditorUtility.LoadSerializedFileAndForget(ProjectSettingsAssetPath);
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] is TsukuyomiRenderPipelineProjectSettings settings)
                    return settings;
            }
#endif

            TsukuyomiRenderPipelineProjectSettings instance = CreateInstance<TsukuyomiRenderPipelineProjectSettings>();
            instance.name = nameof(TsukuyomiRenderPipelineProjectSettings);

#if UNITY_EDITOR
            UnityEditorInternal.InternalEditorUtility.SaveToSerializedFileAndForget(
                new Object[] { instance },
                ProjectSettingsAssetPath,
                true);
#endif

            return instance;
        }
    }
}
