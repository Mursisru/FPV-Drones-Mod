using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace FPVMod.FpvView
{
    /// <summary>
    /// Drive MissileCamera FS onto the FPV drone via reflection only (no MC source edits).
    /// Horizon/acro unlock is Harmony in FpvMissileCameraAcroPatches.
    /// </summary>
    internal static class FpvMissileCameraBridge
    {
        private static bool _resolved;
        private static float _nextResolve;
        private static Type? _feedType;
        private static Type? _fsType;
        private static FieldInfo? _ownedActive;
        private static FieldInfo? _followed;
        private static FieldInfo? _manualFollow;
        private static MethodInfo? _enter;
        private static MethodInfo? _exitIfActive;
        private static MethodInfo? _notifyFsEntered;
        private static Missile? _injected;

        internal static bool TryAttach(Missile drone)
        {
            if (drone == null)
                return false;
            EnsureResolved();
            if (_feedType == null || _fsType == null || _ownedActive == null)
                return false;

            try
            {
                if (_ownedActive.GetValue(null) is IList list)
                {
                    bool found = false;
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (ReferenceEquals(list[i], drone))
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                        list.Add(drone);
                }

                _followed?.SetValue(null, drone);
                _manualFollow?.SetValue(null, true);
                _injected = drone;

                bool active = IsFsActive();
                if (!active)
                {
                    if (_enter != null)
                        _enter.Invoke(null, null);
                    else
                    {
                        MethodInfo? toggle = _fsType.GetMethod(
                            "Toggle", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                        toggle?.Invoke(null, null);
                    }
                    _notifyFsEntered?.Invoke(null, null);
                }

                active = IsFsActive();
                if (active)
                {
                    FpvPlugin.ModLogger?.LogInfo("FPV: MissileCamera FS attached.");
                    return true;
                }

                FpvPlugin.ModLogger?.LogWarning("FPV: MissileCamera FS enter failed — using local feed.");
                ClearInject();
                return false;
            }
            catch (Exception ex)
            {
                FpvPlugin.ModLogger?.LogWarning($"FPV MC bridge: {ex.Message}");
                ClearInject();
                return false;
            }
        }

        internal static void TickKeepAlive(Missile? drone)
        {
            if (drone == null || _injected == null || !ReferenceEquals(_injected, drone))
                return;
            if (_ownedActive?.GetValue(null) is not IList list)
                return;

            bool found = false;
            for (int i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], drone))
                {
                    found = true;
                    break;
                }
            }
            if (!found)
                list.Add(drone);

            _followed?.SetValue(null, drone);
            _manualFollow?.SetValue(null, true);
        }

        internal static void Detach()
        {
            try
            {
                _exitIfActive?.Invoke(null, null);
            }
            catch
            {
                // ignore
            }
            ClearInject();
        }

        private static void ClearInject()
        {
            if (_injected != null && _ownedActive?.GetValue(null) is IList list)
            {
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (ReferenceEquals(list[i], _injected))
                        list.RemoveAt(i);
                }
            }
            _injected = null;
            try
            {
                _manualFollow?.SetValue(null, false);
                _followed?.SetValue(null, null);
            }
            catch
            {
                // ignore
            }
        }

        private static bool IsFsActive()
        {
            try
            {
                PropertyInfo? p = _fsType?.GetProperty(
                    "IsActive", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                return p != null && p.GetValue(null) is bool b && b;
            }
            catch
            {
                return false;
            }
        }

        private static void EnsureResolved()
        {
            if (_resolved)
                return;
            if (Time.unscaledTime < _nextResolve)
                return;
            _nextResolve = Time.unscaledTime + 1f;

            try
            {
                Assembly? mc = null;
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.GetName().Name == "MissileCamera")
                    {
                        mc = asm;
                        break;
                    }
                }
                if (mc == null)
                    return;

                _feedType = mc.GetType("MissileCamera.MissileCameraFeedController");
                _fsType = mc.GetType("MissileCamera.MissileCameraFullscreenController");
                _ownedActive = _feedType?.GetField("OwnedActive", BindingFlags.Static | BindingFlags.NonPublic);
                _followed = _feedType?.GetField("_followedMissile", BindingFlags.Static | BindingFlags.NonPublic);
                _manualFollow = _feedType?.GetField("_manualFollowActive", BindingFlags.Static | BindingFlags.NonPublic);
                _enter = _fsType?.GetMethod("Enter", BindingFlags.Static | BindingFlags.NonPublic);
                _exitIfActive = _fsType?.GetMethod(
                    "ExitIfActive", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                _notifyFsEntered = _feedType?.GetMethod(
                    "NotifyFullscreenEntered", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

                _resolved = _feedType != null && _fsType != null && _ownedActive != null;
                if (_resolved)
                    FpvPlugin.ModLogger?.LogInfo("FPV: MissileCamera reflection ready.");
            }
            catch (Exception ex)
            {
                FpvPlugin.ModLogger?.LogWarning($"FPV MC resolve: {ex.Message}");
            }
        }
    }
}
