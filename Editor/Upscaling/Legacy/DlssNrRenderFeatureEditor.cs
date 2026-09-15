using UnityEditor;
namespace UnityRhi.Dlss.Urp.Editor
{
    [CustomEditor(typeof(DlssNrRenderFeature))]
    internal sealed class DlssNrRenderFeatureEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Legacy migration component. Neural Rendering now runs through TsukuyomiFeature after post processing. Configure it in Project Settings > Tsukuyomi RP and the DLSS Neural Rendering Volume. This component can be removed from the renderer.", MessageType.Info);
        }
    }
}
