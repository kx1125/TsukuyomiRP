using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Tsukuyomi.Rendering
{
    // Owns coverage history only. Scan playback, appearance and Xray never enter the temporal state.
    internal sealed class SceneOutlineTemporal
    {
        private readonly Dictionary<Camera, SceneOutlineHistory> _histories = new();
        private ComputeShader _shader;
        private int _clear, _scatter, _seed, _resolve;
        private UpscalerBackend _backend;
        private GraphicsFormat _ownerFormat;
        private bool _supported;
        internal bool Active { get; private set; }

        internal void Prepare(UniversalCameraData camera, ComputeShader shader)
        {
            Active = false;
#if ENABLE_UPSCALER_FRAMEWORK
            var upscaler = TsukuyomiUpscaling.Active;
            if (shader && camera.historyManager != null && TsukuyomiUpscaling.CurrentCamera == camera.camera
                && upscaler != null && upscaler.isTemporal
                && (upscaler.ActualBackend == UpscalerBackend.DLSS || upscaler.ActualBackend == UpscalerBackend.FSR3)
                && SystemInfo.supportsComputeShaders)
            {
                if (_shader != shader)
                {
                    _shader = shader;
                    _clear = shader.FindKernel("Clear");
                    _scatter = shader.FindKernel("Scatter");
                    _seed = shader.FindKernel("Seed");
                    _resolve = shader.FindKernel("Resolve");
                    _supported = shader.IsSupported(_clear) && shader.IsSupported(_scatter) && shader.IsSupported(_seed) && shader.IsSupported(_resolve)
                        && SupportsUav(GraphicsFormat.R32_UInt) && SupportsUav(GraphicsFormat.R32_SFloat)
                        && SupportsUav(GraphicsFormat.R16G16B16A16_SFloat);
                    _ownerFormat = SupportsUav(GraphicsFormat.R8_UInt)
                        ? GraphicsFormat.R8_UInt : GraphicsFormat.R32_UInt;
                }
                if (_supported)
                {
                    _backend = upscaler.ActualBackend;
                    camera.historyManager.RequestAccess<SceneOutlineHistory>();
                    Active = true;
                }
            }
#endif
            if (!Active) Release(camera.camera);
        }

        private static bool SupportsUav(GraphicsFormat format) => SystemInfo.IsFormatSupported(format, GraphicsFormatUsage.LoadStore);

        internal void Release(Camera camera)
        {
            if (!ReferenceEquals(camera, null) && _histories.Remove(camera, out var history)) history.Release(this);
        }

        internal void Dispose()
        {
            foreach (var history in _histories.Values) history.Release(this);
            _histories.Clear();
        }

        private sealed class Data
        {
            public ComputeShader Shader;
            public int ClearKernel, ScatterKernel, CoverageKernel, GroupsX, GroupsY;
            public Vector4 Size, Parameters, Flags, PreviousViewZ, InverseW;
            public TextureHandle Depth, Normals, Motion, Exclusion, Reconstructed, Count, Owner;
            public TextureHandle PreviousCoverage, PreviousDepth, Coverage, EyeDepth;
        }

        private static readonly int SizeId = Shader.PropertyToID("_OutlineSize");
        private static readonly int ParamsId = Shader.PropertyToID("_OutlineParameters");
        private static readonly int FlagsId = Shader.PropertyToID("_OutlineFlags");
        private static readonly int PrevZId = Shader.PropertyToID("_OutlinePreviousViewZ");
        private static readonly int InvWId = Shader.PropertyToID("_OutlineInverseW");
        private static readonly int DepthId = Shader.PropertyToID("_CameraDepthTexture");
        private static readonly int NormalId = Shader.PropertyToID("_CameraNormalsTexture");
        private static readonly int MotionId = Shader.PropertyToID("_OutlineMotion");
        private static readonly int MaskId = Shader.PropertyToID("_OutlineExclude");
        private static readonly int FakeId = Shader.PropertyToID("_ReconstructedDepth");
        private static readonly int CountId = Shader.PropertyToID("_UseCount");
        private static readonly int OwnerId = Shader.PropertyToID("_NearestOwner");
        private static readonly int FakeWriteId = Shader.PropertyToID("_ReconstructedDepthRW");
        private static readonly int CountWriteId = Shader.PropertyToID("_UseCountRW");
        private static readonly int OwnerWriteId = Shader.PropertyToID("_NearestOwnerRW");
        private static readonly int PrevCoverageId = Shader.PropertyToID("_PreviousCoverage");
        private static readonly int PrevDepthId = Shader.PropertyToID("_PreviousEyeDepth");
        private static readonly int CoverageId = Shader.PropertyToID("_CoverageRW");
        private static readonly int EyeDepthId = Shader.PropertyToID("_EyeDepthRW");

        internal TextureHandle Record(in FeatureGraphContext graph, TextureHandle depth, TextureHandle normals,
            TextureHandle motion, TextureHandle mask, Vector4 parameters, bool needNormals, out Vector2 compositeJitter)
        {
            compositeJitter = Vector2.zero;
            var cameraData = graph.CameraData;
            var camera = cameraData.camera;
            if (!Active || !motion.IsValid()) return TextureHandle.nullHandle;
            var history = cameraData.historyManager.GetHistoryForWrite<SceneOutlineHistory>();
            if (history == null) return TextureHandle.nullHandle;
            _histories[camera] = history;

            var desc = cameraData.cameraTargetDescriptor;
            int w = desc.width, h = desc.height;
            var view = cameraData.GetViewMatrix();
            var projection = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true);
            var jittered = GL.GetGPUProjectionMatrix(cameraData.GetProjectionMatrix(), true);
            var jitter = HistoryTextureHelper.CalculateJitterUv(jittered, projection);
            // Phase-align the color input for NGX. FSR3 consumes coverage directly on the input grid.
            if (_backend == UpscalerBackend.DLSS) compositeJitter = jitter;
            int signature = System.HashCode.Combine(parameters, _backend, TsukuyomiUpscaling.Revision, camera.pixelWidth, camera.pixelHeight);
            bool valid = history.Begin(this, camera, desc, signature, projection, view, jitter, needNormals,
                out var prevView, out var delta, out bool resetScan);
            if (camera.TryGetComponent<UniversalAdditionalCameraData>(out var additional) && additional.resetHistory) valid = false;
            using var node = graph.AddCompute<Data>("Tsukuyomi Scene Outline Temporal Coverage");
            var data = node.Data;
            data.Shader = _shader;
            data.ClearKernel = _clear;
            data.ScatterKernel = _scatter;
            data.CoverageKernel = valid ? _resolve : _seed;
            data.GroupsX = (w + 7) / 8;
            data.GroupsY = (h + 7) / 8;
            data.Size = new Vector4(w, h, 1f / w, 1f / h);
            data.Flags = new Vector4(delta.x, delta.y, (needNormals ? 1 : 0) + (resetScan ? 2 : 0), mask.IsValid() ? 1f : 0f);
            data.Parameters = parameters;
            data.Depth = node.ReadTexture(depth);
            data.Normals = node.ReadTexture(needNormals ? normals : graph.RenderGraph.defaultResources.blackTexture);
            data.Exclusion = node.ReadTexture(ScanEffectShaderData.Optional(graph, mask));
            data.Coverage = node.Resources.ImportTexture(history.Coverage, AccessFlags.Write);
            data.EyeDepth = node.Resources.ImportTexture(history.Depth, AccessFlags.Write);
            data.PreviousCoverage = TextureHandle.nullHandle;
            if (valid)
            {
                var inverseVP = (jittered * view).inverse;
                data.PreviousViewZ = -(prevView * inverseVP).GetRow(2);
                data.InverseW = inverseVP.GetRow(3);
                data.Motion = node.ReadTexture(motion);
                data.Reconstructed = node.Builder.CreateTransientTexture(TextureDescriptors.Color2D(w, h, GraphicsFormat.R32_UInt, "Outline reconstructed depth", randomWrite: true));
                data.Count = node.Builder.CreateTransientTexture(TextureDescriptors.Color2D(w, h, GraphicsFormat.R32_UInt, "Outline use count", randomWrite: true));
                data.Owner = node.Builder.CreateTransientTexture(TextureDescriptors.Color2D(w, h, _ownerFormat, "Outline nearest surface", randomWrite: true));
                data.PreviousCoverage = node.Resources.ImportTexture(history.PreviousCoverage, AccessFlags.Read);
                data.PreviousDepth = node.Resources.ImportTexture(history.PreviousDepth, AccessFlags.Read);
            }
            node.SetRenderFunc(static (state, context) => Execute(state, context));
            return data.Coverage;
        }

        private static void Execute(Data data, ComputeGraphContext context)
        {
            var cmd = context.cmd;
            var shader = data.Shader;
            cmd.SetComputeVectorParam(shader, SizeId, data.Size);
            cmd.SetComputeVectorParam(shader, FlagsId, data.Flags);
            cmd.SetComputeVectorParam(shader, ParamsId, data.Parameters);

            int coverage = data.CoverageKernel;
            if (data.PreviousCoverage.IsValid())
            {
                int clear = data.ClearKernel;
                cmd.SetComputeTextureParam(shader, clear, FakeWriteId, data.Reconstructed);
                cmd.SetComputeTextureParam(shader, clear, CountWriteId, data.Count);
                cmd.DispatchCompute(shader, clear, data.GroupsX, data.GroupsY, 1);

                int scatter = data.ScatterKernel;
                cmd.SetComputeVectorParam(shader, PrevZId, data.PreviousViewZ);
                cmd.SetComputeVectorParam(shader, InvWId, data.InverseW);
                cmd.SetComputeTextureParam(shader, scatter, DepthId, data.Depth);
                cmd.SetComputeTextureParam(shader, scatter, MotionId, data.Motion);
                cmd.SetComputeTextureParam(shader, scatter, FakeWriteId, data.Reconstructed);
                cmd.SetComputeTextureParam(shader, scatter, CountWriteId, data.Count);
                cmd.SetComputeTextureParam(shader, scatter, OwnerWriteId, data.Owner);
                cmd.DispatchCompute(shader, scatter, data.GroupsX, data.GroupsY, 1);

                cmd.SetComputeTextureParam(shader, coverage, MotionId, data.Motion);
                cmd.SetComputeTextureParam(shader, coverage, FakeId, data.Reconstructed);
                cmd.SetComputeTextureParam(shader, coverage, CountId, data.Count);
                cmd.SetComputeTextureParam(shader, coverage, OwnerId, data.Owner);
                cmd.SetComputeTextureParam(shader, coverage, PrevCoverageId, data.PreviousCoverage);
                cmd.SetComputeTextureParam(shader, coverage, PrevDepthId, data.PreviousDepth);
            }
            cmd.SetComputeTextureParam(shader, coverage, DepthId, data.Depth);
            cmd.SetComputeTextureParam(shader, coverage, NormalId, data.Normals);
            cmd.SetComputeTextureParam(shader, coverage, MaskId, data.Exclusion);
            cmd.SetComputeTextureParam(shader, coverage, CoverageId, data.Coverage);
            cmd.SetComputeTextureParam(shader, coverage, EyeDepthId, data.EyeDepth);
            cmd.DispatchCompute(shader, coverage, data.GroupsX, data.GroupsY, 1);
        }
    }
}
