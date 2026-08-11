using FPVMod.Ui;
using UnityEngine;
using UnityEngine.UI;

namespace FPVMod.Hud
{
    /// <summary>COD AC-130 chrome — thin white technical HUD (EN).</summary>
    internal static class GunshipChrome
    {
        // --- Layout (fraction of panel, COD ~4–5% bezel) ---
        internal const float PadXFrac = 0.042f;
        internal const float PadYFrac = 0.052f;

        // --- Colors ---
        internal static readonly Color White = new Color(1f, 1f, 1f, 0.94f);
        internal static readonly Color WhiteDim = new Color(1f, 1f, 1f, 0.78f);
        internal static readonly Color WhiteSoft = new Color(1f, 1f, 1f, 0.42f);
        internal static readonly Color SelFill = new Color(1f, 1f, 1f, 0.06f);
        internal static readonly Color Ready = new Color(0.92f, 0.18f, 0.14f, 0.92f);

        // --- Type ---
        internal const int FontCallsign = 30;
        internal const int FontStatus = 15;
        internal const int FontBody = 16;
        internal const int FontSmall = 14;

        internal static float PadX(FpvPanelMetrics panel) =>
            Mathf.Max(panel.Width * PadXFrac, 34f);

        internal static float PadY(FpvPanelMetrics panel) =>
            Mathf.Max(panel.Height * PadYFrac, 26f);

        // --- Text factory (black outline + soft white ghost = CRT bloom) ---
        internal static Text CreateText(RectTransform parent, string name, TextAnchor align, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Text t = go.GetComponent<Text>();
            t.font = FpvHudFontHelper.Get();
            t.fontSize = fontSize;
            t.fontStyle = FontStyle.Bold;
            t.color = White;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;

            var ghost = go.AddComponent<Outline>();
            ghost.effectColor = new Color(1f, 1f, 1f, 0.18f);
            ghost.effectDistance = new Vector2(1.4f, 0f);

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.82f);
            outline.effectDistance = new Vector2(0.9f, -0.9f);
            return t;
        }

        internal static Image CreateImage(RectTransform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image img = go.GetComponent<Image>();
            FpvUiImageHelper.ApplySolid(img, color);
            img.raycastTarget = false;
            return img;
        }

        // --- COD sat dish + signal arcs (top-left icon) ---
        internal static Texture2D BuildSatIconTex()
        {
            const int s = 40;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
            {
                name = "MC.SatIcon",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
                tex.SetPixel(x, y, Color.clear);

            // Dish body (left)
            FillDisc(tex, 11, 16, 6, Color.white);
            // Mast
            for (int y = 6; y <= 20; y++)
            {
                tex.SetPixel(11, y, Color.white);
                tex.SetPixel(12, y, Color.white);
            }
            // Signal arcs → right (COD bars)
            DrawArc(tex, 14, 18, 8, 0.12f, 0.52f);
            DrawArc(tex, 14, 18, 12, 0.12f, 0.52f);
            DrawArc(tex, 14, 18, 16, 0.12f, 0.52f);
            tex.Apply(false, true);
            return tex;
        }

        private static void FillDisc(Texture2D tex, int cx, int cy, int r, Color c)
        {
            for (int y = cy - r; y <= cy + r; y++)
            for (int x = cx - r; x <= cx + r; x++)
            {
                if (x < 0 || y < 0 || x >= tex.width || y >= tex.height) continue;
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                if (d <= r + 0.45f) tex.SetPixel(x, y, c);
            }
        }

        private static void DrawArc(Texture2D tex, int cx, int cy, int r, float a0, float a1)
        {
            for (int i = 0; i < 56; i++)
            {
                float t = Mathf.Lerp(a0, a1, i / 55f) * Mathf.PI;
                int x = cx + Mathf.RoundToInt(Mathf.Cos(t) * r);
                int y = cy + Mathf.RoundToInt(Mathf.Sin(t) * r);
                if (x >= 0 && y >= 0 && x < tex.width && y < tex.height)
                {
                    tex.SetPixel(x, y, Color.white);
                    if (x + 1 < tex.width) tex.SetPixel(x + 1, y, Color.white);
                }
            }
        }

        internal static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        internal static void Place(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
    }
}
