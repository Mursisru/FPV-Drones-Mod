using FPVMod.Drone;
using FPVMod.Link;
using FPVMod.Session;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FPVMod.FpvView
{
    internal static class FpvOsdCanvas
    {
        private static GameObject? _root;
        private static TextMeshProUGUI? _link;
        private static TextMeshProUGUI? _spd;
        private static TextMeshProUGUI? _alt;
        private static TextMeshProUGUI? _batt;
        private static TextMeshProUGUI? _arm;
        private static TextMeshProUGUI? _rng;
        private static Image? _staticOverlay;

        internal static void Show()
        {
            EnsureUi();
            if (_root != null)
                _root.SetActive(true);
        }

        internal static void Hide()
        {
            if (_root != null)
                _root.SetActive(false);
        }

        internal static void UpdateLink(FpvLinkLevel level)
        {
            if (_link == null)
                return;
            _link.text = level switch
            {
                FpvLinkLevel.Full => "LINK: GOOD",
                FpvLinkLevel.Degraded => "LINK: WEAK",
                _ => "LINK: LOST"
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
                _spd.text = $"SPD: {ac?.SpeedKmh ?? 0f:0} km/h";
            if (_alt != null)
                _alt.text = $"ALT: {drone.radarAlt:0} m";
            if (_batt != null)
                _batt.text = $"BATT: {(ac?.Battery01 ?? 0f) * 100f:0}%";
            if (_arm != null)
                _arm.text = wh != null && wh.IsSafe ? "SAFE" : "ARMED";

            if (_rng != null)
            {
                float rng = RaycastRange(drone);
                _rng.text = rng > 0f ? $"RNG: {rng:0} m" : "RNG: ---";
            }
        }

        private static float RaycastRange(Missile drone)
        {
            Transform cam = drone.transform;
            FpvCameraRigMount? mount = cam.GetComponentInChildren<FpvCameraRigMount>();
            Vector3 origin = mount != null ? mount.transform.position : cam.position;
            Vector3 dir = mount != null ? mount.transform.forward : cam.forward;
            return Physics.Raycast(origin, dir, out RaycastHit hit, 5000f, PhysicsLayers.StaticsMask | PhysicsLayers.ShipsMask)
                ? hit.distance
                : 0f;
        }

        private static void EnsureUi()
        {
            if (_root != null)
                return;

            _root = new GameObject("FPV_OSD");
            Object.DontDestroyOnLoad(_root);
            Canvas canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            _root.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _root.AddComponent<GraphicRaycaster>();

            _staticOverlay = CreateImage(_root.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2000, 2000), Color.clear);
            _link = CreateLabel(_root.transform, new Vector2(0.02f, 0.92f), "LINK: ---");
            _spd = CreateLabel(_root.transform, new Vector2(0.02f, 0.86f), "SPD: ---");
            _alt = CreateLabel(_root.transform, new Vector2(0.02f, 0.80f), "ALT: ---");
            _batt = CreateLabel(_root.transform, new Vector2(0.02f, 0.74f), "BATT: ---");
            _arm = CreateLabel(_root.transform, new Vector2(0.85f, 0.92f), "SAFE");
            _rng = CreateLabel(_root.transform, new Vector2(0.85f, 0.86f), "RNG: ---");
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
            rt.sizeDelta = new Vector2(400, 40);
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 22;
            tmp.color = new Color(0.2f, 1f, 0.35f, 0.95f);
            tmp.text = text;
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
            return img;
        }
    }

    internal sealed class FpvCameraRigMount : MonoBehaviour { }
}
