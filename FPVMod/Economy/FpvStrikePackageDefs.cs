using System.Collections.Generic;
using FPVMod.Bootstrap;
using UnityEngine;

namespace FPVMod.Economy
{
    /// <summary>Shared resolve of Strike Package constituents (launcher + R_SAM + Rearmer).</summary>
    internal static class FpvStrikePackageDefs
    {
        internal static VehicleDefinition? Launcher => DefinitionRegistrar.LauncherDefinition;

        internal static VehicleDefinition? BestRadarSam()
        {
            VehicleDefinition? best = null;
            float bestValue = float.MinValue;
            List<VehicleDefinition>? vehicles = Encyclopedia.i?.vehicles;
            if (vehicles == null)
                return null;

            for (int i = 0; i < vehicles.Count; i++)
            {
                VehicleDefinition? v = vehicles[i];
                if (v == null || v.vehicleType != VehicleType.R_SAM)
                    continue;
                if (v.value >= bestValue)
                {
                    bestValue = v.value;
                    best = v;
                }
            }

            return best;
        }

        internal static VehicleDefinition? AmmoTruck()
        {
            VehicleDefinition? best = null;
            float bestValue = float.MinValue;
            List<VehicleDefinition>? vehicles = Encyclopedia.i?.vehicles;
            if (vehicles == null)
                return null;

            for (int i = 0; i < vehicles.Count; i++)
            {
                VehicleDefinition? v = vehicles[i];
                if (v == null || v.unitPrefab == null)
                    continue;
                if (v.vehicleType == VehicleType.R_SAM || v.vehicleType == VehicleType.IR_SAM ||
                    v.vehicleType == VehicleType.AAA || v.vehicleType == VehicleType.ART ||
                    v.vehicleType == VehicleType.MBT || v.vehicleType == VehicleType.AFV)
                    continue;
                if (DefinitionRegistrar.IsFpvLauncher(v))
                    continue;

                if (v.unitPrefab.GetComponentInChildren<Rearmer>(true) == null)
                    continue;

                if (v.value >= bestValue)
                {
                    bestValue = v.value;
                    best = v;
                }
            }

            return best;
        }

        internal static void AddPackageToSupply(FactionHQ hq, int packages)
        {
            if (hq == null || packages <= 0)
                return;

            VehicleDefinition? launcher = Launcher;
            if (launcher == null)
                return;

            try
            {
                hq.ModifyUnitSupply(launcher, packages);

                VehicleDefinition? sam = BestRadarSam();
                if (sam != null)
                    hq.ModifyUnitSupply(sam, packages);

                VehicleDefinition? ammo = AmmoTruck();
                if (ammo != null)
                    hq.ModifyUnitSupply(ammo, packages);
            }
            catch (System.Exception ex)
            {
                FpvPlugin.ModLogger?.LogWarning($"FpvStrikePackageDefs.AddPackage: {ex.Message}");
            }
        }
    }
}
