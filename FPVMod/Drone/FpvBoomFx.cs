using System;
using System.Reflection;
using UnityEngine;

namespace FPVMod.Drone
{
    /// <summary>
    /// Warhead FX at forced world hit — no Instantate(parent=Datum) PlayOnAwake flash.
    /// </summary>
    internal static class FpvBoomFx
    {
        private static readonly Type? WarheadType =
            typeof(Missile).GetNestedType("Warhead", BindingFlags.Public | BindingFlags.NonPublic);

        private static readonly FieldInfo? DetonatedField =
            WarheadType?.GetField("detonated", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private static readonly FieldInfo? AirEffect =
            WarheadType?.GetField("airEffect", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? ArmorEffect =
            WarheadType?.GetField("armorEffect", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? TerrainEffect =
            WarheadType?.GetField("terrainEffect", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? WaterSurfaceEffect =
            WarheadType?.GetField("waterSurfaceEffect", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? UnderwaterEffect =
            WarheadType?.GetField("underwaterEffect", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? FizzleEffect =
            WarheadType?.GetField("fizzleEffect", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? MissileWarheadField =
            typeof(Missile).GetField("warhead", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static void MarkDetonated(Missile missile)
        {
            object? wh = MissileWarheadField?.GetValue(missile);
            if (wh == null || DetonatedField == null)
                return;
            DetonatedField.SetValue(wh, true);
        }

        internal static void SpawnFromWarhead(
            Missile missile,
            Vector3 worldPos,
            Vector3 normal,
            bool armed,
            float blastYield,
            bool hitArmor,
            bool hitTerrain)
        {
            if (missile == null)
                return;

            object? wh = MissileWarheadField?.GetValue(missile);
            if (wh == null)
                return;

            if (!armed)
            {
                GameObject? fizzle = FizzleEffect?.GetValue(wh) as GameObject;
                if (fizzle != null)
                {
                    Vector3 vel = missile.rb != null && missile.rb.velocity.sqrMagnitude > 0.01f
                        ? missile.rb.velocity
                        : Vector3.forward;
                    Spawn(fizzle, missile.rb != null ? missile.rb.position : worldPos, FastMath.LookRotation(vel));
                }
                return;
            }

            if (normal.sqrMagnitude < 1e-6f)
                normal = Vector3.up;
            else
                normal.Normalize();

            float radiusHint = Mathf.Pow(Mathf.Max(blastYield, 1f), 0.3333f) * 2f;
            bool underSea = worldPos.y < Datum.LocalSeaY + 0.1f;
            Vector3 seaPos = new Vector3(worldPos.x, Datum.LocalSeaY, worldPos.z);
            GameObject? fx = null;

            if (underSea)
            {
                fx = Spawn(UnderwaterEffect?.GetValue(wh) as GameObject, seaPos, Quaternion.identity);
            }
            else
            {
                if (hitTerrain)
                    fx = Spawn(TerrainEffect?.GetValue(wh) as GameObject, worldPos, Quaternion.LookRotation(normal));
                if (hitArmor)
                    fx = Spawn(ArmorEffect?.GetValue(wh) as GameObject, worldPos, Quaternion.LookRotation(normal));

                bool grounded = hitTerrain ||
                    (Physics.Linecast(worldPos, worldPos - Vector3.up * radiusHint, out RaycastHit hit, PhysicsLayers.StaticsMask)
                     && hit.point.y > Datum.LocalSeaY);

                GameObject? waterPrefab = WaterSurfaceEffect?.GetValue(wh) as GameObject;
                if (waterPrefab != null && !grounded &&
                    worldPos.y < Datum.LocalSeaY + radiusHint && worldPos.y > Datum.LocalSeaY + 1f)
                {
                    GameObject? waterFx = Spawn(waterPrefab, seaPos, Quaternion.identity);
                    if (waterFx != null)
                        UnityEngine.Object.Destroy(waterFx, 30f);
                }
            }

            if (fx == null)
                fx = Spawn(AirEffect?.GetValue(wh) as GameObject, worldPos, FastMath.LookRotation(normal));

            if (blastYield > 200f)
            {
                if (fx != null)
                {
                    Shockwave? sw = fx.GetComponentInChildren<Shockwave>();
                    sw?.SetOwner(missile.ownerID, blastYield * 1e-06f);
                }
            }
            else if (fx != null)
            {
                UnityEngine.Object.Destroy(fx, 30f);
            }
        }

        /// <summary>World Instantate (no Datum parent at Awake) → then parent Datum worldPositionStays.</summary>
        internal static GameObject? Spawn(GameObject? prefab, Vector3 worldPos, Quaternion rot)
        {
            if (prefab == null)
                return null;

            GameObject go = UnityEngine.Object.Instantiate(prefab, worldPos, rot);
            go.transform.SetPositionAndRotation(worldPos, rot);

            Transform? datum = Datum.origin;
            if (datum != null)
                go.transform.SetParent(datum, true);

            return go;
        }
    }
}
