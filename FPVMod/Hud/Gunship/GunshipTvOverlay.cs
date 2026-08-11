using UnityEngine;
using UnityEngine.UI;

namespace FPVMod.Hud
{
    /// <summary>FS CRT: vignette + scanlines + grain (ported from MC GunshipTvOverlay).</summary>
    internal sealed class GunshipTvOverlay
    {
        private const int ScanH = 768;
        private const int GrainSize = 192;
        private const int VigSize = 160;
        private const float ScrollSpeed = 22f;

        private readonly RawImage _scan;
        private readonly RawImage _grain;
        private readonly RawImage _vig;
        private readonly Texture2D _scanTex;
        private readonly Texture2D _grainTex;
        private readonly Texture2D _vigTex;
        private float _scroll;
        private float _grainT;

        private GunshipTvOverlay(
            RawImage scan, RawImage grain, RawImage vig,
            Texture2D scanTex, Texture2D grainTex, Texture2D vigTex)
        {
            _scan = scan;
            _grain = grain;
            _vig = vig;
            _scanTex = scanTex;
            _grainTex = grainTex;
            _vigTex = vigTex;
        }

        internal static GunshipTvOverlay Create(RectTransform parent)
        {
            Texture2D vigTex = BuildVignetteTex();
            var vGo = new GameObject("GunshipVignette", typeof(RectTransform), typeof(RawImage));
            vGo.transform.SetParent(parent, false);
            vGo.transform.SetAsFirstSibling();
            GunshipChrome.Stretch(vGo.GetComponent<RectTransform>());
            RawImage vig = vGo.GetComponent<RawImage>();
            vig.texture = vigTex;
            vig.color = new Color(1f, 1f, 1f, 0.9f);
            vig.raycastTarget = false;

            Texture2D scanTex = BuildScanTex();
            var sGo = new GameObject("GunshipTvScan", typeof(RectTransform), typeof(RawImage));
            sGo.transform.SetParent(parent, false);
            sGo.transform.SetSiblingIndex(1);
            GunshipChrome.Stretch(sGo.GetComponent<RectTransform>());
            RawImage scan = sGo.GetComponent<RawImage>();
            scan.texture = scanTex;
            scan.color = new Color(1f, 1f, 1f, 0.95f);
            scan.raycastTarget = false;
            scan.uvRect = new Rect(0f, 0f, 1f, 14f);

            Texture2D grainTex = BuildGrainTex();
            var gGo = new GameObject("GunshipTvGrain", typeof(RectTransform), typeof(RawImage));
            gGo.transform.SetParent(parent, false);
            gGo.transform.SetSiblingIndex(2);
            GunshipChrome.Stretch(gGo.GetComponent<RectTransform>());
            RawImage grain = gGo.GetComponent<RawImage>();
            grain.texture = grainTex;
            grain.color = new Color(1f, 1f, 1f, 0.28f);
            grain.raycastTarget = false;
            grain.uvRect = new Rect(0f, 0f, 3.8f, 2.4f);

            return new GunshipTvOverlay(scan, grain, vig, scanTex, grainTex, vigTex);
        }

        internal void Update()
        {
            if (_scan != null && !_scan.gameObject.activeSelf)
                return;

            _scroll += Time.unscaledDeltaTime * ScrollSpeed;
            if (_scan != null)
            {
                float v = (_scroll % ScanH) / ScanH;
                _scan.uvRect = new Rect(0f, -v, 1f, 14f);
                float a = 0.9f + 0.06f * Mathf.Sin(Time.unscaledTime * 5.2f);
                _scan.color = new Color(1f, 1f, 1f, a);
            }

            if (_grain != null)
            {
                _grainT += Time.unscaledDeltaTime;
                if (_grainT > 0.04f)
                {
                    _grainT = 0f;
                    _grain.uvRect = new Rect(Random.value, Random.value, 3.8f, 2.4f);
                }
            }
        }

        internal void SetEnabled(bool enabled)
        {
            if (_scan != null && _scan.gameObject.activeSelf != enabled)
                _scan.gameObject.SetActive(enabled);
            if (_grain != null && _grain.gameObject.activeSelf != enabled)
                _grain.gameObject.SetActive(enabled);
            if (_vig != null && _vig.gameObject.activeSelf != enabled)
                _vig.gameObject.SetActive(enabled);
        }

        internal void Shutdown()
        {
            try { if (_scan != null) Object.Destroy(_scan.gameObject); } catch { /* ignore */ }
            try { if (_grain != null) Object.Destroy(_grain.gameObject); } catch { /* ignore */ }
            try { if (_vig != null) Object.Destroy(_vig.gameObject); } catch { /* ignore */ }
            try { if (_scanTex != null) Object.Destroy(_scanTex); } catch { /* ignore */ }
            try { if (_grainTex != null) Object.Destroy(_grainTex); } catch { /* ignore */ }
            try { if (_vigTex != null) Object.Destroy(_vigTex); } catch { /* ignore */ }
        }

        private static Texture2D BuildScanTex()
        {
            var tex = new Texture2D(2, ScanH, TextureFormat.RGBA32, false)
            {
                name = "FPV.GunshipScan",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            for (int y = 0; y < ScanH; y++)
            {
                int phase = y % 4;
                float a = phase switch
                {
                    0 => 0.16f,
                    1 => 0.04f,
                    2 => 0.11f,
                    _ => 0.02f
                };
                if ((y % 48) < 2) a = 0.22f;
                var c = new Color(0f, 0f, 0f, a);
                tex.SetPixel(0, y, c);
                tex.SetPixel(1, y, c);
            }
            tex.Apply(false, true);
            return tex;
        }

        private static Texture2D BuildGrainTex()
        {
            var tex = new Texture2D(GrainSize, GrainSize, TextureFormat.RGBA32, false)
            {
                name = "FPV.GunshipGrain",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.HideAndDontSave
            };
            for (int y = 0; y < GrainSize; y++)
            for (int x = 0; x < GrainSize; x++)
            {
                float n = ((x * 37 + y * 17) % 97) / 97f;
                float a = 0f;
                if (n > 0.9f) a = 0.14f;
                else if (n < 0.06f) a = 0.1f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply(false, true);
            return tex;
        }

        private static Texture2D BuildVignetteTex()
        {
            var tex = new Texture2D(VigSize, VigSize, TextureFormat.RGBA32, false)
            {
                name = "FPV.GunshipVig",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            float cx = (VigSize - 1) * 0.5f;
            float maxR = cx * 1.02f;
            for (int y = 0; y < VigSize; y++)
            for (int x = 0; x < VigSize; x++)
            {
                float dx = (x - cx) / maxR;
                float dy = (y - cx) / maxR;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01((r - 0.42f) / 0.62f);
                a = a * a * 0.55f;
                tex.SetPixel(x, y, new Color(0f, 0f, 0f, a));
            }
            tex.Apply(false, true);
            return tex;
        }
    }
}
