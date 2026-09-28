# Changelog

All notable changes to this package are documented in this file.

## [Unreleased]

## [0.2.0] - 2026-09-29

### Added

- Added `GraphFeaturePass` and typed Raster, Compute and Unsafe nodes for recording multiple native RenderGraph passes at one injection point, plus fullscreen and copy helpers.
- Added resource creation/import helpers, explicit texture-slot collection, and shared Depth Pyramid requirements with support and ordering checks.
- Added reusable buffered-history allocation and texture descriptor helpers, plus a two-stage fullscreen example.

### Changed

- Reduced recurring managed allocations with pooled render-data snapshots, static callbacks, reusable effect arrays and post-processing plans, cached depth-pyramid layouts, and reusable upscaler dispatch descriptions.
- Reused frame-resource views and made `FrameContext` a readonly struct.
- Migrated volumetric fog to a typed graph node and separated Bloom generation from Uber composition with an explicit texture dependency.
- Refactored SSGI to use shared buffered-history helpers and expanded the feature development guide for the new recording APIs.

### Fixed

- Reject invalid shader pass indices before recording or publishing a fullscreen post-process output.
- Release destroyed-camera upscaler contexts and destroy released FSR3 render textures.
- Update Unity 6.6 upscaler options and resolution negotiation, and initialize UnityRHI against the editor's selected D3D12 runtime unless explicitly overridden.

## [0.1.9] - 2026-09-28

### Added

- Added OpenPBR SpecularPBR materials and Balanced/High shader variants with their lookup resources.
- Added the unified Tsukuyomi Upscaler for DLSS SR / DLAA and FSR3, with optional DLSS 5 Neural Rendering and bundled Windows native plugins.

### Changed

- Replaced the legacy Standard shaders with OpenPBR-based SpecularPBR lighting.
- Create and cache volumetric fog and depth-downsampling materials from preloaded shader references, removing the bundled material assets and their resource settings.

### Fixed

- Support Unity 6.6 / URP 17.6 upscaler construction, framework-owned options, resolution queries and XR eye IDs while retaining the Unity 6.5 / URP 17.5 integration.
- Use Unity's TypeCache for the graph editor's render-pass menu to avoid scanning unloaded assemblies on Unity 6.6.
- Keep volumetric material state private to each pass, release replaced materials when shaders change, and reuse valid materials when another shader is missing.

## [0.1.8] - 2026-09-13

### Added

- Added Character Common, Face and Hair shaders with shared URP lighting and supporting passes.

### Changed

- Extracted shared spatial, depth, normal, bilateral upsampling and temporal weighting functions into `ShaderLibrary/DenoiseUtils.hlsl`, reused by Contact Shadows, GTAO, SSGI, SSS and volumetric fog.

### Fixed

- Fixed GTAO blur/upsample dispatch coverage so the final row and column are written at every resolution without shifting the filter footprint.
- Guarded out-of-range writes and dispatch threads in GTAO and Contact Shadow denoising.
- Declared GTAO and per-object shadow texture dependencies through their lighting consumers.
- Kept camera color unchanged when a PostPass fails to record, and fixed depth attachment and fullscreen blit builder handling.
- Requested intermediate color targets for color sampling and preserved persistent targets between stacked cameras.
- Reallocated persistent resources when descriptors change, isolated camera histories, and rejected incompatible named frame resources.
- Synchronized Profile pass changes at runtime and skipped empty or inactive bridges, with cached pass ordering and texture-slot metadata.

## [0.1.7] - 2026-08-25

### Added

- Added Screen Space Global Illumination for Tsukuyomi PBR and SSS shaders, including hierarchical tracing, camera histories, temporal validation, dual-stage denoising, and bilateral upsampling.
- Added a grouped SSGI Volume inspector and a history-safe full-screen Debug Output for the final SSGI signal.
- Added a depth-reconstructed Box Volume particle decal shader with HDR emission, angle fading, Custom1 data, GPU-instanced Shuriken projection, and a setup-validating material inspector.

### Fixed

- Fixed black SSGI miss regions when the URP Asset uses Light Probe Groups by falling back to the default Environment Lighting ambient probe.
- Made Local particle render alignment part of Particle Decal validation and repair, with a targeted warning for World alignment ignoring emitter Transform rotation.

## [0.1.6] - 2026-08-10

### Added

- Added a transparent glass shader with sphere-model refraction, reflection-probe Fresnel, direct specular lighting, MatCap overlay, and an alpha-blended non-refraction mode.
- Added a water shader with depth-based absorption, refraction, planar and probe reflections, Gerstner waves, foam, and caustics.

### Changed

- Improved planar-reflection frustum culling and enabled planar-reflection keywords for the water shader.
- Centralized renderer-feature bridge-pass setup and enqueue handling.

## [0.1.5] - 2026-07-26

### Added

- Added pass node inspection and editing to the render graph tooling.
- Added a standard particle shader with its custom material inspector.
- Added render-object pass support and a first-person weapon camera rendering feature.

### Fixed

- Updated shader keywords and includes to resolve Unity 6 rendering warnings.

## [0.1.4] - 2026-07-18

### Fixed

- Renamed the LookDev sample render pipeline asset and renderer data to avoid GUID collisions with Unity's default URP project assets.
- Regenerated the sample render pipeline asset and renderer data GUIDs, and updated the sample setup flow and documentation.

## [0.1.3] - 2026-07-18

### Added

- Added the LookDev package sample with its scene, rendering configuration, and required assets.

### Fixed

- Added a user-confirmed LookDev setup flow that applies the required URP Asset and repairs APV Scene GUID references after sample import.
- Synchronized the package manifest and installation links with the published version.

## [0.1.1] - 2026-07-16

### Fixed

- Added missing Unity metadata for the license and changelog files so the package imports cleanly from an immutable Git package cache.

## [0.1.0] - 2026-07-15

### Added

- Initial public release.
- Custom URP rendering features, shaders, and editor tooling.
- Support for Unity 6000.5 and URP 17.5.
