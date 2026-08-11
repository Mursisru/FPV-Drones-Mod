using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace FPVMod.Effects
{
    /// <summary>Embedded MC shader bundle (fisheye/CRT/IR/bloom/…). No MC.dll.</summary>
    internal static class FpvShaderBundle
    {
        private const string ResourceName = "FPVMod.Shaders.fpv_shaders.bundle";
        private static bool _attempted;
        private static AssetBundle? _bundle;
        private static Shader? _infraredBlit;
        private static readonly Dictionary<string, Shader?> Cache =
            new Dictionary<string, Shader?>(StringComparer.Ordinal);

        internal static Shader? InfraredBlitShader
        {
            get
            {
                EnsureLoaded();
                return _infraredBlit;
            }
        }

        internal static bool TryGetFxShader(string findName, out Shader? shader)
        {
            EnsureLoaded();
            if (Cache.TryGetValue(findName, out shader) && shader != null)
                return true;

            shader = Shader.Find(findName);
            if (shader != null)
            {
                Cache[findName] = shader;
                return true;
            }

            if (_bundle != null)
            {
                string shortName = findName;
                int slash = findName.LastIndexOf('/');
                if (slash >= 0 && slash + 1 < findName.Length)
                    shortName = findName.Substring(slash + 1);

                shader = _bundle.LoadAsset<Shader>(shortName);
                if (shader == null)
                {
                    string[] names = _bundle.GetAllAssetNames();
                    for (int i = 0; i < names.Length; i++)
                    {
                        if (names[i].IndexOf(shortName, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            shader = _bundle.LoadAsset<Shader>(names[i]);
                            if (shader != null)
                                break;
                        }
                    }
                }

                if (shader != null)
                {
                    Cache[findName] = shader;
                    return true;
                }
            }

            Cache[findName] = null;
            return false;
        }

        internal static void EnsureLoaded()
        {
            if (_attempted)
                return;
            _attempted = true;
            try
            {
                byte[]? bytes = ReadEmbedded();
                if (bytes == null || bytes.Length == 0)
                {
                    FpvPlugin.ModLogger?.LogWarning("FPV: shader bundle missing.");
                    return;
                }

                _bundle = AssetBundle.LoadFromMemory(bytes);
                if (_bundle == null)
                {
                    FpvPlugin.ModLogger?.LogWarning("FPV: shader bundle LoadFromMemory null.");
                    return;
                }

                FpvPlugin.ModLogger?.LogInfo("FPV: shader bundle loaded.");
                _infraredBlit = Shader.Find("Hidden/MissileCamera/InfraredBlit");
                if (_infraredBlit == null)
                    _infraredBlit = _bundle.LoadAsset<Shader>("MissileCameraInfraredBlit");
                if (_infraredBlit == null)
                    _infraredBlit = _bundle.LoadAsset<Shader>("Assets/Shaders/MissileCameraInfraredBlit.shader");
                if (_infraredBlit == null)
                {
                    string[] names = _bundle.GetAllAssetNames();
                    for (int i = 0; i < names.Length && _infraredBlit == null; i++)
                    {
                        if (names[i].IndexOf("InfraredBlit", StringComparison.OrdinalIgnoreCase) >= 0)
                            _infraredBlit = _bundle.LoadAsset<Shader>(names[i]);
                    }
                }

                if (_infraredBlit == null)
                    FpvPlugin.ModLogger?.LogWarning("FPV: InfraredBlit shader not in bundle.");
                else
                    FpvPlugin.ModLogger?.LogInfo("FPV: InfraredBlit ok.");
            }
            catch (Exception ex)
            {
                FpvPlugin.ModLogger?.LogWarning($"FPV shader bundle: {ex.Message}");
            }
        }

        /// <summary>Keep bundle across scenes (same as fpvmod_assets).</summary>
        internal static void SoftReset() { /* no Unload(true) */ }

        private static byte[]? ReadEmbedded()
        {
            Assembly asm = typeof(FpvShaderBundle).Assembly;
            using Stream? stream = asm.GetManifestResourceStream(ResourceName);
            if (stream == null)
                return null;
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }
    }
}
