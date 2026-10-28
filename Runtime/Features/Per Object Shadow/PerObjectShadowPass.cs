using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering
{
    [Serializable, InjectionPoint(InjectionPoint.BeforeOpaque)]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Tsukuyomi.Rendering", "Tsukuyomi.Rendering", "PerObjectShadowPlugin")]
    public sealed class PerObjectShadowPass : RenderPassBase, IPerObjectShadowProvider
    {
        public PerObjectShadowPass() => InjectionPoint = InjectionPoint.BeforeOpaque;
        public override string Name => "PerObjectShadow";

        public RenderingLayerMask PerObjectShadowRenderingLayer = TsukuyomiPerObjectShadowDefaults.RenderingLayerMask;

        public TsukuyomiPerObjectShadowDepthBits PerObjectShadowDepthBits = TsukuyomiPerObjectShadowDepthBits.Depth16;

        public TsukuyomiPerObjectShadowTileResolution PerObjectShadowTileResolution = TsukuyomiPerObjectShadowTileResolution._1024;

        [Range(0.0f, 1000.0f)]
        public float PerObjectShadowLengthOffset = 500.0f;

        [NonSerialized] private TsukuyomiPerObjectShadowCasterPass _pass;
        private static bool s_WarnedPerObjectShadowRenderingLayersDisabled;
        [NonSerialized] private Light _perObjectShadowMainLight;
        [NonSerialized] private bool _hasMainLightShadowLayerOverride, _originalMainLightCustomShadowLayers;
        [NonSerialized] private uint _originalMainLightShadowRenderingLayers;
        [field: NonSerialized] public bool Active { get; private set; }
        public override void Build(PassPlanBuilder plan)
        {
            _pass = new TsukuyomiPerObjectShadowCasterPass();
            ShadowDependencies.RequireComposition(plan);
            plan.PrepareAfter<IContactShadowProvider>();
            plan.PrepareAfter<IPcssShadowSettingsProvider>();
            var main = plan.AddNative(_pass, RenderPassEvent.AfterRenderingPrePasses, () => Active,
                context => context.Deferred ? RenderPassEvent.BeforeRenderingGbuffer - 1
                    : context.Find<IContactShadowProvider>()?.Active == true ? RenderPassEvent.AfterRenderingPrePasses + 2 : RenderPassEvent.AfterRenderingPrePasses);
            plan.Provides(main, ShadowDependencies.PerObject);
            plan.Requires(main, ShadowDependencies.Contact, true);
        }
        public override void PrepareRecording(in PassCameraContext context, ref RenderingData data)
        {
            Active = !context.Frame.CameraData.isPreviewCamera && _pass.Configure(this, context.Volumes?.GetComponent<TsukuyomiPerObjectShadowVolume>(),
                context.Find<IPcssShadowSettingsProvider>()?.ShadowSettings ?? default);
            if (Active)
            {
                TsukuyomiPerObjectShadowRenderer.ApplyRenderingLayerMaskToAll(PerObjectShadowRenderingLayer);
                ExcludePerObjectShadowLayerFromMainLight(ref data, PerObjectShadowRenderingLayer);
            }
            else Deactivate(context.Camera);
        }
        public override void Deactivate(Camera camera)
        {
            Active = false; RestoreMainLightShadowLayerOverride();
            TsukuyomiPerObjectShadowRenderer.RestoreRenderingLayersForAll();
            Shader.SetGlobalInt("_PerObjSceneShadowCount", 0);
            Shader.SetGlobalTexture("_PerObjSceneShadowMap", Texture2D.whiteTexture);
        }
        public override void Dispose() { RestoreMainLightShadowLayerOverride(); _pass?.Dispose(); }
        private void ExcludePerObjectShadowLayerFromMainLight(ref RenderingData renderingData, RenderingLayerMask perObjectShadowRenderingLayer)
        {
            uint perObjectShadowLayerMask = perObjectShadowRenderingLayer;
            if (perObjectShadowLayerMask == 0)
            {
                RestoreMainLightShadowLayerOverride();
                return;
            }

            UniversalRenderPipelineAsset urpAsset = UniversalRenderPipeline.asset;
            if (urpAsset != null && !urpAsset.useRenderingLayers && !s_WarnedPerObjectShadowRenderingLayersDisabled)
            {
                Debug.LogWarning("Tsukuyomi Per Object Shadows require the URP Asset \"Use Rendering Layers\" option to exclude their casters from the main light shadow map.");
                s_WarnedPerObjectShadowRenderingLayersDisabled = true;
            }

            int mainLightIndex = renderingData.lightData.mainLightIndex;
            if (mainLightIndex < 0 || mainLightIndex >= renderingData.lightData.visibleLights.Length)
            {
                RestoreMainLightShadowLayerOverride();
                return;
            }

            VisibleLight mainLight = renderingData.lightData.visibleLights[mainLightIndex];
            if (!mainLight.light || mainLight.lightType != LightType.Directional)
            {
                RestoreMainLightShadowLayerOverride();
                return;
            }

            UniversalAdditionalLightData mainLightData = mainLight.light.GetUniversalAdditionalLightData();
            if (_hasMainLightShadowLayerOverride && _perObjectShadowMainLight != mainLight.light)
                RestoreMainLightShadowLayerOverride();

            if (!_hasMainLightShadowLayerOverride)
            {
                _perObjectShadowMainLight = mainLight.light;
                _originalMainLightCustomShadowLayers = mainLightData.customShadowLayers;
                _originalMainLightShadowRenderingLayers = (uint)mainLightData.shadowRenderingLayers;
                _hasMainLightShadowLayerOverride = true;
            }

            uint baseShadowLayers = _originalMainLightCustomShadowLayers
                ? _originalMainLightShadowRenderingLayers
                : uint.MaxValue;
            mainLightData.customShadowLayers = true;
            mainLightData.shadowRenderingLayers = baseShadowLayers & ~perObjectShadowLayerMask;
        }

        private void RestoreMainLightShadowLayerOverride()
        {
            if (!_hasMainLightShadowLayerOverride)
                return;

            if (_perObjectShadowMainLight)
            {
                UniversalAdditionalLightData mainLightData = _perObjectShadowMainLight.GetUniversalAdditionalLightData();
                mainLightData.customShadowLayers = _originalMainLightCustomShadowLayers;
                mainLightData.shadowRenderingLayers = _originalMainLightShadowRenderingLayers;
            }

            _perObjectShadowMainLight = null;
            _hasMainLightShadowLayerOverride = false;
        }

    }
}
