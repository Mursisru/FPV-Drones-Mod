using FPVMod.Drone;
using FPVMod.Link;
using FPVMod.Session;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FPVMod.FpvView
{
    /// <summary>Lightweight FPV HUD (fallback / overlay). FS FLIR rewritten via Harmony when MC present.</summary>
    internal static class FpvOsdCanvas
    {
        private static GameObject? _root;
        private static CanvasGroup? _group;
        private static TextMeshProUGUI? _link;
        private static TextMeshProUGUI? _spd;
        private static TextMeshProUGUI? _alt;
        private static TextMeshProUGUI? _col;
        private static TextMeshProUGUI? _arm;
        private static TextMeshProUGUI? _vsi;
        private static Image? _staticOverlay;

        internal static void Show()
        {
            EnsureUi();
            if (_root != null)
                _root.SetActive(true);
            SetHudOnly();
        }

        internal static void Hide()
        {
            if (_root != null)
                _root.SetActive(false);
        }

        internal static void SetHudOnly()
        {
            if (_group == null)
                return;
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }

        internal static void TickPauseUi()
        {
            if (_root == null || !_root.activeSelf || _group == null)
                return;
            _group.alpha = FpvUiGate.MenuOpen ? 0f : 1f;
            SetHudOnly();
        }

        internal static void UpdateLink(FpvLinkLevel level)
        {
            if (_link == null)
                return;
            _link.text = level switch
            {
                FpvLinkLevel.Full => "LINK GOOD",
                FpvLinkLevel.Degraded => "LINK WEAK",
                _ => "LINK LOST"
            };
            if (_staticOverlay != null)
                _staticOverlay.color = new Color(1f, 1f, 1f, level == FpvLinkLevel.Lost ? 0.65f : level == FpvLinkLevel.Degraded ? 0.2f : 0f);
            FpvPostProcess.SetNoise(level == FpvLinkLevel.Degraded ? 0.35f : level == FpvLinkLevel.Lost ? 0.8f : 0f);
        }

        internal static void RefreshTelemetry()
        {
            Missile? drone = FpvControlSession.Drone;
            if (drone == null)
                return;

            FpvAcroController? ac = drone.GetComponent<FpvAcroController>();
            FpvWarhead? wh = drone.GetComponent<FpvWarhead>();

            if (_spd != null)
                _spd.text = $"SPD {ac?.SpeedKmh ?? 0f:0} km/h";
            if (_alt != null)
                _alt.text = $"ALT {drone.radarAlt:0} m";
            if (_col != null)
                _col.text = ac != null
                    ? $"COL {ac.Collective01 * 100f:0}%  BATT {ac.Battery01 * 100f:0}%"
                    : "COL ---";
            if (_vsi != null)
                _vsi.text = ac != null
                    ? $"VSI {ac.VerticalSpeedMs:+0.0;-0.0;0.0} m/s"
                    : "VSI ---";
            if (_arm != null)
                _arm.text = wh != null && wh.IsSafe ? "SAFE" : "ARMED";
        }

        private static void EnsureUi()
        {
            if (_root != null)
                return;

            _root = new GameObject("FPV_OSD");
            Object.DontDestroyOnLoad(_root);
            Canvas canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            _root.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _group = _root.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;

            _staticOverlay = CreateImage(_root.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2000, 2000), Color.clear);
            _link = CreateLabel(_root.transform, new Vector2(0.02f, 0.92f), "LINK ---");
            _spd = CreateLabel(_root.transform, new Vector2(0.02f, 0.86f), "SPD ---");
            _alt = CreateLabel(_root.transform, new Vector2(0.02f, 0.80f), "ALT ---");
            _vsi = CreateLabel(_root.transform, new Vector2(0.02f, 0.74f), "VSI ---");
            _col = CreateLabel(_root.transform, new Vector2(0.02f, 0.68f), "COL ---");
            _arm = CreateLabel(_root.transform, new Vector2(0.85f, 0.92f), "SAFE");
        }

        private static TextMeshProUGUI CreateLabel(Transform parent, Vector2 anchor, string text)
        {
            GameObject go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(480, 40);
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 22;
            tmp.color = new Color(0.2f, 1f, 0.35f, 0.95f);
            tmp.text = text;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static Image CreateImage(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
        {
            GameObject go = new GameObject("Image");
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            Image img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }
    }

    internal sealed class FpvCameraRigMount : MonoBehaviour { }
}
