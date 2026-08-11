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

            FpvDroneTag? tag = drone.GetComponent<FpvDroneTag>();
            if (tag != null)
                tag.SourceLauncher = launcher;

            GameManager.GetLocalAircraft(out Aircraft? ac);
            HeldAircraft = ac;
            if (HeldAircraft != null)
                FpvJetAutopilotHold.Enable(HeldAircraft);

            FpvCameraRig.Attach(drone);
            FpvOsdCanvas.Show();
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

            if (GameManager.playerInput != null)
                FpvInputBridge.Poll(GameManager.playerInput);

            FpvLinkLevel link = FpvLinkQuality.Evaluate(Drone, Launcher);
            FpvInputBridge.LagBlend = FpvLinkQuality.InputBlend;
            FpvOsdCanvas.UpdateLink(link);

            if (link == FpvLinkLevel.Lost)
            {
                FpvInputBridge.Freeze();
                if (FpvLinkQuality.LostTimeoutElapsed)
                    End();
            }

            if (Drone.GetComponent<FpvAcroController>() is { Battery01: var b } && b <= 0f)
                End();
        }
    }
}
