using System.Collections.Generic;
using NuclearOption.Networking;
using UnityEngine;

namespace FPVMod.Drone
{
    /// <summary>
    /// FPV boom damage. Do NOT trust vanilla FragTrace alone:
    /// - dies on terrainMaterial / null PhysicMaterial
    /// - Linecast fails from inside large hulls
    /// - Overlap only sees TryGetComponent on the collider GO
    /// We apply direct TakeDamage on UnitParts (+ MapBuilding) ourselves.
    /// </summary>
    internal static class FpvBlastDamage
    {
        private const float LiftM = 0.75f;
        private const float MaxPushM = 4f;
        /// <summary>Minimum blast on the struck unit so armor soften cannot zero a kamikaze.</summary>
        private const float ContactBlastFloor = 800f;
        private static readonly Collider[] Buf = new Collider[256];
        private static readonly HashSet<int> SeenParts = new HashSet<int>(128);
        private static readonly HashSet<int> SeenUnits = new HashSet<int>(32);

        internal static void Apply(
            float yield,
            Vector3 surfacePoint,
            Vector3 normal,
            PersistentID ownerId,
            PersistentID missileId,
            Unit? hitUnit,
            Missile? selfMissile)
        {
            if (yield <= 0f || !IsFinite(surfacePoint))
                return;

            Vector3 origin = LiftOffSurface(surfacePoint, normal);

            // Keep vanilla pass for any side-effects; it often deals 0 — we do not rely on it.
            try
            {
                DamageEffects.BlastFrag(yield, origin, ownerId, missileId);
            }
            catch (System.Exception ex)
            {
                FpvPlugin.ModLogger?.LogWarning($"FpvBlast: BlastFrag: {ex.Message}");
            }

            bool server = false;
            try { server = NetworkManagerNuclearOption.i?.Server?.Active == true; }
            catch { /* ignore */ }

            if (!server)
            {
                FpvPlugin.ModLogger?.LogWarning("FpvBlast: Server.Active=false — direct damage skipped");
                return;
            }

            float power = Mathf.Pow(Mathf.Max(yield, 1f), 0.3333f);
            float radius = power * 20f;
            SeenParts.Clear();
            SeenUnits.Clear();
            int partHits = 0;
            int unitHits = 0;

            if (hitUnit != null && !hitUnit.disabled && hitUnit != (Unit?)selfMissile)
            {
                int n = DamageUnit(hitUnit, origin, radius, power, ownerId, contact: true);
                if (n > 0)
                {
                    partHits += n;
                    unitHits++;
                    SeenUnits.Add(hitUnit.GetInstanceID());
                }
            }

            // Nearby units by registry — no collider/FragTrace dependency.
            try
            {
                GlobalPosition gp = origin.ToGlobalPosition();
                List<Unit> all = UnitRegistry.allUnits;
                for (int i = 0; i < all.Count; i++)
                {
                    Unit? u = all[i];
                    if (u == null || u.disabled)
                        continue;
                    if (ReferenceEquals(u, selfMissile))
                        continue;
                    if (u.GetComponent<FpvDroneTag>() != null)
                        continue;
                    if (!SeenUnits.Add(u.GetInstanceID()))
                        continue;
                    if (!FastMath.InRange(u.GlobalPosition(), gp, radius))
                        continue;

                    int damaged = DamageUnit(u, origin, radius, power, ownerId, contact: false);
                    if (damaged > 0)
                    {
                        partHits += damaged;
                        unitHits++;
                    }
                }
            }
            catch (System.Exception ex)
            {
                FpvPlugin.ModLogger?.LogWarning($"FpvBlast: registry sweep: {ex.Message}");
            }

            // Buildings / odd IDamageable not in partLookup.
            partHits += DamageOrphanOverlap(origin, radius, power, ownerId, selfMissile);

            FpvPlugin.ModLogger?.LogInfo(
                $"FpvBlast: DIRECT units={unitHits} parts={partHits} yield={yield} power={power:0.00} " +
                $"r={radius:0.0} origin={origin} hit={hitUnit?.unitName ?? "-"}");
        }

