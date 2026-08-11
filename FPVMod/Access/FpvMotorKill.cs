using System;
using System.Reflection;
using UnityEngine;

namespace FPVMod.Access
{
    /// <summary>Kill stock bomb/missile rocket motors — Burnout() only stops FX, not thrust.</summary>
    internal static class FpvMotorKill
    {
        private static readonly FieldInfo? MotorsField =
            typeof(Missile).GetField("motors", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? MotorField =
            typeof(Missile).GetField("motor", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? MotorStageField =
            typeof(Missile).GetField("motorStage", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo? EngineThrustField =
            typeof(Missile).GetField("engineCurrentThrust", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static void KillAll(Missile? missile)
        {
            if (missile == null)
                return;

            try
            {
                if (MotorsField?.GetValue(missile) is Array motors)
                {
                    for (int i = 0; i < motors.Length; i++)
                    {
                        object? m = motors.GetValue(i);
                        if (m == null)
                            continue;

                        Type t = m.GetType();
                        t.GetField("fuelMass")?.SetValue(m, 0f);
                        t.GetField("thrust")?.SetValue(m, 0f);
                        t.GetMethod("Burnout")?.Invoke(m, new object[] { true });
                    }

                    MotorStageField?.SetValue(missile, motors.Length);
                }

                MotorField?.SetValue(missile, null);
                EngineThrustField?.SetValue(missile, 0f);
            }
            catch (Exception ex)
            {
                FpvPlugin.ModLogger?.LogWarning($"FpvMotorKill: {ex.Message}");
            }
        }
    }
}
