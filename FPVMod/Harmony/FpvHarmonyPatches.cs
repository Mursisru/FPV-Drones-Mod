using System;
using FPVMod.Bootstrap;
using FPVMod.Drone;
using FPVMod.Economy;
using FPVMod.Launcher;
using FPVMod.Network;
using FPVMod.Session;
using HarmonyLib;
using NuclearOption.MissionEditorScripts.Buttons;
using NuclearOption.Networking;
using NuclearOption.SavedMission;
using UnityEngine;

namespace FPVMod.HarmonyPatches
{
    [HarmonyPatch(typeof(Encyclopedia), "AfterLoad", new Type[] { })]
    internal static class EncyclopediaAfterLoadInstancePatch
    {
        private static void Postfix(Encyclopedia __instance) => FpvBootstrap.Run(__instance);
    }

    [HarmonyPatch(typeof(Encyclopedia), "AfterLoad", new Type[] { typeof(Encyclopedia) })]
    internal static class EncyclopediaAfterLoadStaticPatch
    {
        private static void Postfix(Encyclopedia instance) => FpvBootstrap.Run(instance);
    }

    internal static class FpvBootstrap
    {
        internal static void Run(Encyclopedia enc)
        {
            try
            {
                DefinitionRegistrar.TryRegister(enc);
                FpvConvoyBootstrap.TryInject();
            }
            catch (Exception ex)
            {
                FpvPlugin.ModLogger?.LogError($"FPV AfterLoad: {ex}");
            }
        }
    }

    /// <summary>Tracks mission-editor placing definition (vanilla Instantate keeps stock unit.definition).</summary>
    internal static class FpvPendingPlace
    {
        private static UnitDefinition? _def;

        internal static UnitDefinition? Current => _def;
        internal static bool IsLauncher => DefinitionRegistrar.IsFpvLauncher(_def);
        internal static bool IsDrone => DefinitionRegistrar.IsFpvDrone(_def);

        internal static void Begin(UnitDefinition? def) => _def = def;
        internal static void End() => _def = null;
    }

    [HarmonyPatch(typeof(Spawner), nameof(Spawner.TrySpawnVehicle))]
    internal static class SpawnerTrySpawnVehiclePatch
    {
        private static void Postfix(SavedVehicle savedVehicle, GroundVehicle spawnedVehicle, bool __result)
        {
            if (!__result || spawnedVehicle == null || savedVehicle == null)
                return;
            if (savedVehicle.type == FpvConstants.LauncherDefKey)
                PrefabFactory.StampLauncherInstance(spawnedVehicle.gameObject);
        }
    }

    [HarmonyPatch(typeof(Spawner), "TrySpawnMissile")]
    internal static class SpawnerTrySpawnMissilePatch
    {
        private static void Postfix(SavedMissile savedMissile, Missile spawnedMissile, bool __result)
        {
            if (!__result || spawnedMissile == null || savedMissile == null)
                return;
            if (savedMissile.type == FpvConstants.DroneDefKey)
                PrefabFactory.StampDroneInstance(spawnedMissile.gameObject);
        }
    }

    [HarmonyPatch(typeof(Spawner), nameof(Spawner.SpawnVehicle))]
    internal static class SpawnerSpawnVehiclePatch
    {
        private static void Postfix(GroundVehicle __result)
        {
            if (__result == null)
                return;
            if (DefinitionRegistrar.IsFpvLauncher(__result.definition) || FpvPendingPlace.IsLauncher)
                PrefabFactory.StampLauncherInstance(__result.gameObject);
        }
    }

    [HarmonyPatch(typeof(Spawner), nameof(Spawner.SpawnSavedMissile))]
    internal static class SpawnerSpawnSavedMissilePatch
    {
        private static void Postfix(Missile __result)
        {
            if (__result == null)
                return;
            if (DefinitionRegistrar.IsFpvDrone(__result.definition) || FpvPendingPlace.IsDrone)
                PrefabFactory.StampDroneInstance(__result.gameObject);
        }
    }

    [HarmonyPatch(typeof(Spawner), nameof(Spawner.SpawnMissile), new[] { typeof(MissileDefinition), typeof(Vector3), typeof(Quaternion), typeof(Vector3), typeof(Unit), typeof(Unit) })]
    internal static class SpawnerSpawnMissileDefPatch
    {
        private static void Postfix(MissileDefinition missile, Missile __result)
        {
            if (__result == null)
                return;
            if (DefinitionRegistrar.IsFpvDrone(missile) || DefinitionRegistrar.IsFpvDrone(__result.definition))
                PrefabFactory.StampDroneInstance(__result.gameObject);
        }
    }

