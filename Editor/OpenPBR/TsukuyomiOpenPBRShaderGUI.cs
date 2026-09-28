using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering.Editor
{
    public sealed class TsukuyomiOpenPBRShaderGUI : UnityEditor.ShaderGUI
    {
        public override void ValidateMaterial(Material material)
        {
            CoreUtils.SetKeyword(material, "_NORMALMAP", material.GetTexture("_BumpMap") != null);
            Color emission = material.GetColor("_EmissionColor");
            CoreUtils.SetKeyword(material, "_EMISSION", emission.maxColorComponent > 0);
            material.globalIlluminationFlags = emission.maxColorComponent > 0
                ? MaterialGlobalIlluminationFlags.BakedEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }
    }
}
