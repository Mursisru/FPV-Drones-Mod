using FPVMod.Audio;
using FPVMod.Drone;
using FPVMod.FpvView;
using FPVMod.Input;
using FPVMod.Launcher;
using FPVMod.Link;
using UnityEngine;

namespace FPVMod.Session
{
    internal static class FpvControlSession
    {
        internal static bool Active { get; private set; }
        internal static Missile? Drone { get; private set; }
        internal static FpvLauncher? Launcher { get; private set; }
        internal static Aircraft? HeldAircraft { get; private set; }

        /// <summary>True while boom death-cam plays — sticks frozen, no stick fly.</summary>
        internal static bool BoomSpectating => FpvBoomSpectate.Active;

        internal static bool IsControlling(Missile m) =>
            Active && !FpvBoomSpectate.Active && Drone != null && Drone == m;

        internal static void Begin(Missile drone, FpvLauncher launcher)
        {
            if (drone == null || launcher == null)
                return;

            End();

            Drone = drone;
            Launcher = launcher;
            Active = true;
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
            // Listener after feed cam exists — world audio from drone, not jet/CSM.
            FpvListenerBridge.Enter(HeldAircraft);
            if (drone.GetComponent<FpvDroneMotorAudio>() == null)
                drone.gameObject.AddComponent<FpvDroneMotorAudio>();
            FpvOsdCanvas.Show();
            EnsureFlightControls();

            FpvAcroController? acro = drone.GetComponent<FpvAcroController>();
            acro?.BoostLaunch(0.4f);

            FpvPlugin.ModLogger?.LogInfo(
                $"FPV session Begin drone={drone.persistentID} localSim={drone.LocalSim} fc={GameManager.flightControlsEnabled}");
        }

        internal static void End()
        {
            if (!Active && !FpvBoomSpectate.Active)
                return;

            FpvBoomSpectate.Cancel();
            FpvJetAutopilotHold.Disable();
            FpvListenerBridge.Exit();
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
            if (FpvBoomSpectate.Active)
            {
                FpvBoomSpectate.Tick();
                return;
            }

            if (!Active || Drone == null)
                return;

            if (Drone.disabled)
            {
                Vector3 boom = Drone.rb != null ? Drone.rb.position : Drone.transform.position;
                FpvBoomSpectate.TryBegin(Drone, boom);
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
