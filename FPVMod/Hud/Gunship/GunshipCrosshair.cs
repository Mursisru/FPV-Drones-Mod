using UnityEngine;
using UnityEngine.UI;

namespace FPVMod.Hud
{
    /// <summary>COD AC-130 reticle + N/E/S/W (ported from MC; no look-around offset).</summary>
    internal sealed class GunshipCrosshair
    {
        private const int TicksPerArm = 2;
        private static readonly string[] Labels = { "N", "E", "S", "W" };
        private static readonly float[] WorldBearing = { 0f, 90f, 180f, 270f };

        private readonly Text _area;
        private readonly Text[] _nesw;
        private readonly Image _armN;
        private readonly Image _armS;
        private readonly Image _armE;
        private readonly Image _armW;
        private readonly Image _capN;
        private readonly Image _capS;
        private readonly Image _capE;
        private readonly Image _capW;
        private readonly Image[] _ticks;
        private readonly Image _dotN;
        private readonly Image _dotS;
        private readonly Image _dotE;
        private readonly Image _dotW;
        private readonly RectTransform _boreAim;
        private float _cardinalR = 120f;
        private string _lastArea = "";
        private float _lastHdg = float.NaN;
        private Vector2 _lastBoreOffset = new Vector2(float.NaN, float.NaN);

        private GunshipCrosshair(
            RectTransform boreAim,
            Text area, Text[] nesw,
            Image armN, Image armS, Image armE, Image armW,
            Image capN, Image capS, Image capE, Image capW,
            Image[] ticks,
            Image dotN, Image dotS, Image dotE, Image dotW)
        {
            _boreAim = boreAim;
            _area = area;
            _nesw = nesw;
            _armN = armN;
            _armS = armS;
            _armE = armE;
            _armW = armW;
            _capN = capN;
            _capS = capS;
            _capE = capE;
            _capW = capW;
            _ticks = ticks;
            _dotN = dotN;
            _dotS = dotS;
            _dotE = dotE;
            _dotW = dotW;
        }

        internal static GunshipCrosshair Create(RectTransform parent)
        {
            var rootGo = new GameObject("GunshipCrosshair", typeof(RectTransform));
            rootGo.transform.SetParent(parent, false);
            RectTransform root = rootGo.GetComponent<RectTransform>();
            GunshipChrome.Stretch(root);

            var aimGo = new GameObject("BoreAim", typeof(RectTransform));
            aimGo.transform.SetParent(root, false);
            RectTransform boreAim = aimGo.GetComponent<RectTransform>();
            boreAim.anchorMin = boreAim.anchorMax = new Vector2(0.5f, 0.5f);
            boreAim.pivot = new Vector2(0.5f, 0.5f);
            boreAim.anchoredPosition = Vector2.zero;
            boreAim.sizeDelta = Vector2.zero;

            Image armN = Line(boreAim, "ArmN");
            Image armS = Line(boreAim, "ArmS");
            Image armE = Line(boreAim, "ArmE");
            Image armW = Line(boreAim, "ArmW");
            Image capN = Line(boreAim, "CapN");
            Image capS = Line(boreAim, "CapS");
            Image capE = Line(boreAim, "CapE");
            Image capW = Line(boreAim, "CapW");

            var ticks = new Image[TicksPerArm * 4];
            for (int i = 0; i < ticks.Length; i++)
                ticks[i] = Line(boreAim, "Tk" + i);

            Image dotN = Line(boreAim, "DotN");
            Image dotS = Line(boreAim, "DotS");
            Image dotE = Line(boreAim, "DotE");
            Image dotW = Line(boreAim, "DotW");

            var nesw = new Text[4];
            for (int i = 0; i < 4; i++)
            {
                nesw[i] = GunshipChrome.CreateText(boreAim, "Card" + Labels[i], TextAnchor.MiddleCenter, 26);
                nesw[i].text = Labels[i];
                nesw[i].fontStyle = FontStyle.Bold;
                nesw[i].color = GunshipChrome.White;
                GunshipChrome.Place(nesw[i].rectTransform,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(34f, 34f));
            }

            Text area = GunshipChrome.CreateText(boreAim, "Area", TextAnchor.UpperCenter, GunshipChrome.FontBody);
            area.fontStyle = FontStyle.Normal;

            return new GunshipCrosshair(
                boreAim, area, nesw,
                armN, armS, armE, armW,
                capN, capS, capE, capW,
                ticks, dotN, dotS, dotE, dotW);
        }

