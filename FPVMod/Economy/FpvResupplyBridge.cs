using FPVMod.Launcher;
using UnityEngine;

namespace FPVMod.Economy
{
    /// <summary>
    /// Refill FPV launcher ammo from nearby UnitStorage (drones) or same-HQ Rearmer truck.
    /// </summary>
    internal static class FpvResupplyBridge
    {
        private static float _nextScan;

        internal static void Tick()
        {
            if (Time.unscaledTime < _nextScan)
                return;
            _nextScan = Time.unscaledTime + 2f;

            foreach (FpvLauncher launcher in Object.FindObjectsOfType<FpvLauncher>())
            {
                if (launcher == null || launcher.Ammo >= FpvConstants.LauncherCapacity)
                    continue;
                if (TryResupplyFromNearbyStorage(launcher))
                    continue;
                TryResupplyFromNearbyRearmer(launcher);
            }
        }

        private static bool TryResupplyFromNearbyStorage(FpvLauncher launcher)
        {
            Unit? owner = launcher.OwnerUnit;
            if (owner == null)
                return false;

            foreach (UnitStorage storage in Object.FindObjectsOfType<UnitStorage>())
            {
                if (storage == null)
                    continue;
                Unit? storageUnit = storage.GetComponentInParent<Unit>();
                if (storageUnit == null)
                    continue;
                if (FastMath.Distance(owner.GlobalPosition(), storageUnit.GlobalPosition()) >
                    FpvConstants.BatteryResupplyRadiusM)
                    continue;
                if (Encyclopedia.Lookup == null ||
                    !Encyclopedia.Lookup.TryGetValue(FpvConstants.DroneDefKey, out UnitDefinition? droneDef) ||
                    !storage.ContainsUnit(droneDef))
                    continue;

                int need = FpvConstants.LauncherCapacity - launcher.Ammo;
                if (need <= 0)
                    return true;
                launcher.AddAmmo(need);
                return true;
            }

            return false;
        }

        /// <summary>Any friendly Rearmer within radius → top up mod ammo (no Instantate Missile).</summary>
        private static void TryResupplyFromNearbyRearmer(FpvLauncher launcher)
        {
            Unit? owner = launcher.OwnerUnit;
            if (owner == null || owner.NetworkHQ == null)
                return;

            foreach (Rearmer rearmer in Object.FindObjectsOfType<Rearmer>())
            {
                if (rearmer == null)
                    continue;
                Unit? u = rearmer.GetComponentInParent<Unit>();
                if (u == null || u.disabled || u.NetworkHQ != owner.NetworkHQ)
                    continue;
                if (FastMath.Distance(owner.GlobalPosition(), u.GlobalPosition()) >
                    FpvConstants.BatteryResupplyRadiusM)
                    continue;

                int need = FpvConstants.LauncherCapacity - launcher.Ammo;
                if (need <= 0)
                    return;
                launcher.AddAmmo(need);
                return;
            }
        }
    }
}
