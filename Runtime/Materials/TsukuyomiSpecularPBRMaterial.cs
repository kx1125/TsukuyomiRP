using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering
{
    public enum TsukuyomiSpecularPBRMaterialType { Common = 0, Skin = 1 }

    /// <summary>Keeps serialized properties, local keywords and LightMode pass state in sync.</summary>
    public static class TsukuyomiSpecularPBRMaterial
    {
        public const string ShaderName = "TsukuyomiRP/Lit/SpecularPBR";
        public const string SkinKeyword = "_SSS_ON";

        public static void SetMaterialType(Material material, TsukuyomiSpecularPBRMaterialType type)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            if (material.shader == null || material.shader.name != ShaderName)
                throw new ArgumentException("Material must use " + ShaderName, nameof(material));
            material.SetFloat("_MaterialType", type == TsukuyomiSpecularPBRMaterialType.Skin ? 1 : 0);
            Validate(material);
        }

        public static void Validate(Material material)
        {
            if (material == null || material.shader == null || material.shader.name != ShaderName) return;
            bool skin = material.GetFloat("_MaterialType") > 0.5f;
            material.SetFloat("_MaterialType", skin ? 1 : 0);
            CoreUtils.SetKeyword(material, SkinKeyword, skin);
            // Unity's API takes the LightMode tag, not the ShaderLab Name.
            material.SetShaderPassEnabled("SSSSkinMask", skin);
            material.SetShaderPassEnabled("SSSSkinLighting", skin);
            bool clip = material.GetFloat("_AlphaClip") > 0.5f;
            CoreUtils.SetKeyword(material, "_ALPHATEST_ON", clip);
            CoreUtils.SetKeyword(material, "_NORMALMAP", material.GetTexture("_BumpMap") != null);
            CoreUtils.SetKeyword(material, "ENABLE_DETAIL_NORMALMAP", material.GetFloat("_DetailNormal") > 0.5f);
            CoreUtils.SetKeyword(material, "TRANSMISSION", skin && material.GetFloat("_Transmission") > 0.5f);
            CoreUtils.SetKeyword(material, "_RECEIVE_SHADOWS_OFF", material.GetFloat("_ReceiveShadows") == 0);
            CoreUtils.SetKeyword(material, "_SPECULARHIGHLIGHTS_OFF", material.GetFloat("_SpecularHighlights") == 0);
            CoreUtils.SetKeyword(material, "_ENVIRONMENTREFLECTIONS_OFF", material.GetFloat("_EnvironmentReflections") == 0);
            bool emission = material.GetColor("_EmissionColor").maxColorComponent > 0;
            CoreUtils.SetKeyword(material, "_EMISSION", emission);
            var flags = material.globalIlluminationFlags;
            flags &= ~MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            if (!emission) flags |= MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            // Preserve the user's None / Baked / Realtime GI selection.
            material.globalIlluminationFlags = flags;
            material.SetOverrideTag("RenderType", clip ? "TransparentCutout" : "Opaque");
            material.renderQueue = (int)(clip ? RenderQueue.AlphaTest : RenderQueue.Geometry)
                + Mathf.Clamp(Mathf.RoundToInt(material.GetFloat("_QueueOffset")), -50, 50);
        }
    }
}
