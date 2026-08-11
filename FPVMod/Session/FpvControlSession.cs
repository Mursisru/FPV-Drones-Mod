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
        internal static bool Active { get; private set; }
        internal static Missile? Drone { get; private set; }
        internal static FpvLauncher? Launcher { get; private set; }
        internal static Aircraft? HeldAircraft { get; private set; }

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
            FpvInputBridge.ResetSession();

            FpvDroneTag? tag = drone.GetComponent<FpvDroneTag>();
            if (tag != null)
                tag.SourceLauncher = launcher;

            GameManager.GetLocalAircraft(out Aircraft? ac);
            HeldAircraft = ac;
            if (HeldAircraft != null)
                FpvJetAutopilotHold.Enable(HeldAircraft);

            FpvCameraRig.Attach(drone);
            FpvOsdCanvas.Show();

            FpvAcroController? acro = drone.GetComponent<FpvAcroController>();
            acro?.BoostLaunch(0.4f);
        }

        internal static void End()
        {
            if (!Active)
                return;

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

            if (Drone.disabled)
            {
                End();
                return;
            }

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
    }
}
