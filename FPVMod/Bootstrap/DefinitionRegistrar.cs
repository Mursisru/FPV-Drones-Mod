using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using NuclearOption.MissionEditorScripts.Buttons;
using UnityEngine;

namespace FPVMod.Bootstrap
{
    /// <summary>RC-style: clone definitions only; unitPrefab stays vanilla Mirage-registered.</summary>
    internal static class DefinitionRegistrar
    {
        internal static MissileDefinition? DroneDefinition { get; private set; }
        internal static VehicleDefinition? LauncherDefinition { get; private set; }
        internal static MissileDefinition? DroneTemplate { get; private set; }
        internal static VehicleDefinition? LauncherTemplate { get; private set; }

        private static readonly FieldInfo? DisabledField =
            typeof(UnitDefinition).GetField("disabled", BindingFlags.Instance | BindingFlags.NonPublic);

        private static bool _done;

        internal static bool TryRegister(Encyclopedia enc)
        {
            if (enc == null)
                return false;

            try
            {
                LauncherTemplate = FpvPrefabUtil.ResolveLauncherTemplate(enc);
                DroneTemplate = FpvPrefabUtil.ResolveDroneTemplate(enc);
                if (LauncherTemplate?.unitPrefab == null || DroneTemplate?.unitPrefab == null)
                {
                    FpvPlugin.ModLogger?.LogWarning("DefinitionRegistrar: vanilla templates not ready.");
                    return false;
                }

                DroneDefinition = RegisterDrone(enc, DroneTemplate);
                LauncherDefinition = RegisterLauncher(enc, LauncherTemplate);
                _done = DroneDefinition != null && LauncherDefinition != null;
                if (_done)
                    FpvEditorListRefresh.TryRefresh();
                return _done;
            }
            catch (Exception ex)
            {
                FpvPlugin.ModLogger?.LogError($"DefinitionRegistrar: {ex}");
                return false;
            }
        }

        private static MissileDefinition? RegisterDrone(Encyclopedia enc, MissileDefinition template)
        {
            if (Encyclopedia.Lookup != null &&
                Encyclopedia.Lookup.TryGetValue(FpvConstants.DroneDefKey, out UnitDefinition existing) &&
                existing is MissileDefinition md)
            {
                md.unitPrefab = template.unitPrefab;
                md.spawnOffset = Vector3.zero;
                md.length = 2.5f;
                md.width = 2.5f;
                md.height = 0.8f;
                md.mass = FpvConstants.DroneMassKg;
                DisabledField?.SetValue(md, false);
                DroneDefinition = md;
                return md;
            }

            MissileDefinition def = UnityEngine.Object.Instantiate(template);
            def.name = "FPV_Drone_Definition";
            def.jsonKey = FpvConstants.DroneDefKey;
            def.unitName = "FPV Kamikaze Drone";
            def.bogeyName = "FPV Drone";
            def.description = "Manually controlled FPV loitering munition. 50 kg AUW, 40 kg warhead, 5 min battery.";
            def.value = FpvConstants.DroneCost;
            def.radarSize = FpvConstants.RadarSize;
            def.length = 2.5f;
            def.width = 2.5f;
            def.height = 0.8f;
            def.mass = FpvConstants.DroneMassKg;
            def.spawnOffset = Vector3.zero;
            def.unitPrefab = template.unitPrefab;
            def.mapIconSize = 0.6f;
            DisabledField?.SetValue(def, false);

            AddToEncyclopedia(enc, def, enc.missiles);
            FpvPlugin.ModLogger?.LogInfo($"Registered {def.unitName} ({def.jsonKey}) prefab={template.jsonKey}");
            return def;
        }

        private static VehicleDefinition? RegisterLauncher(Encyclopedia enc, VehicleDefinition template)
        {
            if (Encyclopedia.Lookup != null &&
                Encyclopedia.Lookup.TryGetValue(FpvConstants.LauncherDefKey, out UnitDefinition existing) &&
                existing is VehicleDefinition vd)
            {
                vd.unitPrefab = template.unitPrefab;
                DisabledField?.SetValue(vd, false);
                LauncherDefinition = vd;
                return vd;
            }

            VehicleDefinition def = UnityEngine.Object.Instantiate(template);
            def.name = "FPV_Launcher_Definition";
            def.jsonKey = FpvConstants.LauncherDefKey;
            def.unitName = "MSV Drone Launcher";
            def.bogeyName = "FPV Launcher";
            def.description = "Mobile FPV drone launch complex based on MSV MLRS chassis. Holds 8 drones, 6s launch cooldown.";
            def.value = FpvConstants.LauncherCost;
            def.unitPrefab = template.unitPrefab;
            def.mapIconSize = template.mapIconSize > 0f ? template.mapIconSize : 1.2f;
            def.mapIcon = template.mapIcon;
            def.friendlyIcon = template.friendlyIcon;
            def.hostileIcon = template.hostileIcon;
            def.visibleRange = template.visibleRange;
            def.iconRange = template.iconRange;
            def.vehicleType = template.vehicleType;
            def.typeIdentity = template.typeIdentity;
            def.roleIdentity = template.roleIdentity;
            def.length = template.length;
            def.width = template.width;
            def.height = template.height;
            def.mass = template.mass;
            DisabledField?.SetValue(def, false);

            AddToEncyclopedia(enc, def, enc.vehicles);
            FpvPlugin.ModLogger?.LogInfo($"Registered {def.unitName} ({def.jsonKey}) prefab={template.jsonKey}");
            return def;
        }

