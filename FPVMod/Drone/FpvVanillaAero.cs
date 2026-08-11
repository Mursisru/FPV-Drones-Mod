using UnityEngine;

namespace FPVMod.Drone
{
    /// <summary>
    /// Quadratic drag via vanilla air-density curve. Rho clamped — game curve can be huge and "float" falls.
    /// </summary>
    internal static class FpvVanillaAero
    {
        private const float RhoMin = 0.4f;
        private const float RhoMax = 1.35f;

        internal static float SampleAirDensity(Vector3 worldPos)
        {
            float rho = 1.225f;
            try
            {
                if (GameAssets.i != null && GameAssets.i.airDensityAltitude != null)
                {
                    float altKm = worldPos.GlobalY() * 0.001f;
                    rho = GameAssets.i.airDensityAltitude.Evaluate(altKm);
                }
            }
            catch
            {
                // keep default
            }

            return Mathf.Clamp(rho, RhoMin, RhoMax);
        }

        internal static void ApplyDrag(Rigidbody rb, Transform body, float cd, float areaM2)
        {
            if (rb == null || body == null || areaM2 <= 0f)
                return;

            // No wind for FPV — GetWind updrafts looked like "slow fall" with residual lift.
            Vector3 airVel = rb.velocity;
            float speed = airVel.magnitude;
            if (speed < 0.08f)
                return;

            float rho = SampleAirDensity(body.position);
            float dragN = 0.5f * rho * speed * speed * cd * areaM2;
            rb.AddForce(-airVel / speed * dragN, ForceMode.Force);
        }
    }
}
