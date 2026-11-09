# Scene effect APIs

Scene Scan provides four camera-local playback modules and an optional persistent outline.

| Profile node | Public API type | Rendering responsibility |
| --- | --- | --- |
| `VoxelTransition` | `TsukuyomiVoxelTransitionPass` | Original color reference, world-voxel sampled color, distorted color and screen-expansion weight |
| `ScanPostProcess` | `TsukuyomiScanPostProcessPass` | Surface scan/grid, reveal, flash, voxel glitch, horizontal glitch and chromatic split |
| `XrayRender` | `TsukuyomiXrayRenderPass` | Explicit renderer registration, target mask/private depth and highlight contribution |
| `SpatialGridRender` | `TsukuyomiSpatialGridRenderPass` | World-space lattice points and connecting lines |
| `Scene Outline` | `SceneOutlinePass` | Persistent silhouettes and surface creases in the shared color composite |

Add the corresponding Pass nodes through Graph Editor and click **Save & Apply**. Each renderer owns its runtime instances. Frontends call the static Pass APIs without a RendererFeature reference. The shared mask and composition are automatic helpers.

## Profile, Volume and runtime controls

New Profiles are empty. After adding **VoxelTransition**, **ScanPostProcess**, **XrayRender** or **SpatialGridRender** in Graph Editor and saving, the Profile Inspector exposes that node's Enabled switch and parameters. Add the corresponding `TsukuyomiRP/<feature>` Volume override to adjust individual parameters. Only checked Volume parameters override the node; inactive Volume components use node defaults. Scan, voxel glitch, horizontal glitch and chromatic split have separate Volume switches.

For the four playback modules, the Profile's **Enabled** switch makes the module available. An enabled playback node starts idle on every camera. A disabled or absent node skips playback, including requests from a Volume or the camera API.

Parameters resolve from node defaults, then checked Volume fields, then explicit camera overrides. To play a module, call `SetCameraEnabled(camera, true)` or explicitly override its Volume's `enable` to true. Parameter and animation updates alone leave an idle module idle. `SetCameraEnabled(camera, false)` stops playback even while the Volume requests it. `ClearCamera(camera)` removes the camera overrides and returns to the Volume's playback setting, which defaults to off. Other SceneScan modules can still use the shared exclusion mask when Xray highlighting is disabled.

All four Pass types expose inherited static `SetCameraEnabled`, `SetCameraParameters`, `SetCameraAnimation`, `HasCameraOverrides` and `ClearCamera`. `SetCameraParameters` copies a full parameter struct. `SetCameraAnimation` copies only origin, progress, blend and externally evaluated animated contributions, preserving live panel appearance, range and coverage mode. The static state exists before renderer creation and survives renderer recreation. It is isolated by camera and reset on subsystem registration and when returning to Edit Mode, including Play Mode with domain reload disabled. Clear retired camera controls explicitly; destroyed camera keys are also pruned while resolving settings.

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

## Persistent scene outline

In the Profile Graph Editor, open **BeforePostProcess → Add Pass → Scene Scan → Scene Outline** and click **Save & Apply**. The enabled node draws in the shared composite before transparents on cameras with **Post Processing** enabled. Set **Line Color**, **Opacity** and **Width** (output pixels). **Depth Threshold** controls silhouettes and depth discontinuities. Enable **Use Normals** and adjust **Normal Threshold** to include surface creases.

In a global or local Volume Profile, choose **Add Override → TsukuyomiRP → Scene Outline**. Check individual fields to override the Graph settings for cameras whose **Volume Layer Mask** includes the Volume. Color, opacity, width and thresholds blend from the Graph values through overlapping Volumes in priority order. Use **Opacity** for a gradual fade. **Enable**, **Use Normals** and **Temporal Stability** are switches. Leaving the Volume restores the underlying settings. Turning off camera post-processing or overriding **Enable** to off skips the persistent outline and its character mask and releases that camera's outline history. The Graph node must be present and enabled for the Volume to take effect.

While the surface scan plays, its coverage and opening animation transition the same edges to the scan color. Stopping or clearing playback restores the base outline wherever the scan is inactive. Add **Scene Outline Exclude** to a character root to omit its visible geometry from both outline styles. Active child meshes and skinned meshes, including newly attached equipment, are collected for each mask draw. Xray target registration, highlights and playback controls remain independent. The exclusion mask supports the same standard alpha-cutout material properties as the existing Scene Scan masks.

**Temporal Stability** is enabled by default. On a camera running Tsukuyomi DLSS or FSR3, Scene Outline accumulates up to eight samples of geometric edge coverage at the internal render resolution. It uses the nearest surface's motion and checks current and previous depth to reject history at moving silhouettes and newly exposed surfaces. The existing color composite applies the current line color, scan coverage, flash and exclusion. Clearing a scan therefore restores the base style on the next composite. A camera cut, skipped frame, change in resolution or change of upscaler reseeds the history. With camera post-processing enabled, turning off temporal stability or using another upscaler selects the spatial outline.

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

When the persistent outline or surface scan is active, URP records CopyColor before the Scene Scan chain at `BeforeRenderingTransparents`. Transparent materials sample an outline-free `_CameraOpaqueTexture` and draw over the outlined camera color. The chain runs target/exclusion masks, voxel transition sampling, Xray contribution, outline coverage, shared color composition and the spatial grid in that order. FSR3's optional opaque-only capture reads the composited color for its reactive mask. Independent grid and Xray rendering retain their earlier timing when the camera draws neither scene outline style.

When the persistent outline is visible, Frame Debugger groups **Tsukuyomi Scene Outline Temporal Coverage** (when active) and the single **Tsukuyomi Scene Effect Color Composite** draw under **Tsukuyomi Scene Outline**. A new history uses one seed dispatch. Subsequent frames run clear, depth reprojection and resolve inside the same compute pass. With spatial outlines the group contains just the color draw. With the persistent outline off, standalone scans keep their own color composite entry. The optional character exclusion mask is drawn before the group.

The composite adds the target contribution to the surface effects before it applies the screen mix, flash, overall fade and exclusion. Spatial grid uses only the color attachment, scene depth and optional exclusion mask. It requests an intermediate color attachment explicitly, including when no other effect is enabled.

After transparents, voxel glitch runs first, followed by horizontal glitch and chromatic split. These run before the shared post-process composite. Bloom, Diffusion and Tonemapping blend in Profile order in a single Uber draw.

The separated sampled colors and target contribution use full float precision to preserve the original shader calculation before final color storage. These transient buffers are allocated only when their modules record; standalone spatial grid needs none of them.