        internal void Place(FpvPanelMetrics panel)
        {
            float min = panel.MinSide;
            float thick = 1.15f;
            float halfSpan = Mathf.Clamp(min * 0.234f, 120f, 220f);
            float gap = Mathf.Clamp(min * 0.028f, 14f, 24f);
            float arm = halfSpan - gap;
            float capLen = Mathf.Clamp(min * 0.016f, 8f, 13f);
            float tickLen = Mathf.Clamp(min * 0.011f, 5.5f, 9f);
            float dot = Mathf.Clamp(min * 0.0055f, 2.6f, 4.2f);

            float armMid = gap + arm * 0.5f;
            float tip = gap + arm;
            Size(_armN, 0f, armMid, thick, arm);
            Size(_armS, 0f, -armMid, thick, arm);
            Size(_armE, armMid, 0f, arm, thick);
            Size(_armW, -armMid, 0f, arm, thick);

            Size(_capN, 0f, tip, capLen, thick);
            Size(_capS, 0f, -tip, capLen, thick);
            Size(_capE, tip, 0f, thick, capLen);
            Size(_capW, -tip, 0f, thick, capLen);

            for (int i = 0; i < TicksPerArm; i++)
            {
                float u = (i + 1) / (float)(TicksPerArm + 1);
                float d = gap + arm * u;
                int b = i * 4;
                Size(_ticks[b], 0f, d, tickLen, thick);
                Size(_ticks[b + 1], 0f, -d, tickLen, thick);
                Size(_ticks[b + 2], d, 0f, thick, tickLen);
                Size(_ticks[b + 3], -d, 0f, thick, tickLen);
            }

            float dr = gap * 0.72f;
            Size(_dotN, 0f, dr, dot, dot);
            Size(_dotS, 0f, -dr, dot, dot);
            Size(_dotE, dr, 0f, dot, dot);
            Size(_dotW, -dr, 0f, dot, dot);

            _cardinalR = tip + Mathf.Clamp(min * 0.055f, 28f, 52f);
            _lastHdg = float.NaN;
            _lastBoreOffset = new Vector2(float.NaN, float.NaN);

            float areaY = -(_cardinalR + Mathf.Clamp(min * 0.03f, 16f, 30f));
            GunshipChrome.Place(_area.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f),
                new Vector2(0f, areaY), new Vector2(720f, 24f));
        }

        internal void Update(FpvGunshipSnapshot snapshot, FpvPanelMetrics panel)
        {
            SyncBoreAimOffset(panel);

            string area = snapshot.RangeM > 0.5f
                ? "TARGETING AREA: " + Mathf.RoundToInt(snapshot.RangeM) + " m"
                : "TARGETING AREA: ---";
            if (area != _lastArea)
            {
                _lastArea = area;
                _area.text = area;
            }

            UpdateCardinals(snapshot);
        }

        private void SyncBoreAimOffset(FpvPanelMetrics panel)
        {
            if (_boreAim == null)
                return;

            // Skip expensive viewport project when not free-looking.
            if (!FpvView.FpvFsLookAround.IsLooking)
            {
                if (_lastBoreOffset.sqrMagnitude > 0.01f || float.IsNaN(_lastBoreOffset.x))
                {
                    _lastBoreOffset = Vector2.zero;
                    _boreAim.anchoredPosition = Vector2.zero;
                }
                return;
            }

            Vector2 offset = FpvView.FpvFsLookAround.GetBorePanelOffset(
                FpvView.FpvFeedCamera.FeedCamera, panel.Width, panel.Height);
            if (!float.IsNaN(_lastBoreOffset.x)
                && (offset - _lastBoreOffset).sqrMagnitude < 0.25f)
                return;

            _lastBoreOffset = offset;
            _boreAim.anchoredPosition = offset;
        }

        private void UpdateCardinals(FpvGunshipSnapshot snapshot)
        {
            bool show = snapshot.HasFeed;
            for (int i = 0; i < 4; i++)
            {
                if (_nesw[i].enabled != show)
                    _nesw[i].enabled = show;
            }
            if (!show)
                return;

            float hdg = snapshot.HeadingDeg;
            if (!float.IsNaN(_lastHdg) && Mathf.Abs(Mathf.DeltaAngle(_lastHdg, hdg)) < 0.4f)
                return;
            _lastHdg = hdg;

            for (int i = 0; i < 4; i++)
            {
                float screenDeg = WorldBearing[i] - hdg;
                float rad = screenDeg * Mathf.Deg2Rad;
                _nesw[i].rectTransform.anchoredPosition = new Vector2(
                    Mathf.Sin(rad) * _cardinalR,
                    Mathf.Cos(rad) * _cardinalR);
            }
        }

        private static Image Line(RectTransform parent, string name)
        {
            Image img = GunshipChrome.CreateImage(parent, name, GunshipChrome.White);
            RectTransform rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            return img;
        }

        private static void Size(Image img, float x, float y, float w, float h)
        {
            RectTransform rt = img.rectTransform;
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }
    }
}
