using UnityEngine;

namespace FPVMod.FpvView
{
    /// <summary>
    /// Boom death-cam: ~500 m above blast, either helicopter or phone handheld style
    /// (natural shake + living zoom). Vision/UI handled by FpvFeedCamera.
    /// </summary>
    internal static class FpvBoomCam
    {
        private const float HeightM = 500f;
        private const float SideM = 120f;
        private const float BackM = 80f;

        private enum Style : byte
        {
            Helicopter = 0,
            Phone = 1
        }

        private static bool _active;
        private static Style _style;
        private static Vector3 _lookAt;
        private static Vector3 _anchor;
        private static Vector3 _right;
        private static Vector3 _back;
        private static float _startUnscaled;
        private static float _baseFov;
        private static float _seed;

        internal static bool Active => _active;

        internal static void Begin(Transform rig, Camera cam, Vector3 boomWorld, Vector3 approachFwd)
        {
            if (rig == null || cam == null)
                return;

            _active = true;
            _style = (Style)(Random.Range(0, 2));
            _lookAt = boomWorld;
            _startUnscaled = Time.unscaledTime;
            _seed = Random.Range(0f, 100f);
            _baseFov = Mathf.Clamp(cam.fieldOfView, 25f, 90f);

            Vector3 fwd = approachFwd.sqrMagnitude > 0.01f
                ? approachFwd.normalized
                : Vector3.forward;
            _right = Vector3.Cross(Vector3.up, fwd);
            if (_right.sqrMagnitude < 0.01f)
                _right = Vector3.right;
            else
                _right.Normalize();
            _back = -fwd;

            // Slightly off-nadir so the blast reads in 3D, still ~500 m AGL.
            _anchor = boomWorld
                + Vector3.up * HeightM
                + _right * SideM
                + _back * BackM;

            rig.position = _anchor;
            Vector3 to = _lookAt - _anchor;
            if (to.sqrMagnitude > 0.01f)
                rig.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);

            FpvPlugin.ModLogger?.LogInfo($"FPV boom cam style={_style} h={HeightM}m");
        }

        /// <summary>Apply world pose + FOV for current style. Returns FOV to set on feed cam.</summary>
        internal static float Tick(Transform rig, Camera cam)
        {
            if (!_active || rig == null || cam == null)
                return cam != null ? cam.fieldOfView : 60f;

            float t = Time.unscaledTime - _startUnscaled;
            return _style == Style.Helicopter
                ? TickHelicopter(rig, t)
                : TickPhone(rig, t);
        }

        internal static void End()
        {
            _active = false;
        }

        // Slow orbit drift + soft rotor shake + gentle zoom breathe.
        private static float TickHelicopter(Transform rig, float t)
        {
            float orbit = Mathf.Sin(t * 0.35f + _seed) * 18f;
            float driftY = Mathf.Sin(t * 0.22f + _seed * 0.3f) * 6f;
            Vector3 drift = _right * orbit + Vector3.up * driftY + _back * (Mathf.Cos(t * 0.28f) * 10f);

            // Rotor: low amp, mid frequency.
            float n = t * 6.5f + _seed;
            Vector3 shake = new Vector3(
                (Mathf.PerlinNoise(n, _seed) - 0.5f) * 2f,
                (Mathf.PerlinNoise(_seed + 1f, n) - 0.5f) * 2f,
                (Mathf.PerlinNoise(n, n * 0.4f) - 0.5f) * 2f) * 0.55f;

            // Soft boom kick that fades.
            float kick = Mathf.Exp(-t * 1.8f) * 2.2f;
            shake += new Vector3(
                Mathf.Sin(t * 28f + _seed) * kick * 0.35f,
                Mathf.Sin(t * 31f) * kick,
                Mathf.Cos(t * 25f + _seed) * kick * 0.25f);

            Vector3 pos = _anchor + drift + shake;
            rig.position = pos;

            float roll = Mathf.Sin(t * 0.4f + _seed) * 1.8f;
            Vector3 to = _lookAt - pos;
            if (to.sqrMagnitude > 0.01f)
            {
                Quaternion look = Quaternion.LookRotation(to.normalized, Vector3.up);
                rig.rotation = look * Quaternion.Euler(0f, 0f, roll);
            }

            // Zoom: slow optical zoom-in after blast, tiny breathe.
            float zoomIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 2.4f));
            float breathe = Mathf.Sin(t * 0.9f + _seed) * 1.2f;
            float fov = Mathf.Lerp(_baseFov, _baseFov * 0.72f, zoomIn) + breathe;
            return Mathf.Clamp(fov, 18f, 95f);
        }

        // Handheld phone: jittery shake, pinch zoom pulses, micro roll.
        private static float TickPhone(Transform rig, float t)
        {
            float sway = Mathf.Sin(t * 1.1f + _seed) * 8f;
            Vector3 drift = _right * sway + _back * (Mathf.Sin(t * 0.85f) * 5f);

            float n = t * 14f + _seed;
            Vector3 shake = new Vector3(
                (Mathf.PerlinNoise(n, _seed * 0.2f) - 0.5f) * 2f,
                (Mathf.PerlinNoise(_seed, n) - 0.5f) * 2f,
                (Mathf.PerlinNoise(n * 1.3f, n) - 0.5f) * 2f) * 1.15f;

            // Occasional micro-jerk (thumb / step).
            float jerkGate = Mathf.PerlinNoise(t * 0.7f + _seed, 9.1f);
            if (jerkGate > 0.78f)
            {
                float j = (jerkGate - 0.78f) * 8f;
                shake += new Vector3(
                    Mathf.Sin(t * 40f) * j,
                    Mathf.Cos(t * 37f) * j * 0.6f,
                    Mathf.Sin(t * 33f + 1f) * j * 0.4f);
            }

            float kick = Mathf.Exp(-t * 2.2f) * 3f;
            shake += Vector3.up * Mathf.Sin(t * 35f) * kick * 0.4f;

            Vector3 pos = _anchor + drift + shake;
            rig.position = pos;

            float roll = Mathf.Sin(t * 2.2f + _seed) * 3.5f
                + (Mathf.PerlinNoise(t * 3f, _seed) - 0.5f) * 2f;
            Vector3 to = _lookAt - pos;
            if (to.sqrMagnitude > 0.01f)
            {
                Quaternion look = Quaternion.LookRotation(to.normalized, Vector3.up);
                float pitchWobble = (Mathf.PerlinNoise(_seed, t * 2.5f) - 0.5f) * 2.5f;
                rig.rotation = look * Quaternion.Euler(pitchWobble, 0f, roll);
            }

            // Pinch zoom: irregular FOV pulses + overall push-in.
            float push = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 1.8f));
            float pinch = Mathf.Sin(t * 2.4f + _seed) * 4.5f
                + Mathf.Sin(t * 5.1f + _seed * 2f) * 2f;
            float fov = Mathf.Lerp(_baseFov * 1.05f, _baseFov * 0.55f, push) + pinch;
            return Mathf.Clamp(fov, 16f, 100f);
        }
    }
}
