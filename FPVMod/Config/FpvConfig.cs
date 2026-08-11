using BepInEx.Configuration;

namespace FPVMod
{
    internal static class FpvConfig
    {
        internal static ConfigEntry<bool> Enabled { get; private set; } = null!;
        internal static ConfigEntry<bool> RequireModInLobby { get; private set; } = null!;

        internal static ConfigEntry<bool> LookAroundEnabled { get; private set; } = null!;
        internal static ConfigEntry<float> LookAroundMaxDeg { get; private set; } = null!;
        internal static ConfigEntry<float> ZoomMax { get; private set; } = null!;
        internal static ConfigEntry<bool> ZoomResetOnExit { get; private set; } = null!;

        // MC parity: Scanlines = GunshipTvOverlay UI only (PostFx Scanlines stage hard-off).
        internal static ConfigEntry<bool> FxScanlinesEnabled { get; private set; } = null!;
        internal static ConfigEntry<bool> FxChromaticEnabled { get; private set; } = null!;
        internal static ConfigEntry<bool> FxBloomEnabled { get; private set; } = null!;
        internal static ConfigEntry<bool> FxMotionBlurEnabled { get; private set; } = null!;

        internal static void Bind(ConfigFile config)
        {
            Enabled = config.Bind("General", "Enabled", true, "Enable FPV Drones mod features.");
            RequireModInLobby = config.Bind("Network", "RequireModInLobby", true, "Reject clients without FPVMod installed.");

            LookAroundEnabled = config.Bind("Fullscreen", "LookAroundEnabled", true,
                "Hold RMB to free-look the feed camera (±LookAroundMaxDeg from bore).");
            LookAroundMaxDeg = config.Bind("Fullscreen", "LookAroundMaxDeg", 70f,
                "Max free-look cone in degrees.");
            ZoomMax = config.Bind("Fullscreen", "ZoomMax", 50f,
                "Max optical zoom magnification (mouse wheel).");
            ZoomResetOnExit = config.Bind("Fullscreen", "ZoomResetOnExit", true,
                "Reset zoom when leaving FPV session.");

            FxScanlinesEnabled = config.Bind("Effects", "TvScanlines", true,
                "FS CRT scanlines/grain/vignette UI overlay (MC GunshipTvOverlay). Not a full-screen RT blit.");
            FxChromaticEnabled = config.Bind("Effects", "PostFxChromatic", false,
                "Chromatic aberration post-process blit (MC default off).");
            FxBloomEnabled = config.Bind("Effects", "PostFxBloom", false,
                "Soft bloom post-process blit (MC default off).");
            FxMotionBlurEnabled = config.Bind("Effects", "PostFxMotionBlur", false,
                "Motion blur post-process blit (MC default off).");
        }
    }
}
