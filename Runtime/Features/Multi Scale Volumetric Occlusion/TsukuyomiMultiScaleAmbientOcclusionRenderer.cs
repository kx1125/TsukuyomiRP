// Adapted from Unity-Technologies/PostProcessing (v2), MultiScaleVO.cs.
// Copyright (c) Unity Technologies. MIT license: Shaders/Multi Scale Volumetric Occlusion/LICENSE.md.
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Tsukuyomi.Rendering
{
    // Maintained implementation. All textures are owned by the frame RenderGraph.
    internal static class TsukuyomiMultiScaleAmbientOcclusionRenderer
    {
        static readonly float[] SampleThickness =
        {
            Mathf.Sqrt(1f - 0.2f * 0.2f),
            Mathf.Sqrt(1f - 0.4f * 0.4f),
            Mathf.Sqrt(1f - 0.6f * 0.6f),
            Mathf.Sqrt(1f - 0.8f * 0.8f),
            Mathf.Sqrt(1f - 0.2f * 0.2f - 0.2f * 0.2f),
            Mathf.Sqrt(1f - 0.2f * 0.2f - 0.4f * 0.4f),
            Mathf.Sqrt(1f - 0.2f * 0.2f - 0.6f * 0.6f),
            Mathf.Sqrt(1f - 0.2f * 0.2f - 0.8f * 0.8f),
            Mathf.Sqrt(1f - 0.4f * 0.4f - 0.4f * 0.4f),
            Mathf.Sqrt(1f - 0.4f * 0.4f - 0.6f * 0.6f),
            Mathf.Sqrt(1f - 0.4f * 0.4f - 0.8f * 0.8f),
            Mathf.Sqrt(1f - 0.6f * 0.6f - 0.6f * 0.6f)
        };


        internal sealed class Buffers
        {
            public readonly int[] Widths = new int[7];
            public readonly int[] Heights = new int[7];
            public readonly TextureHandle[] LowDepth = new TextureHandle[4];
            public readonly TextureHandle[] TiledDepth = new TextureHandle[4];
            public readonly TextureHandle[] Occlusion = new TextureHandle[4];
            public readonly TextureHandle[] Combined = new TextureHandle[3];
            public readonly float[] InvThickness = new float[12];
            public readonly float[] SampleWeights = new float[12];
            public TextureHandle LinearDepth;
            public Vector2 Size(int level) => new(Widths[level], Heights[level]);
        }

        public static void Allocate(in UnsafePassContext context, Buffers buffers, int width, int height, int scaleCount)
        {
            for (int level = 0; level < 7; level++)
            {
                int divisor = 1 << level;
                buffers.Widths[level] = Mathf.Max(1, (width + divisor - 1) / divisor);
                buffers.Heights[level] = Mathf.Max(1, (height + divisor - 1) / divisor);
            }
            buffers.LinearDepth = Allocate(context, buffers, 0, GraphicsFormat.R16_SFloat, "MSVO Linear Depth");
            for (int i = 0; i < 4; i++)
            {
                // The two downsample kernels generate depth scales in pairs.
                buffers.LowDepth[i] = Allocate(context, buffers, i + 1, GraphicsFormat.R32_SFloat, "MSVO Depth " + i);
                buffers.TiledDepth[i] = Allocate(context, buffers, i + 3, GraphicsFormat.R16_SFloat, "MSVO Interleaved Depth " + i, true);
                if (i < scaleCount)
                    buffers.Occlusion[i] = Allocate(context, buffers, i + 1, GraphicsFormat.R8_UNorm, "MSVO Occlusion " + i);
                if (i < scaleCount - 1)
                    buffers.Combined[i] = Allocate(context, buffers, i + 1, GraphicsFormat.R8_UNorm, "MSVO Combined " + i);
            }
        }

        private static TextureHandle Allocate(in UnsafePassContext context, Buffers buffers, int level, GraphicsFormat format, string name, bool array = false)
        {
            var desc = TextureDescriptors.Color2D(buffers.Widths[level], buffers.Heights[level], format,
                name, randomWrite: true);
            // TemporaryRT used Clamp; Repeat would change AO at the image boundary.
            desc.wrapMode = TextureWrapMode.Clamp;
            if (array) { desc.dimension = TextureDimension.Tex2DArray; desc.slices = 16; }
            return context.GraphResources.CreateTexture(desc, AccessFlags.ReadWrite);
        }

        internal static RenderTargetIdentifier Texture(TextureHandle handle)
        {
            RTHandle texture = handle;
            return texture.nameID;
        }

        public static void GenerateAOMap(CommandBuffer cmd, Buffers buffers, TextureHandle sourceDepth, TextureHandle output,
            ComputeShader downsample1, ComputeShader downsample2, ComputeShader render, ComputeShader upsample,
            Vector4 zBufferParams, float tanHalfFov, TsukuyomiMultiScaleAmbientOcclusionResolvedSettings settings)
        {
            int kernel = downsample1.FindKernel("MultiScaleVODownsample1");
            cmd.SetComputeTextureParam(downsample1, kernel, "Depth", Texture(sourceDepth));
            cmd.SetComputeTextureParam(downsample1, kernel, "LinearZ", Texture(buffers.LinearDepth));
            cmd.SetComputeTextureParam(downsample1, kernel, "DS2x", Texture(buffers.LowDepth[0]));
            cmd.SetComputeTextureParam(downsample1, kernel, "DS4x", Texture(buffers.LowDepth[1]));
            cmd.SetComputeTextureParam(downsample1, kernel, "DS2xAtlas", Texture(buffers.TiledDepth[0]));
            cmd.SetComputeTextureParam(downsample1, kernel, "DS4xAtlas", Texture(buffers.TiledDepth[1]));
            cmd.SetComputeVectorParam(downsample1, "ZBufferParams", zBufferParams);
            cmd.DispatchCompute(downsample1, kernel, buffers.Widths[4], buffers.Heights[4], 1);
            if (settings.ScaleCount > 2)
            {
                kernel = downsample2.FindKernel("MultiScaleVODownsample2");
                cmd.SetComputeTextureParam(downsample2, kernel, "DS4x", Texture(buffers.LowDepth[1]));
                cmd.SetComputeTextureParam(downsample2, kernel, "DS8x", Texture(buffers.LowDepth[2]));
                cmd.SetComputeTextureParam(downsample2, kernel, "DS16x", Texture(buffers.LowDepth[3]));
                cmd.SetComputeTextureParam(downsample2, kernel, "DS8xAtlas", Texture(buffers.TiledDepth[2]));
                cmd.SetComputeTextureParam(downsample2, kernel, "DS16xAtlas", Texture(buffers.TiledDepth[3]));
                cmd.DispatchCompute(downsample2, kernel, buffers.Widths[6], buffers.Heights[6], 1);
            }
            for (int i = 0; i < settings.ScaleCount; i++)
                PushRenderCommands(cmd, render, buffers.TiledDepth[i], buffers.Occlusion[i],
                    new Vector3(buffers.Widths[i + 3], buffers.Heights[i + 3], 16), tanHalfFov, settings, buffers);
            TextureHandle combined = buffers.Occlusion[settings.ScaleCount - 1];
            for (int i = settings.ScaleCount - 2; i >= 0; i--)
            {
                PushUpsampleCommands(cmd, upsample, buffers.LowDepth[i + 1], combined, buffers.LowDepth[i],
                    buffers.Occlusion[i], buffers.Combined[i], buffers.Size(i + 2), buffers.Size(i + 1), settings);
                combined = buffers.Combined[i];
            }
            PushUpsampleCommands(cmd, upsample, buffers.LowDepth[0], combined, buffers.LinearDepth,
                null, output, buffers.Size(1), buffers.Size(0), settings);
        }

        static void PushRenderCommands(CommandBuffer cmd, ComputeShader cs, TextureHandle source, TextureHandle destination, Vector3 sourceSize, float tanHalfFovH, TsukuyomiMultiScaleAmbientOcclusionResolvedSettings settings, Buffers buffers)
        {
            // Here we compute multipliers that convert the center depth value into (the reciprocal
            // of) sphere thicknesses at each sample location. This assumes a maximum sample radius
            // of 5 units, but since a sphere has no thickness at its extent, we don't need to
            // sample that far out. Only samples whole integer offsets with distance less than 25
            // are used. This means that there is no sample at (3, 4) because its distance is
            // exactly 25 (and has a thickness of 0.)

            // The shaders are set up to sample a circular region within a 5-pixel radius.
            float kScreenspaceDiameter = settings.DepthSphereDiameter;

            // SphereDiameter = CenterDepth * ThicknessMultiplier. This will compute the thickness
            // of a sphere centered at a specific depth. The ellipsoid scale can stretch a sphere
            // into an ellipsoid, which changes the characteristics of the AO.
            // TanHalfFovH: Radius of sphere in depth units if its center lies at Z = 1
            // ScreenspaceDiameter: Diameter of sample sphere in pixel units
            // ScreenspaceDiameter / BufferWidth: Ratio of the screen width that the sphere actually covers
            float thicknessMultiplier = 2f * tanHalfFovH * kScreenspaceDiameter / sourceSize.x;

            // This will transform a depth value from [0, thickness] to [0, 1].
            float inverseRangeFactor = 1f / thicknessMultiplier;

            // The thicknesses are smaller for all off-center samples of the sphere. Compute
            // thicknesses relative to the center sample.
            for (int i = 0; i < 12; i++)
                buffers.InvThickness[i] = inverseRangeFactor / SampleThickness[i];

            // These are the weights that are multiplied against the samples because not all samples
            // are equally important. The farther the sample is from the center location, the less
            // they matter. We use the thickness of the sphere to determine the weight.  The scalars
            // in front are the number of samples with this weight because we sum the samples
            // together before multiplying by the weight, so as an aggregate all of those samples
            // matter more. After generating this table, the weights are normalized.
            buffers.SampleWeights[ 0] = 4 * SampleThickness[ 0];    // Axial
            buffers.SampleWeights[ 1] = 4 * SampleThickness[ 1];    // Axial
            buffers.SampleWeights[ 2] = 4 * SampleThickness[ 2];    // Axial
            buffers.SampleWeights[ 3] = 4 * SampleThickness[ 3];    // Axial
            buffers.SampleWeights[ 4] = 4 * SampleThickness[ 4];    // Diagonal
            buffers.SampleWeights[ 5] = 8 * SampleThickness[ 5];    // L-shaped
            buffers.SampleWeights[ 6] = 8 * SampleThickness[ 6];    // L-shaped
            buffers.SampleWeights[ 7] = 8 * SampleThickness[ 7];    // L-shaped
            buffers.SampleWeights[ 8] = 4 * SampleThickness[ 8];    // Diagonal
            buffers.SampleWeights[ 9] = 8 * SampleThickness[ 9];    // L-shaped
            buffers.SampleWeights[10] = 8 * SampleThickness[10];    // L-shaped
            buffers.SampleWeights[11] = 4 * SampleThickness[11];    // Diagonal

            if (!settings.ExhaustiveSampling)
            {
                buffers.SampleWeights[0] = 0;
                buffers.SampleWeights[2] = 0;
                buffers.SampleWeights[5] = 0;
                buffers.SampleWeights[7] = 0;
                buffers.SampleWeights[9] = 0;
            }

            // Normalize the weights by dividing by the sum of all weights
            var totalWeight = 0f;

            foreach (float w in buffers.SampleWeights)
                totalWeight += w;

            for (int i = 0; i < buffers.SampleWeights.Length; i++)
                buffers.SampleWeights[i] /= totalWeight;

            // Set the arguments for the render kernel.
            if (settings.ExhaustiveSampling) cs.EnableKeyword("SAMPLE_EXHAUSTIVELY");
            else cs.DisableKeyword("SAMPLE_EXHAUSTIVELY");
            int kernel = cs.FindKernel("MultiScaleVORender_interleaved");

            cmd.SetComputeFloatParams(cs, "gInvThicknessTable", buffers.InvThickness);
            cmd.SetComputeFloatParams(cs, "gSampleWeightTable", buffers.SampleWeights);
            cmd.SetComputeVectorParam(cs, "gInvSliceDimension", new Vector2(1f / sourceSize.x, 1f / sourceSize.y));
            cmd.SetComputeVectorParam(cs, "AdditionalParams", new Vector2(-1f / settings.ThicknessModifier, settings.Intensity));
            cmd.SetComputeTextureParam(cs, kernel, "DepthTex", Texture(source));
            cmd.SetComputeTextureParam(cs, kernel, "Occlusion", Texture(destination));

            // Calculate the thread group count and add a dispatch command with them.
            uint xsize, ysize, zsize;
            cs.GetKernelThreadGroupSizes(kernel, out xsize, out ysize, out zsize);

            cmd.DispatchCompute(
                cs, kernel,
                ((int)sourceSize.x + (int)xsize - 1) / (int)xsize,
                ((int)sourceSize.y + (int)ysize - 1) / (int)ysize,
                ((int)sourceSize.z + (int)zsize - 1) / (int)zsize
            );
        }

        static void PushUpsampleCommands(CommandBuffer cmd, ComputeShader cs, TextureHandle lowResDepth, TextureHandle interleavedAO, TextureHandle highResDepth, TextureHandle? highResAO, TextureHandle dest, Vector2 lowResDepthSize, Vector2 highResDepthSize, TsukuyomiMultiScaleAmbientOcclusionResolvedSettings settings)
        {
            int kernel = cs.FindKernel(highResAO.HasValue ? "MultiScaleVOUpSample_blendout" : "MultiScaleVOUpSample");

            float stepSize = settings.BlurReferenceWidth / lowResDepthSize.x;
            float bTolerance = 1f - Mathf.Pow(10f, settings.BlurTolerance) * stepSize;
            bTolerance *= bTolerance;
            float uTolerance = Mathf.Pow(10f, settings.UpsampleTolerance);
            float noiseFilterWeight = 1f / (Mathf.Pow(10f, settings.NoiseFilterTolerance) + uTolerance);

            cmd.SetComputeVectorParam(cs, "InvLowResolution", new Vector2(1f / lowResDepthSize.x, 1f / lowResDepthSize.y));
            cmd.SetComputeVectorParam(cs, "InvHighResolution", new Vector2(1f / highResDepthSize.x, 1f / highResDepthSize.y));
            cmd.SetComputeVectorParam(cs, "AdditionalParams", new Vector4(noiseFilterWeight, stepSize, bTolerance, uTolerance));

            cmd.SetComputeTextureParam(cs, kernel, "LoResDB", Texture(lowResDepth));
            cmd.SetComputeTextureParam(cs, kernel, "HiResDB", Texture(highResDepth));
            cmd.SetComputeTextureParam(cs, kernel, "LoResAO1", Texture(interleavedAO));

            if (highResAO != null)
                cmd.SetComputeTextureParam(cs, kernel, "HiResAO", Texture(highResAO.Value));

            cmd.SetComputeTextureParam(cs, kernel, "AoResult", Texture(dest));

            int xcount = ((int)highResDepthSize.x + 17) / 16;
            int ycount = ((int)highResDepthSize.y + 17) / 16;
            cmd.DispatchCompute(cs, kernel, xcount, ycount, 1);
        }

    }
}