        internal static Vector3 LiftOffSurface(Vector3 surface, Vector3 normal)
        {
            Vector3 nrm = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.up;
            Vector3 escape = Vector3.Dot(nrm, Vector3.up) > 0.15f ? nrm : (nrm + Vector3.up).normalized;
            if (escape.sqrMagnitude < 1e-6f)
                escape = Vector3.up;

            Vector3 lifted = surface + escape * LiftM + Vector3.up * (LiftM * 0.35f);

            for (float d = LiftM; d <= MaxPushM; d += 0.5f)
            {
                Vector3 cand = surface + escape * d + Vector3.up * 0.25f;
                int hits = Physics.OverlapSphereNonAlloc(cand, 0.12f, Buf, ~0, QueryTriggerInteraction.Ignore);
                bool blocked = false;
                for (int i = 0; i < hits; i++)
                {
                    Collider c = Buf[i];
                    if (c == null)
                        continue;
                    if (!c.isTrigger)
                    {
                        blocked = true;
                        break;
                    }
                }
                if (!blocked)
                    return cand;
                lifted = cand;
            }

            return lifted;
        }

        private static int DamageUnit(
            Unit unit,
            Vector3 origin,
            float radius,
            float power,
            PersistentID ownerId,
            bool contact)
        {
            if (unit == null || unit.disabled)
                return 0;

            List<UnitPart>? parts = null;
            try { parts = unit.GetAllParts(); }
            catch { return 0; }
            if (parts == null || parts.Count == 0)
            {
                // Scenery / odd units — damage root IDamageable if present.
                if (unit is IDamageable root)
                    return TryHitDamageable(root, origin, radius, power, ownerId, contact) ? 1 : 0;
                return 0;
            }

            int hits = 0;
            for (int i = 0; i < parts.Count; i++)
            {
                UnitPart? part = parts[i];
                if (part == null)
                    continue;
                if (!SeenParts.Add(part.GetInstanceID()))
                    continue;
                if (TryHitDamageable(part, origin, radius, power, ownerId, contact))
                    hits++;
            }

            return hits;
        }

        private static int DamageOrphanOverlap(
            Vector3 origin,
            float radius,
            float power,
            PersistentID ownerId,
            Missile? selfMissile)
        {
            int mask = ~(PhysicsLayers.ExclusionZonesMask.value | PhysicsLayers.IgnoreCollisionsMask.value);
            int n = Physics.OverlapSphereNonAlloc(origin, radius, Buf, mask, QueryTriggerInteraction.Collide);
            int hits = 0;

            for (int i = 0; i < n; i++)
            {
                Collider c = Buf[i];
                if (c == null)
                    continue;
                if (selfMissile != null && c.transform.IsChildOf(selfMissile.transform))
                    continue;

                IDamageable? dmg = c.GetComponentInParent<IDamageable>();
                if (dmg == null)
                    continue;

                // Already hit via unit partLookup.
                if (dmg is Component comp && !SeenParts.Add(comp.GetInstanceID()))
                    continue;

                Unit? u = null;
                try { u = dmg.GetUnit(); }
                catch { /* ignore */ }
                if (u != null && ReferenceEquals(u, selfMissile))
                    continue;
                if (u != null && u.GetComponent<FpvDroneTag>() != null)
                    continue;

                if (TryHitDamageable(dmg, origin, radius, power, ownerId, contact: false))
                    hits++;
            }

            return hits;
        }

