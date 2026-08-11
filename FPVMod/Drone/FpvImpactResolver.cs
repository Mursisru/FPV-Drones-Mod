using FPVMod.Launcher;
using UnityEngine;

namespace FPVMod.Drone
{
    /// <summary>
    /// Impact vs terrain + units. Sanitizes hit.point — Unity SphereCast-from-inside often returns (0,0,0)
    /// which is floating-origin center (= spectator), not the drone.
    /// Armed queries hit triggers (unit hitboxes) and skip self-colliders when picking cast hits.
    /// </summary>
    internal static class FpvImpactResolver
    {
        private const float MinCastM = 0.7f;
        private const float CastMul = 2.8f;
        private const float RadiusFrac = 0.4f;
        private const float ArmedOverlapMul = 1.35f;
        /// <summary>Reject hit.point farther than this from drone (m) — use ClosestPoint/rb instead.</summary>
        private const float MaxHitDriftM = 25f;
        private static readonly Collider[] OverlapBuf = new Collider[48];
        private static readonly RaycastHit[] CastBuf = new RaycastHit[24];

        internal static void Resolve(Missile missile, Rigidbody rb, bool armed)
        {
            if (missile == null || rb == null || missile.disabled)
                return;

            if (armed)
            {
                try { missile.SetTangible(true); } catch { /* ignore */ }
            }

            float radius = Mathf.Clamp(missile.maxRadius * RadiusFrac, 0.35f, 1.1f);
            if (armed)
                radius *= ArmedOverlapMul;

            Vector3 pos = rb.position;
            if (!IsFinite(pos))
                pos = missile.transform.position;

            Vector3 vel = rb.velocity;
            float speed = vel.magnitude;

            Vector3 dir = speed > 0.35f ? vel.normalized : Vector3.down;
            float castLen = Mathf.Max(MinCastM, speed * Time.fixedDeltaTime * CastMul);
            if (speed <= 0.35f)
                castLen = Mathf.Max(castLen, 1.5f);
            // Closing on moving units — look a bit farther when armed.
            if (armed)
                castLen = Mathf.Max(castLen, 2.5f);

            // Match vanilla tangible mask; Collide so trigger hitboxes on vehicles/aircraft count.
            int mask = armed
                ? ~(PhysicsLayers.ExclusionZonesMask.value | PhysicsLayers.IgnoreCollisionsMask.value)
                : (PhysicsLayers.StaticsMask.value | PhysicsLayers.ShipsMask.value);
            QueryTriggerInteraction triggers = armed
                ? QueryTriggerInteraction.Collide
                : QueryTriggerInteraction.Ignore;

            if (TryHit(missile, pos, dir, castLen, radius, mask, triggers, out RaycastHit hit))
            {
                HandleHit(missile, rb, armed, hit, pos);
                return;
            }

            if (speed > 1f && TryHit(missile, pos, -dir, radius * 0.5f, radius * 0.6f, mask, triggers, out hit))
            {
                HandleHit(missile, rb, armed, hit, pos);
                return;
            }

            int n = Physics.OverlapSphereNonAlloc(pos, radius, OverlapBuf, mask, triggers);
            for (int i = 0; i < n; i++)
            {
                Collider c = OverlapBuf[i];
                if (c == null || ShouldIgnoreCollider(missile, c))
                    continue;

                Vector3 closest = SanitizePoint(c.ClosestPoint(pos), pos, c);
                Vector3 nrm = (pos - closest).sqrMagnitude > 1e-6f
                    ? (pos - closest).normalized
                    : Vector3.up;

                if (Physics.Linecast(pos + nrm * 0.05f, closest - nrm * 0.02f, out hit, mask, triggers))
                {
                    if (!ShouldIgnoreCollider(missile, hit.collider))
                    {
                        HandleHit(missile, rb, armed, hit, pos);
                        return;
                    }
                }

                if (!armed)
                {
                    SoftSeparate(rb, nrm);
                    return;
                }

                // Overlap on unit/armor — kamikaze even if Linecast blocked.
                if (IsUnitOrArmor(c) || IsTerrain(c))
                {
                    DetonateAt(missile, closest, nrm, IsTerrain(c), IsUnitOrArmor(c), pos);
                    return;
                }
            }
        }

