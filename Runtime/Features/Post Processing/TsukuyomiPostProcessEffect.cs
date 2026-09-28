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

        // Called before the Uber builder is opened. Returned resources are copied into its pooled snapshot.
        public virtual TextureHandle RecordGraph(in FeatureGraphContext graph, TextureHandle source, Material material)
            => TextureHandle.nullHandle;

        public abstract void Record(in TsukuyomiPostProcessBuildContext context);

        public virtual void ResetUberMaterial(Material material)
        {
        }

        public virtual void Dispose()
        {
        }
    }

    // Owned by one pooled plan. Effects provide operations; the plan handles their lifetime
    // and invocation without requiring each effect to cache instance delegates.
    internal abstract class TsukuyomiPostProcessData
    {
        public virtual void SetupUber(UnsafeGraphContext context, Material material) { }
    }

    internal sealed class TsukuyomiPostProcessPlan
    {
        private readonly TsukuyomiPassData _effectData = new();

        public T GetOrCreateData<T>() where T : class, new() => _effectData.GetOrCreateData<T>();

        public void Clear()
        {
            _uberSetups.Clear();
        }

        private readonly List<TsukuyomiPostProcessData> _uberSetups = new();

        public void AddUberSetup(TsukuyomiPostProcessData data)
        {
            if (data != null)
                _uberSetups.Add(data);
        }

        public void SetupUberMaterial(UnsafeGraphContext context, Material material)
        {
            for (int i = 0; i < _uberSetups.Count; i++)
                _uberSetups[i].SetupUber(context, material);
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
        public readonly TextureHandle EffectOutput;

        public PassResourceBuilder GraphResources => new(RenderGraph, Builder);

        public TsukuyomiPostProcessBuildContext(
            RenderGraph renderGraph,
            IUnsafeRenderGraphBuilder builder,
            UniversalCameraData cameraData,
            TextureHandle sourceColor,
            TextureHandle destinationColor,
            Material uberMaterial,
            TsukuyomiPostProcessPlan plan,
            TextureHandle effectOutput)
        {
            RenderGraph = renderGraph;
            Builder = builder;
            CameraData = cameraData;
            SourceColor = sourceColor;
            DestinationColor = destinationColor;
            UberMaterial = uberMaterial;
            _plan = plan;
            EffectOutput = effectOutput;
        }

        public T GetOrCreateData<T>() where T : class, new() => _plan.GetOrCreateData<T>();

        public TextureHandle CreateTexture(TextureDesc desc)
        {
            return RenderGraph.CreateTexture(desc);
        }

        public TextureHandle CreateTexture(TextureDesc desc, AccessFlags access)
        {
            return GraphResources.CreateTexture(desc, access);
        }

        public TextureDesc CreateColorDesc(int width, int height, string name, bool clearBuffer = false)
        {
            RenderTextureDescriptor cameraDescriptor = CameraData.cameraTargetDescriptor;
            GraphicsFormat colorFormat = cameraDescriptor.graphicsFormat == GraphicsFormat.None
                ? GraphicsFormat.R16G16B16A16_SFloat
                : cameraDescriptor.graphicsFormat;

            return TextureDescriptors.Color2D(Mathf.Max(1, width), Mathf.Max(1, height),
                colorFormat, name, FilterMode.Bilinear, clearBuffer: clearBuffer);
        }

        public void UseTexture(TextureHandle handle, AccessFlags access)
        {
            GraphResources.UseTexture(handle, access);
        }

        public void AddUberSetup(TsukuyomiPostProcessData data)
        {
            _plan.AddUberSetup(data);
        }
    }
}
