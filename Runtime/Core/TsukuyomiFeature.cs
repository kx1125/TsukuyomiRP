using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;

using System.Collections.Generic;
using System;
using System.Reflection;

namespace Tsukuyomi.Rendering
{
    public class TsukuyomiFeature : ScriptableRendererFeature
    {
        public TsukuyomiPipelineProfile Profile;

        private PassRegistry _registry;
        private PassRegistry _contactShadowRegistry;
        private PassRegistry _contactShadowDenoiseRegistry;
        private PassRegistry _depthPyramidRegistry;
        private PassRegistry _gtaoRegistry;
        private PassRegistry _gtaoRestoreRegistry;
        private PassRegistry _pcssRegistry;
        private PassRegistry _pcssRestoreRegistry;
        private PassRegistry _volumeLightRegistry;
        private PassRegistry _fsr3Registry;
        private ResourceHub _resourceHub;
        private List<TsukuyomiBridgePass> _bridgePasses = new();
        private TsukuyomiBridgePass _contactShadowBridgePass;
        private TsukuyomiBridgePass _contactShadowDenoiseBridgePass;
        private TsukuyomiBridgePass _depthPyramidBridgePass;
        private TsukuyomiBridgePass _gtaoBridgePass;
        private TsukuyomiBridgePass _gtaoRestoreBridgePass;
        private TsukuyomiBridgePass _pcssBridgePass;
        private TsukuyomiBridgePass _pcssRestoreBridgePass;
        private TsukuyomiBridgePass _volumeLightBridgePass;
        private TsukuyomiBridgePass _fsr3BridgePass;
        private TsukuyomiContactShadowPass _contactShadowPass;
        private TsukuyomiContactShadowDenoisePass _contactShadowDenoisePass;
        private TsukuyomiDepthPyramidPass _depthPyramidPass;
        private TsukuyomiGroundTruthAmbientOcclusionPass _gtaoPass;
        private TsukuyomiGroundTruthAmbientOcclusionRestoreKeywordsPass _gtaoRestorePass;
        private TsukuyomiPcssScreenSpaceShadowPass _pcssPass;
        private TsukuyomiPcssRestoreShadowKeywordsPass _pcssRestorePass;
        private TsukuyomiVolumetricFogPass _volumeLightPass;
        private TsukuyomiFsr3Pass _fsr3Pass;

