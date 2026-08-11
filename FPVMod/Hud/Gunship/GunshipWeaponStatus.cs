using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace FPVMod.Hud
{
    /// <summary>Bottom-right: fuse / BATT / COL / TV MODE (MC weapon column, FPV labels).</summary>
    internal sealed class GunshipWeaponStatus
    {
        private static readonly StringBuilder Sb = new StringBuilder(96);
        private const float RowH = 20f;
        private const float Pad = 4f;

        private readonly RectTransform _root;
        private readonly Text _body;
        private string _last = "";

        private GunshipWeaponStatus(RectTransform root, Text body)
        {
            _root = root;
            _body = body;
        }

        internal static GunshipWeaponStatus Create(RectTransform parent)
        {
            var go = new GameObject("GunshipWeapon", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform root = go.GetComponent<RectTransform>();

            Text body = GunshipChrome.CreateText(root, "Body", TextAnchor.LowerRight, GunshipChrome.FontBody);
            body.fontStyle = FontStyle.Normal;
            body.lineSpacing = 1.08f;
            return new GunshipWeaponStatus(root, body);
        }

        internal void Place(FpvPanelMetrics panel)
        {
            float px = GunshipChrome.PadX(panel);
            float py = GunshipChrome.PadY(panel);
            const int rows = 4;
            float h = RowH * rows + Pad * 2f;
            GunshipChrome.Place(_root, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-px, py), new Vector2(160f, h));

            GunshipChrome.Place(_body.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);
            _body.rectTransform.offsetMin = new Vector2(Pad, Pad);
            _body.rectTransform.offsetMax = new Vector2(-Pad, -Pad);
        }

        internal void Update(FpvGunshipSnapshot snapshot)
        {
            int batt = Mathf.RoundToInt(Mathf.Clamp01(snapshot.Batt01) * 100f);
            int col = Mathf.RoundToInt(Mathf.Clamp01(snapshot.Col01) * 100f);

            Sb.Length = 0;
            Sb.Append("FUSE ").Append(snapshot.Fuse);
            Sb.Append('\n').Append(batt.ToString(CultureInfo.InvariantCulture)).Append("% BATT");
            Sb.Append('\n').Append(col.ToString(CultureInfo.InvariantCulture)).Append("% COL");
            Sb.Append('\n').Append(FpvView.FpvVisionModeController.GunshipLabel(FpvView.FpvVisionModeController.Mode))
                .Append(" MODE");

            string text = Sb.ToString();
            if (text == _last)
                return;
            _last = text;
            _body.text = text;
        }
    }
}
