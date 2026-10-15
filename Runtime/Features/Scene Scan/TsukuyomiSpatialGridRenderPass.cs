using System.Collections.Generic;
using UnityEngine;

namespace Tsukuyomi.Rendering
{
    [System.Serializable]
    public sealed class TsukuyomiSpatialGridRenderPass : CameraEffectPass<SpatialGridParameters>, System.IDisposable
    {
        private readonly TextureSlot _color = TextureSlot.ReadWrite("Spatial Grid Color", BuiltinTexture.ActiveColor)
            .WithBinding(TextureBinding.ColorAttachment);
        private readonly TextureSlot _depth = TextureSlot.Read("Spatial Grid Depth", BuiltinTexture.CameraDepthTexture);
        private readonly TextureSlot _excluded = TextureSlot.Read("SceneScanExcludedMask", BuiltinTexture.None);
        [System.NonSerialized] private readonly Dictionary<Camera, ISpatialGridBackend> _backends = new();

        private static readonly List<System.WeakReference<TsukuyomiSpatialGridRenderPass>> Instances = new();
        public TsukuyomiSpatialGridRenderPass() => Instances.Add(new(this));
        /// <summary>Release backend caches for this camera in every active renderer, independently of camera overrides.</summary>
        public static void ReleaseCameraResources(Camera camera)
        {
            for (int i = Instances.Count - 1; i >= 0; i--)
                if (Instances[i].TryGetTarget(out var pass)) pass.ReleaseResources(camera); else Instances.RemoveAt(i);
        }
        internal static void ReleaseAllCameraResources()
        {
            for (int i = Instances.Count - 1; i >= 0; i--)
                if (Instances[i].TryGetTarget(out var pass)) pass.ReleaseBackends(); else Instances.RemoveAt(i);
        }
        private void ReleaseBackends()
        {
            foreach (var backend in _backends.Values) backend.Dispose();
            _backends.Clear();
        }
        private void PruneBackends()
        {
            var dead = UnityEngine.Pool.ListPool<Camera>.Get();
            foreach (var camera in _backends.Keys) if (!camera) dead.Add(camera);
            foreach (var camera in dead) ReleaseResources(camera);
            UnityEngine.Pool.ListPool<Camera>.Release(dead);
        }

        private ISpatialGridBackend Backend(Camera camera)
        {
            if (!_backends.TryGetValue(camera, out var backend))
                _backends.Add(camera, backend = new SpatialGridMeshBackend());
            return backend;
        }

        /// <summary>Release a camera's cached backend resources independently of its API state.</summary>
        public void ReleaseResources(Camera camera)
        {
            if (!ReferenceEquals(camera, null) && _backends.Remove(camera, out var backend)) backend.Dispose();
        }
        private bool _missingShaderLogged;

        public override string Name => "Tsukuyomi Spatial Grid Render";
        public override int Priority => -100;
        public override bool RequiresIntermediateColorTarget => true;
        protected override SpatialGridParameters DefaultParameters => SpatialGridParameters.Default;
        protected override bool ResolveSettings(TsukuyomiPipelineProfile profile, UnityEngine.Rendering.VolumeStack stack, out SpatialGridParameters parameters)
            => SceneEffectSettings.Resolve(profile, stack, out parameters);
        protected override void ApplyAnimation(ref SpatialGridParameters parameters, in ScanAnimationParameters animation)
        {
            parameters.OriginWS = animation.OriginWS; parameters.TransitionProgress = animation.Progress;
            parameters.Blend = animation.Blend; parameters.WorldProgress = animation.WorldProgress; parameters.ScreenProgress = animation.ScreenProgress;
        }


        public override void CollectTextureSlots(List<TextureSlot> slots)
        {
            slots.Add(_color); slots.Add(_depth); slots.Add(_excluded);
        }

        internal bool Configure(Camera camera)
        {
            PruneBackends();
            if (!TryGetActiveParameters(camera, out var parameters) || parameters.Intensity <= 0f || parameters.Blend <= 0f) return false;
            if (!TsukuyomiRenderPipelineResourcesProvider.TryGet(out var resources)) return false;
            if (!resources.ScanGridShader)
            {
                if (!_missingShaderLogged) Debug.LogError("SpatialGridRender requires ScanGridShader in TsukuyomiRenderPipelineResources.");
                _missingShaderLogged = true;
                return false;
            }
            _missingShaderLogged = false;
            return Backend(camera).Configure(resources.ScanGridShader);
        }

        public override bool IsActive(in FrameContext frame) =>
            TryGetActiveParameters(frame.CameraData.camera, out var parameters) && parameters.Intensity > 0f && parameters.Blend > 0f;

        public override void RecordGraph(in FeatureGraphContext graph)
        {
            if (TryGetActiveParameters(graph.CameraData.camera, out var parameters))
                Backend(graph.CameraData.camera).RecordGraph(graph, parameters);
        }

        public void Dispose()
        {
            ReleaseBackends();
            ClearAllCameras();
        }
    }
}
