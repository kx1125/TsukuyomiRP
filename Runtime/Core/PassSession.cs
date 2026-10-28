using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering
{
    /// <summary>One immutable configuration snapshot and its executable objects, owned by one renderer feature.</summary>
    public sealed class PassSession : IDisposable
    {
        private sealed class Owner
        {
            internal string Id;
            internal IRenderPassRuntime Runtime;
            internal ResourceHub Resources = new();
            internal readonly List<Type> PrepareAfter = new();
            internal readonly List<RenderPassBase> RecordingPasses = new();
            internal int Order;
            internal bool Failed;
            internal readonly HashSet<string> Contributors = new();
        }
        private readonly TsukuyomiPipelineProfile _snapshot;
        private readonly List<Owner> _owners = new();
        private readonly List<Owner> _prepareOrder = new();
        private readonly Dictionary<string, Owner> _services = new();
        private readonly List<Owner> _serviceOrder = new();
        private readonly List<Owner> _servicePrepareOrder = new();
        private readonly List<PassPlanNode> _passes = new();
        private readonly List<PassPlanNode> _sorted = new();
        private readonly List<PassPlanNode> _sortPending = new();
        private readonly Dictionary<string, PassPlanNode> _providers = new();
        private readonly List<string> _errors = new();
        private readonly HashSet<Camera> _cameras = new();
        // Resource ownership outlives one render. Global/external state ownership does not.
        private readonly HashSet<Camera> _pendingCameras = new();
        private readonly List<Camera> _deadCameras = new();
        private readonly HashSet<string> _reported = new();
        private readonly HashSet<string> _failedThisCamera = new();
        private ScriptableRenderer _renderer;
        private Func<bool> _deferred;
        private bool _disposed;
        public IReadOnlyList<string> Errors => _errors;
        public IReadOnlyList<PassPlanNode> Passes => _sorted;
        public bool Valid => _errors.Count == 0;

        public PassSession(TsukuyomiPipelineProfile profile, bool initialize = true)
        {
            _snapshot = UnityEngine.Object.Instantiate(profile);
            _snapshot.hideFlags = HideFlags.HideAndDontSave;
            if (_snapshot.SchemaVersion != TsukuyomiPipelineProfile.CurrentSchemaVersion)
                _errors.Add($"Unsupported Profile schema {_snapshot.SchemaVersion}; upgrade the pass framework before applying this asset.");
            var ids = new HashSet<string>();
            var singletons = new HashSet<Type>();
            if (_snapshot.Passes != null)
                foreach (var definition in _snapshot.Passes)
                {
                    if (definition == null) { _errors.Add("Missing pass type: restore its assembly before applying this Profile."); continue; }
                    if (string.IsNullOrEmpty(definition.NodeId) || !ids.Add(definition.NodeId))
                    { _errors.Add($"{definition.Name}: missing or duplicate node ID."); continue; }
                    if (!definition.AllowMultiple && !singletons.Add(definition.GetType()))
                    { _errors.Add($"{definition.Name}: this pass allows only one node per Profile."); continue; }
                    if (definition is not LegacyPassDefinition)
                    {
                        var allowed = definition.GetType().GetCustomAttributes<InjectionPointAttribute>(true);
                        bool hasStages = false, validStage = false;
                        foreach (var stage in allowed) { hasStages = true; validStage |= stage.Point == definition.InjectionPoint; }
                        if (!hasStages || !validStage)
                        { _errors.Add($"{definition.Name} ({definition.NodeId}): stage {definition.InjectionPoint} is not allowed."); continue; }
                    }
                    if (!definition.Enabled) continue;
                    try
                    {
                        var runtime = definition.CreateRuntime();
                        if (runtime == null) throw new InvalidOperationException("The pass factory returned no runtime.");
                        var owner = new Owner { Id = definition.NodeId, Runtime = runtime, Order = _snapshot.Passes.IndexOf(definition) };
                        owner.Contributors.Add(owner.Id);
                        _owners.Add(owner);
                        runtime.Build(new PassPlanBuilder(this, owner.Id, owner.Resources, runtime));
                    }
                    catch (Exception e) { _errors.Add($"{definition.Name}: {e.Message}"); }
                }
            // Services can register more services. Build finishes before any GPU resources are initialized.
            for (int i = 0; i < _serviceOrder.Count; i++)
            {
                var service = _serviceOrder[i];
                try { service.Runtime.Build(new PassPlanBuilder(this, service.Id, service.Resources, service.Runtime)); }
                catch (Exception e) { _errors.Add($"{service.Id}: {e.Message}"); }
            }
            Compile();
            if (initialize && Valid)
            {
                foreach (var service in _servicePrepareOrder) InitializeOwner(service);
                foreach (var owner in _prepareOrder) InitializeOwner(owner);
            }
        }

        internal PassPlanNode Add(PassPlanBuilder builder, RenderPassBase pass, ScriptableRenderPass native,
            RenderPassEvent stage, Func<bool> active, Func<PassCameraContext, RenderPassEvent> resolver)
        {
            if (pass == null && native == null) throw new ArgumentNullException(nameof(pass));
            if (pass != null && pass is not (GraphFeaturePass or RasterPass or ComputePass or UnsafePass or PostPass))
                throw new ArgumentException($"{pass.Name}: implement a supported recording pass or use AddNative.");
            int ownerOrder = _snapshot.Passes.FindIndex(p => p?.NodeId == builder.Owner);
            if (ownerOrder < 0) ownerOrder = _serviceOrder.Find(o => o.Id == builder.Owner)?.Order ?? _snapshot.Passes.Count;
            var item = new PassPlanNode { Id = builder.Owner + "/" + _passes.Count, Owner = builder.Owner,
                Pass = pass, Native = native, Event = stage, Active = active, Stage = resolver,
                Resources = builder.Resources, Order = ownerOrder * 10000 + _passes.Count,
                Contributors = (_owners.Find(o => o.Id == builder.Owner) ?? _serviceOrder.Find(o => o.Id == builder.Owner)).Contributors };
            if (pass != null)
            {
                var owner = _owners.Find(o => o.Id == builder.Owner) ?? _serviceOrder.Find(o => o.Id == builder.Owner);
                if (!owner.RecordingPasses.Contains(pass)) owner.RecordingPasses.Add(pass);
                item.Bridge = new TsukuyomiBridgePass(pass, builder.Resources);
                item.Native = item.Bridge;
            }
            _passes.Add(item); return item;
        }
        internal void Provide(string key, PassPlanNode pass)
        {
            if (!_providers.TryAdd(key, pass)) _errors.Add($"{pass.Name}: multiple producers for '{key}'.");
        }
        internal T Service<T>(string key, Func<T> factory, string consumer) where T : class, IRenderPassService
        {
            if (_services.TryGetValue(key, out var existing))
            {
                if (existing.Runtime is T typed) { AddContributors(existing, consumer); return typed; }
                throw new InvalidOperationException($"Provider key '{key}' has incompatible types.");
            }
            int order = _snapshot.Passes.FindIndex(p => p?.NodeId == consumer);
            if (order < 0) order = _serviceOrder.Find(o => o.Id == consumer)?.Order ?? _snapshot.Passes.Count;
            var owner = new Owner { Id = "service/" + key, Runtime = factory(), Order = order };
            AddContributors(owner, consumer);
            _services.Add(key, owner); _serviceOrder.Add(owner); return (T)owner.Runtime;
        }
        private void AddContributors(Owner service, string consumer)
        {
            var source = _owners.Find(o => o.Id == consumer) ?? _serviceOrder.Find(o => o.Id == consumer);
            if (source != null) service.Contributors.UnionWith(source.Contributors);
        }
        internal void PrepareAfter(IRenderPassRuntime runtime, Type type)
        {
            foreach (var owner in _owners) if (ReferenceEquals(owner.Runtime, runtime)) owner.PrepareAfter.Add(type);
            foreach (var owner in _serviceOrder) if (ReferenceEquals(owner.Runtime, runtime)) owner.PrepareAfter.Add(type);
        }
        internal RenderPassBase PreviousDefinition(string id)
        {
            RenderPassBase previous = null;
            foreach (var definition in _snapshot.Passes)
            {
                if (definition != null && definition.NodeId == id) return previous;
                if (definition != null && definition.Enabled) previous = definition;
            }
            return null;
        }
        public void EndCamera(Camera camera)
        {
            // All features receive this callback, including a camera's previous renderer.
            if (!_pendingCameras.Remove(camera)) return;
            foreach (var owner in _owners) Deactivate(owner, camera);
            foreach (var service in _serviceOrder) Deactivate(service, camera);
        }
        internal void EndContext()
        {
            DeactivatePendingCameras();
            PruneCameras();
        }
        private void DeactivatePendingCameras()
        {
            // Also handles interrupted renders or integrations without an end-camera callback.
            foreach (var camera in _pendingCameras)
            {
                foreach (var owner in _owners) Deactivate(owner, camera);
                foreach (var service in _serviceOrder) Deactivate(service, camera);
            }
            _pendingCameras.Clear();
        }
        private void Deactivate(Owner owner, Camera camera)
        {
            try { owner.Runtime.Deactivate(camera); }
            catch (Exception e) { Report(owner.Id + "/deactivate", e.ToString()); }
            foreach (var pass in owner.RecordingPasses)
                if (!ReferenceEquals(pass, owner.Runtime))
                    try { pass.Deactivate(camera); }
                    catch (Exception e) { Report(owner.Id + "/deactivate/" + pass.Name, e.ToString()); }
        }
        internal T Find<T>() where T : class
        {
            foreach (var owner in _owners) if (Capability<T>(owner) is T typed) return typed;
            foreach (var owner in _serviceOrder) if (Capability<T>(owner) is T typed) return typed;
            return null;
        }
        private T Capability<T>(Owner owner) where T : class
        {
            if (owner.Failed || _failedThisCamera.Contains(owner.Id)) return null;
            return owner.Runtime as T ?? (owner.Runtime as IRenderPassCapabilitySource)?.Capability as T;
        }
        internal void Collect<T>(List<T> results) where T : class
        {
            results.Clear();
            foreach (var owner in _owners) if (Capability<T>(owner) is T typed) results.Add(typed);
            foreach (var owner in _serviceOrder) if (Capability<T>(owner) is T typed) results.Add(typed);
        }
        private void Compile()
        {
            _passes.Sort((a, b) => { int stage = a.Event.CompareTo(b.Event); return stage != 0 ? stage : a.Order.CompareTo(b.Order); });
            foreach (var pass in _passes)
                foreach (var request in pass.Requirements)
                    if (_providers.TryGetValue(request.key, out var producer)) pass.Dependencies.Add((producer, request.optional));
                    else if (!request.optional) _errors.Add($"{pass.Name} ({pass.Owner}): missing '{request.key}'.");
            var marks = new Dictionary<PassPlanNode, int>();
            foreach (var pass in _passes) Visit(pass, marks);
            if (Valid) SortPasses();
            var ownerMarks = new Dictionary<Owner, int>();
            foreach (var owner in _owners) VisitOwner(owner, ownerMarks);
            foreach (var owner in _serviceOrder) VisitOwner(owner, ownerMarks);
        }
        private void SortPasses()
        {
            // Reuse the compiled dependencies. Dynamic stages can converge for another camera, so
            // their tie-break must use authored order, not the previous camera's enqueue order.
            _sortPending.Clear(); _sortPending.AddRange(_passes);
            _sortPending.Sort(static (a, b) =>
            { int stage = a.Event.CompareTo(b.Event); return stage != 0 ? stage : a.Order.CompareTo(b.Order); });
            _sorted.Clear();
            while (_sortPending.Count != 0)
            {
                int ready = -1;
                for (int i = 0; i < _sortPending.Count; i++)
                {
                    bool waiting = false;
                    foreach (var dependency in _sortPending[i].Dependencies)
                        if (_sortPending.Contains(dependency.producer)) { waiting = true; break; }
                    if (!waiting) { ready = i; break; }
                }
                if (ready < 0) break; // Compile already rejects cycles.
                _sorted.Add(_sortPending[ready]); _sortPending.RemoveAt(ready);
            }
        }
        private void Visit(PassPlanNode pass, Dictionary<PassPlanNode, int> marks)
        {
            if (marks.TryGetValue(pass, out int mark))
            { if (mark == 1) _errors.Add($"{pass.Name} ({pass.Owner}): dependency cycle."); return; }
            marks[pass] = 1;
            foreach (var dependency in pass.Dependencies)
            {
                if (pass.Stage == null && dependency.producer.Stage == null && dependency.producer.Event > pass.Event)
                    _errors.Add($"{pass.Name}: producer {dependency.producer.Name} is in a later stage.");
                Visit(dependency.producer, marks);
            }
            marks[pass] = 2; _sorted.Add(pass);
        }
        private void VisitOwner(Owner owner, Dictionary<Owner, int> marks)
        {
            if (marks.TryGetValue(owner, out int mark))
            { if (mark == 1) _errors.Add($"{owner.Id}: preparation cycle."); return; }
            marks[owner] = 1;
            foreach (var type in owner.PrepareAfter)
            {
                foreach (var other in _owners) if (HasCapability(other, type)) VisitOwner(other, marks);
                foreach (var other in _serviceOrder)
                    if (HasCapability(other, type))
                    {
                        if (owner.Runtime is not IRenderPassService) _errors.Add($"{owner.Id}: effect preparation cannot depend on a service prepared after effects.");
                        else VisitOwner(other, marks);
                    }
            }
            marks[owner] = 2;
            if (owner.Runtime is IRenderPassService) _servicePrepareOrder.Add(owner); else _prepareOrder.Add(owner);
        }
        private static bool HasCapability(Owner owner, Type type) => type.IsInstanceOfType(owner.Runtime)
            || owner.Runtime is IRenderPassCapabilitySource source && type.IsInstanceOfType(source.Capability);

        public void Enqueue(ScriptableRenderer renderer, ref RenderingData data)
        {
            if (!Valid || _disposed) return;
            PruneCameras();
            var camera = data.cameraData.camera;
            if (camera) { _cameras.Add(camera); _pendingCameras.Add(camera); }
            if (!ReferenceEquals(renderer, _renderer))
            {
                _renderer = renderer;
                var property = renderer.GetType().GetProperty("usesDeferredLighting", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                _deferred = property?.PropertyType == typeof(bool)
                    ? (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), renderer, property.GetGetMethod(true)) : null;
            }
            var requirements = data.frameData.GetOrCreate<ResourceRequirements>(); requirements.Begin(camera);
            _failedThisCamera.Clear();
            var frame = new FrameContext(data.frameData);
            var context = new PassCameraContext(this, renderer, frame, _deferred?.Invoke() ?? false, requirements);
            foreach (var owner in _prepareOrder) PrepareOwner(owner, context, ref data);
            foreach (var service in _servicePrepareOrder) PrepareOwner(service, context, ref data);
            bool stagesChanged = false;
            foreach (var pass in _passes)
            {
                try
                {
                    if (pass.Stage != null)
                    {
                        var stage = pass.Stage(context.WithResources(pass.Resources));
                        stagesChanged |= stage != pass.Event;
                        pass.Event = stage;
                    }
                    pass.Native.renderPassEvent = pass.Event;
                    if (!_failedThisCamera.Contains(pass.Owner) && (pass.Active == null || pass.Active()))
                        pass.Bridge?.CollectResourceRequirements(requirements, context.WithResources(pass.Resources).Frame);
                }
                catch (Exception e) { _failedThisCamera.Add(pass.Owner); Report(pass.Id, e.ToString()); }
            }
            foreach (var service in _servicePrepareOrder) ResolveOwnerResources(service, context, ref data);
            foreach (var owner in _prepareOrder) ResolveOwnerResources(owner, context, ref data);
            requirements.Complete();
            if (stagesChanged) SortPasses();
            // _sorted is topological. URP performs its own stable event sort after enqueue.
            foreach (var pass in _sorted)
            {
                try { pass.IsScheduled = !_failedThisCamera.Contains(pass.Owner) && (pass.Active == null || pass.Active()); }
                catch (Exception e) { pass.IsScheduled = false; _failedThisCamera.Add(pass.Owner); Report(pass.Id, e.ToString()); }
                foreach (var dependency in pass.Dependencies)
                {
                    if (dependency.producer.IsScheduled && dependency.producer.Event > pass.Event)
                    { pass.IsScheduled = false; Report(pass.Id, $"{pass.Name}: dependency stage conflict with {dependency.producer.Name}."); }
                    if (!dependency.optional && !dependency.producer.IsScheduled) pass.IsScheduled = false;
                }
                try
                { if (pass.IsScheduled && pass.Bridge != null) pass.IsScheduled = pass.Bridge.ConfigureInputFromTextureSlots(context.WithResources(pass.Resources).Frame); }
                catch (Exception e) { pass.IsScheduled = false; _failedThisCamera.Add(pass.Owner); Report(pass.Id, e.ToString()); }
                if (pass.IsScheduled) renderer.EnqueuePass(pass.Native);
            }
            foreach (var owner in _owners) DeactivateUnused(owner, camera);
            foreach (var owner in _serviceOrder) DeactivateUnused(owner, camera);
            requirements.ReportFailures();
        }
        private void DeactivateUnused(Owner owner, Camera camera)
        {
            bool hasPass = false;
            foreach (var pass in _passes)
                if (pass.Owner == owner.Id) { if (pass.IsScheduled) return; hasPass = true; }
            if (hasPass) Deactivate(owner, camera);
        }
        private void PrepareOwner(Owner owner, in PassCameraContext context, ref RenderingData data)
        {
            if (owner.Failed) { _failedThisCamera.Add(owner.Id); return; }
            try
            {
                var scoped = context.WithResources(owner.Resources);
                owner.Runtime.Prepare(scoped, ref data);
                // The root pass already received the callback through IRenderPassRuntime.Prepare.
                foreach (var pass in owner.RecordingPasses)
                    if (!ReferenceEquals(pass, owner.Runtime) && pass.Enabled)
                        pass.PrepareRecording(scoped, ref data);
            }
            catch (Exception e) { _failedThisCamera.Add(owner.Id); Deactivate(owner, context.Camera); Report(owner.Id, e.ToString()); }
        }
        private void InitializeOwner(Owner owner)
        {
            try
            {
                owner.Runtime.Initialize();
                foreach (var pass in owner.RecordingPasses)
                    if (!ReferenceEquals(pass, owner.Runtime)) pass.Initialize();
            }
            catch (Exception e) { owner.Failed = true; Report(owner.Id, e.ToString()); }
        }
        private void ResolveOwnerResources(Owner owner, in PassCameraContext context, ref RenderingData data)
        {
            if (_failedThisCamera.Contains(owner.Id)) return;
            try
            {
                var scoped = context.WithResources(owner.Resources);
                owner.Runtime.ResolveResources(scoped, ref data);
                foreach (var pass in owner.RecordingPasses)
                    if (!ReferenceEquals(pass, owner.Runtime) && pass.Enabled) pass.ResolveResources(scoped, ref data);
            }
            catch (Exception e) { _failedThisCamera.Add(owner.Id); Deactivate(owner, context.Camera); Report(owner.Id, e.ToString()); }
        }
        private void Report(string key, string message) { if (_reported.Add(key)) Debug.LogWarning("Tsukuyomi pass: " + message); }
        private void PruneCameras()
        {
            _deadCameras.Clear();
            foreach (var camera in _cameras) if (!camera) _deadCameras.Add(camera);
            foreach (var camera in _deadCameras)
            {
                EndCamera(camera);
                foreach (var owner in _owners) ReleaseCamera(owner, camera);
                foreach (var owner in _serviceOrder) ReleaseCamera(owner, camera);
                _cameras.Remove(camera);
            }
        }
        private void ReleaseCamera(Owner owner, Camera camera)
        {
            try { owner.Runtime.ReleaseCamera(camera); }
            catch (Exception e) { Report(owner.Id + "/camera", e.ToString()); }
            foreach (var pass in owner.RecordingPasses)
                if (!ReferenceEquals(pass, owner.Runtime))
                    try { pass.ReleaseCamera(camera); }
                    catch (Exception e) { Report(owner.Id + "/camera/" + pass.Name, e.ToString()); }
            owner.Resources.ReleaseCamera(camera);
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            DeactivatePendingCameras();
            foreach (var camera in _cameras)
            {
                foreach (var owner in _owners) ReleaseCamera(owner, camera);
                foreach (var owner in _serviceOrder) ReleaseCamera(owner, camera);
            }
            for (int i = _prepareOrder.Count - 1; i >= 0; i--) DisposeOwner(_prepareOrder[i]);
            for (int i = _servicePrepareOrder.Count - 1; i >= 0; i--) DisposeOwner(_servicePrepareOrder[i]);
            _cameras.Clear(); CoreUtils.Destroy(_snapshot);
        }
        private void DisposeOwner(Owner owner)
        {
            for (int i = owner.RecordingPasses.Count - 1; i >= 0; i--)
            {
                var pass = owner.RecordingPasses[i];
                if (!ReferenceEquals(pass, owner.Runtime))
                    try { pass.Dispose(); }
                    catch (Exception e) { Report(owner.Id + "/dispose/" + pass.Name, e.ToString()); }
            }
            try { owner.Runtime.Dispose(); }
            catch (Exception e) { Report(owner.Id + "/dispose", e.ToString()); }
            finally { owner.Resources.Dispose(); }
        }
    }
}
