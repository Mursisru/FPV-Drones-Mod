using BepInEx.Logging;
using FPVMod.Bootstrap;
using FPVMod.FpvView;
using FPVMod.Economy;
using FPVMod.HarmonyPatches;
using FPVMod.Launcher;
using FPVMod.Network;
using FPVMod.Session;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FPVMod.Control
{
    internal sealed class FpvHost : MonoBehaviour
    {
        private static FpvHost? _instance;
        private ManualLogSource? _log;
        private Harmony? _harmony;
        private float _nextBootstrap;

        internal static void Ensure(ManualLogSource logger)
        {
            if (_instance != null)
                return;

            var go = new GameObject("FPVMod.Host");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            _instance = go.AddComponent<FpvHost>();
            _instance._log = logger;
            _instance.Bootstrap();
        }

        private void Bootstrap()
        {
            _harmony = new Harmony(FpvPlugin.PluginGuid);
            try
            {
                _harmony.PatchAll(typeof(FpvBootstrap).Assembly);
                _log?.LogInfo("FPVMod Harmony patched.");
            }
            catch (System.Exception ex)
            {
                _log?.LogError($"FPVMod Harmony PatchAll failed (retrying critical): {ex.Message}");
                try
                {
                    _harmony.CreateClassProcessor(typeof(EncyclopediaAfterLoadInstancePatch)).Patch();
                    _harmony.CreateClassProcessor(typeof(EncyclopediaAfterLoadStaticPatch)).Patch();
                    _harmony.CreateClassProcessor(typeof(SpawnerSpawnVehiclePatch)).Patch();
                    _harmony.CreateClassProcessor(typeof(SpawnerSpawnSavedMissilePatch)).Patch();
                    _harmony.CreateClassProcessor(typeof(SpawnerEditorPlacePatch)).Patch();
                    _harmony.CreateClassProcessor(typeof(NewUnitPanelSpawnUnitPatch)).Patch();
                    _log?.LogInfo("FPVMod critical Harmony patches applied.");
                }
                catch (System.Exception ex2)
                {
                    _log?.LogError($"FPVMod critical Harmony failed: {ex2}");
                }
            }

            // Boom: manual Detonate + RpcDetonate replace (forced pending world + BlastFrag).
            FpvBoomPatches.Ensure(_harmony);

            SceneManager.sceneUnloaded += _ => OnSceneReset();
            FpvNetworkHub.EnsureHandlers();
            FpvMissileCameraAcroPatches.TryPatch(_harmony!);
            FpvMissileCameraHudPatches.TryPatch(_harmony!);
        }

        private void Update()
        {
            if (!FpvConfig.Enabled.Value || !FpvLobbyGate.FeaturesAllowed)
                return;

            FpvNetworkHub.EnsureHandlers();
            FpvLobbyGate.Tick();
            if (_harmony != null)
            {
                FpvBoomPatches.Ensure(_harmony);
                FpvMissileCameraAcroPatches.TryPatch(_harmony);
                FpvMissileCameraHudPatches.TryPatch(_harmony);
            }

            if (Time.unscaledTime >= _nextBootstrap)
            {
                _nextBootstrap = Time.unscaledTime + 2f;
                TryBootstrapDefinitions();
                FpvLauncherMapIcons.SyncAll();
            }

            FpvControlSession.Tick();
            FpvInputRpc.Tick();
            FpvOsdCanvas.RefreshTelemetry();
            FpvCameraRig.LateTick();
            FpvResupplyBridge.Tick();

            if (DynamicMap.mapMaximized)
                FpvLauncherSelectBridge.RefreshVanillaPanel();
        }

        private void FixedUpdate()
        {
            if (FpvControlSession.Active)
                FpvJetAutopilotHold.FixedTick();
        }

        private static void TryBootstrapDefinitions()
        {
            Encyclopedia? enc = Encyclopedia.i;
            if (enc == null)
                return;
            DefinitionRegistrar.TryRegister(enc);
            FpvConvoyBootstrap.TryInject();
        }

        private static void OnSceneReset()
        {
            FpvControlSession.End();
            FpvLobbyGate.Reset();
            FpvPendingPlace.End();
            FpvEditorListRefresh.ResetFlag();
            FpvLauncherMapIcons.ClearAll();
            FpvLauncherSelectBridge.Clear();
            // Keep Encyclopedia Lookup defs + embedded bundle alive across scenes (RC pattern).
            DefinitionRegistrar.SoftReset();
        }
    }
}
