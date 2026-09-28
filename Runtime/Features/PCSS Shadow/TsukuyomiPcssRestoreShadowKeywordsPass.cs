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

        public override void CollectTextureSlots(System.Collections.Generic.List<TextureSlot> slots)
        {
            slots.Add(activeColor);
        }

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

        private sealed class RenderData
        {
            public UniversalShadowData ShadowData;
        }

        public override void Record(in RasterPassContext context)
        {
            var passData = context.GetOrCreateData<RenderData>();
            if (!s_KeywordsInitialized)
                return;

            TextureHandle activeColorTexture = context.Resources.ActiveColor;
            if (!activeColorTexture.IsValid())
                return;

            passData.ShadowData = context.FrameData.Get<UniversalShadowData>();

            context.Builder.SetRenderAttachment(activeColorTexture, 0, AccessFlags.Write);
            context.Builder.AllowGlobalStateModification(true);

            context.SetRenderFunc(passData, static (state, graphContext) =>
            {
                int cascadesCount = state.ShadowData.mainLightShadowCascadesCount;
                bool mainLightShadows = state.ShadowData.supportsMainLightShadows;
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
