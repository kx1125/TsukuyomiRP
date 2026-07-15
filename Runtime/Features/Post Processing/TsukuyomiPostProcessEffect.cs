using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering
{
    internal abstract class TsukuyomiPostProcessEffect
    {
        public abstract string Name { get; }

        public abstract bool Configure(
            TsukuyomiPipelineProfile profile,
            VolumeStack volumeStack,
            TsukuyomiRenderPipelineResources resources);

        public abstract void Record(in TsukuyomiPostProcessBuildContext context);

        public virtual void ResetUberMaterial(Material material)
        {
        }

        public virtual void Dispose()
        {
        }
    }

    internal sealed class TsukuyomiPostProcessPlan
    {
        private readonly List<TsukuyomiPostProcessStage> _stages = new();
        private readonly List<Action<UnsafeGraphContext, Material>> _uberSetups = new();

        public void AddStage(ProfilingSampler sampler, Action<UnsafeGraphContext> execute)
        {
            if (execute == null)
                return;

            _stages.Add(new TsukuyomiPostProcessStage(sampler, execute));
        }

        public void AddUberSetup(Action<UnsafeGraphContext, Material> setup)
        {
            if (setup != null)
                _uberSetups.Add(setup);
        }

        public void ExecuteStages(UnsafeGraphContext context)
        {
            for (int i = 0; i < _stages.Count; i++)
                _stages[i].Execute(context);
        }

        public void SetupUberMaterial(UnsafeGraphContext context, Material material)
        {
            for (int i = 0; i < _uberSetups.Count; i++)
                _uberSetups[i].Invoke(context, material);
        }
    }

    internal readonly struct TsukuyomiPostProcessStage
    {
        private readonly ProfilingSampler _sampler;
        private readonly Action<UnsafeGraphContext> _execute;

        public TsukuyomiPostProcessStage(ProfilingSampler sampler, Action<UnsafeGraphContext> execute)
        {
            _sampler = sampler;
            _execute = execute;
        }

        public void Execute(UnsafeGraphContext context)
        {
            if (_sampler == null)
            {
                _execute(context);
                return;
            }

            using (new ProfilingScope(context.cmd, _sampler))
            {
                _execute(context);
            }
        }
    }

    internal readonly ref struct TsukuyomiPostProcessBuildContext
    {
        private readonly TsukuyomiPostProcessPlan _plan;

        public readonly RenderGraph RenderGraph;
        public readonly IUnsafeRenderGraphBuilder Builder;
        public readonly UniversalCameraData CameraData;
        public readonly TextureHandle SourceColor;
        public readonly TextureHandle DestinationColor;
        public readonly Material UberMaterial;

        public TsukuyomiPostProcessBuildContext(
            RenderGraph renderGraph,
            IUnsafeRenderGraphBuilder builder,
            UniversalCameraData cameraData,
            TextureHandle sourceColor,
            TextureHandle destinationColor,
            Material uberMaterial,
            TsukuyomiPostProcessPlan plan)
        {
            RenderGraph = renderGraph;
            Builder = builder;
            CameraData = cameraData;
            SourceColor = sourceColor;
            DestinationColor = destinationColor;
            UberMaterial = uberMaterial;
            _plan = plan;
        }

        public TextureHandle CreateTexture(TextureDesc desc)
        {
            return RenderGraph.CreateTexture(desc);
        }

        public TextureDesc CreateColorDesc(int width, int height, string name, bool clearBuffer = false)
        {
            RenderTextureDescriptor cameraDescriptor = CameraData.cameraTargetDescriptor;
            GraphicsFormat colorFormat = cameraDescriptor.graphicsFormat == GraphicsFormat.None
                ? GraphicsFormat.R16G16B16A16_SFloat
                : cameraDescriptor.graphicsFormat;

            return new TextureDesc(Mathf.Max(1, width), Mathf.Max(1, height))
            {
                name = name,
                colorFormat = colorFormat,
                depthBufferBits = DepthBits.None,
                msaaSamples = MSAASamples.None,
                clearBuffer = clearBuffer,
                clearColor = Color.clear,
                filterMode = FilterMode.Bilinear
            };
        }

        public void UseTexture(TextureHandle handle, AccessFlags access)
        {
            if (handle.IsValid())
                Builder.UseTexture(handle, access);
        }

        public void AddStage(ProfilingSampler sampler, Action<UnsafeGraphContext> execute)
        {
            _plan.AddStage(sampler, execute);
        }

        public void AddUberSetup(Action<UnsafeGraphContext, Material> setup)
        {
            _plan.AddUberSetup(setup);
        }
    }
}
