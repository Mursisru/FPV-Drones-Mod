using UnityEngine;

namespace FPVMod.Hud
{
    /// <summary>FS panel size for Gunship layout (slim MC PanelMetrics).</summary>
    internal readonly struct FpvPanelMetrics
    {
        internal FpvPanelMetrics(float width, float height)
        {
            Width = Mathf.Max(1f, width);
            Height = Mathf.Max(1f, height);
        }

        internal float Width { get; }
        internal float Height { get; }
        internal float MinSide => Mathf.Min(Width, Height);

        internal static FpvPanelMetrics FromScreen() =>
            new FpvPanelMetrics(Screen.width, Screen.height);

        internal static FpvPanelMetrics FromRect(RectTransform? rt)
        {
            if (rt == null)
                return FromScreen();
            Rect r = rt.rect;
            float w = r.width > 8f ? r.width : Screen.width;
            float h = r.height > 8f ? r.height : Screen.height;
            return new FpvPanelMetrics(w, h);
        }
    }
}
