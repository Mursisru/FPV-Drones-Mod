using FPVMod.FpvView;
using FPVMod.Session;
using HarmonyLib;
using UnityEngine;

namespace FPVMod.HarmonyPatches
{
    /// <summary>
    /// Vanilla CloudLayer emits around CameraStateManager (jet), not the FPV feed.
    /// While FS is active, sample from the drone feed cam so clouds/fog exist in view.
    /// </summary>
    [HarmonyPatch(typeof(CloudLayer), "Update")]
    internal static class FpvCloudLayerUpdatePatch
    {
        private static bool _hijacked;
        private static Vector3 _savedPos;
        private static Quaternion _savedRot;
        private static Vector3 _savedVel;

        [HarmonyPrefix]
        private static void Prefix()
        {
            _hijacked = false;
            if (!FpvControlSession.Active)
                return;

            Camera? feed = FpvFeedCamera.FeedCamera;
            if (feed == null)
                return;

            CameraStateManager? csm = null;
            try { csm = SceneSingleton<CameraStateManager>.i; }
            catch { return; }
            if (csm == null)
                return;

            Transform t = csm.transform;
            _savedPos = t.position;
            _savedRot = t.rotation;
            _savedVel = csm.cameraVelocity;

            Transform feedT = feed.transform;
            t.SetPositionAndRotation(feedT.position, feedT.rotation);
            csm.cameraVelocity = FpvFeedCamera.FeedWorldVelocity;
            _hijacked = true;
        }

        [HarmonyPostfix]
        private static void Postfix()
        {
            if (!_hijacked)
                return;
            _hijacked = false;

            try
            {
                CameraStateManager? csm = SceneSingleton<CameraStateManager>.i;
                if (csm == null)
                    return;
                csm.transform.SetPositionAndRotation(_savedPos, _savedRot);
                csm.cameraVelocity = _savedVel;
            }
            catch { /* ignore */ }
        }
    }
}
