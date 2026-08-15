using System.Collections.Generic;
using FPVMod.Bootstrap;
using FPVMod.Launcher;
using NuclearOption.SavedMission;
using UnityEngine;

namespace FPVMod.Economy
{
    /// <summary>
    /// Puts FPV Strike Package into vanilla VehicleSupply.
    /// Deploy path stays stock: DeployVehicles → VehicleDepot → GroundVehicle AI.
    /// </summary>
    internal static class FpvMissionSupplyInject
    {
        private static readonly HashSet<int> SeededHqIds = new();
        private static float _nextTick;

        internal static void OnMissionLoad(FactionHQ hq, Mission? mission)
        {
            if (hq == null)
                return;
            // Defs may not be ready yet — TickEnsure will retry.
            TrySeed(hq, force: true);
        }

        /// <summary>Host tick: seed missing HQs + soft replenish empty supply.</summary>
        internal static void TickReplenish()
        {
            if (Time.unscaledTime < _nextTick)
                return;
            _nextTick = Time.unscaledTime + 5f;

            if (!CanRun())
                return;

            EnsureDefs();
            if (DefinitionRegistrar.LauncherDefinition == null)
                return;

            foreach (FactionHQ hq in FactionRegistry.GetAllHQs())
            {
                if (hq == null || !hq.IsServer)
                    continue;

                if (!SeededHqIds.Contains(hq.GetInstanceID()))
                {
                    TrySeed(hq, force: false);
                    continue;
                }

                // Soft replenish for long Escalation when stock drained.
                VehicleDefinition launcher = DefinitionRegistrar.LauncherDefinition;
                if (hq.GetUnitSupply(launcher) > 0)
                    continue;
                if (CountAliveLaunchers(hq) >= FpvConstants.MissionResupplyMaxAlive)
                    continue;

                FpvStrikePackageDefs.AddPackageToSupply(hq, 1);
                FpvPlugin.ModLogger?.LogInfo(
                    $"FPV VehicleSupply replenish +1 → {HqLabel(hq)}");
            }
        }

        internal static void ClearSession()
        {
            SeededHqIds.Clear();
            _nextTick = 0f;
        }

        private static void TrySeed(FactionHQ hq, bool force)
        {
            if (!CanRun())
                return;
            if (hq == null || !hq.IsServer)
                return;

            EnsureDefs();
            if (DefinitionRegistrar.LauncherDefinition == null)
                return;

            int id = hq.GetInstanceID();
            if (!force && SeededHqIds.Contains(id))
                return;

            // Need at least one depot eventually; still seed supply so DeployVehicles can drain when depot registers.
            FpvStrikePackageDefs.AddPackageToSupply(hq, FpvConstants.MissionSupplyPackages);
            SeededHqIds.Add(id);
            FpvPlugin.ModLogger?.LogInfo(
                $"FPV VehicleSupply seed +{FpvConstants.MissionSupplyPackages} → {HqLabel(hq)} " +
                $"(launcherSupply={hq.GetUnitSupply(DefinitionRegistrar.LauncherDefinition)})");
        }

        private static bool CanRun()
        {
            GameState gs = GameManager.gameState;
            if (gs == GameState.Encyclopedia || gs == GameState.Menu || gs == GameState.Uninitialized)
                return false;
            return true;
        }

        private static void EnsureDefs()
        {
            if (DefinitionRegistrar.LauncherDefinition != null)
                return;
            Encyclopedia? enc = Encyclopedia.i;
            if (enc != null)
                DefinitionRegistrar.TryRegister(enc);
        }

        private static string HqLabel(FactionHQ hq) =>
            hq.faction != null ? hq.faction.factionName : hq.name;

        private static int CountAliveLaunchers(FactionHQ hq)
        {
            int n = 0;
            FpvLauncher[] list = Object.FindObjectsOfType<FpvLauncher>();
            for (int i = 0; i < list.Length; i++)
            {
                FpvLauncher? l = list[i];
                if (l == null)
                    continue;
                Unit? u = l.OwnerUnit;
                if (u == null || u.disabled || u.NetworkHQ != hq)
                    continue;
                n++;
            }
            return n;
        }
    }
}
