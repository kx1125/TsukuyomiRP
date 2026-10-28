using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tsukuyomi.Rendering
{
    internal static class CameraEffectControls<T> where T : struct
    {
        internal sealed class State
        {
            internal bool? Enabled;
            internal T? Parameters;
            internal ScanAnimationParameters? Animation;
        }
        internal static readonly Dictionary<Camera, State> Cameras = new();
        internal static State Get(Camera camera)
        {
            if (!camera) throw new ArgumentNullException(nameof(camera));
            if (!Cameras.TryGetValue(camera, out var state)) Cameras.Add(camera, state = new State());
            return state;
        }
    }

    internal static class SceneEffectCameras
    {
        private static readonly List<Camera> Dead = new();
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterEditorLifecycle()
        {
            UnityEditor.EditorApplication.playModeStateChanged -= PlayModeChanged;
            UnityEditor.EditorApplication.playModeStateChanged += PlayModeChanged;
        }
        private static void PlayModeChanged(UnityEditor.PlayModeStateChange state)
        {
            // Static camera API controls survive when Domain Reload is disabled.
            if (state == UnityEditor.PlayModeStateChange.EnteredEditMode) Reset();
        }
#endif
        internal static void Prune<T>(Dictionary<Camera, T> cameras)
        {
            Dead.Clear();
            foreach (var camera in cameras.Keys) if (!camera) Dead.Add(camera);
            foreach (var camera in Dead) cameras.Remove(camera);
            Dead.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            CameraEffectControls<VoxelTransitionParameters>.Cameras.Clear();
            CameraEffectControls<ScanPostProcessParameters>.Cameras.Clear();
            CameraEffectControls<XrayRenderParameters>.Cameras.Clear();
            CameraEffectControls<SpatialGridParameters>.Cameras.Clear();
            TsukuyomiScanPostProcessPass.ResetCameraEffects();
            TsukuyomiXrayRenderPass.ResetCameraRenderers();
            TsukuyomiSpatialGridRenderPass.ReleaseAllCameraResources();
        }
    }

    /// <summary>Profile defaults, Volume overrides, then explicit camera controls. Graph nodes copy resolved values.</summary>
    [Serializable]
    public abstract class CameraEffectPass<TParameters> : GraphFeaturePass where TParameters : struct
    {
        private sealed class CameraState
        {
            public bool Enabled;
            public TParameters Parameters;
        }
        [NonSerialized] private readonly Dictionary<Camera, CameraState> _settings = new();
        [NonSerialized] private readonly Dictionary<Camera, CameraEffectControls<TParameters>.State> _cameras = new();
        protected abstract TParameters DefaultParameters { get; }
        protected abstract bool ResolveSettings(RenderPassBase profile, VolumeStack stack, out TParameters parameters);
        protected abstract void ApplyAnimation(ref TParameters parameters, in ScanAnimationParameters animation);

        /// <summary>Override this camera's panel switch. Can be called before a RendererFeature exists.</summary>
        public static void SetCameraEnabled(Camera camera, bool enabled) => CameraEffectControls<TParameters>.Get(camera).Enabled = enabled;
        /// <summary>Override all rendering parameters for this camera, without enabling the effect.</summary>
        public static void SetCameraParameters(Camera camera, in TParameters parameters) => CameraEffectControls<TParameters>.Get(camera).Parameters = parameters;
        /// <summary>Drive animated inputs while retaining panel-authored appearance and coverage mode.</summary>
        public static void SetCameraAnimation(Camera camera, in ScanAnimationParameters animation) => CameraEffectControls<TParameters>.Get(camera).Animation = animation;
        /// <summary>Return control to Profile/Volume. Backend resources have a separate release API.</summary>
        public static void ClearCamera(Camera camera)
        { if (!ReferenceEquals(camera, null)) CameraEffectControls<TParameters>.Cameras.Remove(camera); }
        public static bool HasCameraOverrides(Camera camera) => camera && CameraEffectControls<TParameters>.Cameras.ContainsKey(camera);

        internal void UpdateSettings(Camera camera, RenderPassBase profile, VolumeStack stack)
        {
            SceneEffectCameras.Prune(_cameras);
            SceneEffectCameras.Prune(_settings);
            SceneEffectCameras.Prune(CameraEffectControls<TParameters>.Cameras);
            if (!camera) return;
            if (!_settings.TryGetValue(camera, out var state)) _settings.Add(camera, state = new CameraState());
            state.Enabled = ResolveSettings(profile, stack, out var parameters);
            state.Parameters = parameters;
        }

        // Instance setters remain for renderer integrations using explicit Pass instances.
        public void SetEnabled(Camera camera, bool enabled)
        { if (!camera) throw new ArgumentNullException(nameof(camera)); GetOrCreate(camera).Enabled = enabled; }
        public void SetParameters(Camera camera, in TParameters parameters)
        { if (!camera) throw new ArgumentNullException(nameof(camera)); GetOrCreate(camera).Parameters = parameters; }

        public bool TryGetParameters(Camera camera, out TParameters parameters)
        {
            Read(camera, out parameters, out _, out var exists);
            return exists;
        }
        public bool IsEnabled(Camera camera)
        { Read(camera, out _, out var enabled, out _); return Enabled && enabled; }
        public virtual void Clear(Camera camera)
        { if (!ReferenceEquals(camera, null)) _cameras.Remove(camera); }
        protected internal bool TryGetActiveParameters(Camera camera, out TParameters parameters)
        { Read(camera, out parameters, out var enabled, out _); return Enabled && enabled; }
        public override void ReleaseCamera(Camera camera)
        {
            Clear(camera);
            if (!ReferenceEquals(camera, null)) _settings.Remove(camera);
        }
        protected void ClearAllCameras() { _cameras.Clear(); _settings.Clear(); }

        private void Read(Camera camera, out TParameters parameters, out bool enabled, out bool exists)
        {
            parameters = DefaultParameters; enabled = false; exists = false;
            if (!camera) return;
            if (_settings.TryGetValue(camera, out var state))
            { parameters = state.Parameters; enabled = state.Enabled; exists = true; }
            if (_cameras.TryGetValue(camera, out var manual))
            {
                exists = true;
                if (manual.Enabled.HasValue) enabled = manual.Enabled.Value;
                if (manual.Parameters.HasValue) parameters = manual.Parameters.Value;
            }
            if (!CameraEffectControls<TParameters>.Cameras.TryGetValue(camera, out var control)) return;
            exists = true;
            if (control.Enabled.HasValue) enabled = control.Enabled.Value;
            if (control.Parameters.HasValue) parameters = control.Parameters.Value;
            if (control.Animation.HasValue) ApplyAnimation(ref parameters, control.Animation.Value);
        }
        private CameraEffectControls<TParameters>.State GetOrCreate(Camera camera)
        {
            if (!_cameras.TryGetValue(camera, out var state))
                _cameras.Add(camera, state = new CameraEffectControls<TParameters>.State());
            return state;
        }
    }
}
