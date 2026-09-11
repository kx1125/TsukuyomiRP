using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering
{
    public class TsukuyomiFeature : ScriptableRendererFeature
    {
        public TsukuyomiPipelineProfile Profile;

        private static bool s_WarnedPerObjectShadowRenderingLayersDisabled;

        private Light _perObjectShadowMainLight;
        private bool _hasMainLightShadowLayerOverride;
        private bool _originalMainLightCustomShadowLayers;
        private uint _originalMainLightShadowRenderingLayers;

        private PassRegistry _registry;
        private PassRegistry _contactShadowRegistry;
        private PassRegistry _contactShadowDenoiseRegistry;
        private PassRegistry _depthPyramidRegistry;
        private PassRegistry _gtaoRegistry;
        private PassRegistry _gtaoRestoreRegistry;
        private PassRegistry _ssgiRegistry;
        private PassRegistry _ssgiRestoreRegistry;
        private PassRegistry _ssgiDebugRegistry;
        private PassRegistry _pcssRegistry;
        private PassRegistry _pcssRestoreRegistry;
        private PassRegistry _planarReflectionRegistry;
        private PassRegistry _volumeLightRegistry;
        private PassRegistry _sssSkinRegistry;
        private PassRegistry _postProcessRegistry;
        private ResourceHub _resourceHub;
        private FrameContext _enqueueFrame;
        private readonly List<TsukuyomiBridgePass> _bridgePasses = new();
        private TsukuyomiBridgePass _contactShadowBridgePass;
        private TsukuyomiBridgePass _contactShadowDenoiseBridgePass;
        private TsukuyomiBridgePass _depthPyramidBridgePass;
        private TsukuyomiBridgePass _gtaoBridgePass;
        private TsukuyomiBridgePass _gtaoRestoreBridgePass;
        private TsukuyomiBridgePass _ssgiBridgePass;
        private TsukuyomiBridgePass _ssgiRestoreBridgePass;
        private TsukuyomiBridgePass _ssgiDebugBridgePass;
        private TsukuyomiBridgePass _pcssBridgePass;
        private TsukuyomiBridgePass _pcssRestoreBridgePass;
        private TsukuyomiBridgePass _planarReflectionBridgePass;
        private TsukuyomiBridgePass _volumeLightBridgePass;
        private TsukuyomiBridgePass _sssSkinBridgePass;
        private TsukuyomiBridgePass _postProcessBridgePass;
        private TsukuyomiContactShadowPass _contactShadowPass;
        private TsukuyomiContactShadowDenoisePass _contactShadowDenoisePass;
        private TsukuyomiDepthPyramidPass _depthPyramidPass;
        private TsukuyomiGroundTruthAmbientOcclusionPass _gtaoPass;
        private TsukuyomiGroundTruthAmbientOcclusionRestoreKeywordsPass _gtaoRestorePass;
        private TsukuyomiScreenSpaceGlobalIlluminationPass _ssgiPass;
        private TsukuyomiScreenSpaceGlobalIlluminationRestorePass _ssgiRestorePass;
        private TsukuyomiScreenSpaceGlobalIlluminationDebugPass _ssgiDebugPass;
        private TsukuyomiPcssScreenSpaceShadowPass _pcssPass;
        private TsukuyomiPcssRestoreShadowKeywordsPass _pcssRestorePass;
        private TsukuyomiPlanarReflectionPass _planarReflectionPass;
        private TsukuyomiPerObjectShadowCasterPass _perObjectShadowPass;
        private TsukuyomiVolumetricFogPass _volumeLightPass;
        private TsukuyomiSssSkinPass _sssSkinPass;
        private TsukuyomiPostProcessPass _postProcessPass;
#if ENABLE_UPSCALER_FRAMEWORK
        private TsukuyomiFsr3OpaqueOnlyCapturePass _fsr3OpaqueOnlyCapturePass;
        private TsukuyomiFsr3ManualMaskPass _fsr3ManualMaskPass;
#endif

        private void OnEnable()
        {
            TsukuyomiPcssScreenSpaceShadowPass.InitializeKeywords();
            TsukuyomiPcssRestoreShadowKeywordsPass.InitializeKeywords();
            TsukuyomiGroundTruthAmbientOcclusionPass.InitializeKeywords();
            TsukuyomiScreenSpaceGlobalIlluminationPass.InitializeKeyword();
            TsukuyomiVolumetricFogPass.InitializeKeywords();
        }

        public override void Create()
        {
            _registry = new PassRegistry();
            _contactShadowRegistry = new PassRegistry();
            _contactShadowDenoiseRegistry = new PassRegistry();
            _depthPyramidRegistry = new PassRegistry();
            _gtaoRegistry = new PassRegistry();
            _gtaoRestoreRegistry = new PassRegistry();
            _ssgiRegistry = new PassRegistry();
            _ssgiRestoreRegistry = new PassRegistry();
            _ssgiDebugRegistry = new PassRegistry();
            _pcssRegistry = new PassRegistry();
            _pcssRestoreRegistry = new PassRegistry();
            _planarReflectionRegistry = new PassRegistry();
            _volumeLightRegistry = new PassRegistry();
            _sssSkinRegistry = new PassRegistry();
            _postProcessRegistry = new PassRegistry();
            _resourceHub?.Dispose();
            _resourceHub = new ResourceHub();
            _bridgePasses.Clear();

            _contactShadowPass ??= new TsukuyomiContactShadowPass();
            _contactShadowDenoisePass ??= new TsukuyomiContactShadowDenoisePass();
            _depthPyramidPass ??= new TsukuyomiDepthPyramidPass();
            _gtaoPass ??= new TsukuyomiGroundTruthAmbientOcclusionPass();
            _gtaoRestorePass ??= new TsukuyomiGroundTruthAmbientOcclusionRestoreKeywordsPass();
            _ssgiPass ??= new TsukuyomiScreenSpaceGlobalIlluminationPass();
            _ssgiRestorePass ??= new TsukuyomiScreenSpaceGlobalIlluminationRestorePass();
            _ssgiDebugPass ??= new TsukuyomiScreenSpaceGlobalIlluminationDebugPass();
            _pcssPass ??= new TsukuyomiPcssScreenSpaceShadowPass();
            _pcssRestorePass ??= new TsukuyomiPcssRestoreShadowKeywordsPass();
            _planarReflectionPass ??= new TsukuyomiPlanarReflectionPass();
            _perObjectShadowPass ??= new TsukuyomiPerObjectShadowCasterPass();
            _volumeLightPass ??= new TsukuyomiVolumetricFogPass();
            _sssSkinPass ??= new TsukuyomiSssSkinPass();
            _postProcessPass ??= new TsukuyomiPostProcessPass();
#if ENABLE_UPSCALER_FRAMEWORK
            _fsr3OpaqueOnlyCapturePass ??= new TsukuyomiFsr3OpaqueOnlyCapturePass();
            _fsr3ManualMaskPass ??= new TsukuyomiFsr3ManualMaskPass();
#endif

            _contactShadowPass.InjectionPoint = InjectionPoint.BeforeOpaque;
            _contactShadowDenoisePass.InjectionPoint = InjectionPoint.BeforeOpaque;
            _depthPyramidPass.InjectionPoint = InjectionPoint.BeforeOpaque;
            _gtaoPass.InjectionPoint = InjectionPoint.BeforeOpaque;
            _gtaoRestorePass.InjectionPoint = InjectionPoint.BeforePostProcess;
            _ssgiPass.InjectionPoint = InjectionPoint.BeforeOpaque;
            _ssgiRestorePass.InjectionPoint = InjectionPoint.AfterOpaque;
            _ssgiDebugPass.InjectionPoint = InjectionPoint.BeforePostProcess;
            _pcssPass.InjectionPoint = InjectionPoint.BeforeOpaque;
            _pcssRestorePass.InjectionPoint = InjectionPoint.BeforePostProcess;
            _planarReflectionPass.InjectionPoint = InjectionPoint.BeforeOpaque;
            _volumeLightPass.InjectionPoint = InjectionPoint.BeforePostProcess;
            _sssSkinPass.InjectionPoint = InjectionPoint.BeforeOpaque;
            _postProcessPass.InjectionPoint = InjectionPoint.BeforePostProcess;

            _contactShadowRegistry.AddPass(_contactShadowPass);
            _contactShadowDenoiseRegistry.AddPass(_contactShadowDenoisePass);
            _depthPyramidRegistry.AddPass(_depthPyramidPass);
            _gtaoRegistry.AddPass(_gtaoPass);
            _gtaoRestoreRegistry.AddPass(_gtaoRestorePass);
            _ssgiRegistry.AddPass(_ssgiPass);
            _ssgiRestoreRegistry.AddPass(_ssgiRestorePass);
            _ssgiDebugRegistry.AddPass(_ssgiDebugPass);
            _pcssRegistry.AddPass(_pcssPass);
            _pcssRestoreRegistry.AddPass(_pcssRestorePass);
            _planarReflectionRegistry.AddPass(_planarReflectionPass);
            _volumeLightRegistry.AddPass(_volumeLightPass);
            _sssSkinRegistry.AddPass(_sssSkinPass);
            _postProcessRegistry.AddPass(_postProcessPass);

            _contactShadowBridgePass = new TsukuyomiBridgePass(_contactShadowRegistry, _contactShadowPass.InjectionPoint, _resourceHub)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPrePasses + 1
            };
            _contactShadowDenoiseBridgePass = new TsukuyomiBridgePass(_contactShadowDenoiseRegistry, _contactShadowDenoisePass.InjectionPoint, _resourceHub)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPrePasses + 3
            };
            _depthPyramidBridgePass = new TsukuyomiBridgePass(_depthPyramidRegistry, _depthPyramidPass.InjectionPoint, _resourceHub)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPrePasses
            };
            _gtaoBridgePass = new TsukuyomiBridgePass(_gtaoRegistry, _gtaoPass.InjectionPoint, _resourceHub)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPrePasses + 2
            };
            _gtaoRestoreBridgePass = new TsukuyomiBridgePass(_gtaoRestoreRegistry, _gtaoRestorePass.InjectionPoint, _resourceHub)
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing - 1
            };
            _ssgiBridgePass = new TsukuyomiBridgePass(_ssgiRegistry, _ssgiPass.InjectionPoint, _resourceHub)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPrePasses + 4
            };
            _ssgiRestoreBridgePass = new TsukuyomiBridgePass(_ssgiRestoreRegistry, _ssgiRestorePass.InjectionPoint, _resourceHub)
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents
            };
            _ssgiDebugBridgePass = new TsukuyomiBridgePass(_ssgiDebugRegistry, _ssgiDebugPass.InjectionPoint, _resourceHub)
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
            };
            _pcssBridgePass = new TsukuyomiBridgePass(_pcssRegistry, _pcssPass.InjectionPoint, _resourceHub);
            _pcssRestoreBridgePass = new TsukuyomiBridgePass(_pcssRestoreRegistry, _pcssRestorePass.InjectionPoint, _resourceHub)
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents
            };
            _planarReflectionBridgePass = new TsukuyomiBridgePass(_planarReflectionRegistry, _planarReflectionPass.InjectionPoint, _resourceHub)
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingOpaques - 1
            };
            _volumeLightBridgePass = new TsukuyomiBridgePass(_volumeLightRegistry, _volumeLightPass.InjectionPoint, _resourceHub)
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing - 3
            };
            _sssSkinBridgePass = new TsukuyomiBridgePass(_sssSkinRegistry, _sssSkinPass.InjectionPoint, _resourceHub)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPrePasses + 5
            };
            _postProcessBridgePass = new TsukuyomiBridgePass(_postProcessRegistry, _postProcessPass.InjectionPoint, _resourceHub)
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing - 1
            };

            if (Profile != null && Profile.Passes != null)
            {
                foreach (RenderPassBase pass in Profile.Passes)
                    _registry.AddPass(pass);
            }

            foreach (InjectionPoint point in Enum.GetValues(typeof(InjectionPoint)))
            {
                var bridge = new TsukuyomiBridgePass(_registry, point, _resourceHub)
                {
                    renderPassEvent = MapInjectionPointToEvent(point)
                };
                _bridgePasses.Add(bridge);
            }
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            _registry.Synchronize(Profile != null ? Profile.Passes : null);
            _resourceHub.ReleaseDestroyedCameras();
            Camera camera = renderingData.cameraData.camera;
            _enqueueFrame = new FrameContext(renderingData.frameData,
                camera ? _resourceHub.ForCamera(camera) : _resourceHub);
