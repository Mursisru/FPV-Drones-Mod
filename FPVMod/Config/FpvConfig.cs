using BepInEx.Configuration;

namespace FPVMod
{
    internal static class FpvConfig
    {
        internal static ConfigEntry<bool> Enabled { get; private set; } = null!;
        internal static ConfigEntry<bool> RequireModInLobby { get; private set; } = null!;

        internal static void Bind(ConfigFile config)
        {
            Enabled = config.Bind("General", "Enabled", true, "Enable FPV Drones mod features.");
            RequireModInLobby = config.Bind("Network", "RequireModInLobby", true, "Reject clients without FPVMod installed.");
        }
    }
}
