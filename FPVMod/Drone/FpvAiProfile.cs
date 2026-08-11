using System;
using System.Reflection;
using FPVMod.Access;
using UnityEngine;

namespace FPVMod.Drone
{
    internal static class FpvAiProfile
    {
        internal static void Apply(Missile missile, Unit owner)
        {
            if (missile == null)
                return;

            // Each step isolated — one bad field must not abort possession.
            Try("blast", () => FpvMissileAccess.SetBlastYield(missile, FpvConstants.BlastYieldKg));
            Try("proxy", () => FpvMissileAccess.DisableProxyFuse(missile));
            Try("seeker", () => FpvMissileAccess.ClearSeeker(missile));
            Try("upright", () => FpvMissileAccess.ZeroUpright(missile));
            Try("limits", () => FpvMissileAccess.RelaxLimits(missile));
            Try("torque", () => FpvMissileAccess.SetTorque(missile, 0f)); // no missile fin torque — FPV rates own
            Try("motors", () => FpvMotorKill.KillAll(missile));

            try
            {
                missile.maxRadius = FpvConstants.DroneMaxRadius;
                missile.SetTarget(null);
            }
            catch (Exception ex)
            {
                FpvPlugin.ModLogger?.LogWarning($"FpvAiProfile target: {ex.Message}");
            }

            Try("ir", () =>
            {
                FieldInfo? irField = typeof(Unit).GetField("IRSources",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (irField?.GetValue(missile) is System.Collections.IList list)
                    list.Clear();
            });

            Try("armor", () =>
            {
                foreach (UnitPart part in missile.GetComponentsInChildren<UnitPart>(true))
                {
                    part.hitPoints = FpvConstants.HitPoints;
                    ArmorProperties armor = part.GetArmorProperties();
                    armor.pierceArmor = FpvConstants.PierceArmor;
                    armor.pierceTolerance = FpvConstants.PierceTolerance;
                }
            });

            if (missile.definition is MissileDefinition md)
                md.radarSize = FpvConstants.RadarSize;

            if (owner != null)
            {
                try { missile.NetworkHQ = owner.NetworkHQ; }
                catch (Exception ex) { FpvPlugin.ModLogger?.LogWarning($"FpvAiProfile HQ: {ex.Message}"); }
            }
        }

        private static void Try(string step, Action action)
        {
            try { action(); }
            catch (Exception ex)
            {
                FpvPlugin.ModLogger?.LogWarning($"FpvAiProfile.{step}: {ex.Message}");
            }
        }
    }
}
