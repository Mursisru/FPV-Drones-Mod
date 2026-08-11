using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace FPVMod.Hud
{
    /// <summary>Embedded unit marker sprite (Blender Sprite-0001 → Resources/fpv_unit_icon.png).</summary>
    internal static class FpvUnitIconSprite
    {
        private const string ResourceName = "FPVMod.Resources.fpv_unit_icon.png";

        private static Sprite? _sprite;
        private static Texture2D? _tex;
        private static bool _tried;

        internal static Sprite? Get()
        {
            if (_sprite != null)
                return _sprite;
            if (_tried)
                return null;
            _tried = true;
            try
            {
                Assembly asm = typeof(FpvUnitIconSprite).Assembly;
                using Stream? stream = asm.GetManifestResourceStream(ResourceName);
                if (stream == null)
                {
                    FpvPlugin.ModLogger?.LogWarning($"FPV unit icon missing embed '{ResourceName}'");
                    return null;
                }

                byte[] bytes = new byte[stream.Length];
                int read = 0;
                while (read < bytes.Length)
                {
                    int n = stream.Read(bytes, read, bytes.Length - read);
                    if (n <= 0)
                        break;
                    read += n;
                }

                _tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    name = "FPVMod.UnitIcon",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                if (!ImageConversion.LoadImage(_tex, bytes, markNonReadable: true))
                {
                    UnityEngine.Object.Destroy(_tex);
                    _tex = null;
                    return null;
                }

                _sprite = Sprite.Create(
                    _tex,
                    new Rect(0f, 0f, _tex.width, _tex.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                _sprite.name = "FPVMod.UnitIconSprite";
                _sprite.hideFlags = HideFlags.HideAndDontSave;
            }
            catch (Exception ex)
            {
                FpvPlugin.ModLogger?.LogWarning($"FPV unit icon load failed: {ex.Message}");
            }

            return _sprite;
        }
    }
}
