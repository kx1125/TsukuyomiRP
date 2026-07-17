# Changelog

All notable changes to this package are documented in this file.

## [Unreleased]

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
