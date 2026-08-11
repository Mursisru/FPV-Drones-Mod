# Changelog

All notable changes to this project are documented in this file.

## [2.2.0] — 2026-08-11

### Added

- Owned fullscreen Gunship FS (no MissileCamera.dll dependency): COD HUD, LookAround, optical zoom
- Vision cycle **J**: Color / NVG / WhiteHot / BlackHot / EDGE± (default WhiteHot)
- Embedded shader bundle + InfraredBlit, NVG Volume, MC-parity RenderPrep (TargetCam URP mirror, terrain window, DetailRenderer)
- EOF feed driver, unit marker layer, direct blast damage path for kamikaze hits

### Changed

- Removed MissileCamera bridge/Harmony patches; FPV owns the full FS stack
- PostFx defaults match MC (CRT UI overlay on; bloom/chromatic/motion-blur blit off)

### Fixed

- Infrared vision overwritten by double URP Base pass; Overlay idle + one manual Render
- Boom impact FX/damage at Datum/spectator and soft-lock after detonation

## [2.1.1] — 2026-08-11

### Fixed

- Explosion FX spawn at impact world position: Instantate without `Datum.origin` parent so particles do not PlayOnAwake at floating origin / spectator
- FPV Detonate always uses absolute GlobalPosition Rpc (never target-relative)
- Impact hit point stored via `FpvBoomPending` before fuse fires

## [2.1.0] — 2026-08-11

### Added

- Soft thrust-lapse flight (~270 km/h), quadratic drag, explicit gravity, motor kill
- Map launcher icons / ammo UI, host-authoritative spawn, MissileCamera FS HUD rewrite
- Impact resolver with SAFE arming and armed tangible mask

### Changed

- Collective 0–1, AUW 50 kg / HE 40 kg, thrust nodes at 45°

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
