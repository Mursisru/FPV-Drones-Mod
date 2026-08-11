using System;
using System.Reflection;
using FPVMod.Access;
using FPVMod.Bootstrap;
using FPVMod.Drone;
using FPVMod.Session;
using HarmonyLib;
using UnityEngine;

namespace FPVMod.HarmonyPatches
{
    /// <summary>
    /// FPV Detonate: absolute GlobalPosition Rpc from FpvBoomPending (never relative/target).
    /// Registered manually via FpvBoomPatches — not PatchAll.
    /// </summary>
    internal static class FpvDetonateSafePatch
    {
        private static readonly MethodInfo? DelayedDestroyMethod =
            typeof(Missile).GetMethod("DelayedDestroy", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static bool Prefix(Missile __instance, Vector3 normal, bool hitArmor, bool hitTerrain)
        {
            if (__instance == null || !DefinitionRegistrar.IsFpvMissile(__instance))
                return true;

            FpvWarhead? fuse = __instance.GetComponent<FpvWarhead>();
            if (fuse != null && fuse.IsSafe)
                return false;

            if (__instance.disabled)
                return false;

            try
            {
                __instance.Networkdisabled = true;

                Vector3 dronePos = __instance.rb != null
                    ? __instance.rb.position
                    : __instance.transform.position;

                Unit? relative = null;
                if (!FpvBoomPending.TryPeek(__instance, out Vector3 world, out Vector3 droneAtHit, out relative))
                {
                    world = dronePos;
                    FpvPlugin.ModLogger?.LogError(
                        $"FPV Detonate Safe: no pending hit — fallback rb={world} datum={Datum.origin?.position}");
                }
                else
                {
                    if (droneAtHit.sqrMagnitude > 1f)
                        dronePos = droneAtHit;
                    world = SanitizeBoomWorld(world, dronePos, "Safe");
                    FpvBoomPending.Set(__instance, world, dronePos, relative);
                }

                // Relative Rpc only when we actually hit that unit (pending) — same idea as missile.target.
                if (relative == null || relative.disabled)
                {
                    // keep absolute
                }

                SnapMissile(__instance, world);

                bool armed = __instance.IsArmed();
                if (normal.sqrMagnitude < 1e-6f)
                    normal = Vector3.up;

                if (relative != null && !relative.disabled)
                {
                    Vector3 local = relative.transform.InverseTransformPoint(world);
                    FpvPlugin.ModLogger?.LogInfo(
                        $"FPV boom Safe: relative={relative.unitName} world={world} local={local} armed={armed}");
                    try { __instance.SetTarget(null); } catch { /* ignore */ }
                    __instance.RpcDetonate(relative, true, local, armed, hitArmor, hitTerrain, normal);
                }
                else
                {
                    Vector3 global = world.ToGlobalPosition().AsVector3();
                    FpvPlugin.ModLogger?.LogInfo(
                        $"FPV boom Safe: absolute world={world} global={global} armed={armed}");
                    try { __instance.SetTarget(null); } catch { /* ignore */ }
                    __instance.RpcDetonate(null, false, global, armed, hitArmor, hitTerrain, normal);
                }

                object? task = DelayedDestroyMethod?.Invoke(__instance, new object[] { 2f });
                TryForget(task);
            }
            catch (Exception ex)
            {
                FpvBoomPending.Clear(__instance);
                FpvPlugin.ModLogger?.LogWarning($"FPV Detonate Safe: {ex.Message}");
                return true;
            }

            return false;
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

        /// <summary>Reject floating-origin center (0,0,0) / drifted pending — use drone pose.</summary>
        internal static Vector3 SanitizeBoomWorld(Vector3 world, Vector3 dronePos, string tag)
        {
            const float maxDrift = 25f;
            bool bad = (world - dronePos).sqrMagnitude > maxDrift * maxDrift;
            if (!bad && world.sqrMagnitude < 1e-4f && dronePos.sqrMagnitude > 1f)
                bad = true;
            if (!bad)
                return world;

            FpvPlugin.ModLogger?.LogWarning($"FPV boom {tag}: reject world={world} → drone={dronePos}");
            return dronePos;
        }

        internal static void ForgetUniTask(object? uniTask)
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

        private static void TryForget(object? uniTask) => ForgetUniTask(uniTask);
    }

    /// <summary>
    /// FPV RpcDetonate: forced world from pending for FX.
    /// Damage = DamageEffects.BlastFrag same frame (vanilla formula).
    /// Do NOT defer via ExplosionForceOnPhysicsFrame — Datum jumps and blast misses units.
    /// </summary>
    internal static class FpvRpcDetonateReplacePatch
    {
        private static readonly FieldInfo? EffectsTransformField =
            typeof(Missile).GetField("effectsTransform", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? FlightSoundField =
            typeof(Missile).GetField("flightSound", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? NearbyClipField =
            typeof(Missile).GetField("nearbyDetonationClip", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? MotorField =
            typeof(Missile).GetField("motor", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo? DisappearMethod =
            typeof(Missile).GetMethod("Disappear", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static bool Prefix(
            Missile __instance,
            Unit relativeUnit,
            bool useUnit,
            Vector3 pos,
            bool armed,
            bool hitArmor,
            bool hitTerrain,
            Vector3 normal)
        {
            if (__instance == null)
                return true;

            if (__instance.GetComponent<FpvDroneTag>() == null &&
                (DefinitionRegistrar.IsFpvMissile(__instance) ||
                 DefinitionRegistrar.IsFpvDrone(__instance.definition)))
            {
                PrefabFactory.StampDroneInstance(__instance.gameObject);
            }

            if (!DefinitionRegistrar.IsFpvMissile(__instance))
                return true;

            try
            {
                Vector3 datum = Datum.origin != null ? Datum.origin.position : Vector3.zero;
                Vector3 decoded = useUnit && relativeUnit != null
                    ? relativeUnit.transform.TransformPoint(pos)
                    : pos + datum;

                Vector3 dronePos = __instance.rb != null
                    ? __instance.rb.position
                    : __instance.transform.position;

                bool fromPending = FpvBoomPending.TryConsume(
                    __instance, out Vector3 world, out Vector3 droneAtHit, out Unit? pendingRel);
                if (droneAtHit.sqrMagnitude > 1f)
                    dronePos = droneAtHit;
                else if (dronePos.sqrMagnitude < 1e-4f && __instance.transform.position.sqrMagnitude > 1f)
                    dronePos = __instance.transform.position;

                if (!fromPending)
                {
                    world = FpvDetonateSafePatch.SanitizeBoomWorld(decoded, dronePos, "RpcDecoded");
                    FpvPlugin.ModLogger?.LogWarning(
                        $"FPV boom Rpc: no pending — world={world} relative={relativeUnit != null}");
                }
                else
                {
                    // Keep impact world from pending (do not reparent onto a unit transform).
                    world = FpvDetonateSafePatch.SanitizeBoomWorld(world, dronePos, "RpcPending");
                }

                Unit? rel = relativeUnit != null ? relativeUnit : pendingRel;

                FpvPlugin.ModLogger?.LogInfo(
                    $"FPV boom Rpc: pending={fromPending} world={world} rel={rel?.unitName} armed={armed}");

                RunMotorDestruct(__instance);
                DetachEffects(__instance);
                PlayDetonationSound(__instance);

                if (armed)
                {
                    DisappearMethod?.Invoke(__instance, null);
                    if (__instance.rb != null)
                        __instance.rb.isKinematic = true;
                }

                Snap(__instance, world);

                float yield = FpvMissileAccess.GetBlastYield(__instance);
                FpvBoomFx.SpawnFromWarhead(__instance, world, normal, armed, yield, hitArmor, hitTerrain);
                FpvBoomFx.MarkDetonated(__instance);

                if (FpvControlSession.Active && FpvControlSession.Drone == __instance)
                    FpvBoomSpectate.TryBegin(__instance, world);

                if (armed && yield <= 200f)
                {
                    FpvBlastDamage.Apply(
                        yield,
                        world,
                        normal,
                        __instance.ownerID,
                        __instance.persistentID,
                        rel,
                        __instance);
                }
            }
            catch (Exception ex)
            {
                FpvBoomPending.Clear(__instance);
                FpvPlugin.ModLogger?.LogError($"FPV boom Rpc replace failed: {ex}");
                return true;
            }

            return false;
        }

        private static void Snap(Missile m, Vector3 world)
        {
            m.transform.position = world;
            if (m.rb == null)
                return;
            m.rb.velocity = Vector3.zero;
            m.rb.angularVelocity = Vector3.zero;
            m.rb.position = world;
            m.rb.MovePosition(world);
        }

        private static void RunMotorDestruct(Missile m)
        {
            object? motor = MotorField?.GetValue(m);
            if (motor == null)
                return;
            try
            {
                MethodInfo? destruct = motor.GetType().GetMethod(
                    "Destruct",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                destruct?.Invoke(motor, new object[] { m });
            }
            catch (Exception ex)
            {
                FpvPlugin.ModLogger?.LogWarning($"FPV boom motor Destruct: {ex.Message}");
            }
        }

        private static void DetachEffects(Missile m)
        {
            if (EffectsTransformField?.GetValue(m) is not Transform fx)
                return;
            if (Datum.origin != null)
                fx.SetParent(Datum.origin, true);
            UnityEngine.Object.Destroy(fx.gameObject, 20f);
        }

        private static void PlayDetonationSound(Missile m)
        {
            object? srcObj = FlightSoundField?.GetValue(m);
            if (srcObj == null)
                return;
            try
            {
                Type srcType = srcObj.GetType();
                object? clip = NearbyClipField?.GetValue(m);
                srcType.GetMethod("Stop", Type.EmptyTypes)?.Invoke(srcObj, null);
                if (clip != null)
                    srcType.GetProperty("clip")?.SetValue(srcObj, clip);
                srcType.GetProperty("pitch")?.SetValue(srcObj, 1f);
                srcType.GetProperty("volume")?.SetValue(srcObj, 1f);
                srcType.GetProperty("dopplerLevel")?.SetValue(srcObj, 1f);
                srcType.GetProperty("loop")?.SetValue(srcObj, false);
                srcType.GetMethod("Play", Type.EmptyTypes)?.Invoke(srcObj, null);
            }
            catch (Exception ex)
            {
                FpvPlugin.ModLogger?.LogWarning($"FPV boom sound: {ex.Message}");
            }
        }
    }

    /// <summary>Manual boom patches — survives PatchAll AmbiguousMatch abort.</summary>
    internal static class FpvBoomPatches
    {
        private static bool _done;

        internal static void Ensure(Harmony harmony)
        {
            if (harmony == null || _done)
                return;

            MethodInfo? detonate = AccessTools.Method(
                typeof(Missile),
                nameof(Missile.Detonate),
                new[] { typeof(Vector3), typeof(bool), typeof(bool) });
            MethodInfo? rpc = AccessTools.Method(typeof(Missile), "UserCode_RpcDetonate_897349600");

            if (detonate == null)
                FpvPlugin.ModLogger?.LogError("FPV boom: Missile.Detonate MethodInfo is null");
            if (rpc == null)
                FpvPlugin.ModLogger?.LogError("FPV boom: UserCode_RpcDetonate MethodInfo is null");

            try
            {
                // Always (re)apply via manual Patch — unpatch our id first to avoid double prefix.
                if (detonate != null)
                {
                    harmony.Unpatch(detonate, HarmonyPatchType.Prefix, harmony.Id);
                    harmony.Patch(detonate, prefix: new HarmonyMethod(typeof(FpvDetonateSafePatch), nameof(FpvDetonateSafePatch.Prefix)));
                }

                if (rpc != null)
                {
                    harmony.Unpatch(rpc, HarmonyPatchType.Prefix, harmony.Id);
                    harmony.Patch(rpc, prefix: new HarmonyMethod(typeof(FpvRpcDetonateReplacePatch), nameof(FpvRpcDetonateReplacePatch.Prefix)));
                }

                _done = detonate != null && rpc != null;
                FpvPlugin.ModLogger?.LogInfo(
                    $"FPV: boom patches ready (Detonate={detonate != null}, RpcDetonate={rpc != null})");
            }
            catch (Exception ex)
            {
                FpvPlugin.ModLogger?.LogError($"FPV boom patches failed: {ex}");
            }
        }
    }
}
