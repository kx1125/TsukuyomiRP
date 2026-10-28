using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering
{
    [DisallowMultipleRendererFeature("Tsukuyomi")]
    public sealed class TsukuyomiFeature : ScriptableRendererFeature
    {
        public TsukuyomiPipelineProfile Profile;
        [System.NonSerialized] private TsukuyomiPipelineProfile _source;
        [System.NonSerialized] private PassSession _session;
        [System.NonSerialized] private int _revision = -1;
        [System.NonSerialized] private int _switches;
        [System.NonSerialized] private bool _callbacksRegistered, _renderingContext, _suspended;
        private readonly List<PassSession> _retired = new();

        public override void Create()
        {
            RegisterCallbacks();
            Retire(); ResetConfiguration();
            if (!_renderingContext) DisposeRetired();
        }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!isActive || _suspended)
            {
                Retire(); ResetConfiguration();
                if (!_renderingContext) DisposeRetired();
                return;
            }
            // A retained renderer can resume without Create after a lifecycle transition.
            RegisterCallbacks();
            _renderingContext = true;
            int switches = Profile ? Profile.SwitchSignature() : 0;
            if (ConfigurationChanged(switches))
            {
                Retire(); _source = Profile; _revision = Profile ? Profile.Revision : -1; _switches = switches;
                if (Profile)
                {
                    _session = new PassSession(Profile);
                    foreach (var error in _session.Errors) Debug.LogError("Tsukuyomi Profile: " + error, Profile);
                }
            }
            _session?.Enqueue(renderer, ref renderingData);
        }
        private bool ConfigurationChanged(int switches)
            => !ReferenceEquals(_source, Profile) || (Profile && _session == null) || (!Profile && _session != null)
                || (Profile && (_revision != Profile.Revision || switches != _switches));
        private void ResetConfiguration() { _source = null; _revision = -1; _switches = 0; }
        private void Retire() { if (_session != null) { _retired.Add(_session); _session = null; } }
        private void BeginContext(ScriptableRenderContext context, List<Camera> cameras) => _renderingContext = true;
        private void EndCamera(ScriptableRenderContext context, Camera camera)
        {
            _session?.EndCamera(camera);
            foreach (var session in _retired) session.EndCamera(camera);
        }
        private void EndContext(ScriptableRenderContext context, List<Camera> cameras)
        {
            _renderingContext = false;
            // A removed camera need not render again to release its resources. Likewise, a Profile
            // edit/switch must retire the old snapshot even when no camera enqueues this feature.
            if (!isActive || _suspended || ConfigurationChanged(Profile ? Profile.SwitchSignature() : 0))
            { Retire(); ResetConfiguration(); }
            _session?.EndContext();
            DisposeRetired();
        }
        private void DisposeRetired()
        {
            foreach (var session in _retired) session.Dispose();
            _retired.Clear();
        }
        private void ReleaseRuntime()
        {
            _renderingContext = false;
            Retire(); ResetConfiguration(); DisposeRetired();
        }
        private void SuspendRuntime() { _suspended = true; ReleaseRuntime(); }
        private void OnDisable() => Dispose();
        private void OnDestroy() => Dispose();

        private void RegisterCallbacks()
        {
            if (_callbacksRegistered) return;
            _callbacksRegistered = true;
            RenderPipelineManager.beginContextRendering += BeginContext;
            RenderPipelineManager.endContextRendering += EndContext;
            RenderPipelineManager.endCameraRendering += EndCamera;
            Application.quitting += SuspendRuntime;
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Dispose;
            UnityEditor.EditorApplication.playModeStateChanged += PlayModeChanged;
            UnityEditor.EditorApplication.quitting += SuspendRuntime;
            UnityEditor.EditorApplication.update += UpdateEditorLifecycle;
#endif
        }
        private void UnregisterCallbacks()
        {
            if (!_callbacksRegistered) return;
            _callbacksRegistered = false;
            RenderPipelineManager.beginContextRendering -= BeginContext;
            RenderPipelineManager.endContextRendering -= EndContext;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            Application.quitting -= SuspendRuntime;
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= Dispose;
            UnityEditor.EditorApplication.playModeStateChanged -= PlayModeChanged;
            UnityEditor.EditorApplication.quitting -= SuspendRuntime;
            UnityEditor.EditorApplication.update -= UpdateEditorLifecycle;
#endif
        }
#if UNITY_EDITOR
        private void PlayModeChanged(UnityEditor.PlayModeStateChange state)
        {
            // Covers disabled Domain Reload as well as the normal domain/scene reload path.
            if (state == UnityEditor.PlayModeStateChange.ExitingEditMode || state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
                SuspendRuntime();
            else if (state == UnityEditor.PlayModeStateChange.EnteredEditMode || state == UnityEditor.PlayModeStateChange.EnteredPlayMode)
                _suspended = false;
        }
        private void UpdateEditorLifecycle()
        {
            // SetActive only changes a flag. An idle/paused Editor may not render another context.
            if (!_renderingContext && !isActive && (_session != null || _retired.Count != 0)) ReleaseRuntime();
        }
#endif
        protected override void Dispose(bool disposing)
        {
            UnregisterCallbacks();
            ReleaseRuntime(); _suspended = false;
            base.Dispose(disposing);
        }
    }
}
