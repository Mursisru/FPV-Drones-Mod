using UnityEngine;

namespace FPVMod.Access
{
    /// <summary>LevelInfo ambient/daylight for IR exposure (MC LevelInfoAccess port).</summary>
    internal static class FpvLevelInfoAccess
    {
        internal static bool TryGet(out LevelInfo levelInfo)
        {
            levelInfo = null!;
            try
            {
                LevelInfo? instance = NetworkSceneSingleton<LevelInfo>.i;
                if (instance == null)
                    return false;
                levelInfo = instance;
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static bool TryGetAmbientLight(out float ambient)
        {
            ambient = 1f;
            if (!TryGet(out LevelInfo levelInfo))
                return false;
            try { ambient = levelInfo.GetAmbientLight(); }
            catch
            {
                ambient = 1f;
                return false;
            }
            if (float.IsNaN(ambient) || float.IsInfinity(ambient))
            {
                ambient = 1f;
                return false;
            }
            return true;
        }

        internal static bool TryGetDaylightFactor(Vector3 worldPosition, out float daylight)
        {
            daylight = 1f;
            if (!TryGet(out LevelInfo levelInfo))
                return false;
            try { daylight = levelInfo.GetDaylightFactor(worldPosition); }
            catch
            {
                daylight = 1f;
                return false;
            }
            if (float.IsNaN(daylight) || float.IsInfinity(daylight))
            {
                daylight = 1f;
                return false;
            }
            return true;
        }
    }
}
