using System;
using System.Reflection;
using FPVMod.Drone;
using HarmonyLib;
using UnityEngine;

namespace FPVMod.HarmonyPatches
{
    /// <summary>
    /// FPV boom at drone transform.position — skip vanilla RpcDetonate global/relative math
    /// (that was placing FX near Datum/spectator).
    /// </summary>
    [HarmonyPatch(typeof(Missile), nameof(Missile.Detonate))]
    internal static class FpvDetonateSafePatch
    {
        private static readonly FieldInfo? WarheadField =
            typeof(Missile).GetField("warhead", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? BlastField =
            typeof(Missile).GetField("blastYield", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private static readonly MethodInfo? DisappearMethod =
            typeof(Missile).GetMethod("Disappear", BindingFlags.Instance | BindingFlags.NonPublic);

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
                __instance.SetTarget(null);
                __instance.Networkdisabled = true;

                // Scene position where the drone actually is (raycast / rb).
                Vector3 fxPos = __instance.transform.position;
                if (__instance.rb != null)
                    fxPos = __instance.rb.position;

                bool armed = __instance.IsArmed();
                float blast = 25f;
                if (BlastField != null)
                    blast = Convert.ToSingle(BlastField.GetValue(__instance) ?? blast);

                PersistentID ownerId = default;
                try { ownerId = __instance.ownerID; } catch { /* ignore */ }

                object? warhead = WarheadField?.GetValue(__instance);
                if (warhead != null)
                {
                    MethodInfo? det = warhead.GetType().GetMethod(
                        "Detonate",
                        BindingFlags.Instance | BindingFlags.Public,
                        null,
                        new[]
                        {
                            typeof(Rigidbody), typeof(PersistentID), typeof(Vector3), typeof(Vector3),
                            typeof(bool), typeof(float), typeof(bool), typeof(bool)
                        },
                        null);
                    det?.Invoke(warhead, new object[]
                    {
                        __instance.rb, ownerId, fxPos, normal, armed, blast, hitArmor, hitTerrain
                    });
                }

                if (armed)
                {
                    DisappearMethod?.Invoke(__instance, null);
                    if (__instance.rb != null)
                        __instance.rb.isKinematic = true;
                }

                object? task = DelayedDestroyMethod?.Invoke(__instance, new object[] { 2f });
                TryForget(task);
            }
            catch (Exception ex)
            {
                FpvPlugin.ModLogger?.LogWarning($"FPV Detonate local: {ex.Message}");
                return true; // fall back to vanilla
            }

            return false;
        }

        private static void TryForget(object? uniTask)
        {
            if (uniTask == null)
                return;
            try
            {
                // Cysharp.Threading.Tasks.UniTaskExtensions.Forget
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type? ext = asm.GetType("Cysharp.Threading.Tasks.UniTaskExtensions");
                    MethodInfo? forget = ext?.GetMethod(
                        "Forget",
                        BindingFlags.Static | BindingFlags.Public,
                        null,
                        new[] { uniTask.GetType() },
                        null);
                    if (forget != null)
                    {
                        forget.Invoke(null, new[] { uniTask });
                        return;
                    }
                }
            }
            catch
            {
                // ignore
            }
        }
    }
}
