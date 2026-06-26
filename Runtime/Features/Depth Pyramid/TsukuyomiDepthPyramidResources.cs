using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Tsukuyomi.Rendering
{
    internal static class TsukuyomiDepthPyramidResources
    {
        public const int MaxMipCount = 15;
        public const int MaxCheckerboardMipCount = 2;
        public const string DepthPyramid = "_TsukuyomiDepthPyramid";
        public const string DepthPyramidMipLevelOffsets = "_TsukuyomiDepthPyramidMipLevelOffsets";

        public static PackedMipChainInfo ComputePackedMipChainInfo(int width, int height, int checkerboardMipCount = 0)
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            checkerboardMipCount = Mathf.Clamp(checkerboardMipCount, 0, MaxCheckerboardMipCount);

            Vector2Int[] mipSizes = new Vector2Int[MaxMipCount];
            Vector2Int[] mipOffsets = new Vector2Int[MaxMipCount];
            Vector2Int[] mipOffsetsCheckerboard = new Vector2Int[MaxMipCount];
            Vector2Int atlasSize = new(width, height);

            mipSizes[0] = new Vector2Int(width, height);
            mipOffsets[0] = Vector2Int.zero;
            mipOffsetsCheckerboard[0] = Vector2Int.zero;

            int mipLevel = 0;
            Vector2Int mipSize = new(width, height);
            bool hasCheckerboard = checkerboardMipCount != 0;
            int maxCheckerboardLevelCount = hasCheckerboard ? 1 + checkerboardMipCount : 0;

            do
            {
                mipLevel++;

                if (mipLevel >= MaxMipCount)
                    break;

                mipSize.x = Mathf.Max(1, (mipSize.x + 1) >> 1);
                mipSize.y = Mathf.Max(1, (mipSize.y + 1) >> 1);

                mipSizes[mipLevel] = mipSize;

                Vector2Int prevMipSize = mipSizes[mipLevel - 1];
                Vector2Int prevMipBegin = mipOffsets[mipLevel - 1];
                Vector2Int prevMipBeginCheckerboard = mipOffsetsCheckerboard[mipLevel - 1];
                Vector2Int mipBegin;
                Vector2Int mipBeginCheckerboard;

                if (mipLevel == 1)
                {
                    mipBegin = NextMipBegin(prevMipBegin, prevMipSize, PackDirection.Down);
                    mipBeginCheckerboard = hasCheckerboard
                        ? NextMipBegin(mipBegin, mipSize, PackDirection.Right)
                        : mipBegin;
                }
                else
                {
                    bool isOdd = (mipLevel & 1) != 0;
                    PackDirection dir = (isOdd ^ hasCheckerboard) ? PackDirection.Down : PackDirection.Right;
                    mipBegin = NextMipBegin(prevMipBegin, prevMipSize, dir);
                    mipBeginCheckerboard = NextMipBegin(prevMipBeginCheckerboard, prevMipSize, dir);
                }

                mipOffsets[mipLevel] = mipBegin;
                mipOffsetsCheckerboard[mipLevel] = mipBeginCheckerboard;

                atlasSize.x = Mathf.Max(atlasSize.x, mipBegin.x + mipSize.x);
                atlasSize.y = Mathf.Max(atlasSize.y, mipBegin.y + mipSize.y);
                atlasSize.x = Mathf.Max(atlasSize.x, mipBeginCheckerboard.x + mipSize.x);
                atlasSize.y = Mathf.Max(atlasSize.y, mipBeginCheckerboard.y + mipSize.y);
            } while (mipSize.x > 1 || mipSize.y > 1);

            return new PackedMipChainInfo(
                atlasSize,
                mipSizes,
                mipOffsets,
                mipOffsetsCheckerboard,
                mipLevel + 1,
                hasCheckerboard ? maxCheckerboardLevelCount : 0);
        }

        public static TextureDesc CreateDepthPyramidDesc(RenderTextureDescriptor cameraDescriptor, PackedMipChainInfo mipInfo)
        {
            return new TextureDesc(mipInfo.TextureSize.x, mipInfo.TextureSize.y)
            {
                name = DepthPyramid,
                colorFormat = GraphicsFormat.R32_SFloat,
                depthBufferBits = DepthBits.None,
                msaaSamples = MSAASamples.None,
                enableRandomWrite = true,
                clearBuffer = false,
                filterMode = FilterMode.Point
            };
        }

        public static TextureSlot CreateDepthPyramidSlot(RenderTextureDescriptor cameraDescriptor, ResourceAccess access, int checkerboardMipCount = 0)
        {
            PackedMipChainInfo mipInfo = ComputePackedMipChainInfo(cameraDescriptor.width, cameraDescriptor.height, checkerboardMipCount);
            TextureDesc desc = CreateDepthPyramidDesc(cameraDescriptor, mipInfo);
            return access switch
            {
                ResourceAccess.Read => TextureSlot.Read(DepthPyramid, desc),
                ResourceAccess.Write => TextureSlot.Write(DepthPyramid, desc),
                ResourceAccess.ReadWrite => TextureSlot.ReadWrite(DepthPyramid, desc),
                _ => TextureSlot.Read(DepthPyramid, desc)
            };
        }

        public static BufferSlot CreateDepthPyramidMipLevelOffsetsBufferSlot(ResourceAccess access)
        {
            BufferDesc desc = new(MaxMipCount, sizeof(int) * 2, GraphicsBuffer.Target.Structured)
            {
                name = DepthPyramidMipLevelOffsets
            };

            return access switch
            {
                ResourceAccess.Read => BufferSlot.Read(DepthPyramidMipLevelOffsets, desc),
                ResourceAccess.Write => BufferSlot.Write(DepthPyramidMipLevelOffsets, desc),
                ResourceAccess.ReadWrite => BufferSlot.ReadWrite(DepthPyramidMipLevelOffsets, desc),
                _ => BufferSlot.Read(DepthPyramidMipLevelOffsets, desc)
            };
        }

        private enum PackDirection
        {
            Right,
            Down
        }

        private static Vector2Int NextMipBegin(Vector2Int prevMipBegin, Vector2Int prevMipSize, PackDirection dir)
        {
            Vector2Int mipBegin = prevMipBegin;
            if (dir == PackDirection.Right)
                mipBegin.x += prevMipSize.x;
            else
                mipBegin.y += prevMipSize.y;
            return mipBegin;
        }

        internal readonly struct PackedMipChainInfo
        {
            public readonly Vector2Int TextureSize;
            public readonly Vector2Int[] MipSizes;
            public readonly Vector2Int[] MipOffsets;
            public readonly Vector2Int[] MipOffsetsCheckerboard;
            public readonly int MipCount;
            public readonly int MipCountCheckerboard;

            public Vector4 FirstTwoDepthMipOffsets
            {
                get
                {
                    Vector2Int first = MipCount > 1 ? MipOffsets[1] : Vector2Int.zero;
                    Vector2Int second = MipCount > 2 ? MipOffsets[2] : first;
                    return new Vector4(first.x, first.y, second.x, second.y);
                }
            }

            public PackedMipChainInfo(
                Vector2Int textureSize,
                Vector2Int[] mipSizes,
                Vector2Int[] mipOffsets,
                Vector2Int[] mipOffsetsCheckerboard,
                int mipCount,
                int mipCountCheckerboard)
            {
                TextureSize = textureSize;
                MipSizes = mipSizes;
                MipOffsets = mipOffsets;
                MipOffsetsCheckerboard = mipOffsetsCheckerboard;
                MipCount = mipCount;
                MipCountCheckerboard = mipCountCheckerboard;
            }

            public Vector2Int[] CreateMipLevelOffsetsBufferData()
            {
                Vector2Int[] offsets = new Vector2Int[MaxMipCount];
                int count = Mathf.Min(MipCount, MaxMipCount);
                for (int i = 0; i < count; i++)
                    offsets[i] = MipOffsets[i];

                return offsets;
            }
        }
    }
}
