using System.Collections.Generic;
using System.Reflection;
using FPVMod.Bootstrap;
using UnityEngine;

namespace FPVMod.Economy
{
    /// <summary>Player Contribute UI only — AI uses VehicleSupply via FpvMissionSupplyInject.</summary>
    internal static class FpvConvoyBootstrap
    {
        private static bool _done;

        internal static void TryInject()
        {
            if (_done || DefinitionRegistrar.LauncherDefinition == null || DefinitionRegistrar.DroneDefinition == null)
                return;

            try
            {
                foreach (Faction faction in Resources.FindObjectsOfTypeAll<Faction>())
                {
                    if (faction == null)
                        continue;
                    InjectIntoFaction(faction);
                }
                _done = true;
                FpvPlugin.ModLogger?.LogInfo("FPV Strike Package convoy injected.");
            }
            catch (System.Exception ex)
            {
                FpvPlugin.ModLogger?.LogError($"FpvConvoyBootstrap: {ex}");
            }
        }

        private static void InjectIntoFaction(Faction faction)
        {
            FieldInfo? field = typeof(Faction).GetField("convoyGroups", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field?.GetValue(faction) is not List<Faction.ConvoyGroup> groups)
                return;

            foreach (Faction.ConvoyGroup g in groups)
            {
                if (g != null && g.Name == FpvConstants.ConvoyName)
                    return;
            }

            var package = new Faction.ConvoyGroup
            {
                Name = FpvConstants.ConvoyName,
                Constituents = new List<Faction.ConvoyUnit>
                {
                    new() { Type = DefinitionRegistrar.LauncherDefinition, Count = 1 }
                }
            };

            VehicleDefinition? sam = FpvStrikePackageDefs.BestRadarSam();
            if (sam != null)
                package.Constituents.Add(new Faction.ConvoyUnit { Type = sam, Count = 1 });

            VehicleDefinition? ammo = FpvStrikePackageDefs.AmmoTruck();
            if (ammo != null)
                package.Constituents.Add(new Faction.ConvoyUnit { Type = ammo, Count = 1 });

            groups.Add(package);
        }
    }
}
