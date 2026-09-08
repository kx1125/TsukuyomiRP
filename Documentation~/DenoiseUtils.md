# Denoise Utils

`ShaderLibrary/DenoiseUtils.hlsl` provides pure HLSL functions for denoising math.
It has no pipeline includes, texture/sampler declarations, constant buffers,
camera globals, render-resource allocation, or dispatch code. Include it from
either a compute or raster shader:

```hlsl
#include "Packages/tsukuyomi.render-pipelines.universal/ShaderLibrary/DenoiseUtils.hlsl"

// Load and decode the signal, depth and normal in the calling shader.
float weight = DenoiseSpatialWeight(offset, spatialVariance)
    * DenoiseExponentialDepthWeight(centerDepth, sampleDepth, depthTolerance)
    * DenoiseNormalWeight(centerNormal, sampleNormal, normalExponent);
colorSum += sampleColor * weight;
weightSum += weight;
```

## Available algorithms

| Functions | Contract |
| --- | --- |
| `DenoiseGaussianWeight` | Scalar or RGB Gaussian weight; input is distance divided by sigma. |
| `DenoiseSpatialWeight` | Two-dimensional exponential spatial weight with explicit variance. |
| `DenoiseLinearDepthWeight`, `DenoiseExponentialDepthWeight` | Depth rejection with caller-selected sensitivity, bias or tolerance. |
| `DenoiseNormalWeight`, `DenoiseNormalErrorWeight`, `DenoisePlaneWeight` | Normal and plane rejection; positions/normals must share a coordinate space. |
| `DenoiseBilinearWeight`, `DenoiseInverseDepthWeights` | Upsampling weights; sample order and depth encoding are caller-owned. |
| `DenoiseResolve4` | Resolve four scalar, RGB or RGBA samples with explicit fallback value and weight. |
| `DenoiseHistoryGeometryValid` | Geometric history rejection from already reconstructed positions and normals. |
| `DenoiseTemporalHistoryWeight` | Advance a valid history's sample count and compute its blend weight. |

Use finite inputs, positive variances/tolerances and a positive total resolve
weight. Depth thresholds use the same units/encoding as the supplied depths.
Power-based normal weights expect unit normals. Temporal weight evaluation
expects a valid previous sample count of at least one and a maximum count of
at least one; invalid history must be reset by the caller before reading it.

## Integration boundaries

Contact Shadow, GTAO, SSGI, fog and SSS retain their existing sample footprints,
parameters, texture reads/writes and scheduling. Their original weight responses
remain distinct; sharing utilities does not select a new filtering algorithm.

- GTAO unpacks its AO/depth signal before passing values to the utilities.
- SSGI owns motion-vector/jitter reprojection, history validity, resets and the
  sample count stored in history alpha. The utilities do not read history.
- Fog's raster blur continues to preserve transmittance in alpha. The legacy
  RGBA compute path continues to filter all four channels.
- `DenoiseResolve4` does not assume that a neutral signal is white. Existing
  upsamplers explicitly pass their original fallback value of one.
- Skin masks, scattering profiles and final composition stay with SSS.

The GTAO fused blur/upsample retains its `2*p` / `2*p-1` output phase. Dispatch
`ceil((fullSize / 2 + 1) / 8)` groups per axis using integer division for
`fullSize / 2`, and discard out-of-range stores. The extra half-grid point
covers the final row/column without shifting the existing filter footprint.
All threads must reach the group barriers, including threads at the border.