    [HarmonyPatch(typeof(Spawner), nameof(Spawner.SpawnMissile), new[] { typeof(GameObject), typeof(Vector3), typeof(Quaternion), typeof(Vector3), typeof(Unit), typeof(Unit) })]
    internal static class SpawnerSpawnMissileGoPatch
    {
        private static void Postfix(Missile __result)
        {
            if (__result == null)
                return;
            if (DefinitionRegistrar.IsFpvDrone(__result.definition) || __result.GetComponent<FpvDroneTag>() != null)
                PrefabFactory.StampDroneInstance(__result.gameObject);
        }
    }

    [HarmonyPatch(typeof(Spawner), nameof(Spawner.SpawnMissileEncyclopedia))]
    internal static class SpawnerSpawnMissileEncPatch
    {
        private static void Postfix(MissileDefinition missile, Missile __result)
        {
            if (__result == null)
                return;
            if (DefinitionRegistrar.IsFpvDrone(missile))
                PrefabFactory.StampDroneInstance(__result.gameObject);
        }
    }

    [HarmonyPatch(typeof(Spawner), nameof(Spawner.SpawnUnit))]
    internal static class SpawnerSpawnUnitPatch
    {
        private static void Postfix(UnitDefinition unit, Unit __result)
        {
            if (__result == null || unit == null)
                return;
            PrefabFactory.StampByDefinition(__result.gameObject, unit);
        }
    }

    [HarmonyPatch(typeof(Spawner), nameof(Spawner.SpawnFromUnitDefinitionInEditor))]
    internal static class SpawnerEditorPlacePatch
    {
        private static void Prefix(UnitDefinition placingDefinition)
        {
            FpvPendingPlace.Begin(placingDefinition);
        }

        private static void Postfix(UnitDefinition placingDefinition, Unit __result)
        {
            try
            {
                if (__result != null && placingDefinition != null)
                    PrefabFactory.StampByDefinition(__result.gameObject, placingDefinition);
            }
            finally
            {
                FpvPendingPlace.End();
            }
        }
    }

    [HarmonyPatch(typeof(NewUnitPanel), "SpawnUnit")]
    internal static class NewUnitPanelSpawnUnitPatch
    {
        private static void Prefix(UnitDefinition unitDefinition)
        {
            FpvPendingPlace.Begin(unitDefinition);
        }

        private static void Postfix(UnitDefinition unitDefinition, NewUnitPanel __instance)
        {
            try
            {
                var field = AccessTools.Field(typeof(NewUnitPanel), "placingObject");
                if (field?.GetValue(__instance) is GameObject go)
                    PrefabFactory.StampByDefinition(go, unitDefinition);
            }
            catch
            {
                // ignore
            }
        }
    }

    [HarmonyPatch(typeof(NewUnitPanel), "CancelPlace")]
    internal static class NewUnitPanelCancelPlacePatch
    {
        private static void Postfix() => FpvPendingPlace.End();
    }

    [HarmonyPatch(typeof(Missile), "ServerFixedUpdate")]
    internal static class MissileServerFixedUpdatePatch
    {
        private static bool Prefix(Missile __instance)
        {
            if (__instance.GetComponent<FpvDroneTag>() == null)
                return true;

            FpvAcroController? ctrl = __instance.GetComponent<FpvAcroController>();
            if (ctrl == null)
                return true;

            if (FpvControlSession.IsControlling(__instance) || __instance.LocalSim)
                ctrl.ApplyFlight(__instance);

            return false;
        }
    }

    [HarmonyPatch(typeof(UnitMapIcon), nameof(UnitMapIcon.ClickIcon))]
    internal static class UnitMapIconClickPatch
    {
        private static void Postfix(UnitMapIcon __instance)
        {
            try
            {
                Unit? unit = __instance.unit;
                if (unit == null)
                    return;
                FpvLauncher? launcher = unit.GetComponentInChildren<FpvLauncher>();
                if (launcher == null)
                    return;

                if (GameManager.GetLocalHQ(out FactionHQ? hq) && unit.NetworkHQ != hq)
                    return;

                FpvMapLaunchPanelTarget.Current = launcher;
                FpvMapLaunchPanel.Show(launcher);
            }
            catch
            {
                // ignore
            }
        }
    }

    [HarmonyPatch(typeof(PilotPlayerState), "PlayerAxisControls")]
    internal static class PilotAxisBlockPatch
    {
        private static bool Prefix() => !FpvControlSession.Active;
    }

    [HarmonyPatch(typeof(UnitPart), nameof(UnitPart.TakeDamage))]
    internal static class FpvLaserVulnerabilityPatch
    {
        private static void Prefix(UnitPart __instance, ref float blastDamage, ref float fireDamage)
        {
            if (__instance.GetComponentInParent<FpvDroneTag>() == null)
                return;
            blastDamage *= 3f;
            fireDamage *= 3f;
        }
    }
}
