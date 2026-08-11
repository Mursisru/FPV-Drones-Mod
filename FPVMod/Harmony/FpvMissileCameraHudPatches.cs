using System;
using System.Globalization;
using System.Reflection;
using System.Text;
using FPVMod.Drone;
using FPVMod.Link;
using FPVMod.Session;
using HarmonyLib;
using UnityEngine;

namespace FPVMod.HarmonyPatches
{
    /// <summary>
    /// Rewrite MissileCamera FS FLIR panels for FPV drone (no MC source edits).
    /// </summary>
    internal static class FpvMissileCameraHudPatches
    {
        private static bool _patched;
        private static Type? _flirHudType;
        private static FieldInfo? _mslPanel;
        private static FieldInfo? _launchPanel;
        private static FieldInfo? _tgtTrack;
        private static FieldInfo? _tgtEngage;
        private static FieldInfo? _guidance;
        private static FieldInfo? _mslKin;
        private static FieldInfo? _sys;
        private static MethodInfo? _setTitle;
        private static MethodInfo? _setBodyStr;
        private static readonly StringBuilder Sb = new StringBuilder(256);

        internal static void TryPatch(Harmony harmony)
        {
            if (_patched || harmony == null)
                return;

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

                _flirHudType = mc.GetType("MissileCamera.MissileCameraFlirHud");
                if (_flirHudType == null)
                    return;

                MethodInfo? apply = AccessTools.Method(_flirHudType, "ApplyContent");
                if (apply == null)
                    return;

                Type? panelType = mc.GetType("MissileCamera.MissileCameraFlirPanel");
                _setTitle = panelType != null ? AccessTools.Method(panelType, "SetTitle", new[] { typeof(string) }) : null;
                _setBodyStr = panelType != null ? AccessTools.Method(panelType, "SetBody", new[] { typeof(string) }) : null;

                _mslPanel = AccessTools.Field(_flirHudType, "_mslPanel");
                _launchPanel = AccessTools.Field(_flirHudType, "_launchPanel");
                _tgtTrack = AccessTools.Field(_flirHudType, "_tgtTrackPanel");
                _tgtEngage = AccessTools.Field(_flirHudType, "_tgtEngagePanel");
                _guidance = AccessTools.Field(_flirHudType, "_guidancePanel");
                _mslKin = AccessTools.Field(_flirHudType, "_mslKinPanel");
                _sys = AccessTools.Field(_flirHudType, "_sys");

                harmony.Patch(apply, postfix: new HarmonyMethod(typeof(FpvMissileCameraHudPatches), nameof(ApplyContentPostfix)));
                _patched = true;
                FpvPlugin.ModLogger?.LogInfo("FPV: patched MissileCameraFlirHud for drone FS indicators.");
            }
            catch (Exception ex)
            {
                FpvPlugin.ModLogger?.LogWarning($"FPV MC HUD patch: {ex.Message}");
            }
        }

        private static void ApplyContentPostfix(object __instance)
        {
            if (!FpvControlSession.Active || __instance == null)
                return;

            Missile? drone = FpvControlSession.Drone;
            if (drone == null || drone.disabled)
                return;

            FpvAcroController? ac = drone.GetComponent<FpvAcroController>();
            FpvWarhead? wh = drone.GetComponent<FpvWarhead>();
            FpvLinkLevel link = FpvLinkQuality.Evaluate(drone, FpvControlSession.Launcher);

            float spd = ac?.SpeedKmh ?? 0f;
            float vsi = ac?.VerticalSpeedMs ?? 0f;
            float thr = (ac?.Collective01 ?? 0f) * 100f;
            float batt = (ac?.Battery01 ?? 0f) * 100f;
            float alt = 0f;
            try { alt = drone.radarAlt; } catch { alt = drone.transform.position.y; }
            int hdg = Mathf.RoundToInt(Mathf.Repeat(drone.transform.eulerAngles.y, 360f));
            string fuse = wh != null && wh.IsSafe ? "SAFE" : "ARMED";
            string linkTxt = link switch
            {
                FpvLinkLevel.Full => "GOOD",
                FpvLinkLevel.Degraded => "WEAK",
                _ => "LOST"
            };

            float pitch = drone.transform.localEulerAngles.x;
            if (pitch > 180f) pitch -= 360f;
            float roll = drone.transform.localEulerAngles.z;
            if (roll > 180f) roll -= 360f;

            if (_sys is { } sysF && sysF.GetValue(__instance) is UnityEngine.UI.Text sys)
                sys.text = "FPV DRONE LINK  CH1/1";

            SetPanel(_mslPanel, __instance, "FPV", SbClear()
                .Append("MODE ACRO")
                .Append("\nFUSE ").Append(fuse)
                .Append("\nLINK ").Append(linkTxt)
                .Append("\nCOL  ").Append(thr.ToString("0", CultureInfo.InvariantCulture)).Append('%')
                .Append("\nALT  ").Append(alt.ToString("0", CultureInfo.InvariantCulture)).Append('m')
                .Append("\nSPD  ").Append(spd.ToString("0", CultureInfo.InvariantCulture)).Append(" km/h")
                .Append("\nVSI  ").Append(vsi.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture)).Append(" m/s")
                .Append("\nHDG  ").Append(hdg.ToString(CultureInfo.InvariantCulture)).Append('°')
                .Append("\nPIT  ").Append(pitch.ToString("0", CultureInfo.InvariantCulture)).Append('°')
                .Append("\nROL  ").Append(roll.ToString("0", CultureInfo.InvariantCulture)).Append('°')
                .Append("\nAUW  ").Append(FpvConstants.DroneMassKg.ToString("0", CultureInfo.InvariantCulture)).Append(" kg")
                .Append("\nHE   ").Append(FpvConstants.WarheadMassKg.ToString("0", CultureInfo.InvariantCulture)).Append(" kg")
                .ToString());

