using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering.Editor.ShaderGUI
{
    public sealed class TsukuyomiPBRShaderGUI : UnityEditor.ShaderGUI
    {
        private const string AlphaTestKeyword = "_ALPHATEST_ON";
        private const string NormalMapKeyword = "_NORMALMAP";
        private const string EmissionKeyword = "_EMISSION";
        private const string SurfaceTransparentKeyword = "_SURFACE_TYPE_TRANSPARENT";
        private const string AlphaPremultiplyKeyword = "_ALPHAPREMULTIPLY_ON";
        private const string AlphaModulateKeyword = "_ALPHAMODULATE_ON";
        private const string SpecularHighlightsOffKeyword = "_SPECULARHIGHLIGHTS_OFF";
        private const string EnvironmentReflectionsOffKeyword = "_ENVIRONMENTREFLECTIONS_OFF";
        private const string ReceiveShadowsOffKeyword = "_RECEIVE_SHADOWS_OFF";

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            EditorGUI.BeginChangeCheck();
            materialEditor.PropertiesDefaultGUI(properties);

            if (EditorGUI.EndChangeCheck())
            {
                foreach (Object target in materialEditor.targets)
                {
                    if (target is Material material)
                    {
                        ValidateMaterial(material);
                    }
                }
            }
        }

        public override void ValidateMaterial(Material material)
        {
            SetupKeywords(material);
            SetupSurface(material);
        }

        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        {
            base.AssignNewShaderToMaterial(material, oldShader, newShader);
            ValidateMaterial(material);
        }

        private static void SetupKeywords(Material material)
        {
            SetKeyword(material, AlphaTestKeyword, GetFloat(material, "_AlphaClip") > 0.5f);
            SetKeyword(material, NormalMapKeyword, material.HasProperty("_BumpMap") && material.GetTexture("_BumpMap") != null);
            SetKeyword(material, EmissionKeyword, IsEmissionEnabled(material));
            SetKeyword(material, SpecularHighlightsOffKeyword, material.HasProperty("_SpecularHighlights") && material.GetFloat("_SpecularHighlights") == 0.0f);
            SetKeyword(material, EnvironmentReflectionsOffKeyword, material.HasProperty("_EnvironmentReflections") && material.GetFloat("_EnvironmentReflections") == 0.0f);
            SetKeyword(material, ReceiveShadowsOffKeyword, material.HasProperty("_ReceiveShadows") && material.GetFloat("_ReceiveShadows") == 0.0f);

            if (material.HasProperty("_MainTex") && material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_MainTex", material.GetTexture("_BaseMap"));
                material.SetTextureScale("_MainTex", material.GetTextureScale("_BaseMap"));
                material.SetTextureOffset("_MainTex", material.GetTextureOffset("_BaseMap"));
            }

            if (material.HasProperty("_Color") && material.HasProperty("_BaseColor"))
            {
                material.SetColor("_Color", material.GetColor("_BaseColor"));
            }
        }

        private static void SetupSurface(Material material)
        {
            bool alphaClip = GetFloat(material, "_AlphaClip") > 0.5f;
            bool transparent = GetFloat(material, "_Surface") > 0.5f;
            int queueOffset = Mathf.Clamp((int)GetFloat(material, "_QueueOffset"), -50, 50);

            SetKeyword(material, SurfaceTransparentKeyword, transparent);

            if (transparent)
            {
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = (int)RenderQueue.Transparent + queueOffset;
                SetFloat(material, "_ZWrite", 0.0f);
                SetupTransparentBlend(material);
            }
            else
            {
                material.SetOverrideTag("RenderType", alphaClip ? "TransparentCutout" : "Opaque");
                material.renderQueue = (alphaClip ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry) + queueOffset;
                SetFloat(material, "_SrcBlend", (float)BlendMode.One);
                SetFloat(material, "_DstBlend", (float)BlendMode.Zero);
                SetFloat(material, "_SrcBlendAlpha", (float)BlendMode.One);
                SetFloat(material, "_DstBlendAlpha", (float)BlendMode.Zero);
                SetFloat(material, "_ZWrite", 1.0f);
                SetKeyword(material, AlphaPremultiplyKeyword, false);
                SetKeyword(material, AlphaModulateKeyword, false);
            }
        }

        private static void SetupTransparentBlend(Material material)
        {
            int blendMode = Mathf.RoundToInt(GetFloat(material, "_Blend"));

            SetKeyword(material, AlphaPremultiplyKeyword, blendMode == 1);
            SetKeyword(material, AlphaModulateKeyword, blendMode == 3);

            switch (blendMode)
            {
                case 1:
                    SetFloat(material, "_SrcBlend", (float)BlendMode.One);
                    SetFloat(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    SetFloat(material, "_SrcBlendAlpha", (float)BlendMode.One);
                    SetFloat(material, "_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                    break;
                case 2:
                    SetFloat(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
                    SetFloat(material, "_DstBlend", (float)BlendMode.One);
                    SetFloat(material, "_SrcBlendAlpha", (float)BlendMode.One);
                    SetFloat(material, "_DstBlendAlpha", (float)BlendMode.One);
                    break;
                case 3:
                    SetFloat(material, "_SrcBlend", (float)BlendMode.DstColor);
                    SetFloat(material, "_DstBlend", (float)BlendMode.Zero);
                    SetFloat(material, "_SrcBlendAlpha", (float)BlendMode.DstAlpha);
                    SetFloat(material, "_DstBlendAlpha", (float)BlendMode.Zero);
                    break;
                default:
                    SetFloat(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
                    SetFloat(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    SetFloat(material, "_SrcBlendAlpha", (float)BlendMode.One);
                    SetFloat(material, "_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                    break;
            }
        }

        private static bool IsEmissionEnabled(Material material)
        {
            if (!material.HasProperty("_EmissionColor"))
            {
                return false;
            }

            Color emissionColor = material.GetColor("_EmissionColor");
            bool enabled = emissionColor.maxColorComponent > 0.0f;
            material.globalIlluminationFlags = enabled
                ? material.globalIlluminationFlags & ~MaterialGlobalIlluminationFlags.EmissiveIsBlack
                : material.globalIlluminationFlags | MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            return enabled;
        }

        private static float GetFloat(Material material, string name)
        {
            return material.HasProperty(name) ? material.GetFloat(name) : 0.0f;
        }

        private static void SetFloat(Material material, string name, float value)
        {
            if (material.HasProperty(name))
            {
                material.SetFloat(name, value);
            }
        }

        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled)
            {
                material.EnableKeyword(keyword);
            }
            else
            {
                material.DisableKeyword(keyword);
            }
        }
    }
}
