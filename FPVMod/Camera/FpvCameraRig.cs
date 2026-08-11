using UnityEngine;

namespace FPVMod.FpvView
{
    /// <summary>
    /// Prefer MissileCamera FS with BodyLockAcro (no horizon/turn-look limits).
    /// Fallback: local body-locked feed.
    /// </summary>
    internal static class FpvCameraRig
    {
        private static bool _usingMc;
        private static bool _usingLocal;

        internal static void Attach(Missile drone)
        {
            if (drone == null)
                return;

            Detach();

            _usingMc = FpvMissileCameraBridge.TryAttach(drone);
            if (_usingMc)
                return;

            FpvFeedCamera.Attach(drone);
            _usingLocal = true;
        }

        internal static void LateTick()
        {
            if (_usingMc)
                FpvMissileCameraBridge.TickKeepAlive(Session.FpvControlSession.Drone);
            if (_usingLocal)
                FpvFeedCamera.LateTick();
        }

        internal static void Detach()
        {
            if (_usingMc)
            {
                FpvMissileCameraBridge.Detach();
                _usingMc = false;
            }
            if (_usingLocal)
            {
                FpvFeedCamera.Detach();
                _usingLocal = false;
            }
        }
    }
}
