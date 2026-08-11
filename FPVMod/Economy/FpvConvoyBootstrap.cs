using System.Collections.Generic;
using System.Reflection;
using FPVMod.Bootstrap;
using UnityEngine;

namespace FPVMod.Economy
{
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

            VehicleDefinition? sam = Encyclopedia.i?.vehicles?.Find(v =>
                v != null && !string.IsNullOrEmpty(v.jsonKey) &&
                v.jsonKey.IndexOf("SAM", System.StringComparison.OrdinalIgnoreCase) >= 0);

            var package = new Faction.ConvoyGroup
            {
                Name = FpvConstants.ConvoyName,
                Constituents = new List<Faction.ConvoyUnit>
                {
                    new() { Type = DefinitionRegistrar.LauncherDefinition, Count = 1 },
                    new() { Type = FindAmmoTruck(), Count = 1 }
                }
            };

            if (sam != null)
            {
                package.Constituents.Add(new Faction.ConvoyUnit { Type = sam, Count = 1 });
                package.Constituents.Add(new Faction.ConvoyUnit { Type = sam, Count = 1 });
            }

            groups.Add(package);
        }

        private static UnitDefinition FindAmmoTruck()
        {
            VehicleDefinition? truck = Encyclopedia.i?.vehicles?.Find(v =>
                v != null && v.jsonKey != null &&
                (v.jsonKey.IndexOf("truck", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                 v.jsonKey.IndexOf("munition", System.StringComparison.OrdinalIgnoreCase) >= 0));
            return truck ?? DefinitionRegistrar.LauncherDefinition!;
        }
    }
}
