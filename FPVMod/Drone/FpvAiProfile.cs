using System.Reflection;
using FPVMod.Access;
using FPVMod.Launcher;
using UnityEngine;

namespace FPVMod.Drone
{
    internal static class FpvAiProfile
    {
        internal static void Apply(Missile missile, Unit owner)
        {
            if (missile == null)
                return;

            FpvMissileAccess.SetBlastYield(missile, FpvConstants.BlastYieldKg);
            FpvMissileAccess.DisableProxyFuse(missile);
            FpvMissileAccess.ClearSeeker(missile);
            FpvMissileAccess.ZeroUpright(missile);
            FpvMissileAccess.RelaxLimits(missile);
            FpvMissileAccess.SetTorque(missile, FpvConstants.AcroTorque);

            missile.maxRadius = FpvConstants.DroneMaxRadius;
            missile.SetTarget(null);

            FieldInfo? irField = typeof(Unit).GetField("IRSources",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (irField?.GetValue(missile) is System.Collections.IList list)
                list.Clear();

            foreach (UnitPart part in missile.GetComponentsInChildren<UnitPart>(true))
            {
                part.hitPoints = FpvConstants.HitPoints;
                ArmorProperties armor = part.GetArmorProperties();
                armor.pierceArmor = FpvConstants.PierceArmor;
                armor.pierceTolerance = FpvConstants.PierceTolerance;
            }

            if (missile.definition is MissileDefinition md)
                md.radarSize = FpvConstants.RadarSize;

            if (owner != null)
                missile.NetworkHQ = owner.NetworkHQ;
        }
    }
}
