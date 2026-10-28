using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering
{
    public enum ResourceRequirementStatus { NotRequested, Pending, Ready, MissingProducer, Unsupported, OrderConflict }

    /// <summary>A pass-defined request, resolved by that pass's resource producer.</summary>
    public readonly struct ResourceRequest<T> where T : struct
    {
        public readonly RenderPassBase Consumer;
        public readonly RenderPassEvent ConsumerEvent;
        public readonly T Request;
        public readonly ResourceRequirementStatus Status;
        internal ResourceRequest(RenderPassBase consumer, RenderPassEvent consumerEvent, in T request, ResourceRequirementStatus status)
        { Consumer = consumer; ConsumerEvent = consumerEvent; Request = request; Status = status; }
    }

    internal interface IResourceRequestSet
    {
        int Count { get; }
        ResourceRequirementStatus GetStatus(RenderPassBase consumer);
        void Complete();
        void ReportFailures();
        void Clear();
    }

    /// <summary>Typed, reused storage. Providers classify only requests they understand.</summary>
    public sealed class ResourceRequestSet<T> : IResourceRequestSet where T : struct
    {
        private readonly List<ResourceRequest<T>> _entries = new();
        private int _reportedFailures;
        public int Count => _entries.Count;
        public ResourceRequest<T> this[int index] => _entries[index];
        internal void Add(RenderPassBase consumer, RenderPassEvent stage, in T request)
            => _entries.Add(new ResourceRequest<T>(consumer, stage, request, ResourceRequirementStatus.Pending));
        public void SetStatus(int index, ResourceRequirementStatus status)
        {
            var entry = _entries[index];
            _entries[index] = new ResourceRequest<T>(entry.Consumer, entry.ConsumerEvent, entry.Request, status);
        }
        public ResourceRequirementStatus GetStatus(RenderPassBase consumer)
        {
            var result = ResourceRequirementStatus.NotRequested;
            foreach (var entry in _entries)
            {
                if (!ReferenceEquals(entry.Consumer, consumer)) continue;
                result = entry.Status;
                if (result != ResourceRequirementStatus.Ready) return result;
            }
            return result;
        }
        void IResourceRequestSet.Complete()
        {
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].Status == ResourceRequirementStatus.Pending)
                    SetStatus(i, ResourceRequirementStatus.MissingProducer);
        }
        void IResourceRequestSet.ReportFailures()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            foreach (var entry in _entries)
            {
                if (entry.Status <= ResourceRequirementStatus.Ready) continue;
                int bit = 1 << (int)entry.Status;
                if ((_reportedFailures & bit) != 0) continue;
                _reportedFailures |= bit;
                Debug.LogWarning($"Tsukuyomi resource {typeof(T).Name}: {entry.Status} for '{entry.Consumer.Name}'. The consumer will be skipped.");
            }
#endif
        }
        void IResourceRequestSet.Clear() => _entries.Clear();
    }

    public readonly ref struct ResourceRequirementCollector
    {
        private readonly ResourceRequirements _requirements;
        private readonly RenderPassBase _consumer;
        private readonly RenderPassEvent _consumerEvent;
        internal ResourceRequirementCollector(ResourceRequirements requirements, RenderPassBase consumer, RenderPassEvent consumerEvent)
        {
            _requirements = requirements;
            _consumer = consumer;
            _consumerEvent = consumerEvent;
        }
        public void Require<T>(in T request) where T : struct
            => _requirements.GetRequests<T>().Add(_consumer, _consumerEvent, request);
    }

    /// <summary>Owned by the current URP camera invocation. Contains no transient graph handles.</summary>
    public sealed class ResourceRequirements : ContextItem
    {
        private readonly Dictionary<Type, IResourceRequestSet> _requests = new();
        public ulong Invocation { get; private set; }
        public Camera Camera { get; private set; }
        public int RequestCount
        {
            get { int count = 0; foreach (var set in _requests.Values) count += set.Count; return count; }
        }
        public ResourceRequestSet<T> GetRequests<T>() where T : struct
        {
            if (!_requests.TryGetValue(typeof(T), out var set))
            { set = new ResourceRequestSet<T>(); _requests.Add(typeof(T), set); }
            return (ResourceRequestSet<T>)set;
        }

        // Called for EVERY AddRenderPasses, including repeat renders within one Time.frameCount.
        public void Begin(Camera camera)
        {
            Reset();
            Invocation++;
            Camera = camera;
        }

        public void Collect(RenderPassBase pass, RenderPassEvent consumerEvent, in FrameContext frame)
        {
            if (pass == null || !pass.IsActive(frame)) return;
            pass.CollectResourceRequirements(frame, new ResourceRequirementCollector(this, pass, consumerEvent));
        }

        internal void Complete() { foreach (var set in _requests.Values) set.Complete(); }

        public ResourceRequirementStatus GetStatus(RenderPassBase consumer)
        {
            var status = ResourceRequirementStatus.NotRequested;
            foreach (var set in _requests.Values)
            {
                var result = set.GetStatus(consumer);
                if (result == ResourceRequirementStatus.NotRequested) continue;
                status = result;
                if (status != ResourceRequirementStatus.Ready) return status;
            }
            return status;
        }

        public bool CanRecord(RenderPassBase consumer)
        {
            var status = GetStatus(consumer);
            return status == ResourceRequirementStatus.NotRequested || status == ResourceRequirementStatus.Ready;
        }

        internal void ReportFailures()
        {
            foreach (var set in _requests.Values) set.ReportFailures();
        }

        public override void Reset()
        {
            foreach (var set in _requests.Values) set.Clear();
            Camera = null;
        }
    }
}
