using FPVMod.Input;
using FPVMod.Session;
using Mirage;
using Mirage.SocketLayer;
using NuclearOption.Networking;
using UnityEngine;

namespace FPVMod.Network
{
    [NetworkMessage]
    internal struct FpvInputMsg
    {
        internal PersistentID DroneId;
        internal byte Pitch;
        internal byte Roll;
        internal byte Yaw;
        internal byte Throttle;
    }

    internal static class FpvInputRpc
    {
        private static float _nextSend;

        internal static void Tick()
        {
            if (!FpvControlSession.Active || FpvControlSession.Drone == null)
                return;

            NetworkManagerNuclearOption? nm = NetworkManagerNuclearOption.i;
            if (nm?.Server?.Active == true && nm.Client?.Active != true)
                return;
            if (nm?.Client?.Active != true)
                return;

            if (Time.unscaledTime < _nextSend)
                return;
            _nextSend = Time.unscaledTime + 0.05f;

            FpvInputSample s = FpvInputBridge.Sample;
            nm.Client.Send(new FpvInputMsg
            {
                DroneId = FpvControlSession.Drone.persistentID,
                Pitch = AxisToByte(s.Pitch),
                Roll = AxisToByte(s.Roll),
                Yaw = AxisToByte(s.Yaw),
                Throttle = ThrottleToByte(s.Throttle)
            }, Channel.Reliable);
        }

        internal static void OnInputMsg(INetworkPlayer conn, FpvInputMsg msg)
        {
            if (NetworkManagerNuclearOption.i?.Server?.Active != true)
                return;
            Missile? drone = FpvSpawnRpc.FindMissile(msg.DroneId);
            if (drone == null || !drone.LocalSim)
                return;
            FpvInputBridge.ApplyCompressed(msg.Pitch, msg.Roll, msg.Yaw, msg.Throttle);
        }

        private static byte AxisToByte(float v) =>
            (byte)Mathf.Clamp(Mathf.RoundToInt((v + 1f) * 127.5f), 0, 255);

        private static byte ThrottleToByte(float t) =>
            (byte)Mathf.Clamp(Mathf.RoundToInt(t * 255f), 0, 255);
    }
}
