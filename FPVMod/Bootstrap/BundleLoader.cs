using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace FPVMod.Bootstrap
{
    /// <summary>Loads embedded AssetBundle or returns null for fallback prefab cloning.</summary>
    internal static class BundleLoader
    {
        private static AssetBundle? _bundle;
        private static bool _tried;

        internal static AssetBundle? Bundle
        {
            get
            {
                if (!_tried)
                    TryLoad();
                return _bundle;
            }
        }

        internal static Texture2D? LoadDroneAlbedoTexture()
        {
            string[] keys =
            {
                "texture_rpgb",
                "Texture_RPGB",
                "texture_rpgb.png",
                "assets/models/texture_rpgb.png"
            };

            foreach (string key in keys)
            {
                Texture2D? tex = LoadAsset<Texture2D>(key);
                if (tex != null)
                    return tex;
            }

            AssetBundle? b = Bundle;
            if (b == null)
                return null;

            try
            {
                foreach (string name in b.GetAllAssetNames())
                {
                    if (name.IndexOf(".png", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    Texture2D? tex = b.LoadAsset<Texture2D>(name);
                    if (tex != null)
                        return tex;
                }
            }
            catch
            {
                // ignore
            }

            return null;
        }

        internal static T? LoadAsset<T>(string assetName) where T : UnityEngine.Object
        {
            AssetBundle? b = Bundle;
            if (b == null)
                return null;

            string[] candidates =
            {
                assetName,
                $"Assets/Models/{assetName}.prefab",
                $"assets/models/{assetName}.prefab"
            };

            foreach (string key in candidates)
            {
                try
                {
                    T? asset = b.LoadAsset<T>(key);
                    if (asset != null)
                        return asset;
                }
                catch
                {
                    // try next key
                }
            }

            try
            {
                string[] names = b.GetAllAssetNames();
                foreach (string name in names)
                {
                    if (name.IndexOf(assetName, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    T? asset = b.LoadAsset<T>(name);
                    if (asset != null)
                        return asset;
                }
            }
            catch
            {
                // ignore
            }

            return null;
        }

        private static void TryLoad()
        {
            _tried = true;
            try
            {
                Assembly asm = typeof(BundleLoader).Assembly;
                using Stream? stream = asm.GetManifestResourceStream(FpvConstants.BundleResourceName);
                if (stream == null)
                {
                    FpvPlugin.ModLogger?.LogInfo("FPVMod: no embedded asset bundle — using cloned vanilla visuals.");
                    return;
                }

                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                _bundle = AssetBundle.LoadFromMemory(ms.ToArray());
                if (_bundle == null)
                {
                    FpvPlugin.ModLogger?.LogWarning("FPVMod: AssetBundle.LoadFromMemory failed.");
                    return;
                }

                string[] names = _bundle.GetAllAssetNames();
                FpvPlugin.ModLogger?.LogInfo($"FPVMod: bundle loaded ({names.Length} assets): {string.Join(", ", names)}");
            }
            catch (Exception ex)
            {
                FpvPlugin.ModLogger?.LogWarning($"FPVMod: bundle load failed: {ex.Message}");
            }
        }

        /// <summary>Soft unload — do not destroy loaded assets (would blank stamped FBX meshes).</summary>
        internal static void Unload()
        {
            // Intentionally no AssetBundle.Unload(true) during scene changes.
            // Bundle stays in memory for the process lifetime.
        }
    }
}
