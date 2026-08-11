using FPVMod.Drone;
using FPVMod.Link;
using FPVMod.Session;
using UnityEngine;

namespace FPVMod.FpvView
{
    internal static class FpvCameraRig
    {
        private static Camera? _cam;
        private static Transform? _mount;
        private static float _savedFov;
        private static Transform? _savedParent;
        private static Vector3 _savedLocalPos;
        private static Quaternion _savedLocalRot;

        internal static void Attach(Missile drone)
        {
            if (drone == null)
                return;

            _cam = Camera.main;
            if (_cam == null)
                return;

            _mount = FindMount(drone.transform);
            _savedFov = _cam.fieldOfView;
            _savedParent = _cam.transform.parent;
            _savedLocalPos = _cam.transform.localPosition;
            _savedLocalRot = _cam.transform.localRotation;

            _cam.transform.SetParent(_mount, false);
            _cam.transform.localPosition = Vector3.zero;
            _cam.transform.localRotation = Quaternion.identity;
            _cam.fieldOfView = FpvConstants.CameraFov;

            FpvPostProcess.Enable(_cam);
            FlightHud.EnableCanvas(false);
        }

        internal static void Detach()
        {
            if (_cam == null)
                return;

            FpvPostProcess.Disable();
            _cam.fieldOfView = _savedFov;
            if (_savedParent != null)
            {
                _cam.transform.SetParent(_savedParent, false);
                _cam.transform.localPosition = _savedLocalPos;
                _cam.transform.localRotation = _savedLocalRot;
            }

            if (SceneSingleton<CameraStateManager>.i != null)
                SceneSingleton<CameraStateManager>.i.SetFollowingUnit(null);

            FlightHud.EnableCanvas(SceneSingleton<CameraStateManager>.i?.currentState ==
                                   SceneSingleton<CameraStateManager>.i?.cockpitState);

            _cam = null;
            _mount = null;
        }

        private static Transform FindMount(Transform root)
        {
            Transform? m = root.Find("FPV_Visual/CameraMount");
            if (m == null)
                m = root.Find("CameraMount");
            return m != null ? m : root;
        }
    }
}
