using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering
{
    /// <summary>Precomputed OpenPBR tables. Uploads once, never integrates on the GPU.</summary>
    [CreateAssetMenu(menuName = "Tsukuyomi RP/OpenPBR Resources", fileName = "OpenPBRResources")]
    public sealed class TsukuyomiOpenPBRResources : ScriptableObject
    {
        [SerializeField] private TextAsset dfg;
        [SerializeField] private TextAsset dielectric;
        [SerializeField] private TextAsset fuzz;
        private Texture2D _dfgTexture, _fuzzTexture;
        private Texture3D _dielectricTexture;
        private static readonly int DfgId = Shader.PropertyToID("_TsukuyomiOpenPBRDFG");
        private static readonly int DielectricId = Shader.PropertyToID("_TsukuyomiOpenPBRDielectric");
        private static readonly int FuzzId = Shader.PropertyToID("_TsukuyomiOpenPBRFuzz");
        public const int PayloadBytes = 32768 + 65536 + 8192;

        public bool IsValid => dfg != null && dielectric != null && fuzz != null
            && dfg.bytes.Length == 32768 && dielectric.bytes.Length == 65536 && fuzz.bytes.Length == 8192;

        public void EnsureLoaded()
        {
            if (_dfgTexture != null && _dielectricTexture != null && _fuzzTexture != null) return;
            if (!IsValid) throw new InvalidOperationException("OpenPBR LUT payloads are missing or have incorrect dimensions. Rebuild using Tools~/OpenPBR/build_luts.py.");
            Release();
            _dfgTexture = Make2D("OpenPBR DFG", 64, dfg.bytes);
            _fuzzTexture = Make2D("OpenPBR Fuzz LTC", 32, fuzz.bytes);
            _dielectricTexture = new Texture3D(32, 32, 32, TextureFormat.RHalf, false)
            { name = "OpenPBR Dielectric Energy", wrapMode = TextureWrapMode.Clamp,
              filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            _dielectricTexture.SetPixelData(dielectric.bytes, 0);
            _dielectricTexture.Apply(false, true);
        }

        private static Texture2D Make2D(string label, int size, byte[] bytes)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBAHalf, false, true)
            { name = label, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
              hideFlags = HideFlags.HideAndDontSave };
            texture.LoadRawTextureData(bytes);
            texture.Apply(false, true);
            return texture;
        }

        public void Bind()
        {
            EnsureLoaded();
            Shader.SetGlobalTexture(DfgId, _dfgTexture);
            Shader.SetGlobalTexture(DielectricId, _dielectricTexture);
            Shader.SetGlobalTexture(FuzzId, _fuzzTexture);
        }

        public void Bind(ComputeShader shader, int kernel)
        {
            EnsureLoaded();
            shader.SetTexture(kernel, DfgId, _dfgTexture);
            shader.SetTexture(kernel, DielectricId, _dielectricTexture);
            shader.SetTexture(kernel, FuzzId, _fuzzTexture);
        }

        public void Release()
        {
            CoreUtils.Destroy(_dfgTexture); CoreUtils.Destroy(_fuzzTexture); CoreUtils.Destroy(_dielectricTexture);
            _dfgTexture = null; _fuzzTexture = null; _dielectricTexture = null;
        }

        private void OnDisable() => Release();
    }

    /// <summary>Reuses the existing preloaded pipeline resource asset in Editor and Player.</summary>
    public static class TsukuyomiOpenPBRResourceBinding
    {
        private static TsukuyomiOpenPBRResources s_Bound;
        private static bool s_Warned;

        // Edit-mode cameras also need the LUTs after imports and assembly reloads.
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Initialize()
        {
            RenderPipelineManager.beginCameraRendering -= BindCamera;
            RenderPipelineManager.beginCameraRendering += BindCamera;
            s_Bound = null;
            s_Warned = false;
        }

        private static void BindCamera(ScriptableRenderContext context, Camera camera)
        {
            var pipelineResources = TsukuyomiRenderPipelineResourcesProvider.Current;
            if (pipelineResources == null || pipelineResources.OpenPBRResources == null) return;
            var resources = pipelineResources.OpenPBRResources;
            // TextAsset.bytes allocates a copy. Validate only when the resource changes;
            // EnsureLoaded validates again if a released texture needs to be rebuilt.
            if (s_Bound != resources && !resources.IsValid)
            {
                if (!s_Warned) Debug.LogError("Tsukuyomi OpenPBR LUT resources are incomplete.", resources);
                s_Warned = true;
                return;
            }
            // Binding each camera also recovers after tests/custom rendering changed globals.
            resources.Bind();
            s_Bound = resources;
        }

        public static void Release()
        {
            RenderPipelineManager.beginCameraRendering -= BindCamera;
            if (s_Bound != null) s_Bound.Release();
            s_Bound = null;
        }
    }
}
