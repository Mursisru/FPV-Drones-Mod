using FPVMod.Link;
using UnityEngine;

namespace FPVMod.FpvView
{
    /// <summary>Session glue for link static on owned FS. Labels live in FpvFlirHud.</summary>
    internal static class FpvOsdCanvas
    {
        internal static void Show()
        {
            // FS chrome is created by FpvFeedCamera.
        }

        internal static void Hide()
        {
            FpvFeedCamera.SetLinkStatic(0f);
            FpvPostProcess.SetNoise(0f);
        }

        internal static void TickPauseUi() => FpvFeedCamera.TickPauseUi();

        internal static void UpdateLink(FpvLinkLevel level)
        {
            float staticA = level == FpvLinkLevel.Lost ? 0.65f : level == FpvLinkLevel.Degraded ? 0.2f : 0f;
            FpvFeedCamera.SetLinkStatic(staticA);
            FpvPostProcess.SetNoise(level == FpvLinkLevel.Degraded ? 0.35f : level == FpvLinkLevel.Lost ? 0.8f : 0f);
        }

        internal static void RefreshTelemetry()
        {
            // Telemetry tick is FpvFeedCamera.LateTick → FpvFlirHud.
        }
    }

    internal sealed class FpvCameraRigMount : MonoBehaviour { }
}
