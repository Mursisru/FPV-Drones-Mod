using UnityEngine;
using UnityEngine.UI;

namespace FPVMod.Hud
{
    /// <summary>Bottom-left COD NAV footer.</summary>
    internal sealed class GunshipNavFooter
    {
        private readonly Text _line;
        private string _last = "";
        private float _prgSmooth;

        private GunshipNavFooter(Text line) => _line = line;

        internal static GunshipNavFooter Create(RectTransform parent)
        {
            Text line = GunshipChrome.CreateText(parent, "GunshipNav", TextAnchor.LowerLeft, GunshipChrome.FontBody);
            line.fontStyle = FontStyle.Normal;
            line.color = GunshipChrome.White;
            line.lineSpacing = 1.08f;
            return new GunshipNavFooter(line);
        }

        internal void Place(FpvPanelMetrics panel)
        {
            float px = GunshipChrome.PadX(panel);
            float py = GunshipChrome.PadY(panel);
            GunshipChrome.Place(_line.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(px, py), new Vector2(380f, 70f));
        }

        internal void Update(FpvGunshipSnapshot snapshot)
        {
            string geo = string.IsNullOrEmpty(snapshot.GridText) ? "---" : snapshot.GridText;
            string corr = snapshot.Link == "GOOD" ? "" : snapshot.Link;

            float targetPrg = snapshot.HasFeed ? 1f : 0f;
            float noise = (Mathf.PerlinNoise(Time.unscaledTime * 0.55f, 8.2f) - 0.5f) * 0.04f;
            _prgSmooth = Mathf.MoveTowards(_prgSmooth, targetPrg, Time.unscaledDeltaTime * 0.35f);
            float prg = Mathf.Clamp01(_prgSmooth + (snapshot.HasFeed ? noise : 0f));

            string text = "GEOPOINT  " + geo
                + "\nNAV PRG  " + prg.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                + "\nNAV CORR  " + corr
                + "\nLINK  " + snapshot.Link;
            if (text == _last)
                return;
            _last = text;
            _line.text = text;
        }
    }
}
