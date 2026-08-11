using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace FPVMod.Hud
{
    /// <summary>Top-left COD: sat + callsign + LAT/LON/SPD/HDG/ALT (ported from MC).</summary>
    internal sealed class GunshipTelemetry
    {
        private static readonly StringBuilder Sb = new StringBuilder(180);
        private const float JitterHz = 12f;

        private readonly RawImage _icon;
        private readonly Texture2D _iconTex;
        private readonly Text _callsign;
        private readonly Text _status;
        private readonly Text _body;
        private string _lastCall = "";
        private string _lastStatus = "";
        private float _jitterT;
        private float _baseLat;
        private float _baseLon;
        private float _baseSpd;
        private float _baseAlt;
        private float _baseHdg;
        private bool _haveBase;

        private GunshipTelemetry(RawImage icon, Texture2D iconTex, Text callsign, Text status, Text body)
        {
            _icon = icon;
            _iconTex = iconTex;
            _callsign = callsign;
            _status = status;
            _body = body;
        }

        internal static GunshipTelemetry Create(RectTransform parent)
        {
            Texture2D tex = GunshipChrome.BuildSatIconTex();
            var igo = new GameObject("GunshipSat", typeof(RectTransform), typeof(RawImage));
            igo.transform.SetParent(parent, false);
            RawImage icon = igo.GetComponent<RawImage>();
            icon.texture = tex;
            icon.color = GunshipChrome.White;
            icon.raycastTarget = false;

            Text callsign = GunshipChrome.CreateText(parent, "GunshipCallsign", TextAnchor.UpperLeft, GunshipChrome.FontCallsign);
            Text status = GunshipChrome.CreateText(parent, "GunshipStatus", TextAnchor.UpperLeft, GunshipChrome.FontStatus);
            Text body = GunshipChrome.CreateText(parent, "GunshipBody", TextAnchor.UpperLeft, GunshipChrome.FontBody);
            status.fontStyle = FontStyle.Normal;
            status.color = GunshipChrome.WhiteDim;
            body.fontStyle = FontStyle.Normal;
            body.lineSpacing = 1.02f;
            return new GunshipTelemetry(icon, tex, callsign, status, body);
        }

        internal void Place(FpvPanelMetrics panel)
        {
            float px = GunshipChrome.PadX(panel);
            float py = -GunshipChrome.PadY(panel);

            GunshipChrome.Place(_icon.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(px, py - 1f), new Vector2(30f, 30f));
            GunshipChrome.Place(_callsign.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(px + 36f, py), new Vector2(520f, 34f));
            GunshipChrome.Place(_status.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(px + 36f, py - 32f), new Vector2(280f, 18f));
            GunshipChrome.Place(_body.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(px, py - 56f), new Vector2(480f, 96f));
        }

        internal void Update(FpvGunshipSnapshot snapshot)
        {
            string call = string.IsNullOrEmpty(snapshot.Callsign) ? "FPV-1" : snapshot.Callsign.ToUpperInvariant();
            if (call != _lastCall)
            {
                _lastCall = call;
                _callsign.text = call;
            }

            string status = snapshot.HasFeed ? "ON STATION" : "NO SIGNAL";
            if (status != _lastStatus)
            {
                _lastStatus = status;
                _status.text = status;
                _icon.color = snapshot.HasFeed ? GunshipChrome.White : GunshipChrome.WhiteSoft;
            }

            _baseLat = snapshot.PosX;
            _baseLon = snapshot.PosZ;
            _baseHdg = snapshot.HeadingDeg;
            _baseSpd = snapshot.SpeedKmh;
            _baseAlt = snapshot.AltM;
            _haveBase = true;

            _jitterT += Time.unscaledDeltaTime;
            if (_jitterT < 1f / JitterHz && _body.text.Length > 0)
                return;
            _jitterT = 0f;
            _body.text = FormatBodyLive();
        }

        internal void Shutdown()
        {
            try
            {
                if (_iconTex != null)
                    Object.Destroy(_iconTex);
            }
            catch { /* ignore */ }
        }

        private string FormatBodyLive()
        {
            if (!_haveBase)
                return "---";

            float jLat = _baseLat + (Mathf.PerlinNoise(Time.unscaledTime * 3.1f, 0.2f) - 0.5f) * 0.35f;
            float jLon = _baseLon + (Mathf.PerlinNoise(0.7f, Time.unscaledTime * 2.7f) - 0.5f) * 0.35f;
            float jSpd = _baseSpd + (Mathf.PerlinNoise(Time.unscaledTime * 4.2f, 1.1f) - 0.5f) * 1.8f;
            float jAlt = _baseAlt + (Mathf.PerlinNoise(2.3f, Time.unscaledTime * 3.6f) - 0.5f) * 0.7f;
            int hdg = Mathf.RoundToInt(_baseHdg + (Mathf.PerlinNoise(Time.unscaledTime * 1.5f, 4f) - 0.5f) * 0.4f);
            if (hdg < 0) hdg += 360;
            if (hdg >= 360) hdg -= 360;

            Sb.Length = 0;
            AppendDms(Sb, jLat, true);
            Sb.Append('\n');
            AppendDms(Sb, jLon, false);
            Sb.Append('\n');
            Sb.Append("SPD ").Append(Mathf.Max(0f, jSpd).ToString("0", CultureInfo.InvariantCulture)).Append(" km/h");
            Sb.Append("  HDG ").Append(hdg.ToString("000", CultureInfo.InvariantCulture)).Append(" T");
            Sb.Append('\n');
            Sb.Append("ALT ").Append(Mathf.Max(0f, jAlt).ToString("0", CultureInfo.InvariantCulture)).Append("m");
            return Sb.ToString();
        }

        private static void AppendDms(StringBuilder sb, float meters, bool ns)
        {
            float degAbs = Mathf.Abs(meters) * 0.00001f;
            int d = (int)degAbs;
            float rem = (degAbs - d) * 60f;
            int m = (int)rem;
            float s = (rem - m) * 60f;
            char hemi = ns ? (meters >= 0f ? 'N' : 'S') : (meters >= 0f ? 'E' : 'W');
            sb.Append(d.ToString("000", CultureInfo.InvariantCulture)).Append('°').Append(' ')
                .Append(m.ToString("00", CultureInfo.InvariantCulture)).Append('\'').Append(' ')
                .Append(s.ToString("00.000", CultureInfo.InvariantCulture)).Append('"').Append(' ').Append(hemi);
        }
    }
}