            string plat = "---";
            try
            {
                if (FpvControlSession.Launcher?.OwnerUnit != null)
                    plat = FpvControlSession.Launcher.OwnerUnit.definition?.unitName ?? "LAUNCHER";
            }
            catch { /* ignore */ }

            SetPanel(_launchPanel, __instance, "GCS", SbClear()
                .Append("PLAT ").Append(plat)
                .Append("\nBATT ").Append(batt.ToString("0", CultureInfo.InvariantCulture)).Append('%')
                .Append("\nTWR  ").Append(FpvConstants.DroneMaxTwr.ToString("0", CultureInfo.InvariantCulture))
                .Append("\nMASS ").Append(FpvConstants.DroneMassKg.ToString("0", CultureInfo.InvariantCulture)).Append(" kg")
                .ToString());

            SetPanel(_tgtTrack, __instance, "AIM", SbClear()
                .Append("BORE SIGHT")
                .Append("\nCAM  BODY")
                .Append("\nTILT ").Append(FpvConstants.CameraPitchDeg.ToString("0", CultureInfo.InvariantCulture)).Append('°')
                .ToString());

            float rng = RayRng(drone);
            SetPanel(_tgtEngage, __instance, "IMPACT", SbClear()
                .Append("FUSE ").Append(fuse)
                .Append("\nLRF  ").Append(rng > 0f ? rng.ToString("0", CultureInfo.InvariantCulture) + " m" : "---")
                .Append("\nHE   ").Append(FpvConstants.WarheadMassKg.ToString("0", CultureInfo.InvariantCulture)).Append(" kg")
                .ToString());

            SetPanel(_guidance, __instance, "CONTROL", SbClear()
                .Append("MODE RATE")
                .Append("\nTRK  PILOT")
                .Append("\nLINK ").Append(linkTxt)
                .Append("\nCOL  ").Append(thr.ToString("0", CultureInfo.InvariantCulture)).Append('%')
                .ToString());

            SetPanel(_mslKin, __instance, "KIN", SbClear()
                .Append("SPD  ").Append(spd.ToString("0", CultureInfo.InvariantCulture)).Append(" km/h")
                .Append("\nVSI  ").Append(vsi.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture)).Append(" m/s")
                .Append("\nCOL  ").Append(thr.ToString("0", CultureInfo.InvariantCulture)).Append('%')
                .Append("\nALT  ").Append(alt.ToString("0", CultureInfo.InvariantCulture)).Append('m')
                .ToString());
        }

        private static StringBuilder SbClear()
        {
            Sb.Length = 0;
            return Sb;
        }

        private static void SetPanel(FieldInfo? field, object hud, string title, string body)
        {
            if (field == null || _setTitle == null || _setBodyStr == null)
                return;
            object? panel = field.GetValue(hud);
            if (panel == null)
                return;
            try
            {
                _setTitle.Invoke(panel, new object[] { title });
                _setBodyStr.Invoke(panel, new object[] { body });
            }
            catch
            {
                // ignore
            }
        }

        private static float RayRng(Missile drone)
        {
            Vector3 o = drone.transform.position;
            Vector3 d = drone.transform.forward;
            return Physics.Raycast(o, d, out RaycastHit hit, 5000f, PhysicsLayers.StaticsMask | PhysicsLayers.ShipsMask)
                ? hit.distance
                : 0f;
        }
    }
}
