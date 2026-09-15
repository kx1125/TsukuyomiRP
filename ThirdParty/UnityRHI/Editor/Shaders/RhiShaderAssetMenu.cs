using UnityEditor;

namespace UnityRhi.EditorTools
{
    internal static class RhiShaderAssetMenu
    {
        [MenuItem("Assets/Create/UnityRHI/RHI Shader")]
        private static void CreateShaderModule() =>
            CreateAssetWithContent("New RHI Shader.rhishader", "");

        [MenuItem("Assets/Create/UnityRHI/Shader Keywords")]
        private static void CreateShaderKeywords() =>
            CreateAssetWithContent(
                "New RHI Shader Keywords.rhikeywords", "");

        private static void CreateAssetWithContent(string filename, string content)
        {
#if UNITY_6000_5_OR_NEWER
            ProjectWindowUtil.CreateAssetWithTextContent(filename, content);
#else
            ProjectWindowUtil.CreateAssetWithContent(filename, content);
#endif
        }
    }
}
