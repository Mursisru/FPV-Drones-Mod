using FPVMod.FpvView;
using FPVMod.Drone;
using FPVMod.Input;
using FPVMod.Launcher;
using FPVMod.Link;
using UnityEngine;

namespace FPVMod.Session
{
    internal static class FpvControlSession
    {
        private const float EndDelaySec = 0.8f;

        internal static bool Active { get; private set; }
        internal static Missile? Drone { get; private set; }
        internal static FpvLauncher? Launcher { get; private set; }
        internal static Aircraft? HeldAircraft { get; private set; }

        private static float _endAfterUnscaled = -1f;

        internal static bool IsControlling(Missile m) =>
            Active && Drone != null && Drone == m;

        internal static void Begin(Missile drone, FpvLauncher launcher)
        {
            if (drone == null || launcher == null)
                return;

            End();

            Drone = drone;
            Launcher = launcher;
            Active = true;
            _endAfterUnscaled = -1f;
            FpvInputBridge.ResetSession();
            EnsureFlightControls();

            FpvDroneTag? tag = drone.GetComponent<FpvDroneTag>();
            if (tag != null)
                tag.SourceLauncher = launcher;

            GameManager.GetLocalAircraft(out Aircraft? ac);
            HeldAircraft = ac;
            if (HeldAircraft != null)
                FpvJetAutopilotHold.Enable(HeldAircraft);

            FpvCameraRig.Attach(drone);
            FpvOsdCanvas.Show();
            EnsureFlightControls();

            FpvAcroController? acro = drone.GetComponent<FpvAcroController>();
            acro?.BoostLaunch(0.4f);

            FpvPlugin.ModLogger?.LogInfo(
                $"FPV session Begin drone={drone.persistentID} localSim={drone.LocalSim} fc={GameManager.flightControlsEnabled}");
        }

        internal static void End()
        {
            if (!Active && _endAfterUnscaled < 0f)
                return;

            _endAfterUnscaled = -1f;
            FpvJetAutopilotHold.Disable();
            FpvCameraRig.Detach();
            FpvOsdCanvas.Hide();
            FpvInputBridge.Freeze();
            FpvLinkQuality.Reset();

            Drone = null;
            Launcher = null;
            HeldAircraft = null;
            Active = false;
        }

        internal static void Tick()
        {
            if (!Active || Drone == null)
                return;

            if (_endAfterUnscaled >= 0f)
            {
                if (Time.unscaledTime >= _endAfterUnscaled)
                    End();
                return;
            }

            if (Drone.disabled)
            {
                _endAfterUnscaled = Time.unscaledTime + EndDelaySec;
                return;
            }

            EnsureFlightControls();

            FpvOsdCanvas.TickPauseUi();
            FpvFeedCamera.TickPauseUi();

            if (FpvUiGate.BlocksFlightInput)
            {
                FpvInputBridge.Freeze();
                return;
            }

            if (GameManager.playerInput != null)
                FpvInputBridge.Poll(GameManager.playerInput);

            FpvLinkLevel link = FpvLinkQuality.Evaluate(Drone, Launcher);
            FpvOsdCanvas.UpdateLink(link);

            if (link == FpvLinkLevel.Lost && FpvLinkQuality.LostTimeoutElapsed)
                End();

            if (Drone.GetComponent<FpvAcroController>() is { Battery01: var b } && b <= 0f)
                End();
        }

        /// <summary>MC FS reset after boom often leaves this false — next drone can't fly.</summary>
        internal static void EnsureFlightControls()
        {
            try
            {
                if (!GameManager.flightControlsEnabled)
                    GameManager.flightControlsEnabled = true;
            }
            catch
            {
                // ignore
            }
        }
    }
}
