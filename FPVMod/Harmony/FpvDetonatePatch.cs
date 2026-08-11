using System;
using System.Reflection;
using FPVMod.Drone;
using HarmonyLib;
using UnityEngine;

namespace FPVMod.HarmonyPatches
{
    /// <summary>
    /// FPV boom: force global Rpc path + Instantate FX in world space (no Datum.origin parent).
    /// Vanilla Instantate(prefab, Datum.origin) can PlayOnAwake particles at Datum before SetPosition.
    /// </summary>
    [HarmonyPatch(typeof(Missile), nameof(Missile.Detonate))]
    internal static class FpvDetonateSafePatch
    {
        private static readonly MethodInfo? DelayedDestroyMethod =
            typeof(Missile).GetMethod("DelayedDestroy", BindingFlags.Instance | BindingFlags.NonPublic);

        private static bool Prefix(Missile __instance, Vector3 normal, bool hitArmor, bool hitTerrain)
        {
            if (__instance == null || __instance.GetComponent<FpvDroneTag>() == null)
                return true;

            FpvWarhead? fuse = __instance.GetComponent<FpvWarhead>();
            if (fuse != null && fuse.IsSafe)
                return false;

            if (__instance.disabled)
                return false;

            try
            {
                // Never use target-relative Rpc (FX sticks to wrong unit / spectator).
                __instance.SetTarget(null);
                __instance.Networkdisabled = true;

                Vector3 world = ResolveWorldBoomPos(__instance);
                SnapMissile(__instance, world);

                Vector3 global = world.ToGlobalPosition().AsVector3();
                bool armed = __instance.IsArmed();
                if (normal.sqrMagnitude < 1e-6f)
                    normal = Vector3.up;

                // Same net path as vanilla Detonate, but always absolute GlobalPosition.
                __instance.RpcDetonate(null, false, global, armed, hitArmor, hitTerrain, normal);

                object? task = DelayedDestroyMethod?.Invoke(__instance, new object[] { 2f });
                TryForget(task);
            }
            catch (Exception ex)
            {
                FpvBoomPending.Clear(__instance);
                FpvPlugin.ModLogger?.LogWarning($"FPV Detonate: {ex.Message}");
                return true;
            }

            return false;
        }

        private static Vector3 ResolveWorldBoomPos(Missile m)
        {
            if (FpvBoomPending.TryConsume(m, out Vector3 pending))
                return pending;
            if (m.rb != null)
                return m.rb.position;
            return m.transform.position;
        }

        private static void SnapMissile(Missile m, Vector3 world)
        {
            m.transform.position = world;
            if (m.rb == null)
                return;
            m.rb.velocity = Vector3.zero;
            m.rb.angularVelocity = Vector3.zero;
            m.rb.position = world;
            m.rb.MovePosition(world);
        }

        private static void TryForget(object? uniTask)
        {
            if (uniTask == null)
                return;
            try
            {
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type? ext = asm.GetType("Cysharp.Threading.Tasks.UniTaskExtensions");
                    MethodInfo? forget = ext?.GetMethod(
                        "Forget",
                        BindingFlags.Static | BindingFlags.Public,
                        null,
                        new[] { uniTask.GetType() },
                        null);
                    if (forget == null)
                        continue;
                    forget.Invoke(null, new[] { uniTask });
                    return;
                }
            }
            catch
            {
                // ignore
            }
        }
    }

    /// <summary>
    /// Replace Warhead FX Instantate: world position, no Datum.parent (particles emit at impact).
    /// </summary>
    [HarmonyPatch]
    internal static class FpvWarheadDetonatePatch
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

        private static MethodBase? TargetMethod()
        {
            if (WarheadType == null)
                return null;
            return AccessTools.Method(
                WarheadType,
                "Detonate",
                new[]
                {
                    typeof(Rigidbody), typeof(PersistentID), typeof(Vector3), typeof(Vector3),
                    typeof(bool), typeof(float), typeof(bool), typeof(bool)
                });
        }

        private static bool Prefix(
            object __instance,
            Rigidbody rb,
            PersistentID ownerID,
            Vector3 position,
            Vector3 normal,
            bool armed,
            float blastYield,
            bool hitArmor,
            bool hitTerrain)
        {
            if (rb == null || rb.GetComponent<FpvDroneTag>() == null)
                return true;

            if (DetonatedField != null && DetonatedField.GetValue(__instance) is true)
                return false;

            DetonatedField?.SetValue(__instance, true);

            if (!armed)
            {
                GameObject? fizzle = FizzleEffect?.GetValue(__instance) as GameObject;
                if (fizzle != null)
                {
                    Vector3 vel = rb.velocity.sqrMagnitude > 0.01f ? rb.velocity : Vector3.forward;
                    SpawnWorld(fizzle, rb.position, FastMath.LookRotation(vel));
                }
                return false;
            }

            if (normal.sqrMagnitude < 1e-6f)
                normal = Vector3.up;
            else
                normal.Normalize();

            float radiusHint = Mathf.Pow(Mathf.Max(blastYield, 1f), 0.3333f) * 2f;
            bool underSea = position.y < Datum.LocalSeaY + 0.1f;
            Vector3 seaPos = new Vector3(position.x, Datum.LocalSeaY, position.z);
            GameObject? fx = null;

            if (underSea)
            {
                fx = SpawnWorld(UnderwaterEffect?.GetValue(__instance) as GameObject, seaPos, Quaternion.identity);
            }
            else
            {
                if (hitTerrain)
                    fx = SpawnWorld(TerrainEffect?.GetValue(__instance) as GameObject, position, Quaternion.LookRotation(normal));
                if (hitArmor)
                    fx = SpawnWorld(ArmorEffect?.GetValue(__instance) as GameObject, position, Quaternion.LookRotation(normal));

                bool grounded = hitTerrain ||
                    (Physics.Linecast(position, position - Vector3.up * radiusHint, out RaycastHit hit, PhysicsLayers.StaticsMask)
                     && hit.point.y > Datum.LocalSeaY);

                GameObject? waterPrefab = WaterSurfaceEffect?.GetValue(__instance) as GameObject;
                if (waterPrefab != null && !grounded &&
                    position.y < Datum.LocalSeaY + radiusHint && position.y > Datum.LocalSeaY + 1f)
                {
                    GameObject? waterFx = SpawnWorld(waterPrefab, seaPos, Quaternion.identity);
                    if (waterFx != null)
                        UnityEngine.Object.Destroy(waterFx, 30f);
                }
            }

            if (fx == null)
                fx = SpawnWorld(AirEffect?.GetValue(__instance) as GameObject, position, FastMath.LookRotation(normal));

            if (blastYield > 200f)
            {
                if (fx != null)
                {
                    Shockwave? sw = fx.GetComponentInChildren<Shockwave>();
                    sw?.SetOwner(ownerID, blastYield * 1e-06f);
                }
            }
            else if (fx != null)
            {
                UnityEngine.Object.Destroy(fx, 30f);
            }

            return false;
        }

        private static GameObject? SpawnWorld(GameObject? prefab, Vector3 worldPos, Quaternion rot)
        {
            if (prefab == null)
                return null;
            // No parent — particles Awake/Play at impact, not at Datum.origin.
            GameObject go = UnityEngine.Object.Instantiate(prefab, worldPos, rot);
            if (go.transform.parent != null)
                go.transform.SetParent(null, true);
            return go;
        }
    }
}