#if ENABLE_UPSCALER_FRAMEWORK
            _fsr3ManualMaskPass.Configure(Profile);
            renderer.EnqueuePass(_fsr3OpaqueOnlyCapturePass);
            renderer.EnqueuePass(_fsr3ManualMaskPass);
#endif
            if (Profile == null)
            {
                RestoreMainLightShadowLayerOverride();
                TsukuyomiPerObjectShadowRenderer.RestoreRenderingLayersForAll();
                TsukuyomiPlanarReflectionPass.ClearGlobals();
                TsukuyomiScreenSpaceGlobalIlluminationPass.ClearGlobals();
                return;
            }

            VolumeStack volumeStack = VolumeManager.instance.stack;
            TsukuyomiPcssVolume pcssVolume = volumeStack?.GetComponent<TsukuyomiPcssVolume>();
            TsukuyomiPerObjectShadowVolume perObjectShadowVolume = volumeStack?.GetComponent<TsukuyomiPerObjectShadowVolume>();
            TsukuyomiContactShadowVolume contactShadowVolume = volumeStack?.GetComponent<TsukuyomiContactShadowVolume>();
            TsukuyomiGroundTruthAmbientOcclusionVolume gtaoVolume = volumeStack?.GetComponent<TsukuyomiGroundTruthAmbientOcclusionVolume>();
            TsukuyomiScreenSpaceGlobalIlluminationVolume ssgiVolume = volumeStack?.GetComponent<TsukuyomiScreenSpaceGlobalIlluminationVolume>();
            TsukuyomiVolumeLightVolume volumeLightVolume = volumeStack?.GetComponent<TsukuyomiVolumeLightVolume>();
            TsukuyomiSssSkinVolume sssSkinVolume = volumeStack?.GetComponent<TsukuyomiSssSkinVolume>();

            bool contactShadowsEnabled = _contactShadowPass != null && _contactShadowPass.Configure(Profile, contactShadowVolume);
            bool contactShadowDenoiseEnabled = _contactShadowDenoisePass != null && _contactShadowDenoisePass.Configure(Profile, contactShadowVolume);

            if (_planarReflectionPass != null && _planarReflectionPass.Configure(Profile))
            {
                EnqueueBridge(renderer, _planarReflectionBridgePass);
            }
            else
            {
                TsukuyomiPlanarReflectionPass.ClearGlobals();
            }

            if (contactShadowsEnabled)
            {
                EnqueueBridge(renderer, _contactShadowBridgePass);

                if (contactShadowDenoiseEnabled)
                {
                    EnqueueBridge(renderer, _contactShadowDenoiseBridgePass);
                }
            }

            bool usesDeferredLighting = UsesDeferredLighting(renderer);
            bool gtaoEnabled = _gtaoPass != null && _gtaoPass.Configure(Profile, gtaoVolume);
            bool ssgiEnabled = !usesDeferredLighting
                && _ssgiPass != null
                && _ssgiPass.Configure(Profile, ssgiVolume, ref renderingData);
            bool depthPyramidEnabled = (gtaoEnabled || ssgiEnabled)
                && _depthPyramidPass != null
                && _depthPyramidPass.Configure();
            if (depthPyramidEnabled)
            {
                EnqueueBridge(renderer, _depthPyramidBridgePass);
            }

            if (gtaoEnabled && depthPyramidEnabled)
            {
                EnqueueBridge(renderer, _gtaoBridgePass);
                EnqueueBridge(renderer, _gtaoRestoreBridgePass);
            }

            if (ssgiEnabled && depthPyramidEnabled)
            {
                EnqueueBridge(renderer, _ssgiBridgePass);
                EnqueueBridge(renderer, _ssgiRestoreBridgePass);
                if (_ssgiPass.DebugOutput && _ssgiDebugPass != null && _ssgiDebugPass.Configure())
                    EnqueueBridge(renderer, _ssgiDebugBridgePass);
            }
            else
            {
                TsukuyomiScreenSpaceGlobalIlluminationPass.ClearGlobals();
            }

            bool perObjectShadowsEnabled = _perObjectShadowPass != null && _perObjectShadowPass.Configure(Profile, perObjectShadowVolume, pcssVolume);
            if (perObjectShadowsEnabled)
            {
                TsukuyomiPerObjectShadowRenderer.ApplyRenderingLayerMaskToAll(Profile.PerObjectShadowRenderingLayer);
                ExcludePerObjectShadowLayerFromMainLight(ref renderingData, Profile.PerObjectShadowRenderingLayer);
                _perObjectShadowPass.renderPassEvent = usesDeferredLighting
                    ? RenderPassEvent.BeforeRenderingGbuffer - 1
                    : contactShadowsEnabled
                        ? RenderPassEvent.AfterRenderingPrePasses + 2
                        : RenderPassEvent.AfterRenderingPrePasses;
                renderer.EnqueuePass(_perObjectShadowPass);
            }
            else
            {
                RestoreMainLightShadowLayerOverride();
                TsukuyomiPerObjectShadowRenderer.RestoreRenderingLayersForAll();
            }

            if (_pcssPass != null && _pcssPass.Configure(Profile, pcssVolume, contactShadowsEnabled, contactShadowDenoiseEnabled, perObjectShadowsEnabled))
            {
                _pcssBridgePass.renderPassEvent = usesDeferredLighting
                    ? RenderPassEvent.BeforeRenderingGbuffer
                    : contactShadowsEnabled
                        ? RenderPassEvent.AfterRenderingPrePasses + 4
                        : RenderPassEvent.AfterRenderingPrePasses + 1;
                EnqueueBridge(renderer, _pcssBridgePass);
                EnqueueBridge(renderer, _pcssRestoreBridgePass);
            }

            if (_volumeLightPass != null && _volumeLightPass.Configure(Profile, volumeLightVolume))
            {
                EnqueueBridge(renderer, _volumeLightBridgePass);
            }

            if (_postProcessPass != null && _postProcessPass.Configure(Profile, volumeStack))
            {
                EnqueueBridge(renderer, _postProcessBridgePass);
            }

            bool useSharedDepthNormals = RequiresCameraNormals(
                contactShadowDenoiseEnabled,
                gtaoEnabled && depthPyramidEnabled,
                ssgiEnabled && depthPyramidEnabled);
            if (_sssSkinPass != null && _sssSkinPass.Configure(Profile, sssSkinVolume, useSharedDepthNormals))
            {
                EnqueueBridge(renderer, _sssSkinBridgePass);
            }

            foreach (TsukuyomiBridgePass bridge in _bridgePasses)
            {
                EnqueueBridge(renderer, bridge);
            }
        }

        private void EnqueueBridge(ScriptableRenderer renderer, TsukuyomiBridgePass bridge)
        {
            if (renderer == null || bridge == null)
                return;

            if (bridge.ConfigureInputFromTextureSlots(_enqueueFrame))
                renderer.EnqueuePass(bridge);
        }

        private bool RequiresCameraNormals(bool contactShadowDenoiseEnabled, bool gtaoEnabled, bool ssgiEnabled)
        {
            return (contactShadowDenoiseEnabled && PassRegistry.PassRequiresBuiltinTexture(_contactShadowDenoisePass, BuiltinTexture.CameraNormals))
                || (gtaoEnabled && PassRegistry.PassRequiresBuiltinTexture(_gtaoPass, BuiltinTexture.CameraNormals))
                || (ssgiEnabled && PassRegistry.PassRequiresBuiltinTexture(_ssgiPass, BuiltinTexture.CameraNormals))
                || (_registry != null && _registry.RequiresBuiltinTexture(BuiltinTexture.CameraNormals));
        }
        private RenderPassEvent MapInjectionPointToEvent(InjectionPoint point)
        {
            return point switch
            {
                InjectionPoint.BeforeRendering => RenderPassEvent.BeforeRendering,
                InjectionPoint.BeforeOpaque => RenderPassEvent.BeforeRenderingOpaques,
                InjectionPoint.AfterOpaque => RenderPassEvent.AfterRenderingOpaques,
                InjectionPoint.BeforeSkybox => RenderPassEvent.BeforeRenderingSkybox,
                InjectionPoint.BeforePostProcess => RenderPassEvent.BeforeRenderingPostProcessing,
                InjectionPoint.AfterPostProcess => RenderPassEvent.AfterRenderingPostProcessing,
                InjectionPoint.AfterRendering => RenderPassEvent.AfterRendering,
                _ => RenderPassEvent.AfterRendering
            };
        }

        private static bool UsesDeferredLighting(ScriptableRenderer renderer)
        {
            if (renderer == null)
                return false;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            PropertyInfo property = renderer.GetType().GetProperty("usesDeferredLighting", flags);
            return property != null && property.PropertyType == typeof(bool) && (bool)property.GetValue(renderer);
        }
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

        protected override void Dispose(bool disposing)
        {
            RestoreMainLightShadowLayerOverride();
            TsukuyomiPerObjectShadowRenderer.RestoreRenderingLayersForAll();
            _resourceHub?.Dispose();
            _resourceHub = null;
            _contactShadowPass = null;
            _contactShadowDenoisePass = null;
            _contactShadowBridgePass = null;
            _contactShadowDenoiseBridgePass = null;
            _depthPyramidPass = null;
            _depthPyramidBridgePass = null;
            _gtaoPass = null;
            _gtaoBridgePass = null;
            _gtaoRestorePass = null;
            _gtaoRestoreBridgePass = null;
            _ssgiPass = null;
            _ssgiBridgePass = null;
            _ssgiRestorePass = null;
            _ssgiRestoreBridgePass = null;
            _ssgiDebugPass?.Dispose();
            _ssgiDebugPass = null;
            _ssgiDebugBridgePass = null;
            _pcssPass?.Dispose();
            _pcssPass = null;
            _pcssRestorePass = null;
            _pcssBridgePass = null;
            _pcssRestoreBridgePass = null;
            _planarReflectionPass = null;
            _planarReflectionBridgePass = null;
            _perObjectShadowPass?.Dispose();
            _perObjectShadowPass = null;
            _volumeLightPass?.Dispose();
            _volumeLightPass = null;
            _volumeLightBridgePass = null;
            _postProcessPass?.Dispose();
            _postProcessPass = null;
            _postProcessBridgePass = null;
            _sssSkinPass?.Dispose();
            _sssSkinPass = null;
            _sssSkinBridgePass = null;
#if ENABLE_UPSCALER_FRAMEWORK
            _fsr3OpaqueOnlyCapturePass = null;
            _fsr3ManualMaskPass = null;
#endif
            base.Dispose(disposing);
        }
    }
}









