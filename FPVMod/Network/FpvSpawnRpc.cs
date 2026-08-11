using FPVMod.Bootstrap;
using FPVMod.Drone;
using FPVMod.Launcher;
using FPVMod.Session;
using Mirage;
using Mirage.SocketLayer;
using NuclearOption.Networking;
using UnityEngine;

namespace FPVMod.Network
{
    [NetworkMessage]
    internal struct FpvLaunchRequestMsg
    {
        internal PersistentID LauncherId;
    }

    [NetworkMessage]
    internal struct FpvLaunchResultMsg
    {
        internal PersistentID DroneId;
        internal bool Success;
    }

    internal static class FpvSpawnRpc
    {
        internal static void RequestLaunch(FpvLauncher launcher)
        {
            if (launcher == null)
                return;

            Unit? unit = launcher.OwnerUnit;
            if (unit == null)
            {
                FpvPlugin.ModLogger?.LogWarning("FPV launch: launcher has no unit.");
                return;
            }

            FpvLauncherSelectBridge.PendingLauncher = launcher;
            FpvLauncherSelectBridge.LaunchTarget = launcher;

            NetworkManagerNuclearOption? nm = NetworkManagerNuclearOption.i;

            // Listen-server / host: Server.Active && Client.Active — must spawn locally (Begin on host).
            // Old path sent Client.Send and never called Begin on host → "Select Aircraft does nothing".
            if (nm?.Server?.Active == true)
            {
                TryServerLaunch(unit.persistentID, null);
                return;
            }

            if (nm?.Client?.Active == true)
            {
                nm.Client.Send(new FpvLaunchRequestMsg { LauncherId = unit.persistentID }, Channel.Reliable);
                return;
            }

            TryServerLaunch(unit.persistentID, null);
        }

        internal static void OnLaunchRequest(INetworkPlayer conn, FpvLaunchRequestMsg msg)
        {
            if (NetworkManagerNuclearOption.i?.Server?.Active != true)
                return;
            TryServerLaunch(msg.LauncherId, conn);
        }

        internal static void OnLaunchResult(INetworkPlayer conn, FpvLaunchResultMsg msg)
        {
            if (!msg.Success)
            {
                FpvPlugin.ModLogger?.LogWarning("FPV launch result: failed.");
                return;
            }

            Missile? drone = FindMissile(msg.DroneId);
            FpvLauncher? launcher = FpvLauncherSelectBridge.LaunchTarget ?? FpvLauncherSelectBridge.PendingLauncher;
            if (drone == null)
            {
                FpvPlugin.ModLogger?.LogWarning($"FPV launch result: drone {msg.DroneId} not found yet.");
                return;
            }
            if (launcher == null)
            {
                FpvPlugin.ModLogger?.LogWarning("FPV launch result: no LaunchTarget.");
                return;
            }

            FpvLauncherSelectBridge.AfterLaunch();
            FpvControlSession.Begin(drone, launcher);
        }

        private static void TryServerLaunch(PersistentID launcherId, INetworkPlayer? requester)
        {
            FpvLauncher? launcher = FindLauncher(launcherId);
            if (launcher == null || !launcher.CanLaunch())
            {
                FpvPlugin.ModLogger?.LogWarning("FPV server launch: launcher missing/not ready.");
                FpvLauncherSelectBridge.ResetLaunchGate();
                Reply(requester, PersistentID.None, false);
                return;
            }

            Unit? owner = launcher.OwnerUnit;
            if (owner == null || owner.disabled)
            {
                FpvLauncherSelectBridge.ResetLaunchGate();
                Reply(requester, PersistentID.None, false);
                return;
            }

            if (!IsFriendly(owner))
            {
                FpvLauncherSelectBridge.ResetLaunchGate();
                Reply(requester, PersistentID.None, false);
                return;
            }

            MissileDefinition? def = DefinitionRegistrar.DroneDefinition;
            Spawner? spawner = NetworkSceneSingleton<Spawner>.i;
            if (def == null || spawner == null)
            {
                FpvPlugin.ModLogger?.LogError("FPV server launch: DroneDefinition or Spawner null.");
                FpvLauncherSelectBridge.ResetLaunchGate();
                Reply(requester, PersistentID.None, false);
                return;
            }

            // Soft climb-out — clear of truck without rocket punch.
            Vector3 pos = owner.transform.position + owner.transform.up * 8f + owner.transform.forward * 6f;
            Vector3 vel = owner.transform.forward * 18f + owner.transform.up * 6f;
            Missile drone;
            try
            {
                drone = spawner.SpawnMissile(def, pos, owner.transform.rotation, vel, null, owner);
            }
            catch (System.Exception ex)
            {
                FpvPlugin.ModLogger?.LogError($"FPV SpawnMissile: {ex}");
                FpvLauncherSelectBridge.ResetLaunchGate();
                Reply(requester, PersistentID.None, false);
                return;
            }

            if (drone == null)
            {
                FpvLauncherSelectBridge.ResetLaunchGate();
                Reply(requester, PersistentID.None, false);
                return;
            }

            try
            {
                PrefabFactory.StampDroneInstance(drone.gameObject);
                FpvAiProfile.Apply(drone, owner);
            }
            catch (System.Exception ex)
            {
                FpvPlugin.ModLogger?.LogError($"FPV stamp/profile: {ex}");
            }

            FpvDroneTag tag = drone.GetComponent<FpvDroneTag>() ?? drone.gameObject.AddComponent<FpvDroneTag>();
            tag.SourceLauncher = launcher;

            if (!launcher.TryConsumeLaunch())
            {
                FpvPlugin.ModLogger?.LogWarning("FPV launch: ammo consume failed after spawn.");
            }

            FpvPlugin.ModLogger?.LogInfo($"FPV spawned drone {drone.persistentID} from {owner.unitName}.");

            if (requester == null)
            {
                // Collapse map first so Camera.main is the gameplay cam, then possess.
                FpvLauncherSelectBridge.AfterLaunch();
                FpvControlSession.Begin(drone, launcher);
            }
            else
            {
                Reply(requester, drone.persistentID, true);
            }
        }

        private static void Reply(INetworkPlayer? conn, PersistentID droneId, bool ok)
        {
            if (conn == null)
                return;
            conn.Send(new FpvLaunchResultMsg { DroneId = droneId, Success = ok }, Channel.Reliable);
        }

        private static bool IsFriendly(Unit unit)
        {
            if (!GameManager.GetLocalHQ(out FactionHQ? hq) || hq == null)
                return true;
            return unit.NetworkHQ == hq;
        }

        private static FpvLauncher? FindLauncher(PersistentID id)
        {
            foreach (FpvLauncher l in Object.FindObjectsOfType<FpvLauncher>())
            {
                Unit? u = l.OwnerUnit;
                if (u != null && u.persistentID == id)
                    return l;
            }
            return null;
        }

        internal static Missile? FindMissile(PersistentID id)
        {
            foreach (Missile m in Object.FindObjectsOfType<Missile>())
            {
                if (m.persistentID == id)
                    return m;
            }
            return null;
        }
    }
}
