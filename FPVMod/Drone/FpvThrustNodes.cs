using UnityEngine;

namespace FPVMod.Drone
{
    /// <summary>
    /// Thrust through CoM, 45° tilt. Prop-style thrust lapse vs airspeed (no hard V cap).
    /// </summary>
    internal sealed class FpvThrustNodes : MonoBehaviour
    {
        private const float AftTiltDeg = 45f;

        private Rigidbody? _rb;

        internal void Ensure(Rigidbody rb) => _rb = rb;

        /// <summary>
        /// Newtons along thrust axis. lapse01 from airspeed curve (1 at hover, →0 near design V).
        /// </summary>
        internal void ApplyThrustNewtons(float throttle01, float maxThrustN, float airspeedLapse01)
        {
            if (_rb == null)
                return;

            float t = Mathf.Clamp01(throttle01) * Mathf.Clamp01(airspeedLapse01);
            if (t <= 0.001f || maxThrustN <= 0f)
                return;

            Vector3 dir = Quaternion.AngleAxis(AftTiltDeg, transform.right) * transform.up;
            if (dir.sqrMagnitude < 1e-6f)
                dir = transform.up;
            else
                dir.Normalize();

            _rb.AddForceAtPosition(dir * (maxThrustN * t), _rb.worldCenterOfMass, ForceMode.Force);
        }

        /// <summary>1 at v=0, 0 at ThrustLapseRef — soft ~270 km/h ceiling with drag.</summary>
        internal static float AirspeedLapse(float speedMs)
        {
            float vRef = FpvConstants.ThrustLapseRefMs;
            if (vRef < 1f)
                return 1f;
            float x = speedMs / vRef;
            return Mathf.Clamp01(1f - x * x);
        }
    }
}
