using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering
{
    public enum ResourceRequirementStatus { NotRequested, Pending, Ready, MissingProducer, Unsupported, OrderConflict }

    /// <summary>One packed min-depth variant. Mip count is a coverage request; compatible requests share a full pyramid.</summary>
    public readonly struct DepthPyramidRequest : IEquatable<DepthPyramidRequest>
    {
        public readonly BuiltinTexture Source;
        public readonly RenderPassEvent GenerationEvent;
        public readonly GraphicsFormat Format;
        public readonly int CheckerboardMipCount;
        public readonly int MipCount;

        public DepthPyramidRequest(int mipCount, BuiltinTexture source = BuiltinTexture.CameraDepthTexture,
            RenderPassEvent generationEvent = RenderPassEvent.AfterRenderingPrePasses,
            GraphicsFormat format = GraphicsFormat.R32_SFloat, int checkerboardMipCount = 0)
        {
            Source = source;
            GenerationEvent = generationEvent;
            Format = format;
            CheckerboardMipCount = checkerboardMipCount;
            MipCount = mipCount;
        }

        public bool Equals(DepthPyramidRequest other) => Source == other.Source && GenerationEvent == other.GenerationEvent
            && Format == other.Format && CheckerboardMipCount == other.CheckerboardMipCount;
        public override bool Equals(object obj) => obj is DepthPyramidRequest other && Equals(other);
        public override int GetHashCode() => HashCode.Combine((int)Source, (int)GenerationEvent, (int)Format, CheckerboardMipCount);
        internal bool Supported => Source == BuiltinTexture.CameraDepthTexture
            && GenerationEvent == RenderPassEvent.AfterRenderingPrePasses && Format == GraphicsFormat.R32_SFloat
            && CheckerboardMipCount == 0 && MipCount > 0 && MipCount <= 15;
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
        public void RequireDepthPyramid() => RequireDepthPyramid(new DepthPyramidRequest(mipCount: 15));
        public void RequireDepthPyramid(in DepthPyramidRequest request) => _requirements.Add(_consumer, _consumerEvent, request);
    }

    /// <summary>Owned by the current URP camera invocation. Contains no transient graph handles.</summary>
    public sealed class ResourceRequirements : ContextItem
    {
        private struct Entry
        {
            public RenderPassBase Consumer;
            public RenderPassEvent ConsumerEvent;
            public DepthPyramidRequest Request;
            public ResourceRequirementStatus Status;
        }
        private readonly List<Entry> _entries = new();
        private int _reportedFailures;
        public ulong Invocation { get; private set; }
        public Camera Camera { get; private set; }
        public int DepthPyramidMipCount { get; private set; }
        public bool NeedsDepthPyramid { get; private set; }
        public int RequestCount => _entries.Count;

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

        internal void Add(RenderPassBase consumer, RenderPassEvent consumerEvent, in DepthPyramidRequest request)
        {
            _entries.Add(new Entry { Consumer = consumer, ConsumerEvent = consumerEvent, Request = request,
                Status = ResourceRequirementStatus.Pending });
        }

        // First classify independent requests. Unsupported variants do not poison compatible consumers.
        public bool ResolveDepthPyramid(RenderPassEvent producerEvent, bool producerAvailable, bool cameraSupported)
        {
            NeedsDepthPyramid = false;
            DepthPyramidMipCount = 0;
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                entry.Status = !cameraSupported || !entry.Request.Supported ? ResourceRequirementStatus.Unsupported
                    : !producerAvailable ? ResourceRequirementStatus.MissingProducer
                    : producerEvent != entry.Request.GenerationEvent || producerEvent >= entry.ConsumerEvent
                        ? ResourceRequirementStatus.OrderConflict : ResourceRequirementStatus.Ready;
                _entries[i] = entry;
                if (entry.Status == ResourceRequirementStatus.Ready)
                {
                    NeedsDepthPyramid = true;
                    DepthPyramidMipCount = Mathf.Max(DepthPyramidMipCount, entry.Request.MipCount);
                }
            }
            return NeedsDepthPyramid;
        }

        public ResourceRequirementStatus GetStatus(RenderPassBase consumer)
        {
            var status = ResourceRequirementStatus.NotRequested;
            for (int i = 0; i < _entries.Count; i++)
            {
                if (!ReferenceEquals(_entries[i].Consumer, consumer)) continue;
                status = _entries[i].Status;
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            for (int i = 0; i < _entries.Count; i++)
            {
                var status = _entries[i].Status;
                if (status <= ResourceRequirementStatus.Ready) continue;
                int bit = 1 << (int)status;
                if ((_reportedFailures & bit) != 0) continue;
                _reportedFailures |= bit;
                Debug.LogWarning($"Tsukuyomi Depth Pyramid: {status} for '{_entries[i].Consumer.Name}'. The consumer will be skipped.");
            }
#endif
        }

        public override void Reset()
        {
            _entries.Clear();
            NeedsDepthPyramid = false;
            DepthPyramidMipCount = 0;
            Camera = null;
        }
    }
}
