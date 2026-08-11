using FPVMod.FpvView;
using FPVMod.Input;
using UnityEngine;

namespace FPVMod.Session
{
    /// <summary>
    /// After detonation: side/overhead shot of the boom, same vision, no UI, shake, input locked —
    /// then session End.
    /// </summary>
    internal static class FpvBoomSpectate
    {
        private const float DurationSec = 3.6f;

        private static bool _active;
        private static float _endAtUnscaled = -1f;

        internal static bool Active => _active;

        internal static void TryBegin(Missile? drone, Vector3 boomWorld)
        {
            if (_active)
                return;
            if (!FpvControlSession.Active)
                return;
            if (drone != null && FpvControlSession.Drone != null && drone != FpvControlSession.Drone)
                return;

            if (boomWorld.sqrMagnitude < 1e-4f && drone != null)
            {
                boomWorld = drone.rb != null ? drone.rb.position : drone.transform.position;
            }

            _active = true;
            _endAtUnscaled = Time.unscaledTime + DurationSec;
            FpvInputBridge.Freeze();
            FpvInputBridge.ResetSession();
            FpvFeedCamera.BeginBoomSpectate(boomWorld);
            FpvPlugin.ModLogger?.LogInfo($"FPV boom spectate begin world={boomWorld}");
        }

        internal static void Tick()
        {
            if (!_active)
                return;

            FpvInputBridge.Freeze();
            FpvFeedCamera.TickBoomSpectate();

            if (Time.unscaledTime >= _endAtUnscaled)
                FpvControlSession.End();
        }

        internal static void Cancel()
        {
            if (!_active)
                return;
            _active = false;
            _endAtUnscaled = -1f;
            FpvFeedCamera.EndBoomSpectate();
        }
    }
}
