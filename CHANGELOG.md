# Changelog

All notable changes to this package are documented in this file.

The format is based on Keep a Changelog, and this project follows Semantic Versioning.

## [2.0.2] - 2026-05-10

### Fixed
- Parallel bullet modules no longer freeze WebGL/WebGPU builds. The previous implementation used `System.Threading.Tasks.Parallel.For`, which deadlocks on Unity's single-threaded web player because the ThreadPool has no workers.

### Changed
- `IParallelBulletModule` now schedules a Burst-compiled `IJobParallelFor` via a new `Schedule(...)` method (with a matching `DisposeJobResources()`). Modules execute as chained jobs after `BulletJob`, giving true multi-core + Burst execution on desktop/console and safe inline execution on WebGL.
- Built-in parallel modules (`AngularVelocityModule`, `SpeedOverTimeModule`, `BulletSizeOverTimeModule`, `BulletDamageOverTimeModule`) rewritten to use Burst jobs and a baked `NativeCurve` lookup for their `AnimationCurve`s.

### Added
- `NativeCurve` helper for baking `AnimationCurve`s into Burst-friendly `NativeArray<float>` lookup tables.

## [2.0.1] - 2026-02-19

### Added
- Added a Unity Package Manager sample entry for `Demo Scene`.
- Added demo sample content under `Samples~/Demo Scene`, including a playable scene and helper scripts.

### Changed
- Updated README install guidance and added sample import instructions.
- Removed temporary documentation link references until docs are ready.

## [0.1.0] - 2026-02-19

### Added
- Initial public open source package release for `com.wayfarergames.bulletfury`.
- Core bullet spawning, simulation, rendering, and module extension APIs.

### Changed
- Updated package metadata and documentation links for public distribution.
- Improved runtime safety around missing render data references in `BulletSpawner`.

### Fixed
- Removed editor debug logging noise from render data property drawers.