        private static void AddToEncyclopedia<T>(Encyclopedia enc, T def, List<T>? list) where T : UnitDefinition
        {
            if (list == null)
                return;
            if (!list.Contains(def))
                list.Add(def);

            if (Encyclopedia.Lookup == null)
                Encyclopedia.Lookup = new Dictionary<string, UnitDefinition>();
            Encyclopedia.Lookup[def.jsonKey] = def;

            if (enc.IndexLookup == null)
                enc.IndexLookup = new List<INetworkDefinition>();
            if (!enc.IndexLookup.Contains(def))
            {
                typeof(INetworkDefinition).GetProperty("LookupIndex")?.SetValue(def, (int?)enc.IndexLookup.Count);
                enc.IndexLookup.Add(def);
            }
        }

        internal static bool IsFpvLauncher(UnitDefinition? def) =>
            def != null && def.jsonKey == FpvConstants.LauncherDefKey;

        internal static bool IsFpvDrone(UnitDefinition? def) =>
            def != null && def.jsonKey == FpvConstants.DroneDefKey;

        internal static bool IsFpvLauncherUnit(Unit? unit) =>
            unit != null && (unit.GetComponent<Launcher.FpvLauncher>() != null || IsFpvLauncher(unit.definition));

        internal static bool IsFpvDroneUnit(Unit? unit) =>
            unit != null && (unit.GetComponent<Drone.FpvDroneTag>() != null || IsFpvDrone(unit.definition));

        /// <summary>Tag, fuse, def key, or unitName — works on clients before/without stamp.</summary>
        internal static bool IsFpvMissile(Missile? missile)
        {
            if (missile == null)
                return false;
            if (missile.GetComponent<Drone.FpvDroneTag>() != null)
                return true;
            if (missile.GetComponent<Drone.FpvWarhead>() != null)
                return true;
            if (IsFpvDrone(missile.definition))
                return true;
            try
            {
                string? name = missile.unitName;
                if (!string.IsNullOrEmpty(name) &&
                    name.IndexOf("FPV", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            catch
            {
                // ignore
            }
            return false;
        }

        /// <summary>Soft reset — keep Lookup defs; only clear session flags.</summary>
        internal static void SoftReset()
        {
            _done = false;
        }
    }

    /// <summary>Rebuild NewUnitPanel dropdown snapshots after late registration.</summary>
    internal static class FpvEditorListRefresh
    {
        private static bool _refreshed;

        internal static void TryRefresh()
        {
            try
            {
                FieldInfo? field = AccessTools.Field(typeof(NewUnitPanel), "unitProviders");
                if (field?.GetValue(null) is not System.Collections.IDictionary dict || dict.Count == 0)
                    return;

                Encyclopedia? enc = Encyclopedia.i;
                if (enc == null)
                    return;

                Type? providerType = AccessTools.Inner(typeof(NewUnitPanel), "UnitOptionProvider");
                MethodInfo? create = providerType?.GetMethod("Create", BindingFlags.Public | BindingFlags.Static);
                if (create == null)
                    return;

                Type sortModeType = providerType!.GetNestedType("SortMode");
                object sortName = Enum.Parse(sortModeType, "Name");

                MethodInfo createVehicle = create.MakeGenericMethod(typeof(VehicleDefinition));
                MethodInfo createMissile = create.MakeGenericMethod(typeof(MissileDefinition));

                dict["vehicles"] = createVehicle.Invoke(null, new object[] { enc.vehicles, sortName });
                dict["missiles"] = createMissile.Invoke(null, new object[] { enc.missiles, sortName });

                if (!_refreshed)
                {
                    _refreshed = true;
                    FpvPlugin.ModLogger?.LogInfo("FPVMod: refreshed NewUnitPanel unitProviders.");
                }
            }
            catch (Exception ex)
            {
                FpvPlugin.ModLogger?.LogWarning($"FPVMod: unitProviders refresh failed: {ex.Message}");
            }
        }

        internal static void ResetFlag() => _refreshed = false;
    }
}
