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
                return;

            FpvMapLaunchPanelTarget.Current = launcher;

            NetworkManagerNuclearOption? nm = NetworkManagerNuclearOption.i;
            if (nm?.Server?.Active == true && nm.Client?.Active != true)
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
                return;
            Missile? drone = FindMissile(msg.DroneId);
            FpvLauncher? launcher = FpvMapLaunchPanelTarget.Current;
            if (drone != null && launcher != null)
                FpvControlSession.Begin(drone, launcher);
        }

        private static void TryServerLaunch(PersistentID launcherId, INetworkPlayer? requester)
        {
            FpvLauncher? launcher = FindLauncher(launcherId);
            if (launcher == null || !launcher.CanLaunch())
            {
                Reply(requester, PersistentID.None, false);
                return;
            }

            Unit? owner = launcher.OwnerUnit;
            if (owner == null || owner.disabled)
            {
                Reply(requester, PersistentID.None, false);
                return;
            }

            if (!IsFriendly(owner))
            {
                Reply(requester, PersistentID.None, false);
                return;
            }

            if (!launcher.TryConsumeLaunch())
            {
                Reply(requester, PersistentID.None, false);
                return;
            }

            MissileDefinition? def = DefinitionRegistrar.DroneDefinition;
            Spawner? spawner = NetworkSceneSingleton<Spawner>.i;
            if (def == null || spawner == null)
            {
                Reply(requester, PersistentID.None, false);
                return;
            }

            Vector3 pos = owner.transform.position + owner.transform.up * 3f + owner.transform.forward * 2f;
            Vector3 vel = owner.transform.forward * 5f;
            Missile drone = spawner.SpawnMissile(def, pos, owner.transform.rotation, vel, null, owner);
            PrefabFactory.StampDroneInstance(drone.gameObject);
            FpvAiProfile.Apply(drone, owner);

            FpvDroneTag tag = drone.GetComponent<FpvDroneTag>() ?? drone.gameObject.AddComponent<FpvDroneTag>();
            tag.SourceLauncher = launcher;

            if (requester == null)
                FpvControlSession.Begin(drone, launcher);
            else
                Reply(requester, drone.persistentID, true);

            FpvMapLaunchPanel.Refresh();
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

    internal static class FpvMapLaunchPanelTarget
    {
        internal static FpvLauncher? Current { get; set; }
    }
}
