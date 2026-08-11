using FPVMod.Bootstrap;
using System.Collections.Generic;
using UnityEngine;

namespace FPVMod.Launcher
{
    /// <summary>Map tooltip / weapon station readout: drones instead of MLRS rockets.</summary>
    internal static class FpvLauncherAmmoUi
    {
        private static readonly Dictionary<int, WeaponInfo> InfoClones = new();

        internal static void Sync(Unit? unit, FpvLauncher launcher)
        {
            if (unit == null || launcher == null || unit.weaponStations == null)
                return;

            string droneName = DefinitionRegistrar.DroneDefinition?.unitName ?? "FPV Kamikaze Drone";
            int ammo = launcher.Ammo;
            int capacity = FpvConstants.LauncherCapacity;
            bool reloading = launcher.GetState() == FpvLauncherState.Cooldown;

            foreach (WeaponStation station in unit.weaponStations)
            {
                if (station == null)
                    continue;

                station.Ammo = ammo;
                station.FullAmmo = capacity;
                station.Reloading = reloading;

                WeaponInfo? info = CloneDroneWeaponInfo(station.WeaponInfo, droneName);
                if (info != null)
                    station.WeaponInfo = info;
            }
        }

        private static WeaponInfo? CloneDroneWeaponInfo(WeaponInfo? source, string droneName)
        {
            if (source == null)
                return null;

            int key = source.GetInstanceID();
            if (!InfoClones.TryGetValue(key, out WeaponInfo? clone) || clone == null)
            {
                clone = Object.Instantiate(source);
                clone.name = "FPV_DroneWeaponInfo";
                InfoClones[key] = clone;
            }

            clone.weaponName = droneName;
            clone.shortName = "FPV Drone";
            return clone;
        }
    }
}
