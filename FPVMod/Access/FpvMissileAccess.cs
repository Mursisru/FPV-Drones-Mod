using System.Reflection;
using UnityEngine;

namespace FPVMod.Access
{
    internal static class FpvMissileAccess
    {
        private static readonly FieldInfo? InputsField =
            typeof(Missile).GetField("inputs", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? ThrottleField =
            typeof(Missile).GetField("throttle", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private static readonly FieldInfo? TorqueField =
            typeof(Missile).GetField("torque", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? BlastYieldField =
            typeof(Missile).GetField("blastYield", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private static readonly FieldInfo? ProxyFuseField =
            typeof(Missile).GetField("proxyFuse", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? SeekerField =
            typeof(Missile).GetField("seeker", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? UprightField =
            typeof(Missile).GetField("uprightPreference", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private static readonly FieldInfo? MaxTurnRateField =
            typeof(Missile).GetField("maxTurnRate", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private static readonly FieldInfo? GLimitField =
            typeof(Missile).GetField("gLimit", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private static readonly MethodInfo? ApplyAeroMethod =
            typeof(Missile).GetMethod("ApplyAero", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo? DetectCollisionsMethod =
            typeof(Missile).GetMethod("DetectCollisions", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo? UpdateRadarAltMethod =
            typeof(Missile).GetMethod("UpdateRadarAlt", BindingFlags.Instance | BindingFlags.Public);

        internal static void SetInputs(Missile m, Vector3 inputs) =>
            InputsField?.SetValue(m, inputs);

        internal static Vector3 GetInputs(Missile m) =>
            InputsField != null ? (Vector3)InputsField.GetValue(m)! : Vector3.zero;

        internal static void SetThrottle(Missile m, float t) =>
            ThrottleField?.SetValue(m, t);

        internal static void SetTorque(Missile m, float torque) =>
            TorqueField?.SetValue(m, torque);

        internal static void SetBlastYield(Missile m, float yield) =>
            BlastYieldField?.SetValue(m, yield);

        internal static void DisableProxyFuse(Missile m)
        {
            // proxyFuse is Missile.ProxyFuse (class), NOT bool — SetValue(false) throws ArgumentException
            // and aborts launch before FpvControlSession.Begin.
            if (ProxyFuseField == null || m == null)
                return;
            ProxyFuseField.SetValue(m, null);
        }

        internal static void ClearSeeker(Missile m) =>
            SeekerField?.SetValue(m, null);

        internal static void ZeroUpright(Missile m) =>
            UprightField?.SetValue(m, 0f);

        internal static void RelaxLimits(Missile m)
        {
            MaxTurnRateField?.SetValue(m, 999f);
            GLimitField?.SetValue(m, 99f);
        }

        internal static void CallApplyAero(Missile m) =>
            ApplyAeroMethod?.Invoke(m, null);

        internal static void CallDetectCollisions(Missile m) =>
            DetectCollisionsMethod?.Invoke(m, null);

        internal static void CallUpdateRadarAlt(Missile m) =>
            UpdateRadarAltMethod?.Invoke(m, null);
    }
}
