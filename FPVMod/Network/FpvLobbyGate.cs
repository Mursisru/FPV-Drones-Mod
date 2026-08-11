using Mirage;
using Mirage.SocketLayer;
using NuclearOption.Networking;
using UnityEngine;

namespace FPVMod.Network
{
    [NetworkMessage]
    internal struct FpvModPresenceQueryMsg
    {
        internal int Magic;
    }

    [NetworkMessage]
    internal struct FpvModPresenceReplyMsg
    {
        internal int Magic;
        internal string Version;
    }

    internal static class FpvLobbyGate
    {
        internal const int PresenceMagic = unchecked((int)0x46505644); // 'FPVD'

        private enum Phase : byte { Idle, Checking, Allowed, Denied }

        private static Phase _phase = Phase.Idle;
        private static float _deadline;
        private static float _nextQuery;
        private static bool _logged;

        internal static bool FeaturesAllowed
        {
            get
            {
                if (!FpvConfig.Enabled.Value)
                    return false;

                NetworkManagerMode mode = SafeMode();
                if (mode == NetworkManagerMode.None && GameManager.gameState != GameState.Multiplayer)
                    return true;
                if (mode == NetworkManagerMode.Host || mode == NetworkManagerMode.Server)
                    return true;
                if (mode == NetworkManagerMode.Client)
                    return _phase == Phase.Allowed;
                return _phase != Phase.Denied;
            }
        }

        internal static void Reset()
        {
            _phase = Phase.Idle;
            _deadline = 0f;
            _nextQuery = 0f;
            _logged = false;
        }

        internal static void Tick()
        {
            if (!FpvConfig.RequireModInLobby.Value)
            {
                _phase = Phase.Allowed;
                return;
            }

            NetworkManagerMode mode = SafeMode();
            if (mode == NetworkManagerMode.Host || mode == NetworkManagerMode.Server || mode == NetworkManagerMode.None)
            {
                _phase = Phase.Allowed;
                return;
            }

            if (mode != NetworkManagerMode.Client || NetworkManagerNuclearOption.i?.Client?.Active != true)
            {
                _phase = Phase.Idle;
                return;
            }

            if (_phase == Phase.Allowed)
                return;

            if (_phase != Phase.Checking)
            {
                _phase = Phase.Checking;
                _deadline = Time.unscaledTime + 4f;
                _nextQuery = 0f;
            }

            if (Time.unscaledTime >= _nextQuery)
            {
                _nextQuery = Time.unscaledTime + 0.75f;
                NetworkManagerNuclearOption.i?.Client?.Send(
                    new FpvModPresenceQueryMsg { Magic = PresenceMagic }, Channel.Reliable);
            }

            if (Time.unscaledTime >= _deadline && _phase == Phase.Checking)
            {
                _phase = Phase.Denied;
                if (!_logged)
                {
                    _logged = true;
                    FpvPlugin.ModLogger?.LogWarning("FPVMod: host missing mod — features disabled on client.");
                }
            }
        }

        internal static void OnPresenceQuery(INetworkPlayer conn, FpvModPresenceQueryMsg msg)
        {
            if (msg.Magic != PresenceMagic)
                return;
            conn.Send(new FpvModPresenceReplyMsg
            {
                Magic = PresenceMagic,
                Version = AppVersion.DisplayVersion
            }, Channel.Reliable);
        }

        internal static void OnPresenceReply(INetworkPlayer conn, FpvModPresenceReplyMsg msg)
        {
            if (msg.Magic != PresenceMagic)
                return;
            _phase = Phase.Allowed;
            if (!_logged)
            {
                _logged = true;
                FpvPlugin.ModLogger?.LogInfo($"FPVMod presence OK (host {msg.Version}).");
            }
        }

        private static NetworkManagerMode SafeMode()
        {
            try { return NetworkManagerNuclearOption.i?.NetworkMode ?? NetworkManagerMode.None; }
            catch { return NetworkManagerMode.None; }
        }
    }
}
