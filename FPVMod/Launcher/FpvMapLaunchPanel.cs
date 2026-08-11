using FPVMod.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FPVMod.Launcher
{
    internal static class FpvMapLaunchPanel
    {
        private static GameObject? _panel;
        private static TextMeshProUGUI? _name;
        private static TextMeshProUGUI? _status;
        private static TextMeshProUGUI? _ammo;
        private static Button? _launchBtn;
        private static FpvLauncher? _target;

        internal static void Show(FpvLauncher launcher)
        {
            if (launcher == null)
                return;
            EnsureUi();
            _target = launcher;
            FpvMapLaunchPanelTarget.Current = launcher;
            Refresh();
            if (_panel != null)
                _panel.SetActive(true);
        }

        internal static void Hide()
        {
            _target = null;
            if (_panel != null)
                _panel.SetActive(false);
        }

        internal static void Refresh()
        {
            if (_target == null)
                return;
            Unit? unit = _target.OwnerUnit;
            if (_name != null)
                _name.text = unit != null ? unit.unitName : "FPV Launcher";
            if (_ammo != null)
                _ammo.text = $"AMMO: {_target.Ammo}/{FpvConstants.LauncherCapacity}";
            if (_status != null)
            {
                _status.text = _target.State switch
                {
                    FpvLauncherState.Ready => "STATUS: READY",
                    FpvLauncherState.Cooldown => $"STATUS: COOLDOWN {_target.CooldownRemaining:0}s",
                    _ => "STATUS: EMPTY"
                };
            }
            if (_launchBtn != null)
                _launchBtn.interactable = _target.CanLaunch();
        }

        private static void OnLaunchClicked()
        {
            if (_target == null)
                return;
            FpvSpawnRpc.RequestLaunch(_target);
        }

        private static void EnsureUi()
        {
            if (_panel != null)
                return;

            Transform? canvas = Object.FindObjectOfType<GameplayUI>()?.gameplayCanvas?.transform;
            if (canvas == null)
            {
                FpvPlugin.ModLogger?.LogWarning("FpvMapLaunchPanel: GameplayUI canvas not found.");
                return;
            }

            _panel = new GameObject("FPV_LaunchPanel");
            _panel.transform.SetParent(canvas, false);
            RectTransform rt = _panel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 40f);
            rt.sizeDelta = new Vector2(520f, 120f);

            Image bg = _panel.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.08f, 0.12f, 0.92f);

            _name = CreateText(_panel.transform, new Vector2(16f, 78f), 24, TextAlignmentOptions.Left);
            _ammo = CreateText(_panel.transform, new Vector2(16f, 48f), 20, TextAlignmentOptions.Left);
            _status = CreateText(_panel.transform, new Vector2(16f, 18f), 20, TextAlignmentOptions.Left);

            GameObject btnGo = new GameObject("LaunchButton");
            btnGo.transform.SetParent(_panel.transform, false);
            RectTransform brt = btnGo.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(1f, 0.5f);
            brt.anchorMax = new Vector2(1f, 0.5f);
            brt.pivot = new Vector2(1f, 0.5f);
            brt.anchoredPosition = new Vector2(-16f, 0f);
            brt.sizeDelta = new Vector2(180f, 56f);
            Image btnImg = btnGo.AddComponent<Image>();
            btnImg.color = new Color(0.15f, 0.55f, 0.25f, 1f);
            _launchBtn = btnGo.AddComponent<Button>();
            _launchBtn.onClick.AddListener(OnLaunchClicked);
            TextMeshProUGUI btnText = btnGo.AddComponent<TextMeshProUGUI>();
            btnText.text = "LAUNCH FPV";
            btnText.fontSize = 22;
            btnText.alignment = TextAlignmentOptions.Center;
            btnText.color = Color.white;

            _panel.SetActive(false);
        }

        private static TextMeshProUGUI CreateText(Transform parent, Vector2 pos, float size, TextAlignmentOptions align)
        {
            GameObject go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(320f, 28f);
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.color = Color.white;
            return tmp;
        }
    }
}
