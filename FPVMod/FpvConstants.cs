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
        internal const float DroneMassKg = 50f;

        internal const float BatterySeconds = 300f;
        internal const float MaxSpeedKmh = 250f;
        internal const float MaxSpeedMs = MaxSpeedKmh / 3.6f;

        internal const float BlastYieldKg = 25f;
        internal const float PierceAp = 700f;
        internal const float ArmingDelaySec = 2f;

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

        internal const float StabilizerStrength = 0.18f;
        internal const float AcroTorque = 45f;
        internal const float AcroThrust = 120f;

        internal const string BundleResourceName = "FPVMod.Resources.fpvmod_assets";
    }
}
