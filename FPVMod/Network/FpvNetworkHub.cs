using Mirage;
using NuclearOption.Networking;
using UnityEngine;

namespace FPVMod.Network
{
    internal static class FpvNetworkHub
    {
        private static int _nmId;
        private static bool _clientHandlers;
        private static bool _serverHandlers;

        internal static void EnsureHandlers()
        {
            NetworkManagerNuclearOption? nm = NetworkManagerNuclearOption.i;
            if (nm == null)
                return;

            int id = nm.GetInstanceID();
            if (id != _nmId)
            {
                _nmId = id;
                _clientHandlers = false;
                _serverHandlers = false;
            }

            try
            {
                if (!_clientHandlers && nm.Client?.MessageHandler != null)
                {
                    nm.Client.MessageHandler.RegisterHandler<FpvLaunchResultMsg>(FpvSpawnRpc.OnLaunchResult, false);
                    nm.Client.MessageHandler.RegisterHandler<FpvModPresenceReplyMsg>(FpvLobbyGate.OnPresenceReply, false);
                    _clientHandlers = true;
                }
            }
            catch (System.Exception ex)
            {
                FpvPlugin.ModLogger?.LogWarning($"FPV client handlers: {ex.Message}");
            }

            try
            {
                if (!_serverHandlers && nm.Server?.MessageHandler != null)
                {
                    nm.Server.MessageHandler.RegisterHandler<FpvLaunchRequestMsg>(FpvSpawnRpc.OnLaunchRequest, false);
                    nm.Server.MessageHandler.RegisterHandler<FpvInputMsg>(FpvInputRpc.OnInputMsg, false);
                    nm.Server.MessageHandler.RegisterHandler<FpvModPresenceQueryMsg>(FpvLobbyGate.OnPresenceQuery, false);
                    _serverHandlers = true;
                }
            }
            catch (System.Exception ex)
            {
                FpvPlugin.ModLogger?.LogWarning($"FPV server handlers: {ex.Message}");
            }
        }
    }
}
