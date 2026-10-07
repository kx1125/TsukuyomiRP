using UnityEngine;
using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering
{
    [CreateAssetMenu(menuName = "Tsukuyomi RP/Rect Light Resources", fileName = "RectLightResources")]
    public sealed class TsukuyomiRectLightResources : ScriptableObject
    {
        [SerializeField] private TextAsset ggxMatrix, ggxAmplitude, disneyMatrix, disneyFGD;
        public Texture2D GGXMatrix { get; private set; }
        public Texture2D GGXAmplitude { get; private set; }
        public Texture2D DisneyMatrix { get; private set; }
        public Texture2D DisneyFGD { get; private set; }
        internal RTHandle[] Handles;
        private int _users;
        private bool _warned;

        public bool EnsureLoaded()
        {
            if (GGXMatrix && GGXAmplitude && DisneyMatrix && DisneyFGD) return true;
            ReleaseTextures();
            if (!Valid(ggxMatrix, 32768) || !Valid(ggxAmplitude, 32768)
                || !Valid(disneyMatrix, 32768) || !Valid(disneyFGD, 8192))
            {
                if (!_warned) Debug.LogWarning("Tsukuyomi Rect Light: missing or invalid LUT payloads; light contribution disabled.", this);
                _warned = true;
                return false;
            }
            GGXMatrix = Make(ggxMatrix, TextureFormat.RGBAHalf);
            GGXAmplitude = Make(ggxAmplitude, TextureFormat.RGBAHalf);
            DisneyMatrix = Make(disneyMatrix, TextureFormat.RGBAHalf);
            DisneyFGD = Make(disneyFGD, TextureFormat.RHalf);
            Handles = new[] { RTHandles.Alloc(GGXMatrix), RTHandles.Alloc(GGXAmplitude),
                RTHandles.Alloc(DisneyMatrix), RTHandles.Alloc(DisneyFGD) };
            _warned = false;
            return true;
        }

        private static bool Valid(TextAsset asset, int size) => asset && asset.bytes.Length == size;
        private static Texture2D Make(TextAsset asset, TextureFormat format)
        {
            var texture = new Texture2D(64, 64, format, false, true)
            { name = "Rect Light " + asset.name, filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            texture.LoadRawTextureData(asset.bytes);
            texture.Apply(false, true);
            return texture;
        }

        internal void Acquire() => _users++;
        internal void Release()
        {
            _users = Mathf.Max(0, _users - 1);
            if (_users == 0) ReleaseTextures();
        }
        private void ReleaseTextures()
        {
            TsukuyomiRectLightPass.ClearGlobals();
            if (Handles != null) foreach (var handle in Handles) handle?.Release();
            Handles = null;
            CoreUtils.Destroy(GGXMatrix); CoreUtils.Destroy(GGXAmplitude);
            CoreUtils.Destroy(DisneyMatrix); CoreUtils.Destroy(DisneyFGD);
            GGXMatrix = GGXAmplitude = DisneyMatrix = DisneyFGD = null;
        }
        private void OnValidate() { _warned = false; ReleaseTextures(); }
        private void OnDisable() { _users = 0; ReleaseTextures(); }
    }
}
