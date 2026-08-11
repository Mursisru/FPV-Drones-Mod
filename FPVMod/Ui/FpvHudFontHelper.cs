using UnityEngine;

namespace FPVMod.Ui
{
    /// <summary>Builtin UI font (ported from MC HudFontHelper).</summary>
    internal static class FpvHudFontHelper
    {
        private static Font? _font;

        internal static Font Get()
        {
            if (_font != null)
                return _font;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            return _font;
        }
    }
}
