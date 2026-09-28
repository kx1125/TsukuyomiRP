using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Tsukuyomi.Rendering
{
    [System.Serializable]
    internal sealed class TsukuyomiDepthPyramidPass : ComputePass
    {
        private const int TileSize = 8;
        private const string CopyKernelName = "CopyDepth";
        private const string DownsampleKernelName = "KDepthDownsample8DualUav";
        private const int CheckerboardMipCount = 0;

        private static readonly int CameraDepthTextureId = Shader.PropertyToID("_CameraDepthTexture");
        private static readonly int DepthMipChainId = Shader.PropertyToID("_DepthMipChain");
        private static readonly int DepthPyramidGlobalId = Shader.PropertyToID("_DepthPyramid");
        private static readonly int DepthPyramidMipLevelOffsetsId = Shader.PropertyToID("_DepthPyramidMipLevelOffsets");
        private static readonly int CameraSizeId = Shader.PropertyToID("_DepthPyramidCameraSize");
        private static readonly int MinDstCountId = Shader.PropertyToID("_MinDstCount");
        private static readonly int CbDstCountId = Shader.PropertyToID("_CbDstCount");
        private static readonly int SrcOffsetId = Shader.PropertyToID("_SrcOffset");
        private static readonly int SrcLimitId = Shader.PropertyToID("_SrcLimit");
        private static readonly int DstSize0Id = Shader.PropertyToID("_DstSize0");
        private static readonly int DstSize1Id = Shader.PropertyToID("_DstSize1");
        private static readonly int DstSize2Id = Shader.PropertyToID("_DstSize2");
        private static readonly int DstSize3Id = Shader.PropertyToID("_DstSize3");
        private static readonly int MinDstOffset0Id = Shader.PropertyToID("_MinDstOffset0");
        private static readonly int MinDstOffset1Id = Shader.PropertyToID("_MinDstOffset1");
        private static readonly int MinDstOffset2Id = Shader.PropertyToID("_MinDstOffset2");
        private static readonly int MinDstOffset3Id = Shader.PropertyToID("_MinDstOffset3");
        private static readonly int CbDstOffset0Id = Shader.PropertyToID("_CbDstOffset0");
        private static readonly int CbDstOffset1Id = Shader.PropertyToID("_CbDstOffset1");

        [Read(BuiltinTexture.CameraDepthTexture)]
        public TextureSlot depth = TextureSlot.Read("Depth", BuiltinTexture.CameraDepthTexture);

        private ComputeShader _computeShader;
        private int _copyKernel = -1;
        private int _downsampleKernel = -1;
        private static readonly ProfilingSampler _profilingSampler = new("Tsukuyomi Depth Pyramid");

        public override void CollectTextureSlots(System.Collections.Generic.List<TextureSlot> slots)
        {
            slots.Add(depth);
        }

        public override string Name => "Tsukuyomi Depth Pyramid";

        public bool Configure()
        {
            if (!TsukuyomiRenderPipelineResourcesProvider.TryGet(out TsukuyomiRenderPipelineResources resources))
                return false;

            _computeShader = resources.DepthPyramidComputeShader;
            if (_computeShader == null)
            {
                Debug.LogError("Tsukuyomi Depth Pyramid requires a DepthPyramid compute shader in TsukuyomiRenderPipelineResources.");
                return false;
            }

            _copyKernel = _computeShader.FindKernel(CopyKernelName);
            _downsampleKernel = _computeShader.FindKernel(DownsampleKernelName);
            return _copyKernel >= 0 && _downsampleKernel >= 0;
        }

        public override bool IsActive(in FrameContext frame)
        {
            return base.IsActive(frame) && _computeShader != null && _copyKernel >= 0 && _downsampleKernel >= 0;
        }

        private sealed class RenderData
        {
            public TextureHandle CameraDepth;
            public TextureHandle DepthPyramid;
            public BufferHandle MipOffsetsBuffer;
            public ComputeShader ComputeShader;
            public int CopyKernel;
            public int DownsampleKernel;
            public int Width;
            public int Height;
            public Vector2Int[] MipSizes;
            public Vector2Int[] MipOffsets;
            public Vector2Int[] MipOffsetsCheckerboard;
            public int MipCount;
            public int MipCountCheckerboard;
            public Vector2Int[] MipLevelOffsetsBufferData;
            public readonly int[] IntPair = new int[2];
        }

        public override void Record(in ComputePassContext context)
        {
            var passData = context.GetOrCreateData<RenderData>();
            if (_computeShader == null || context.CameraData.isPreviewCamera)
                return;

            TextureHandle cameraDepth = context.GetTexture(depth);
            if (!cameraDepth.IsValid())
                return;

            RenderTextureDescriptor cameraDescriptor = context.CameraData.cameraTargetDescriptor;
            TsukuyomiDepthPyramidResources.PackedMipChainInfo mipInfo =
                TsukuyomiDepthPyramidResources.ComputePackedMipChainInfo(cameraDescriptor.width, cameraDescriptor.height, CheckerboardMipCount);
            TextureSlot depthPyramidSlot = TsukuyomiDepthPyramidResources.CreateDepthPyramidSlot(cameraDescriptor, ResourceAccess.ReadWrite, mipInfo);
            TextureHandle depthPyramid = context.GetTexture(depthPyramidSlot);
            if (!depthPyramid.IsValid())
                return;

            BufferSlot mipOffsetsSlot = TsukuyomiDepthPyramidResources.CreateDepthPyramidMipLevelOffsetsBufferSlot(ResourceAccess.Write);
            BufferHandle mipOffsetsBuffer = context.GetBuffer(mipOffsetsSlot);
            if (!mipOffsetsBuffer.IsValid())
                return;

            context.BindTexture(cameraDepth, depth);
            context.BindTexture(depthPyramid, depthPyramidSlot);
            context.BindBuffer(mipOffsetsBuffer, mipOffsetsSlot);
            context.Builder.AllowPassCulling(false);
            context.Builder.AllowGlobalStateModification(true);

            passData.ComputeShader = _computeShader;
            passData.CopyKernel = _copyKernel;
            passData.DownsampleKernel = _downsampleKernel;
            passData.Width = cameraDescriptor.width;
            passData.Height = cameraDescriptor.height;
            passData.MipSizes = mipInfo.MipSizes;
            passData.MipOffsets = mipInfo.MipOffsets;
            passData.MipOffsetsCheckerboard = mipInfo.MipOffsetsCheckerboard;
            passData.MipCount = mipInfo.MipCount;
            passData.MipCountCheckerboard = mipInfo.MipCountCheckerboard;
            passData.MipLevelOffsetsBufferData = mipInfo.MipOffsets;

            passData.CameraDepth = cameraDepth;
            passData.DepthPyramid = depthPyramid;
            passData.MipOffsetsBuffer = mipOffsetsBuffer;

            context.SetRenderFunc(passData, static (state, graphContext) =>
            {
                graphContext.cmd.SetComputeTextureParam(state.ComputeShader, state.CopyKernel, CameraDepthTextureId, state.CameraDepth);
                graphContext.cmd.SetComputeTextureParam(state.ComputeShader, state.CopyKernel, DepthMipChainId, state.DepthPyramid);
                graphContext.cmd.SetComputeVectorParam(state.ComputeShader, CameraSizeId, new Vector4(state.Width, state.Height, 0.0f, 0.0f));
                graphContext.cmd.DispatchCompute(state.ComputeShader, state.CopyKernel, DivRoundUp(state.Width, TileSize), DivRoundUp(state.Height, TileSize), 1);

                graphContext.cmd.SetBufferData(state.MipOffsetsBuffer, state.MipLevelOffsetsBufferData);

                for (int dstIndex0 = 1; dstIndex0 < state.MipCount;)
                {
                    int minCount = Mathf.Min(state.MipCount - dstIndex0, 4);
                    int cbCount = 0;
                    if (dstIndex0 < state.MipCountCheckerboard)
                        cbCount = Mathf.Min(state.MipCountCheckerboard - dstIndex0, minCount);

                    int dstIndex1 = Mathf.Min(dstIndex0 + 1, state.MipCount - 1);
                    int dstIndex2 = Mathf.Min(dstIndex0 + 2, state.MipCount - 1);
                    int dstIndex3 = Mathf.Min(dstIndex0 + 3, state.MipCount - 1);
                    Vector2Int srcOffset = state.MipOffsets[dstIndex0 - 1];
                    Vector2Int srcLimit = state.MipSizes[dstIndex0 - 1] - Vector2Int.one;

                    graphContext.cmd.SetComputeTextureParam(state.ComputeShader, state.DownsampleKernel, DepthMipChainId, state.DepthPyramid);
                    graphContext.cmd.SetComputeIntParam(state.ComputeShader, MinDstCountId, minCount);
                    graphContext.cmd.SetComputeIntParam(state.ComputeShader, CbDstCountId, cbCount);
                    SetComputeVector2Int(graphContext.cmd, state.ComputeShader, SrcOffsetId, srcOffset, state.IntPair);
                    SetComputeVector2Int(graphContext.cmd, state.ComputeShader, SrcLimitId, srcLimit, state.IntPair);
                    SetComputeVector2Int(graphContext.cmd, state.ComputeShader, DstSize0Id, state.MipSizes[dstIndex0], state.IntPair);
                    SetComputeVector2Int(graphContext.cmd, state.ComputeShader, DstSize1Id, state.MipSizes[dstIndex1], state.IntPair);
                    SetComputeVector2Int(graphContext.cmd, state.ComputeShader, DstSize2Id, state.MipSizes[dstIndex2], state.IntPair);
                    SetComputeVector2Int(graphContext.cmd, state.ComputeShader, DstSize3Id, state.MipSizes[dstIndex3], state.IntPair);
                    SetComputeVector2Int(graphContext.cmd, state.ComputeShader, MinDstOffset0Id, state.MipOffsets[dstIndex0], state.IntPair);
                    SetComputeVector2Int(graphContext.cmd, state.ComputeShader, MinDstOffset1Id, state.MipOffsets[dstIndex1], state.IntPair);
                    SetComputeVector2Int(graphContext.cmd, state.ComputeShader, MinDstOffset2Id, state.MipOffsets[dstIndex2], state.IntPair);
                    SetComputeVector2Int(graphContext.cmd, state.ComputeShader, MinDstOffset3Id, state.MipOffsets[dstIndex3], state.IntPair);
                    SetComputeVector2Int(graphContext.cmd, state.ComputeShader, CbDstOffset0Id, state.MipOffsetsCheckerboard[dstIndex0], state.IntPair);
                    SetComputeVector2Int(graphContext.cmd, state.ComputeShader, CbDstOffset1Id, state.MipOffsetsCheckerboard[dstIndex1], state.IntPair);
                    graphContext.cmd.DispatchCompute(
                        state.ComputeShader,
                        state.DownsampleKernel,
                        DivRoundUp(state.MipSizes[dstIndex0].x, TileSize),
                        DivRoundUp(state.MipSizes[dstIndex0].y, TileSize),
                        1);

                    dstIndex0 += minCount;
                }

                graphContext.cmd.SetGlobalTexture(DepthPyramidGlobalId, state.DepthPyramid);
                graphContext.cmd.SetGlobalBuffer(DepthPyramidMipLevelOffsetsId, state.MipOffsetsBuffer);
            }, _profilingSampler);
        }

        private static void SetComputeVector2Int(ComputeCommandBuffer cmd, ComputeShader shader, int id, Vector2Int value, int[] values)
        {
            // The command records the values now; reuse the scratch array for the next command.
            values[0] = value.x;
            values[1] = value.y;
            cmd.SetComputeIntParams(shader, id, values);
        }

        private static int DivRoundUp(int value, int divisor)
        {
            return (value + divisor - 1) / divisor;
        }
    }
}
