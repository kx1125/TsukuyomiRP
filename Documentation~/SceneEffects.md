# Scene effect APIs

`TsukuyomiFeature` owns four independent camera-local rendering features:

| Read-only entry | Public type | Rendering responsibility |
| --- | --- | --- |
| `VoxelTransition` | `TsukuyomiVoxelTransitionPass` | Original color reference, world-voxel sampled color, distorted color and screen-expansion weight |
| `ScanPostProcess` | `TsukuyomiScanPostProcessPass` | Surface scan/grid, reveal, flash, voxel glitch, horizontal glitch and chromatic split |
| `XrayRender` | `TsukuyomiXrayRenderPass` | Explicit renderer registration, target mask/private depth and highlight contribution |
| `SpatialGridRender` | `TsukuyomiSpatialGridRenderPass` | World-space lattice points and connecting lines |

The renderer owns these instances internally. Frontends call the static Pass APIs; no `RendererFeature` reference or runtime asset lookup is needed. Do not add these built-in instances to `Profile.Passes`.

## Profile, Volume and runtime controls

The Profile inspector exposes **VoxelTransition**, **ScanPostProcess**, **XrayRender** and **SpatialGridRender**, each with an enable switch and strongly typed parameters. New Profiles default to disabled. Add the corresponding `TsukuyomiRP/<feature>` Volume override to adjust individual parameters. Only checked Volume parameters override the Profile; inactive Volume components use Profile defaults. Scan, voxel glitch, horizontal glitch and chromatic split have separate Volume switches.

Precedence is **Profile → checked Volume overrides → explicit static camera overrides**. A static enable override can enable a module even without Profile settings. Parameter overrides do not implicitly enable it. `ClearCamera(camera)` removes runtime overrides and returns control to Profile/Volume; it does not force the panel-controlled module off.

All four Pass types expose inherited static `SetCameraEnabled`, `SetCameraParameters`, `SetCameraAnimation`, `HasCameraOverrides` and `ClearCamera`. `SetCameraParameters` copies a full parameter struct. `SetCameraAnimation` copies only origin, progress, blend and externally evaluated animated contributions, preserving live panel appearance, range and coverage mode. The static state exists before renderer creation and survives renderer recreation. It is isolated by camera and reset on subsystem registration, including Play Mode with domain reload disabled. Clear retired camera controls explicitly; destroyed camera keys are also pruned while resolving settings.

For an authored scan, set panel coverage to `Scan`; publish a `ScanAnimationParameters` snapshot each frame. Independent grid usage defaults to `Full`. A frontend timeline should avoid publishing complete rendering parameters every frame so artists can continue adjusting the panels.

## Standalone spatial grid

```csharp
var grid = SpatialGridParameters.Default; // Full coverage, spacing 5, radius 40
grid.OriginWS = origin.position;
grid.Color = new Color(0f, .5f, 1f, 1f);
TsukuyomiSpatialGridRenderPass.SetCameraParameters(camera, grid);
TsukuyomiSpatialGridRenderPass.SetCameraEnabled(camera, true);

// Stop recording draws but retain settings/cache.
TsukuyomiSpatialGridRenderPass.SetCameraEnabled(camera, false);

// Drop camera settings independently of backend resources.
TsukuyomiSpatialGridRenderPass.ClearCamera(camera);
TsukuyomiSpatialGridRenderPass.ReleaseCameraResources(camera);
```

The instance methods remain available for integrations that own explicit Pass instances; they override panel defaults and precede static camera controls. `Dispose` releases renderer-owned resources and resolved settings; static camera overrides survive renderer recreation. Static `ClearCamera` and `ReleaseCameraResources` accept previously destroyed camera references for cleanup.

`SpatialGridCoverage.Full` ignores the scanning coverage masks. `Blend` still controls overall fade and the pulse remains externally configurable. For a composed scan, select `Scan` in the panel and publish animated progress/Blend through `SetCameraAnimation`. Range, ring width, noise and pulse speed remain panel-authored unless a complete parameter override is supplied. No curve or timer is evaluated inside the renderer.

The internal `ISpatialGridBackend` only configures rendering, records RenderGraph nodes and releases resources. Each camera owns its `SpatialGridMeshBackend`, material and mesh cache. Public parameters expose no mesh, vertex/index or buffer layout. A future particle/compute backend can replace the internal implementation while preserving project calls.

The Mesh backend preserves world-lattice alignment (origin rounded to the nearest spacing), spherical coverage, depth attenuation, exclusion, additive blending and pulses. Its private limits are radius 1–40 and 1–8 grid steps on either side of each axis. Effective radius is also limited to `halfCount * spacing`; smaller spacing may reduce coverage. The default 17³ lattice has 4,913 points, 13,872 connections, 75,140 vertices and 37,570 triangles in one mesh draw.

## Independent post effects

```csharp
TsukuyomiScanPostProcessPass.SetCameraParameters(camera, ScanPostProcessParameters.Default);
TsukuyomiScanPostProcessPass.SetCameraEnabled(camera, true);
TsukuyomiScanPostProcessPass.SetCameraEffectEnabled(camera, ScanPostEffect.VoxelGlitch, false);
TsukuyomiScanPostProcessPass.SetCameraEffectEnabled(camera, ScanPostEffect.HorizontalGlitch, true);
TsukuyomiScanPostProcessPass.SetCameraEffectEnabled(camera, ScanPostEffect.ChromaticSplit, false);
```

`ScanPostEffect.Scan` controls the surface scan/grid, reveal and flash. The other three switches are independent. Flags survive subsequent parameter updates. Horizontal glitch and chromatic split share one sampling node when either has a nonzero contribution. Voxel glitch runs first. Full coverage allows standalone voxel glitch, using `VoxelTransitionWeight` directly; scan coverage retains the original progress/reveal modulation.

`VoxelTransitionParameters` separately controls sampling strength, distortion strength, completion and external coverage. Disabling VoxelTransition makes shared composition use the original color and screen weight 1.

## Renderer masks

```csharp
TsukuyomiXrayRenderPass.SetCameraRenderers(camera, new[] {
    new RendererMaskEntry(primaryRenderer, 1),
    new RendererMaskEntry(secondaryRenderer, 2),
    new RendererMaskEntry(excludedRenderer, 0, RendererMaskRole.Excluded)
});
TsukuyomiXrayRenderPass.SetCameraParameters(camera, XrayRenderParameters.Default);
TsukuyomiXrayRenderPass.SetCameraEnabled(camera, true);
```

Registration is copied. Positive numeric tags select the primary palette (1) or secondary palette (>1); gameplay categories belong to the project adapter. Excluded entries can supply the optional exclusion mask even when Xray highlighting is disabled. Clear registration/state when the camera is retired. Renderer visibility, frustum/layer filtering, submeshes and standard URP/legacy alpha clipping retain the original behavior.

## Execution and precision

Before transparents: target/excluded masks → voxel transition sampling → Xray contribution → shared color composition → spatial grid. Shared composition retains surface effects + target contribution, screen mix, flash, overall fade and exclusion, in that order. Spatial grid uses only the color attachment, scene depth and optional exclusion mask. It requests an intermediate color attachment explicitly, including when no other effect is enabled.

After transparents and before Bloom: voxel glitch → horizontal/chromatic sampling. Bloom and tonemapping remain scheduled by the existing pipeline feature.

The separated sampled colors and target contribution use full float precision to preserve the original shader calculation before final color storage. These transient buffers are allocated only when their modules record; standalone spatial grid needs none of them.
