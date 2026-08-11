# FPV Drones Mod

![Version](https://img.shields.io/badge/version-2.0.0-blue)
![BepInEx](https://img.shields.io/badge/BepInEx-5.x-green)
![License](https://img.shields.io/badge/license-MIT-lightgrey)

BepInEx mod for **Nuclear Option** that adds a mobile FPV drone launcher and manually controlled kamikaze drones.

> [!IMPORTANT]
> Requires **Nuclear Option** with **BepInEx 5** installed. Place `FPVMod.dll` in `BepInEx/plugins/`.

## Features

- **MSV Drone Launcher** — MLRS chassis with 8-drone capacity and map-based launch UI
- **FPV Kamikaze Drone** — acro flight control, 5-minute battery, 25 kg warhead
- **Virtual possession** — control drones without hijacking the player aircraft slot
- **Link quality** — range, line-of-sight, and jamming affect control latency and OSD static
- **Economy** — "FPV Strike Package" convoy group ($900k)

## Installation

1. Install [BepInEx 5](https://docs.bepinex.dev/) for Nuclear Option.
2. Copy `FPVMod.dll` to:
   ```
   Nuclear Option/BepInEx/plugins/FPVMod.dll
   ```
3. Launch the game. Check `BepInEx/LogOutput.log` for `FPV Drones Mod 2.0.0 loaded.`

## Usage

1. Place **MSV Drone Launcher** or buy the **FPV Strike Package** convoy.
2. Click the launcher on the strategic map.
3. Press **LAUNCH FPV** in the bottom panel.
4. Fly with standard aircraft controls while the link is active.

## Building from Source

```powershell
# Rebuild embedded asset bundle (requires Unity 2022.3.62f3)
.\scripts\bake-fpv-bundle.ps1

# Or build DLL only (uses existing bundle in FPVMod/Resources/)
dotnet build FPVMod\FPVMod.csproj -c Release
```

Output: `FPVMod/bin/Release/net48/FPVMod.dll`

## Architecture

Drones use the **Missile + Aircraft Hybrid (MAH)** pattern: vanilla `bomb_125_1` / `Truck2-MLRS` prefabs are spawned, then stamped at runtime with FPV components and custom FBX visuals.

## License

MIT — see repository license file.
