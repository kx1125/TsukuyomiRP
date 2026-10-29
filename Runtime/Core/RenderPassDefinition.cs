using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering
{
    /// <summary>Serialized authoring data. Never owns materials, histories or executable passes.</summary>
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Tsukuyomi.Rendering", "Tsukuyomi.Rendering", "RenderPluginDefinition")]
    public abstract class RenderPassDefinition : RenderPassBase
    {
        public abstract override IRenderPassRuntime CreateRuntime();
    }

    public interface IRenderPassRuntime : IDisposable
    {
        void Build(PassPlanBuilder plan);
        void Initialize();
        void Prepare(in PassCameraContext context, ref RenderingData renderingData);
        void ResolveResources(in PassCameraContext context, ref RenderingData renderingData);
        void Deactivate(Camera camera);
        void ReleaseCamera(Camera camera);
    }

    public abstract class RenderPassRuntime : IRenderPassRuntime
    {
        public abstract void Build(PassPlanBuilder plan);
        public virtual void Initialize() { }
        public virtual void Prepare(in PassCameraContext context, ref RenderingData renderingData) { }
        /// <summary>Runs after every configuration and resource request has been collected.</summary>
        public virtual void ResolveResources(in PassCameraContext context, ref RenderingData renderingData) { }
        public virtual void Deactivate(Camera camera) { }
        public virtual void ReleaseCamera(Camera camera) { }
        public virtual void Dispose() { }
    }

    /// <summary>Shared helpers are configured after their contributing effects, before request collection.</summary>
    public interface IRenderPassService : IRenderPassRuntime { }
    public interface IRenderPassCapabilitySource { object Capability { get; } }

    public readonly struct PassCameraContext
    {
        public readonly FrameContext Frame;
        public readonly ScriptableRenderer Renderer;
        public readonly VolumeStack Volumes;
        public readonly bool Deferred;
        public readonly ResourceRequirements Requirements;
        private readonly PassSession _session;
        internal PassCameraContext(PassSession session, ScriptableRenderer renderer, FrameContext frame,
            bool deferred, ResourceRequirements requirements)
        {
            _session = session; Renderer = renderer; Frame = frame; Deferred = deferred;
            Volumes = VolumeManager.instance.stack; Requirements = requirements;
        }
        public Camera Camera => Frame.CameraData.camera;
        public T Find<T>() where T : class => _session.Find<T>();
        public void Collect<T>(List<T> results) where T : class => _session.Collect(results);
        internal PassCameraContext WithResources(ResourceHub resources)
            => new(_session, Renderer, new FrameContext(Frame.URPFrameData, Camera ? resources.ForCamera(Camera) : resources), Deferred, Requirements);
    }

    /// <summary>A node in the compiled execution graph, independent of any concrete effect.</summary>
    public sealed class PassPlanNode
    {
        public string Id { get; internal set; }
        public string Owner { get; internal set; }
        public string Name => Pass != null ? Pass.Name : Native.GetType().Name;
        public RenderPassEvent Event { get; internal set; }
        public bool IsScheduled { get; internal set; }
        internal HashSet<string> Contributors;
        public bool ContributesTo(string nodeId) => Contributors != null && Contributors.Contains(nodeId);
        public IEnumerable<string> DependencyDescriptions
        {
            get { foreach (var dependency in Dependencies) yield return dependency.producer.Name + (dependency.optional ? " (optional)" : " (required)"); }
        }
        internal RenderPassBase Pass;
        internal ScriptableRenderPass Native;
        internal TsukuyomiBridgePass Bridge;
        internal Func<bool> Active;
        internal Func<PassCameraContext, RenderPassEvent> Stage;
        internal readonly List<(string key, bool optional)> Requirements = new();
        internal readonly List<(PassPlanNode producer, bool optional)> Dependencies = new();
        internal int Order;
        internal ResourceHub Resources;
    }

    public sealed class PassPlanBuilder
    {
        private readonly PassSession _session;
        internal readonly string Owner;
        internal readonly ResourceHub Resources;
        internal readonly IRenderPassRuntime Runtime;
        internal PassPlanBuilder(PassSession session, string owner, ResourceHub resources, IRenderPassRuntime runtime)
        { _session = session; Owner = owner; Resources = resources; Runtime = runtime; }

        public PassPlanNode Add(RenderPassBase pass, RenderPassEvent stage, Func<bool> active = null,
            Func<PassCameraContext, RenderPassEvent> resolveStage = null)
            => _session.Add(this, pass, null, stage, active, resolveStage);

        public PassPlanNode AddNative(ScriptableRenderPass pass, RenderPassEvent stage, Func<bool> active = null,
            Func<PassCameraContext, RenderPassEvent> resolveStage = null)
            => _session.Add(this, null, pass, stage, active, resolveStage);

        public void Provides(PassPlanNode pass, string capability) => _session.Provide(capability, pass);
        public void Requires(PassPlanNode pass, string capability, bool optional = false)
            => pass.Requirements.Add((capability, optional));
        public void After(PassPlanNode pass, PassPlanNode producer, bool optional = false)
            => pass.Dependencies.Add((producer, optional));
        public void PrepareAfter<T>() where T : class => _session.PrepareAfter(Runtime, typeof(T));

        /// <summary>The key includes the provider's format/layout/stage variant. Consumers share one owner.</summary>
        public T Service<T>(string key, Func<T> factory) where T : class, IRenderPassService
            => _session.Service(key, factory, Owner);

        public RenderPassBase PreviousDefinition => _session.PreviousDefinition(Owner);
        public static RenderPassEvent EventFor(InjectionPoint point) => point switch
        {
            InjectionPoint.BeforeRendering => RenderPassEvent.BeforeRendering,
            InjectionPoint.BeforeOpaque => RenderPassEvent.BeforeRenderingOpaques,
            InjectionPoint.AfterOpaque => RenderPassEvent.AfterRenderingOpaques,
            InjectionPoint.BeforeSkybox => RenderPassEvent.BeforeRenderingSkybox,
            InjectionPoint.BeforePostProcess => RenderPassEvent.BeforeRenderingPostProcessing - 1,
            InjectionPoint.AfterPostProcess => RenderPassEvent.AfterRenderingPostProcessing,
            _ => RenderPassEvent.AfterRendering
        };
    }

}
