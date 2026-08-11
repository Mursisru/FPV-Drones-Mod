using FPVMod.Access;
using UnityEngine;

namespace FPVMod.FpvView
{
    /// <summary>
    /// Ambient → IR blit exposure (MC InfraredPolicy ComputeExposure).
    /// FS always uses manual vision; this only supplies EV, not auto-toggle.
    /// </summary>
    internal static class FpvInfraredPolicy
    {
        private const float AmbientExposureMin = 0.02f;
        private const float AmbientExposureMax = 0.4f;
        private const float PolicyIntervalSeconds = 1f;

        private static float _nextEvaluateUnscaled;
        private static float _cachedExposure = 1.15f;
        private static float _cachedAmbient = 1f;

        internal static float Exposure => _cachedExposure;
        internal static float CachedAmbient => _cachedAmbient;

        internal static void Reset()
        {
            _nextEvaluateUnscaled = 0f;
            _cachedExposure = 1.15f;
            _cachedAmbient = 1f;
        }

        internal static float Evaluate(Vector3 missileWorldPosition)
        {
            float now = Time.unscaledTime;
            if (now < _nextEvaluateUnscaled)
                return _cachedExposure;

            _nextEvaluateUnscaled = now + PolicyIntervalSeconds;
            _ = missileWorldPosition;

            if (!FpvLevelInfoAccess.TryGetAmbientLight(out float ambient))
            {
                try { ambient = RenderSettings.ambientIntensity; }
                catch { ambient = 0.2f; }
            }

            _cachedAmbient = ambient;
            _cachedExposure = ComputeExposure(ambient);
            return _cachedExposure;
        }

        /// <summary>Same curve as TargetCam.UpdateExposure in IR mode.</summary>
        internal static float ComputeExposure(float ambient)
        {
            float t = Mathf.InverseLerp(AmbientExposureMin, AmbientExposureMax, ambient);
            return Mathf.Lerp(3f, -0.5f, t);
        }
    }
}
