using BepInEx;
using BepInEx.Logging;
using FPVMod.Control;
using FPVMod.Effects;
using FPVMod.FpvView;

namespace FPVMod
{
    [BepInPlugin(PluginGuid, PluginName, AppVersion.BepInSemVer)]
    public sealed class FpvPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.mursisru.fpvmod";
        public const string PluginName = "FPV Drones Mod";

        internal static ManualLogSource? ModLogger { get; private set; }

        private void Awake()
        {
            ModLogger = Logger;
            FpvConfig.Bind(Config);
            FpvShaderBundle.EnsureLoaded();
            FpvFeedDriverHost.Ensure();
            FpvHost.Ensure(ModLogger);
            ModLogger.LogInfo(
                $"{PluginName} {AppVersion.DisplayVersion} loaded. IR blit={(FpvInfraredBlit.IsAvailable ? "ok" : "MISSING")}");
        }
    }
}
