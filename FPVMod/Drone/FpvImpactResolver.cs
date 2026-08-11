using FPVMod.Launcher;
using UnityEngine;

namespace FPVMod.Drone
{
    /// <summary>
    /// Impact vs terrain + units. When armed uses vanilla tangible mask (~ExclusionZones = Default/units).
    /// </summary>
    internal static class FpvImpactResolver
    {
        private const float MinCastM = 0.7f;
        private const float CastMul = 2.8f;
        private const float RadiusFrac = 0.4f;
        private static readonly Collider[] OverlapBuf = new Collider[32];
        private static readonly RaycastHit[] CastBuf = new RaycastHit[16];

        internal static void Resolve(Missile missile, Rigidbody rb, bool armed)
        {
            if (missile == null || rb == null || missile.disabled)
                return;

            if (armed)
            {
                try { missile.SetTangible(true); } catch { /* ignore */ }
            }

            float radius = Mathf.Clamp(missile.maxRadius * RadiusFrac, 0.35f, 1.1f);
            Vector3 pos = rb.position;
            Vector3 vel = rb.velocity;
            float speed = vel.magnitude;

            Vector3 dir = speed > 0.35f ? vel.normalized : Vector3.down;
            float castLen = Mathf.Max(MinCastM, speed * Time.fixedDeltaTime * CastMul);
            if (speed <= 0.35f)
                castLen = Mathf.Max(castLen, 1.5f);

            int mask = armed
                ? ~PhysicsLayers.ExclusionZonesMask.value
                : (PhysicsLayers.StaticsMask.value | PhysicsLayers.ShipsMask.value);

            if (TryHit(pos, dir, castLen, radius, mask, out RaycastHit hit))
            {
                HandleHit(missile, rb, armed, hit);
                return;
            }

            if (speed > 1f && TryHit(pos, -dir, radius * 0.5f, radius * 0.6f, mask, out hit))
            {
                HandleHit(missile, rb, armed, hit);
                return;
            }

            int n = Physics.OverlapSphereNonAlloc(pos, radius, OverlapBuf, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider c = OverlapBuf[i];
                if (c == null || ShouldIgnoreCollider(missile, c))
                    continue;

                Vector3 closest = c.ClosestPoint(pos);
                Vector3 nrm = (pos - closest).sqrMagnitude > 1e-6f
                    ? (pos - closest).normalized
                    : Vector3.up;

                if (Physics.Linecast(pos + nrm * 0.05f, closest - nrm * 0.02f, out hit, mask))
                {
                    HandleHit(missile, rb, armed, hit);
                    return;
                }

                if (!armed)
                {
                    SoftSeparate(rb, nrm);
                    return;
                }

                DetonateAt(missile, closest, nrm, IsTerrain(c), IsUnitOrArmor(c));
                return;
            }
        }

        private static bool TryHit(Vector3 pos, Vector3 dir, float len, float radius, int mask, out RaycastHit hit)
        {
            int count = Physics.SphereCastNonAlloc(
                pos, radius, dir, CastBuf, len, mask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            int bestI = -1;
            for (int i = 0; i < count; i++)
            {
                if (CastBuf[i].collider == null)
                    continue;
                if (CastBuf[i].distance < best)
                {
                    best = CastBuf[i].distance;
                    bestI = i;
                }
            }
            if (bestI >= 0)
            {
                hit = CastBuf[bestI];
                return true;
            }

            return Physics.Linecast(pos, pos + dir * len, out hit, mask);
        }

        private static void HandleHit(Missile missile, Rigidbody rb, bool armed, RaycastHit hit)
        {
            Collider? c = hit.collider;
            if (c == null || ShouldIgnoreCollider(missile, c))
                return;

            if (!armed)
            {
                SoftSeparate(rb, hit.normal);
                return;
            }

            bool terrain = IsTerrain(c);
            bool armor = !terrain && (IsUnitOrArmor(c) || hit.point.y >= Datum.LocalSeaY);
            DetonateAt(missile, hit.point, hit.normal, terrain, armor);
        }

        private static bool ShouldIgnoreCollider(Missile missile, Collider c)
        {
            if (c.transform.IsChildOf(missile.transform))
                return true;

            Unit? hitUnit = c.GetComponentInParent<Unit>();
            if (hitUnit != null && missile.owner != null && hitUnit == missile.owner)
                return true;

            FpvDroneTag? tag = missile.GetComponent<FpvDroneTag>();
            if (tag?.SourceLauncher != null && hitUnit != null &&
                tag.SourceLauncher.OwnerUnit == hitUnit)
                return true;

            if (c.GetComponentInParent<FpvLauncher>() != null &&
                missile.GetComponent<FpvWarhead>() is { IsSafe: true })
                return true;

            return false;
        }

        private static void DetonateAt(Missile missile, Vector3 point, Vector3 normal, bool terrain, bool armor)
        {
            try
            {
                if (normal.sqrMagnitude < 1e-6f)
                    normal = Vector3.up;
                else
                    normal.Normalize();

                try { missile.SetTarget(null); } catch { /* ignore */ }
                FpvBoomPending.Set(missile, point);
                missile.transform.position = point;
                if (missile.rb != null)
                {
                    missile.rb.velocity = Vector3.zero;
                    missile.rb.angularVelocity = Vector3.zero;
                    missile.rb.position = point;
                    missile.rb.MovePosition(point);
                }

                try
                {
                    var f = typeof(Missile).GetField(
                        "impactFuse",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    f?.SetValue(missile, true);
                    if (!missile.IsArmed())
                        missile.Arm();
                }
                catch
                {
                    // ignore
                }

                missile.Detonate(normal, armor, terrain);
            }
            catch (System.Exception ex)
            {
                FpvPlugin.ModLogger?.LogWarning($"FpvImpact: Detonate failed: {ex.Message}");
            }
        }

        private static void SoftSeparate(Rigidbody rb, Vector3 normal)
        {
            float into = Vector3.Dot(rb.velocity, -normal);
            if (into > 0f)
                rb.velocity += normal * into;
            rb.position += normal * 0.08f;
            rb.MovePosition(rb.position);
        }

        private static bool IsTerrain(Collider? c)
        {
            if (c == null || GameAssets.i == null)
                return false;
            try
            {
                return c.sharedMaterial == GameAssets.i.terrainMaterial;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsUnitOrArmor(Collider c) =>
            c != null && (c.GetComponentInParent<Unit>() != null || c.GetComponentInParent<IDamageable>() != null);
    }
}
