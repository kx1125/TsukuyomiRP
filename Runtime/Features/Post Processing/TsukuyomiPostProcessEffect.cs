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
            PostProcessPassDefinition profile,
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

    // Values are shared with the Uber shader.
    internal enum PostBlendKind
    {
        Bloom = 1,
        Diffusion = 2,
        Tonemapping = 3
    }

    internal abstract class TsukuyomiPostProcessData
    {
        public abstract PostBlendKind Kind { get; }
        public abstract void SetupUber(UnsafeGraphContext context, Material material);
    }

    internal sealed class TsukuyomiPostProcessPlan
    {
        internal static readonly int OrderId = Shader.PropertyToID("_TsukuyomiPostOrder");

        private readonly TsukuyomiPassData _effectData = new();
        private readonly List<TsukuyomiPostProcessData> _steps = new();
        private Vector4 _order;

        public bool UsesLegacyOrder => (_order.y == 0 || _order.x < _order.y)
            && (_order.z == 0 || _order.y < _order.z);

        public T GetOrCreateData<T>() where T : class, new()
        {
            return _effectData.GetOrCreateData<T>();
        }

        public void Clear()
        {
            _steps.Clear();
            _order = Vector4.zero;
        }

        public void AddUberSetup(TsukuyomiPostProcessData data)
        {
            _order[_steps.Count] = (int)data.Kind;
            _steps.Add(data);
        }

        public void SetupUberMaterial(UnsafeGraphContext context, Material material)
        {
            material.SetVector(OrderId, _order);
            for (int i = 0; i < _steps.Count; i++)
                _steps[i].SetupUber(context, material);
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
