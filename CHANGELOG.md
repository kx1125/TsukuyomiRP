# Changelog

All notable changes to this package are documented in this file.

## [Unreleased]

### Fixed

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