        private static bool TryHitDamageable(
            IDamageable dmg,
            Vector3 origin,
            float radius,
            float power,
            PersistentID ownerId,
            bool contact)
        {
            try
            {
                ArmorProperties? armor = dmg.GetArmorProperties();
                if (armor == null)
                    return false;

                Transform? xform = dmg.GetTransform();
                if (xform == null)
                    return false;

                Vector3 target = ClosestOn(dmg, origin);
                float dist = Vector3.Distance(origin, target);
                if (dist > radius)
                    return false;

                float scaled = Mathf.Max(dist / Mathf.Max(power, 0.01f), 1f);
                float raw = 25000f / (scaled * scaled * scaled);
                float soften = Mathf.Clamp01(
                    (power * 500f - armor.blastArmor) / (armor.blastArmor * 2f + 0.01f));
                raw *= soften;
                raw -= armor.blastArmor;

                if (contact)
                    raw = Mathf.Max(raw, ContactBlastFloor);

                if (raw <= 0f && !contact)
                    return false;

                float extent = EstimateExtent(dmg);
                float amount = Mathf.Clamp01(
                    Mathf.Max(dist * dist, power * power) / Mathf.Max(0.25f, extent * extent));
                amount /= 1f + armor.blastArmor * 0.05f;
                amount = Mathf.Clamp01(amount);
                if (contact)
                    amount = Mathf.Max(amount, 0.85f);

                float pierce = 0f;
                if (contact)
                {
                    // Same idea as Missile.PenetrateObject with impactFuseDelay=0.
                    pierce = Mathf.Max(0f, FpvConstants.PierceAp - armor.pierceArmor);
                }

                float blast = Mathf.Max(raw, contact ? ContactBlastFloor : 0f);
                if (blast <= 0f && pierce <= 0f)
                    return false;

                if (power >= 0.5f)
                {
                    try { dmg.TakeShockwave(origin, blast, power); }
                    catch { /* ignore */ }
                }

                if (dmg is UnitPart partCheck)
                {
                    var saved = partCheck.parentUnit != null
                        ? partCheck.parentUnit.SavedUnit as NuclearOption.SavedMission.SavedScenery
                        : null;
                    if (saved != null && saved.indestructible)
                    {
                        FpvPlugin.ModLogger?.LogWarning(
                            $"FpvBlast: SKIP indestructible scenery '{partCheck.parentUnit?.unitName}'");
                        return false;
                    }

                    if (partCheck.parentUnit != null && !partCheck.parentUnit.IsServer)
                    {
                        FpvPlugin.ModLogger?.LogWarning(
                            $"FpvBlast: SKIP not IsServer unit '{partCheck.parentUnit.unitName}'");
                        return false;
                    }
                }

                float hpBefore = dmg is UnitPart up ? up.hitPoints : -1f;
                dmg.TakeDamage(pierce, blast, amount, 0f, contact ? 50f : 0f, ownerId);

                FpvPlugin.ModLogger?.LogInfo(
                    $"FpvBlast: hit '{xform.name}' dist={dist:0.0} pierce={pierce:0} blast={blast:0} " +
                    $"amt={amount:0.00} hpBefore={hpBefore:0} contact={contact}");
                return true;
            }
            catch (System.Exception ex)
            {
                FpvPlugin.ModLogger?.LogWarning($"FpvBlast: TakeDamage failed: {ex.Message}");
                return false;
            }
        }

        private static Vector3 ClosestOn(IDamageable dmg, Vector3 origin)
        {
            if (dmg is Component c)
            {
                Collider? col = c.GetComponent<Collider>();
                if (col == null)
                    col = c.GetComponentInChildren<Collider>();
                if (col != null)
                {
                    Vector3 p = col.ClosestPoint(origin);
                    if (IsFinite(p))
                        return p;
                }

                Transform t = c.transform;
                if (t != null)
                    return t.position;
            }

            try
            {
                Transform? x = dmg.GetTransform();
                if (x != null)
                    return x.position;
            }
            catch { /* ignore */ }

            return origin;
        }

        private static float EstimateExtent(IDamageable dmg)
        {
            if (dmg is Component c)
            {
                Collider? col = c.GetComponent<Collider>();
                if (col != null)
                {
                    Vector3 e = col.bounds.extents;
                    return Mathf.Max(0.5f, (e.x + e.y + e.z) * 0.3333f);
                }
            }
            return 1f;
        }

        private static bool IsFinite(Vector3 v) =>
            !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
              float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
    }
}