        private void OnEnable()
        {
            TsukuyomiPcssScreenSpaceShadowPass.InitializeKeywords();
            TsukuyomiPcssRestoreShadowKeywordsPass.InitializeKeywords();
            TsukuyomiGroundTruthAmbientOcclusionPass.InitializeKeywords();
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
            _pcssRegistry = new PassRegistry();
            _pcssRestoreRegistry = new PassRegistry();
            _volumeLightRegistry = new PassRegistry();
            _fsr3Registry = new PassRegistry();
            _resourceHub?.Dispose();
            _resourceHub = new ResourceHub();
            _bridgePasses.Clear();
            _contactShadowPass ??= new TsukuyomiContactShadowPass();
            _contactShadowDenoisePass ??= new TsukuyomiContactShadowDenoisePass();
            _depthPyramidPass ??= new TsukuyomiDepthPyramidPass();
            _gtaoPass ??= new TsukuyomiGroundTruthAmbientOcclusionPass();
            _gtaoRestorePass ??= new TsukuyomiGroundTruthAmbientOcclusionRestoreKeywordsPass();
            _pcssPass ??= new TsukuyomiPcssScreenSpaceShadowPass();
            _pcssRestorePass ??= new TsukuyomiPcssRestoreShadowKeywordsPass();
            _volumeLightPass ??= new TsukuyomiVolumetricFogPass();
            _fsr3Pass ??= new TsukuyomiFsr3Pass();
            _contactShadowPass.InjectionPoint = InjectionPoint.BeforeOpaque;
            _contactShadowDenoisePass.InjectionPoint = InjectionPoint.BeforeOpaque;
            _depthPyramidPass.InjectionPoint = InjectionPoint.BeforeOpaque;
            _gtaoPass.InjectionPoint = InjectionPoint.BeforeOpaque;
            _gtaoRestorePass.InjectionPoint = InjectionPoint.BeforePostProcess;
            _pcssPass.InjectionPoint = InjectionPoint.BeforeOpaque;
            _pcssRestorePass.InjectionPoint = InjectionPoint.BeforePostProcess;
            _volumeLightPass.InjectionPoint = InjectionPoint.BeforePostProcess;
            _fsr3Pass.InjectionPoint = InjectionPoint.AfterPostProcess;
            _contactShadowRegistry.AddPass(_contactShadowPass);
            _contactShadowDenoiseRegistry.AddPass(_contactShadowDenoisePass);
            _depthPyramidRegistry.AddPass(_depthPyramidPass);
            _gtaoRegistry.AddPass(_gtaoPass);
            _gtaoRestoreRegistry.AddPass(_gtaoRestorePass);
            _pcssRegistry.AddPass(_pcssPass);
            _pcssRestoreRegistry.AddPass(_pcssRestorePass);
            _volumeLightRegistry.AddPass(_volumeLightPass);
            _fsr3Registry.AddPass(_fsr3Pass);
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
            _pcssBridgePass = new TsukuyomiBridgePass(_pcssRegistry, _pcssPass.InjectionPoint, _resourceHub);
            _pcssRestoreBridgePass = new TsukuyomiBridgePass(_pcssRestoreRegistry, _pcssRestorePass.InjectionPoint, _resourceHub)
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents
            };
            _volumeLightBridgePass = new TsukuyomiBridgePass(_volumeLightRegistry, _volumeLightPass.InjectionPoint, _resourceHub)
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing - 3
            };
            _fsr3BridgePass = new TsukuyomiBridgePass(_fsr3Registry, _fsr3Pass.InjectionPoint, _resourceHub)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing - 1
            };

            // Register passes from the baked Profile
            if (Profile != null && Profile.Passes != null)
            {
                foreach (var pass in Profile.Passes)
                {
                    _registry.AddPass(pass);
                }
            }

            // Create a bridge pass for each injection point
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
            if (Profile == null)
            {
                EnqueueFsr3Pass(renderer, ref renderingData);
                return;
            }

            VolumeStack volumeStack = VolumeManager.instance.stack;
            TsukuyomiPcssVolume pcssVolume = volumeStack?.GetComponent<TsukuyomiPcssVolume>();
            TsukuyomiContactShadowVolume contactShadowVolume = volumeStack?.GetComponent<TsukuyomiContactShadowVolume>();
            TsukuyomiGroundTruthAmbientOcclusionVolume gtaoVolume = volumeStack?.GetComponent<TsukuyomiGroundTruthAmbientOcclusionVolume>();
            TsukuyomiVolumeLightVolume volumeLightVolume = volumeStack?.GetComponent<TsukuyomiVolumeLightVolume>();

            bool contactShadowsEnabled = _contactShadowPass != null && _contactShadowPass.Configure(Profile, contactShadowVolume);
            bool contactShadowDenoiseEnabled = _contactShadowDenoisePass != null && _contactShadowDenoisePass.Configure(Profile, contactShadowVolume);

            if (contactShadowsEnabled)
            {
                _contactShadowBridgePass.ConfigureInputFromTextureSlots();
                renderer.EnqueuePass(_contactShadowBridgePass);

                if (contactShadowDenoiseEnabled)
                {
                    _contactShadowDenoiseBridgePass.ConfigureInputFromTextureSlots();
                    renderer.EnqueuePass(_contactShadowDenoiseBridgePass);
                }
            }

            bool gtaoEnabled = _gtaoPass != null && _gtaoPass.Configure(Profile, gtaoVolume);
            bool depthPyramidEnabled = gtaoEnabled && _depthPyramidPass != null && _depthPyramidPass.Configure();
            if (depthPyramidEnabled)
            {
                _depthPyramidBridgePass.ConfigureInputFromTextureSlots();
                renderer.EnqueuePass(_depthPyramidBridgePass);
            }

            if (gtaoEnabled && depthPyramidEnabled)
            {
                _gtaoBridgePass.ConfigureInputFromTextureSlots();
                renderer.EnqueuePass(_gtaoBridgePass);
                _gtaoRestoreBridgePass.ConfigureInputFromTextureSlots();
                renderer.EnqueuePass(_gtaoRestoreBridgePass);
            }

            if (_pcssPass != null && _pcssPass.Configure(Profile, pcssVolume, contactShadowsEnabled, contactShadowDenoiseEnabled))
            {
                bool usesDeferredLighting = UsesDeferredLighting(renderer);
                _pcssBridgePass.renderPassEvent = usesDeferredLighting
                    ? RenderPassEvent.BeforeRenderingGbuffer
                    : contactShadowsEnabled
                        ? RenderPassEvent.AfterRenderingPrePasses + 4
                        : RenderPassEvent.AfterRenderingPrePasses + 1;
                _pcssBridgePass.ConfigureInputFromTextureSlots();
                _pcssRestoreBridgePass.ConfigureInputFromTextureSlots();
                renderer.EnqueuePass(_pcssBridgePass);
                renderer.EnqueuePass(_pcssRestoreBridgePass);
            }

            if (_volumeLightPass != null && _volumeLightPass.Configure(Profile, volumeLightVolume))
            {
                _volumeLightBridgePass.ConfigureInputFromTextureSlots();
                renderer.EnqueuePass(_volumeLightBridgePass);
            }

            EnqueueFsr3Pass(renderer, ref renderingData);

            foreach (var bridge in _bridgePasses)
            {
                bridge.ConfigureInputFromTextureSlots();
                renderer.EnqueuePass(bridge);
            }
        }

        private void EnqueueFsr3Pass(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (TsukuyomiRenderPipelineResourcesProvider.TryGet(out TsukuyomiRenderPipelineResources resources) &&
                _fsr3Pass != null && _fsr3Pass.Configure(resources))
            {
                _fsr3Pass.PrepareCameraJitter(ref renderingData);
                _fsr3BridgePass.ConfigureInputFromTextureSlots();
                renderer.EnqueuePass(_fsr3BridgePass);
            }
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

        protected override void Dispose(bool disposing)
        {
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
            _pcssPass?.Dispose();
            _pcssPass = null;
            _pcssRestorePass = null;
            _pcssBridgePass = null;
            _pcssRestoreBridgePass = null;
            _volumeLightPass?.Dispose();
            _volumeLightPass = null;
            _volumeLightBridgePass = null;
            _fsr3Pass?.Dispose();
            _fsr3Pass = null;
            _fsr3BridgePass = null;
            base.Dispose(disposing);
        }
    }
}






