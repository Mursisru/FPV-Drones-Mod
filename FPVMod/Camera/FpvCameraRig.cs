using UnityEngine;

namespace FPVMod.FpvView
{
    /// <summary>Always owned FS feed (body-lock). MissileCamera is not required.</summary>
    internal static class FpvCameraRig
    {
        private static bool _active;

        internal static void Attach(Missile drone)
        {
            if (drone == null)
                return;

            Detach();
            FpvFeedCamera.Attach(drone);
            _active = true;
        }

        internal static void LateTick()
        {
            if (_active)
                FpvFeedCamera.LateTick();
        }

        internal static void Detach()
        {
            if (!_active)
                return;
            FpvFeedCamera.Detach();
            _active = false;
        }
    }
}
