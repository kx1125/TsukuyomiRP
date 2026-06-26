using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering
{
    [System.Serializable]
    internal sealed class TsukuyomiPcssRestoreShadowKeywordsPass : RasterPass
    {
        private static GlobalKeyword MainLightShadowsKeyword;
        private static GlobalKeyword MainLightShadowCascadesKeyword;
        private static GlobalKeyword MainLightShadowScreenKeyword;
        private static GlobalKeyword ContactShadowsKeyword;
        private static bool s_KeywordsInitialized;

        [Write(BuiltinTexture.ActiveColor)]
        public TextureSlot activeColor = TextureSlot.Write("ActiveColor", BuiltinTexture.ActiveColor);

        public override string Name => "Tsukuyomi Restore Main Light Shadow Keywords";

        internal static void InitializeKeywords()
        {
            if (s_KeywordsInitialized)
                return;

            MainLightShadowsKeyword = GlobalKeyword.Create(ShaderKeywordStrings.MainLightShadows);
            MainLightShadowCascadesKeyword = GlobalKeyword.Create(ShaderKeywordStrings.MainLightShadowCascades);
            MainLightShadowScreenKeyword = GlobalKeyword.Create(ShaderKeywordStrings.MainLightShadowScreen);
            ContactShadowsKeyword = GlobalKeyword.Create("_CONTACT_SHADOWS");
            s_KeywordsInitialized = true;
        }

        public override void Record(in RasterPassContext context)
        {
            if (!s_KeywordsInitialized)
                return;

            TextureHandle activeColorTexture = context.Resources.ActiveColor;
            if (!activeColorTexture.IsValid())
                return;

            UniversalShadowData shadowData = context.FrameData.Get<UniversalShadowData>();

            context.Builder.SetRenderAttachment(activeColorTexture, 0, AccessFlags.Write);
            context.Builder.AllowGlobalStateModification(true);
            context.SetRenderFunc((data, graphContext) =>
            {
                int cascadesCount = shadowData.mainLightShadowCascadesCount;
                bool mainLightShadows = shadowData.supportsMainLightShadows;
                bool receiveShadowsNoCascade = mainLightShadows && cascadesCount == 1;
                bool receiveShadowsCascades = mainLightShadows && cascadesCount > 1;

                graphContext.cmd.SetKeyword(MainLightShadowScreenKeyword, false);
                graphContext.cmd.SetKeyword(ContactShadowsKeyword, false);
                graphContext.cmd.SetKeyword(MainLightShadowsKeyword, receiveShadowsNoCascade);
                graphContext.cmd.SetKeyword(MainLightShadowCascadesKeyword, receiveShadowsCascades);
            });
        }
    }
}
