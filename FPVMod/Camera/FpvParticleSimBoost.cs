using System.Collections.Generic;
using UnityEngine;

namespace FPVMod.FpvView
{
    /// <summary>
    /// ParticleSystem Automatic ignores orphan Overlay cams for some FX.
    /// Force AlwaysSimulate nearby + keep Effects/Water/Sun in feed culling (MC + FPV).
    /// </summary>
    internal static class FpvParticleSimBoost
    {
        private const float RadiusSq = 8000f * 8000f;
        private const float ScanInterval = 0.15f;
        private const int MaxPatch = 192;

        private static readonly List<ParticleSystem> Patched = new List<ParticleSystem>(MaxPatch);
        private static readonly List<ParticleSystemCullingMode> PrevModes =
            new List<ParticleSystemCullingMode>(MaxPatch);

        private static float _nextScanUnscaled;
        private static ParticleSystem[]? _cache;
        private static float _nextCacheUnscaled;

        internal static void Tick(Camera? feed)
        {
            if (feed == null)
                return;

            WidenCulling(feed);

            float now = Time.unscaledTime;
            if (now < _nextScanUnscaled)
                return;
            _nextScanUnscaled = now + ScanInterval;

            if (_cache == null || now >= _nextCacheUnscaled)
            {
                _nextCacheUnscaled = now + 0.75f;
                _cache = Object.FindObjectsOfType<ParticleSystem>();
            }

            ParticleSystem[]? systems = _cache;
            if (systems == null || systems.Length == 0)
                return;

            Vector3 origin = feed.transform.position;
            RestoreFar(origin);

            for (int i = 0; i < systems.Length && Patched.Count < MaxPatch; i++)
            {
                ParticleSystem? ps = systems[i];
                if (ps == null)
                    continue;
                if (Patched.Contains(ps))
                    continue;

                // Clouds / weather: always pin while FS active (emit near feed via CloudLayer hijack).
                bool cloud = IsCloudSystem(ps);
                if (!cloud)
                {
                    if (!ps.isPlaying && !ps.IsAlive())
                        continue;
                    Vector3 p = ps.transform.position;
                    if ((p - origin).sqrMagnitude > RadiusSq)
                        continue;
                }

                ParticleSystemCullingMode mode = ps.main.cullingMode;
                if (mode == ParticleSystemCullingMode.AlwaysSimulate)
                    continue;

                var main = ps.main;
                PrevModes.Add(mode);
                Patched.Add(ps);
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            }
        }

        internal static void Clear()
        {
            for (int i = 0; i < Patched.Count; i++)
            {
                ParticleSystem? ps = Patched[i];
                if (ps == null)
                    continue;
                try
                {
                    var main = ps.main;
                    main.cullingMode = PrevModes[i];
                }
                catch { /* destroyed */ }
            }
            Patched.Clear();
            PrevModes.Clear();
            _cache = null;
            _nextScanUnscaled = 0f;
            _nextCacheUnscaled = 0f;
        }

        private static void RestoreFar(Vector3 origin)
        {
            for (int i = Patched.Count - 1; i >= 0; i--)
            {
                ParticleSystem? ps = Patched[i];
                if (ps == null)
                {
                    Patched.RemoveAt(i);
                    PrevModes.RemoveAt(i);
                    continue;
                }

                if (IsCloudSystem(ps))
                    continue;
                if ((ps.transform.position - origin).sqrMagnitude <= RadiusSq * 1.25f)
                    continue;

                try
                {
                    var main = ps.main;
                    main.cullingMode = PrevModes[i];
                }
                catch { /* ignore */ }
                Patched.RemoveAt(i);
                PrevModes.RemoveAt(i);
            }
        }

        private static bool IsCloudSystem(ParticleSystem ps)
        {
            Transform t = ps.transform;
            for (int d = 0; d < 6 && t != null; d++)
            {
                if (t.GetComponent<CloudLayer>() != null)
                    return true;
                t = t.parent;
            }
            return false;
        }

        /// <summary>MC: reference mask + Effects|TransparentFX; FPV also Water|Sun.</summary>
        internal static void WidenCulling(Camera cam)
        {
            if (cam == null)
                return;

            int want = cam.cullingMask
                | (int)PhysicsLayers.EffectsMask
                | (int)PhysicsLayers.TransparentFXMask
                | (int)PhysicsLayers.WaterMask
                | (int)PhysicsLayers.SunMask;
            if (cam.cullingMask != want)
                cam.cullingMask = want;
        }
    }
}
