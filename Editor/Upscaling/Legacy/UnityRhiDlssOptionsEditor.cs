#if ENABLE_UPSCALER_FRAMEWORK
using UnityEditor;
namespace UnityRhi.Dlss.Urp.Editor
{
    [CustomEditor(typeof(UnityRhiDlssOptions))]
    internal sealed class UnityRhiDlssOptionsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Legacy DLSS settings retained for migration. Configure the Tsukuyomi Upscaler options on this URP asset or open Project Settings > Tsukuyomi RP.", MessageType.Info);
            using (new EditorGUI.DisabledScope(true)) DrawDefaultInspector();
        }
    }
}
#endif
