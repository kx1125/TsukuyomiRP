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
        private readonly ProfilingSampler _profilingSampler = new("Tsukuyomi Depth Pyramid");

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

        public override void Record(in ComputePassContext context)
        {
            if (_computeShader == null || context.CameraData.isPreviewCamera)
                return;

            TextureHandle cameraDepth = context.GetTexture(depth);
            if (!cameraDepth.IsValid())
                return;

            RenderTextureDescriptor cameraDescriptor = context.CameraData.cameraTargetDescriptor;
            TsukuyomiDepthPyramidResources.PackedMipChainInfo mipInfo =
                TsukuyomiDepthPyramidResources.ComputePackedMipChainInfo(cameraDescriptor.width, cameraDescriptor.height, CheckerboardMipCount);
            TextureSlot depthPyramidSlot = TsukuyomiDepthPyramidResources.CreateDepthPyramidSlot(cameraDescriptor, ResourceAccess.ReadWrite, CheckerboardMipCount);
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

            ComputeShader computeShader = _computeShader;
            int copyKernel = _copyKernel;
            int downsampleKernel = _downsampleKernel;
            int width = cameraDescriptor.width;
            int height = cameraDescriptor.height;
            Vector2Int[] mipSizes = mipInfo.MipSizes;
            Vector2Int[] mipOffsets = mipInfo.MipOffsets;
            Vector2Int[] mipOffsetsCheckerboard = mipInfo.MipOffsetsCheckerboard;
            int mipCount = mipInfo.MipCount;
            int mipCountCheckerboard = mipInfo.MipCountCheckerboard;
            Vector2Int[] mipLevelOffsetsBufferData = mipInfo.CreateMipLevelOffsetsBufferData();
            ProfilingSampler profilingSampler = _profilingSampler;

            context.SetRenderFunc((data, graphContext) =>
            {
                using (new ProfilingScope(graphContext.cmd, profilingSampler))
                {
                    graphContext.cmd.SetComputeTextureParam(computeShader, copyKernel, CameraDepthTextureId, cameraDepth);
                    graphContext.cmd.SetComputeTextureParam(computeShader, copyKernel, DepthMipChainId, depthPyramid);
                    graphContext.cmd.SetComputeVectorParam(computeShader, CameraSizeId, new Vector4(width, height, 0.0f, 0.0f));
                    graphContext.cmd.DispatchCompute(computeShader, copyKernel, DivRoundUp(width, TileSize), DivRoundUp(height, TileSize), 1);

                    graphContext.cmd.SetBufferData(mipOffsetsBuffer, mipLevelOffsetsBufferData);

                    for (int dstIndex0 = 1; dstIndex0 < mipCount;)
                    {
                        int minCount = Mathf.Min(mipCount - dstIndex0, 4);
                        int cbCount = 0;
                        if (dstIndex0 < mipCountCheckerboard)
                            cbCount = Mathf.Min(mipCountCheckerboard - dstIndex0, minCount);

                        int dstIndex1 = Mathf.Min(dstIndex0 + 1, mipCount - 1);
                        int dstIndex2 = Mathf.Min(dstIndex0 + 2, mipCount - 1);
                        int dstIndex3 = Mathf.Min(dstIndex0 + 3, mipCount - 1);
                        Vector2Int srcOffset = mipOffsets[dstIndex0 - 1];
                        Vector2Int srcLimit = mipSizes[dstIndex0 - 1] - Vector2Int.one;

                        graphContext.cmd.SetComputeTextureParam(computeShader, downsampleKernel, DepthMipChainId, depthPyramid);
                        graphContext.cmd.SetComputeIntParam(computeShader, MinDstCountId, minCount);
                        graphContext.cmd.SetComputeIntParam(computeShader, CbDstCountId, cbCount);
                        SetComputeVector2Int(graphContext.cmd, computeShader, SrcOffsetId, srcOffset);
                        SetComputeVector2Int(graphContext.cmd, computeShader, SrcLimitId, srcLimit);
                        SetComputeVector2Int(graphContext.cmd, computeShader, DstSize0Id, mipSizes[dstIndex0]);
                        SetComputeVector2Int(graphContext.cmd, computeShader, DstSize1Id, mipSizes[dstIndex1]);
                        SetComputeVector2Int(graphContext.cmd, computeShader, DstSize2Id, mipSizes[dstIndex2]);
                        SetComputeVector2Int(graphContext.cmd, computeShader, DstSize3Id, mipSizes[dstIndex3]);
                        SetComputeVector2Int(graphContext.cmd, computeShader, MinDstOffset0Id, mipOffsets[dstIndex0]);
                        SetComputeVector2Int(graphContext.cmd, computeShader, MinDstOffset1Id, mipOffsets[dstIndex1]);
                        SetComputeVector2Int(graphContext.cmd, computeShader, MinDstOffset2Id, mipOffsets[dstIndex2]);
                        SetComputeVector2Int(graphContext.cmd, computeShader, MinDstOffset3Id, mipOffsets[dstIndex3]);
                        SetComputeVector2Int(graphContext.cmd, computeShader, CbDstOffset0Id, mipOffsetsCheckerboard[dstIndex0]);
                        SetComputeVector2Int(graphContext.cmd, computeShader, CbDstOffset1Id, mipOffsetsCheckerboard[dstIndex1]);
                        graphContext.cmd.DispatchCompute(
                            computeShader,
                            downsampleKernel,
                            DivRoundUp(mipSizes[dstIndex0].x, TileSize),
                            DivRoundUp(mipSizes[dstIndex0].y, TileSize),
                            1);

                        dstIndex0 += minCount;
                    }

                    graphContext.cmd.SetGlobalTexture(DepthPyramidGlobalId, depthPyramid);
                    graphContext.cmd.SetGlobalBuffer(DepthPyramidMipLevelOffsetsId, mipOffsetsBuffer);
                }
            });
        }

        private static void SetComputeVector2Int(ComputeCommandBuffer cmd, ComputeShader shader, int id, Vector2Int value)
        {
            cmd.SetComputeIntParams(shader, id, value.x, value.y);
        }

        private static int DivRoundUp(int value, int divisor)
        {
            return (value + divisor - 1) / divisor;
        }
    }
}
