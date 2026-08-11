using UnityEngine;

namespace FPVMod.FpvView
{
    /// <summary>FS free-look: hold RMB to pan feed ±cone (MC FsLookAround port).</summary>
    internal static class FpvFsLookAround
    {
        private const float DegPerUnit = 1.1f;
        private const float Deadzone = 0.015f;
        private const float BoreProjectDist = 2000f;

        private static float _yawDeg;
        private static float _pitchDeg;

        internal static bool IsLooking => _yawDeg * _yawDeg + _pitchDeg * _pitchDeg > 0.25f;

        internal static Vector2 GetBorePanelOffset(Camera? cam, float panelW, float panelH)
        {
            if (!IsLooking || cam == null || panelW < 1f || panelH < 1f)
                return Vector2.zero;

            try
            {
                Transform ct = cam.transform;
                Transform? parent = ct.parent;
                if (parent == null)
                    return Vector2.zero;

                Quaternion lookLocal = BuildLookOffsetLocal();
                Quaternion boreLocal = ct.localRotation * Quaternion.Inverse(lookLocal);
                Vector3 boreFwdWorld = parent.rotation * (boreLocal * Vector3.forward);
                if (boreFwdWorld.sqrMagnitude < 1e-8f)
                    return Vector2.zero;

                Vector3 worldPt = ct.position + boreFwdWorld.normalized * BoreProjectDist;
                Vector3 vp = cam.WorldToViewportPoint(worldPt);
                if (vp.z <= 0.05f || float.IsNaN(vp.x) || float.IsNaN(vp.y))
                    return Vector2.zero;

                return new Vector2((vp.x - 0.5f) * panelW, (vp.y - 0.5f) * panelH);
            }
            catch
            {
                return Vector2.zero;
            }
        }

        internal static void Reset()
        {
            _yawDeg = 0f;
            _pitchDeg = 0f;
            FpvLookAroundHud.SetVisible(false);
        }

        internal static void Tick(bool sessionActive)
        {
            if (!sessionActive || !FpvConfig.LookAroundEnabled.Value || FpvUiGate.MenuOpen)
            {
                Reset();
                return;
            }

            if (!UnityEngine.Input.GetMouseButton(1))
            {
                if (IsLooking)
                    Reset();
                return;
            }

            float mx = UnityEngine.Input.GetAxisRaw("Mouse X");
            float my = UnityEngine.Input.GetAxisRaw("Mouse Y");
            if (mx * mx + my * my >= Deadzone * Deadzone)
            {
                _yawDeg += mx * DegPerUnit;
                _pitchDeg -= my * DegPerUnit;
                ClampCone();
            }

            FpvLookAroundHud.SetVisible(true);
        }

        internal static void ApplyToCamera(Camera? camera)
        {
            if (!IsLooking || camera == null)
                return;
            try
            {
                camera.transform.localRotation *= BuildLookOffsetLocal();
            }
            catch { /* ignore */ }
        }

        private static Quaternion BuildLookOffsetLocal() =>
            Quaternion.AngleAxis(_yawDeg, Vector3.up)
            * Quaternion.AngleAxis(_pitchDeg, Vector3.right);

        private static void ClampCone()
        {
            float max = Mathf.Clamp(FpvConfig.LookAroundMaxDeg.Value, 15f, 89f);
            Vector2 v = new Vector2(_yawDeg, _pitchDeg);
            float mag = v.magnitude;
            if (mag > max && mag > 1e-4f)
            {
                v *= max / mag;
                _yawDeg = v.x;
                _pitchDeg = v.y;
            }
        }
    }
}
