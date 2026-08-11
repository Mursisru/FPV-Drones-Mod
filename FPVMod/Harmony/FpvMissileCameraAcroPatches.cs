using System;
using System.Reflection;
using FPVMod.Session;
using HarmonyLib;
using UnityEngine;

namespace FPVMod.HarmonyPatches
{
    /// <summary>
    /// Runtime Harmony on MissileCamera only from FPV — no MC source edits.
    /// While FPV session active: body-lock feed (no horizon / turn-look roll).
    /// </summary>
    internal static class FpvMissileCameraAcroPatches
    {
        private static bool _patched;
        private static Type? _rigType;
        private static FieldInfo? _rootField;
        private static FieldInfo? _camField;
        private static FieldInfo? _boreRoll;
        private static FieldInfo? _horizonRoll;
        private static FieldInfo? _horizonVel;
        private static FieldInfo? _rollVel;
        private static MethodInfo? _fsLookAround;

        internal static void TryPatch(Harmony harmony)
        {
            if (_patched || harmony == null)
                return;

            try
            {
                Assembly? mc = null;
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.GetName().Name == "MissileCamera")
                    {
                        mc = asm;
                        break;
                    }
                }
                if (mc == null)
                    return;

                _rigType = mc.GetType("MissileCamera.MissileCameraRig");
                if (_rigType == null)
                    return;

                MethodInfo? applyPose = AccessTools.Method(_rigType, "ApplyPose");
                MethodInfo? advanceRoll = AccessTools.Method(_rigType, "AdvanceRoll");
                if (applyPose == null)
                    return;

                _rootField = AccessTools.Field(_rigType, "_root");
                _camField = AccessTools.Field(_rigType, "_camera");
                _boreRoll = AccessTools.Field(_rigType, "_boreRollDeg");
                _horizonRoll = AccessTools.Field(_rigType, "_horizonLevelRoll");
                _horizonVel = AccessTools.Field(_rigType, "_horizonLevelVelocity");
                _rollVel = AccessTools.Field(_rigType, "_rollVelocity");

                Type? look = mc.GetType("MissileCamera.MissileCameraFsLookAround");
                _fsLookAround = look != null
                    ? AccessTools.Method(look, "ApplyToCamera", new[] { typeof(Camera) })
                    : null;

                harmony.Patch(applyPose, postfix: new HarmonyMethod(typeof(FpvMissileCameraAcroPatches), nameof(ApplyPosePostfix)));
                if (advanceRoll != null)
                    harmony.Patch(advanceRoll, prefix: new HarmonyMethod(typeof(FpvMissileCameraAcroPatches), nameof(AdvanceRollPrefix)));

                _patched = true;
                FpvPlugin.ModLogger?.LogInfo("FPV: patched MissileCameraRig for body-lock acro (FPV-only).");
            }
            catch (Exception ex)
            {
                FpvPlugin.ModLogger?.LogWarning($"FPV MC acro patch: {ex.Message}");
            }
        }

        private static bool AdvanceRollPrefix()
        {
            return !FpvControlSession.Active;
        }

        private static void ApplyPosePostfix(object __instance)
        {
            if (!FpvControlSession.Active || __instance == null)
                return;

            try
            {
                _boreRoll?.SetValue(__instance, 0f);
                _horizonRoll?.SetValue(__instance, 0f);
                _horizonVel?.SetValue(__instance, 0f);
                _rollVel?.SetValue(__instance, 0f);

                if (_rootField?.GetValue(__instance) is GameObject root && root != null)
                    root.transform.localRotation = Quaternion.identity;

                if (_fsLookAround != null && _camField?.GetValue(__instance) is Camera cam && cam != null)
                    _fsLookAround.Invoke(null, new object[] { cam });
            }
            catch
            {
                // ignore
            }
        }
    }
}
