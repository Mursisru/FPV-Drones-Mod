namespace FPVMod
{
    /// <summary>Balance and tuning constants from design doc.</summary>
    internal static class FpvConstants
    {
        internal const string DroneDefKey = "fpv_drone";
        internal const string LauncherDefKey = "fpv_launcher_msv";
        internal const string ConvoyName = "FPV Strike Package";

        internal const float LauncherCost = 900_000f;
        internal const float DroneCost = 20_000f;

        internal const int LauncherCapacity = 8;
        internal const float LauncherCooldownSec = 6f;

        /// <summary>All-up mass (airframe + battery + 40 kg warhead).</summary>
        internal const float DroneMassKg = 50f;
        /// <summary>Warhead / blast yield mass (kg TNT-equivalent for game blast).</summary>
        internal const float WarheadMassKg = 40f;
        internal const float DroneSpanM = 1.5f;
        /// <summary>Max thrust/weight at zero airspeed (snappy climb). Lapses with speed.</summary>
        internal const float DroneMaxTwr = 20f;
        /// <summary>Parasite Cd — bleeds energy; with thrust lapse ≈270 km/h soft ceiling.</summary>
        internal const float DroneCd = 0.95f;
        /// <summary>Design cruise soft asymptote (km/h) — not a hard clamp.</summary>
        internal const float DesignMaxSpeedKmh = 270f;
        internal const float DesignMaxSpeedMs = DesignMaxSpeedKmh / 3.6f;
        /// <summary>Thrust→0 reference (slightly above design so level flight can settle ~270).</summary>
        internal const float ThrustLapseRefMs = 88f;

        internal const float BatterySeconds = 300f;
        /// <summary>OSD reference — physics uses DesignMaxSpeed* + lapse/drag.</summary>
        internal const float MaxSpeedKmh = DesignMaxSpeedKmh;

        internal const float BlastYieldKg = WarheadMassKg;
        internal const float PierceAp = 700f;
        /// <summary>Seconds after launch before impact fuse arms (must leave launcher ~12 m).</summary>
        internal const float ArmingDelaySec = 3f;

        internal const float RadarSize = 0.001f;
        internal const float DroneMaxRadius = 1.5f;
        /// <summary>Target world length (m) for FBX visual after stamp — compensates tiny bomb_125 parent scale.</summary>
        internal const float DroneVisualLengthM = 2.5f;
        /// <summary>Local euler on FPV_Visual root (Blender Z-up → Unity Y-up, no double bake).</summary>
        internal const float DroneVisualRotX = -90f;
        internal const float DroneVisualRotY = 0f;
        internal const float DroneVisualRotZ = 0f;
        /// <summary>FBX material name token — only this slot gets Texture_RPGB.</summary>
        internal const string AmmoMaterialToken = "RPGB";
        internal const float HitPoints = 250f;
        internal const float PierceArmor = 2f;
        internal const float PierceTolerance = 3f;

        internal const float LinkRangeM = 20_000f;
        internal const float OrbitRadiusM = 8_000f;

        internal const float CameraFov = 95f;
        internal const float CameraPitchDeg = 17f;

        internal const string BundleResourceName = "FPVMod.Resources.fpvmod_assets";

        /// <summary>Orange tint for launcher map pick icons (vanilla airbase sprite).</summary>
        internal static readonly UnityEngine.Color LauncherMapIconColor = new UnityEngine.Color(1f, 0.48f, 0.05f, 1f);

        // --- Battery / resupply radii (proximity refill; drive is vanilla) ---
        internal const float BatteryResupplyRadiusM = 45f;

        // --- Mission VehicleSupply inject (Escalation / Domination / etc.) ---
        /// <summary>Packages (launcher+SAM+ammo each) added to each faction HQ at mission seed.</summary>
        internal const int MissionSupplyPackages = 3;
        /// <summary>Soft replenish interval when supply emptied (long Escalation). Tick also retries seed every 5s.</summary>
        internal const float MissionResupplyIntervalSec = 180f;
        /// <summary>Do not replenish while this many FPV launchers are still alive for the HQ.</summary>
        internal const int MissionResupplyMaxAlive = 3;
    }
}
