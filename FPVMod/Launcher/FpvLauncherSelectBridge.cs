using FPVMod.Access;
using FPVMod.Network;
using NuclearOption.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FPVMod.Launcher
{
    /// <summary>Vanilla selectAirbasePanel + Select Aircraft → FPV launch.</summary>
    internal static class FpvLauncherSelectBridge
    {
        internal static FpvLauncher? PendingLauncher { get; set; }
        /// <summary>Survives panel hide / map minimize until spawn completes.</summary>
        internal static FpvLauncher? LaunchTarget { get; set; }
        internal static FpvLauncherMapIcon? SelectedIcon { get; private set; }

        private static Button? _wiredBtn;
        private static bool _launchInFlight;

        internal static void Select(FpvLauncher launcher, FpvLauncherMapIcon? icon)
        {
            if (launcher == null)
                return;

            PendingLauncher = launcher;
            LaunchTarget = launcher;
            SelectedIcon = icon;
            _launchInFlight = false;
            ApplyPanel();
        }

        internal static void RefreshVanillaPanel(GameplayUI? ui = null)
        {
            if (PendingLauncher == null && LaunchTarget == null)
                return;
            ApplyPanel(ui);
        }

        internal static void Clear()
        {
            PendingLauncher = null;
            LaunchTarget = null;
            SelectedIcon?.DeselectIcon();
            SelectedIcon = null;
            _launchInFlight = false;
        }

        /// <summary>Drop UI selection only — keep LaunchTarget for in-flight RPC.</summary>
        internal static void ClearSelectionOnly()
        {
            PendingLauncher = null;
            SelectedIcon?.DeselectIcon();
            SelectedIcon = null;
        }

        internal static void AfterLaunch()
        {
            _launchInFlight = false;
            Clear();
            try
            {
                SceneSingleton<DynamicMap>.i?.Minimize();
            }
            catch
            {
                // ignore
            }
            try
            {
                SceneSingleton<GameplayUI>.i?.HideSelectAirbase();
            }
            catch
            {
                // ignore
            }
        }

        /// <summary>Called from Harmony Prefix and direct Button listener.</summary>
        internal static bool TryHandleSelectAircraft()
        {
            FpvLauncher? launcher = PendingLauncher ?? LaunchTarget;
            if (launcher == null)
                return false;

            if (_launchInFlight)
                return true;

            if (!launcher.CanLaunch())
            {
                FpvPlugin.ModLogger?.LogWarning("FPV SelectAircraft: launcher not ready.");
                return true;
            }

            _launchInFlight = true;
            LaunchTarget = launcher;
            PendingLauncher = launcher;
            FpvPlugin.ModLogger?.LogInfo("FPV SelectAircraft: requesting launch.");
            try
            {
                FpvSpawnRpc.RequestLaunch(launcher);
            }
            catch (System.Exception ex)
            {
                _launchInFlight = false;
                FpvPlugin.ModLogger?.LogError($"FPV SelectAircraft: {ex}");
            }
            return true;
        }

        internal static void ResetLaunchGate() => _launchInFlight = false;

        private static void ApplyPanel(GameplayUI? ui = null)
        {
            ui ??= SceneSingleton<GameplayUI>.i;
            FpvLauncher? launcher = PendingLauncher ?? LaunchTarget;
            if (ui == null || launcher == null)
                return;

            GameObject? panel = FpvReflection.GetField<GameObject>(ui, "selectAirbasePanel");
            TMP_Text? label = FpvReflection.GetField<TMP_Text>(ui, "airbaseName");
            Button? selectBtn = FpvReflection.GetField<Button>(ui, "selectAircraftButton");
            if (panel == null || label == null || selectBtn == null)
            {
                FpvPlugin.ModLogger?.LogWarning("FPV SelectBridge: selectAirbasePanel fields missing.");
                return;
            }

            WireButton(selectBtn);

            panel.SetActive(true);
            Unit? unit = launcher.OwnerUnit;
            label.text = unit != null ? unit.unitName : "MSV Drone Launcher";
            bool showBtn = CanShowSelectAircraft(launcher);
            selectBtn.gameObject.SetActive(showBtn);
            selectBtn.interactable = showBtn;
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel.GetComponent<RectTransform>());
        }

        private static void WireButton(Button selectBtn)
        {
            if (_wiredBtn == selectBtn)
                return;
            if (_wiredBtn != null)
                _wiredBtn.onClick.RemoveListener(OnSelectAircraftButton);
            _wiredBtn = selectBtn;
            selectBtn.onClick.AddListener(OnSelectAircraftButton);
        }

        private static void OnSelectAircraftButton()
        {
            // Backup if UnityEvent order / Harmony miss — no-op when not FPV selection.
            TryHandleSelectAircraft();
        }

        private static bool CanShowSelectAircraft(FpvLauncher launcher)
        {
            if (GameManager.gameResolution == GameResolution.Defeat)
                return false;
            if (!GameManager.GetLocalPlayer<Player>(out Player? player) || player == null)
                return false;
            if (player.HQ == null)
                return false;

            // Allow FPV even if a wreck/disabled aircraft ref is still on HUD.
            Aircraft? ac = SceneSingleton<CombatHUD>.i != null ? SceneSingleton<CombatHUD>.i.aircraft : null;
            if (ac != null && !ac.disabled)
                return false;

            Unit? unit = launcher.OwnerUnit;
            if (unit == null || unit.disabled)
                return false;
            if (unit.NetworkHQ != player.HQ)
                return false;
            return launcher.CanLaunch();
        }
    }
}