        /// <summary>Rigidbody contact with moving units (raycasts often miss after PhysX separates).</summary>
        internal static void ResolveContact(Missile missile, Rigidbody rb, bool armed, Collision collision)
        {
            if (!armed || missile == null || rb == null || missile.disabled || collision == null)
                return;
            if (collision.contactCount < 1)
                return;

            ContactPoint cp = collision.GetContact(0);
            Collider? c = cp.otherCollider != null ? cp.otherCollider : collision.collider;
            if (c == null || ShouldIgnoreCollider(missile, c))
                return;
            if (!IsUnitOrArmor(c) && !IsTerrain(c))
                return;

            Vector3 dronePos = rb.position;
            Vector3 point = SanitizePoint(cp.point, dronePos, c);
            Vector3 nrm = cp.normal.sqrMagnitude > 1e-6f ? cp.normal : Vector3.up;
            DetonateAt(missile, point, nrm, IsTerrain(c), IsUnitOrArmor(c), dronePos);
        }

        private static bool TryHit(
            Missile missile,
            Vector3 pos,
            Vector3 dir,
            float len,
            float radius,
            int mask,
            QueryTriggerInteraction triggers,
            out RaycastHit hit)
        {
            hit = default;
            Vector3 origin = pos - dir * (radius + 0.05f);
            float castDist = len + radius + 0.05f;

            int count = Physics.SphereCastNonAlloc(
                origin, radius, dir, CastBuf, castDist, mask, triggers);

            float best = float.MaxValue;
            int bestI = -1;
            for (int i = 0; i < count; i++)
            {
                Collider? c = CastBuf[i].collider;
                if (c == null || ShouldIgnoreCollider(missile, c))
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

            if (Physics.Linecast(pos, pos + dir * len, out hit, mask, triggers))
                return !ShouldIgnoreCollider(missile, hit.collider);

            hit = default;
            return false;
        }

        private static void HandleHit(Missile missile, Rigidbody rb, bool armed, RaycastHit hit, Vector3 dronePos)
        {
            Collider? c = hit.collider;
            if (c == null || ShouldIgnoreCollider(missile, c))
                return;

            if (!armed)
            {
                SoftSeparate(rb, hit.normal.sqrMagnitude > 1e-6f ? hit.normal : Vector3.up);
                return;
            }

            Vector3 point = SanitizePoint(hit.point, dronePos, c);
            Vector3 normal = hit.normal.sqrMagnitude > 1e-6f ? hit.normal : (dronePos - point).normalized;
            if (normal.sqrMagnitude < 1e-6f)
                normal = Vector3.up;

            bool terrain = IsTerrain(c);
            bool armor = !terrain && (IsUnitOrArmor(c) || point.y >= Datum.LocalSeaY);
            DetonateAt(missile, point, normal, terrain, armor, dronePos);
        }

        internal static Vector3 SanitizePoint(Vector3 raw, Vector3 dronePos, Collider? col)
        {
            if (!IsFinite(dronePos))
                dronePos = Vector3.zero;

            float maxSqr = MaxHitDriftM * MaxHitDriftM;
            bool rawBad = !IsFinite(raw) || (raw - dronePos).sqrMagnitude > maxSqr;

            if (!rawBad && raw.sqrMagnitude < 1e-4f && dronePos.sqrMagnitude > 1f)
                rawBad = true;

            if (!rawBad)
                return raw;

            if (col != null)
            {
                Vector3 closest = col.ClosestPoint(dronePos);
                if (IsFinite(closest) && (closest - dronePos).sqrMagnitude <= maxSqr)
                {
                    FpvPlugin.ModLogger?.LogWarning(
                        $"FpvImpact: bad hit.point={raw} → ClosestPoint={closest} drone={dronePos}");
                    return closest;
                }
            }

            FpvPlugin.ModLogger?.LogWarning(
                $"FpvImpact: bad hit.point={raw} → fallback drone={dronePos}");
            return dronePos;
        }

        internal static bool ShouldIgnoreCollider(Missile missile, Collider? c)
        {
            if (c == null || missile == null)
                return true;
            if (c.transform.IsChildOf(missile.transform))
                return true;
            if (c.attachedRigidbody != null && c.attachedRigidbody == missile.rb)
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

        private static void DetonateAt(
            Missile missile,
            Vector3 point,
            Vector3 normal,
            bool terrain,
            bool armor,
            Vector3 dronePos)
        {
            try
            {
                if (missile.disabled)
                    return;

                point = SanitizePoint(point, dronePos, null);
                if (normal.sqrMagnitude < 1e-6f)
                    normal = Vector3.up;
                else
                    normal.Normalize();

                try { missile.SetTarget(null); } catch { /* ignore */ }

                FpvPlugin.ModLogger?.LogInfo(
                    $"FpvImpact: boom at {point} drone={dronePos} terrain={terrain} armor={armor}");

                FpvBoomPending.Set(missile, point, dronePos);
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

        internal static bool IsUnitOrArmor(Collider? c) =>
            c != null && (c.GetComponentInParent<Unit>() != null || c.GetComponentInParent<IDamageable>() != null);

        private static bool IsFinite(Vector3 v) =>
            !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
              float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
    }
}
