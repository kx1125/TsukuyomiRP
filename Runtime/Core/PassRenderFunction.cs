using System;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Tsukuyomi.Rendering
{
    // The graph owns the snapshot and callback references. The adapters are cached per type,
    // never capture a feature instance, and never query the record-data cache during execution.
    internal static class PassRenderFunction<T> where T : class, new()
    {
        internal static readonly BaseRenderFunc<TsukuyomiPassData, RasterGraphContext> Raster = ExecuteRaster;
        internal static readonly BaseRenderFunc<TsukuyomiPassData, ComputeGraphContext> Compute = ExecuteCompute;
        internal static readonly BaseRenderFunc<TsukuyomiPassData, UnsafeGraphContext> Unsafe = ExecuteUnsafe;

        internal static void Bind<TContext>(TsukuyomiPassData pass, T data,
            BaseRenderFunc<T, TContext> renderFunc, ProfilingSampler sampler)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (renderFunc == null)
                throw new ArgumentNullException(nameof(renderFunc));
            pass.RenderData = data;
            pass.RenderFunction = renderFunc;
            pass.RenderSampler = sampler;
        }

        private static void ExecuteRaster(TsukuyomiPassData pass, RasterGraphContext context)
        {
            using (new ProfilingScope(context.cmd, pass.RenderSampler))
                ((BaseRenderFunc<T, RasterGraphContext>)pass.RenderFunction)((T)pass.RenderData, context);
        }

        private static void ExecuteCompute(TsukuyomiPassData pass, ComputeGraphContext context)
        {
            using (new ProfilingScope(context.cmd, pass.RenderSampler))
                ((BaseRenderFunc<T, ComputeGraphContext>)pass.RenderFunction)((T)pass.RenderData, context);
        }

        private static void ExecuteUnsafe(TsukuyomiPassData pass, UnsafeGraphContext context)
        {
            using (new ProfilingScope(context.cmd, pass.RenderSampler))
                ((BaseRenderFunc<T, UnsafeGraphContext>)pass.RenderFunction)((T)pass.RenderData, context);
        }
    }
}
