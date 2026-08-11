# Changelog

All notable changes to this project are documented in this file.

## [2.0.0] — 2026-08-11

### Added

- Full MAH architecture: Missile entity with aircraft-style acro control
- MSV Drone Launcher (`fpv_launcher_msv`) and FPV Kamikaze Drone (`fpv_drone`) definitions
- Map-only launch flow with host-authoritative spawn RPC
- Virtual possession session, link quality, jet autopilot hold, and OSD overlay
- FPV Strike Package convoy injection ($900k)
- Embedded asset bundle with custom FBX drone model

### Fixed

- Invisible units: RC-style vanilla prefab stamping instead of custom NetworkIdentity prefabs
- Drone scale compensation for tiny `bomb_125` parent transform
- Runtime materials: game-compatible shader + bundle albedo texture
- Bomb/warhead submesh materials: per-slot texture with neutral tint
