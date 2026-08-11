using FPVMod.Launcher;
using UnityEngine;

namespace FPVMod.Economy
{
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
                TryResupplyFromNearbyStorage(launcher);
            }
        }

        private static void TryResupplyFromNearbyStorage(FpvLauncher launcher)
        {
            Unit? owner = launcher.OwnerUnit;
            if (owner == null)
                return;

            foreach (UnitStorage storage in Object.FindObjectsOfType<UnitStorage>())
            {
                if (storage == null)
                    continue;
                Unit? storageUnit = storage.GetComponentInParent<Unit>();
                if (storageUnit == null)
                    continue;
                if (FastMath.Distance(owner.GlobalPosition(), storageUnit.GlobalPosition()) > 30f)
                    continue;
                if (Encyclopedia.Lookup == null ||
                    !Encyclopedia.Lookup.TryGetValue(FpvConstants.DroneDefKey, out UnitDefinition? droneDef) ||
                    !storage.ContainsUnit(droneDef))
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
